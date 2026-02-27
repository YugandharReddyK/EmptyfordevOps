using NgrDrillSheetApp.Calculations;
using NgrDrillSheetApp.IO;
using NgrDrillSheetApp.Models;
using NgrDrillSheetApp.Utils;

namespace NgrDrillSheetApp.Processing;

/// <summary>
/// Main well data processing pipeline.
/// Translates VBA: TriggerProcessWellData (Module1) and SolnExecPostProcess.
///
/// Pipeline:
///   1. Filter "Good" survey records
///   2. Sort by measured depth
///   3. Optionally interpolate Azi/Inc for directional sensor offset
///   4. Process QFC per station
///   5. Run pseudo-drill position correlation
///   6. Run post-processing (convergence/divergence analysis)
///   7. Return solution records
/// </summary>
public class WellDataProcessor
{
    private readonly WellProcessingConfig _config;
    private readonly ReferenceWellData _refWell;
    private readonly QfcProcessor _qfc;

    public WellDataProcessor(WellProcessingConfig config, ReferenceWellData refWell)
    {
        _config = config;
        _refWell = refWell;
        // Convert named tuple (RateB, Factor) to positional (X, Y) for QfcProcessor
        var table = config.QfcInterpTable
            .Select(t => (X: t.RateB, Y: t.Factor))
            .ToList();
        _qfc = new QfcProcessor(table);
    }

    /// <summary>
    /// Process all survey records end-to-end and return solution records.
    /// Maps to VBA: TriggerProcessWellData (Module1)
    /// </summary>
    public List<SolutionRecord> Process(List<SurveyRecord> allRecords)
    {
        // Step 1: Filter to "Good" records only
        var goodRecords = allRecords
            .Where(r => string.Equals(r.GBU, "G", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (goodRecords.Count == 0)
        {
            Console.WriteLine("  No 'Good' records found. Processing skipped.");
            return new List<SolutionRecord>();
        }

        Console.WriteLine($"  Processing {goodRecords.Count} Good records...");

        // Determine tie-on coordinates
        double tieOnN = _refWell.TieOnNorth;
        double tieOnE = _refWell.TieOnEast;
        double tieOnTvd = _refWell.TieOnTVD;

        if (_config.InterpolationEnabled && _config.DirSensorOffset != 0.0 && goodRecords.Count >= 2)
        {
            double md0 = goodRecords[0].EditedMdMwd;
            double md1 = goodRecords[1].EditedMdMwd;

            double ratio = (md1 - md0) != 0 ? _config.DirSensorOffset / (md1 - md0) : 0;

            // TempTieOn0 = the fixed solution tie-on (GetSolnTieOn equivalent)
            double n0 = _refWell.TieOnNorth;
            double e0 = _refWell.TieOnEast;
            double tvd0 = _refWell.TieOnTVD;

            double[] delta = PositionCalculator.DeltaPosition(
                md0, goodRecords[0].EditedInclination, goodRecords[0].EditedAzimuth,
                md1, goodRecords[1].EditedInclination, goodRecords[1].EditedAzimuth
                );
            double n1 = n0 + delta[0];
            double e1 = e0 + delta[1];
            double tvd1 = tvd0 + delta[2];

            tieOnN = n0 + ratio * (n1 - n0);
            tieOnE = e0 + ratio * (e1 - e0);
            tieOnTvd = tvd0 + ratio * (tvd1 - tvd0);
        }

        goodRecords.Sort((a, b) => a.EditedMdMwd.CompareTo(b.EditedMdMwd));

        if (_config.InterpolationEnabled && _config.DirSensorOffset != 0.0 && goodRecords.Count >= 2)
        {
            ApplyDirSensorInterpolation(goodRecords);
        }

        // Initialize QFC parameters
        _qfc.UpdateParams(_config.MD0, _refWell.TDP);

        // Initialize position calculator
        var posCalc = new PositionCalculator(_refWell);

        // Step 4 & 5: Process QFC and position correlation per row
        var solutions = new List<SolutionRecord>(goodRecords.Count);
        int qfcNbp = 0;

        for (int i = 0; i < goodRecords.Count; i++)
        {
            var rec = goodRecords[i];
            var sol = new SolutionRecord();
            sol.CopyFromSurveyRecord(rec);

            // --- QFC ---
            QfcOutput qfcOut;
            // Look backwards for last station that passed the initial gate
            int histIdx = -1;
            for (int j = i - 1; j >= 0; j--)
            {
                if (solutions[j].QfcInitGateOk)
                {
                    histIdx = j;
                    break;
                }
            }

            if (histIdx >= 0)
            {
                // Send history + current as a 2-element array
                var qIn = new QfcInput[2];
                qIn[0] = new QfcInput
                {
                    MD = solutions[histIdx].MdMwd,
                    Dis = solutions[histIdx].QfcDis,
                    Dir = solutions[histIdx].QfcDir,
                    PDLast = posCalc.GetPseudoDepth(histIdx),
                    B = solutions[histIdx].HField,
                    TA = solutions[histIdx].ToolAngle,
                    SnrQF = (int)solutions[histIdx].SnrQfValue,
                    TfsQF = (int)solutions[histIdx].TfsQfValue,
                };
                qIn[1] = new QfcInput
                {
                    MD = rec.EditedMdMwd,
                    Dis = rec.EditedDistance,
                    Dir = rec.EditedDirection,
                    PDLast = 0, // Unknown until after processing
                    B = rec.HField,
                    TA = rec.ToolAngle,
                    SnrQF = (int)rec.SnrQfValue,
                    TfsQF = (int)rec.TfsQfValue,
                };
                qfcOut = _qfc.Process(qIn, ref qfcNbp);
            }
            else
            {
                // Single station (no valid history)
                var qIn = new QfcInput[1];
                qIn[0] = new QfcInput
                {
                    MD = rec.EditedMdMwd,
                    Dis = rec.EditedDistance,
                    Dir = rec.EditedDirection,
                    PDLast = 0,
                    B = rec.HField,
                    TA = rec.ToolAngle,
                    SnrQF = (int)rec.SnrQfValue,
                    TfsQF = (int)rec.TfsQfValue,
                };
                qfcOut = _qfc.Process(qIn, ref qfcNbp);
            }

            // Write QFC outputs
            sol.QfcDis = qfcOut.Dis;
            sol.QfcDir = qfcOut.Dir;
            sol.QfcCFlag = qfcOut.CFlag;
            sol.QfcQFa = qfcOut.QFa;
            sol.QfcQFr = qfcOut.QFr;
            sol.QfcQFt = qfcOut.QFt;
            sol.QfcInitGateOk = qfcOut.InitGateOk;
            sol.QfcExecOk = qfcOut.ExecOk;

            // --- Position Correlation ---
            if (i == 0)
            {
                posCalc.InitializeFirstStation(
                    rec.EditedMdMwd, rec.EditedInclination, rec.EditedAzimuth,
                    qfcOut.Dis, qfcOut.Dir,
                    tieOnN, tieOnE, tieOnTvd);
            }
            else if (i == goodRecords.Count - 1)
            {
                posCalc.FinalizeLastStation(
                    rec.EditedMdMwd, rec.EditedInclination, rec.EditedAzimuth,
                    qfcOut.Dis, qfcOut.Dir);
            }
            else
            {
                posCalc.StepStation(
                    rec.EditedMdMwd, rec.EditedInclination, rec.EditedAzimuth,
                    qfcOut.Dis, qfcOut.Dir);
            }

            solutions.Add(sol);
        }

        // Copy position correlation outputs back to solutions
        CopyPositionOutputs(posCalc, solutions);

        // Step 6: Post-processing (convergence/divergence)
        PostProcess(solutions);

        Console.WriteLine($"  Pipeline complete. {solutions.Count} solution records generated.");
        return solutions;
    }

    /// <summary>
    /// Apply directional sensor offset interpolation to Azi and Inc.
    /// Maps to VBA: TriggerProcessWellData interpolation block (Module1 ~lines 870-895)
    /// </summary>
    private void ApplyDirSensorInterpolation(List<SurveyRecord> records)
    {
        double offset = _config.DirSensorOffset;
        var mdValues = records.Select(r => r.EditedMdMwd).ToList();
        var aziValues = records.Select(r => r.EditedAzimuth).ToList();
        var incValues = records.Select(r => r.EditedInclination).ToList();

        for (int i = 0; i < records.Count; i++)
        {
            double targetMd = records[i].EditedMdMwd + offset;
            records[i].EditedAzimuth = Interpolation.LinterpSyncRange(mdValues, aziValues, targetMd);
            records[i].EditedInclination = Interpolation.LinterpSyncRange(mdValues, incValues, targetMd);
            records[i].EditedMdMwd += offset;
        }
    }

    /// <summary>
    /// Copy position calculator outputs into solution records.
    /// </summary>
    private void CopyPositionOutputs(PositionCalculator posCalc, List<SolutionRecord> solutions)
    {
        var rows = posCalc.DrillRows;
        int count = Math.Min(rows.Count, solutions.Count);

        for (int i = 0; i < count; i++)
        {
            var row = rows[i];
            var sol = solutions[i];

            // Ranging HS/RS
            sol.PcoHS = row.RangeHS;
            sol.PcoRS = row.RangeRS;

            // Tie-on position
            sol.PcoToMd = row.DepthTO;
            sol.PcoToInc = row.IncTO;
            sol.PcoToAzi = row.AzTO;
            sol.PcoToNorth = row.NorthCor;
            sol.PcoToEast = row.EastCor;
            sol.PcoToTvd = row.VertCor;

            // Pseudo-drill (reference well correlated)
            sol.PcoPsdMd = row.DepthI;
            sol.PcoPsdInc = row.IncI;
            sol.PcoPsdAzi = row.AzI;
            sol.PcoPsdNorth = row.NorthI;
            sol.PcoPsdEast = row.EastI;
            sol.PcoPsdTvd = row.TvdI;

            // Errors
            sol.PcoMinDist = row.MinDist;
            sol.PcoErrTotal = row.TotErr;
            sol.PcoErrNorth = row.NErrCol;
            sol.PcoErrEast = row.EErrCol;
            sol.PcoErrTvd = row.VErrCol;

            // Back-projected survey
            sol.PcoMdBack = row.DepthRev;
            sol.PcoIncBack = row.IncRev;
            sol.PcoAzBack = row.AzRev;
        }
    }

    /// <summary>
    /// Post-processing: compute azimuth/inc differences and convergence/divergence indicators.
    /// Maps to VBA: SolnExecPostProcess (Module1)
    /// </summary>
    private void PostProcess(List<SolutionRecord> solutions)
    {
        foreach (var sol in solutions)
        {
            double aziTO = MathHelper.RelativeAngleUnwrapper(sol.PcoPsdAzi, sol.PcoToAzi);
            double aziDiff = aziTO - sol.PcoPsdAzi;
            double incDiff = sol.PcoToInc - sol.PcoPsdInc;
            double hs = sol.PcoHS;
            double rs = sol.PcoRS;

            sol.AziRefDiff = aziDiff;
            sol.IncRefDiff = incDiff;

            // Inclination convergence/divergence (vertical plane)
            if (hs < 0.0)
            {
                sol.HsRefRef = "Below";
                sol.IncDivCon = incDiff > 0 ? "Converging" : incDiff < 0 ? "Diverging" : "Parallel";
            }
            else if (hs > 0.0)
            {
                sol.HsRefRef = "Above";
                sol.IncDivCon = incDiff < 0 ? "Converging" : incDiff > 0 ? "Diverging" : "Parallel";
            }
            else
            {
                sol.HsRefRef = "In Line";
                sol.IncDivCon = incDiff == 0 ? "Parallel" : "Diverging";
            }

            // Azimuth convergence/divergence (horizontal plane)
            if (rs < 0.0)
            {
                sol.RsRefRef = "Left";
                sol.AziDivCon = aziDiff > 0 ? "Converging" : aziDiff < 0 ? "Diverging" : "Parallel";
            }
            else if (rs > 0.0)
            {
                sol.RsRefRef = "Right";
                sol.AziDivCon = aziDiff < 0 ? "Converging" : aziDiff > 0 ? "Diverging" : "Parallel";
            }
            else
            {
                sol.RsRefRef = "In Line";
                sol.AziDivCon = aziDiff == 0 ? "Parallel" : "Diverging";
            }
        }
    }
}
