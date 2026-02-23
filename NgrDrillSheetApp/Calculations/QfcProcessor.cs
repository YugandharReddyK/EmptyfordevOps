using NgrDrillSheetApp.Models;

namespace NgrDrillSheetApp.Calculations;

/// <summary>
/// Quality Factor Calculation (QFC) engine.
/// Direct translation of Module2: ProcQFC, UpdateQfcParams.
/// Evaluates survey quality (QFa, QFr, QFt), performs EOP detection,
/// and applies distance/direction corrections when necessary.
/// </summary>
public class QfcProcessor
{
    // --- Amplitude thresholds ---
    private const double THRSH_B_QFA_GOOD = 0.25;
    private const double THRSH_B_QFA_MARGINAL = 0.1;

    // --- Tilt angle thresholds ---
    private const double THRSH_TA_QFT_GOOD = 10.0;
    private const double THRSH_TA_QFT_MARGINAL = 15.0;

    // --- Rate / EOP thresholds ---
    private const int THRSH_NBP = 3;
    private const double THRSH_COMBO_RATE_B = 0.2;
    private const double THRSH_COMBO_RATE_GB = 0.2;
    private const double THRSH_SINGLE_RATE_B = 0.2;
    private const double THRSH_SINGLE_RATE_GB = 0.2;
    private const double THRSH_COMBO_TA = 10.0;
    private const double THRSH_SINGLE_TA = 15.0;

    private const double ACTIVATE_METERS_FROM_EOP = 300.0;

    /// <summary>Total Depth Point – auto-retrieved from reference well</summary>
    private double _tdp;

    /// <summary>MD0 – start of lateral section (0.0 if blank)</summary>
    private double _md0;

    /// <summary>QFC interpolation lookup table for distance correction</summary>
    private readonly IReadOnlyList<(double X, double Y)> _qfcInterpTable;

    public QfcProcessor(IReadOnlyList<(double X, double Y)> qfcInterpTable)
    {
        _qfcInterpTable = qfcInterpTable;
    }

    /// <summary>
    /// Update the MD0 and TDP parameters before processing.
    /// Maps to VBA: Public Function UpdateQfcParams
    /// </summary>
    public void UpdateParams(double md0, double tdp)
    {
        _md0 = md0 > 0.0 ? md0 : 0.0;
        _tdp = tdp > 0.0 ? tdp : 0.0;
    }

    /// <summary>
    /// Process QFC for one survey station.
    /// Maps to VBA: Public Function ProcQFC (Module2)
    ///
    /// Parameters:
    ///   inRef: Array of QfcInput. Highest index is real-time, lower is history.
    ///          Dis/Dir in history MUST be from previous QFC outputs.
    ///   nbp:   Consecutive bad-point counter. Caller initializes to 0 before tie-on.
    /// </summary>
    public QfcOutput Process(QfcInput[] inRef, ref int nbp)
    {
        var result = new QfcOutput { ExecOk = true };

        try
        {
            int rtIdx = inRef.Length - 1;
            int lastIdx = rtIdx - 1;

            // --- Evaluate QFa (amplitude quality) ---
            if (inRef[rtIdx].B > THRSH_B_QFA_GOOD)
                result.QFa = QualityFactor.Good;
            else if (inRef[rtIdx].B > THRSH_B_QFA_MARGINAL)
                result.QFa = QualityFactor.Marginal;
            else
                result.QFa = QualityFactor.Bad;

            // --- Evaluate QFt (tilt quality) ---
            if (Math.Abs(inRef[rtIdx].TA) <= THRSH_TA_QFT_GOOD)
                result.QFt = QualityFactor.Good;
            else if (Math.Abs(inRef[rtIdx].TA) <= THRSH_TA_QFT_MARGINAL)
                result.QFt = QualityFactor.Marginal;
            else
                result.QFt = QualityFactor.Bad;

            // --- Evaluate initial quality gateway ---
            result.InitGateOk = inRef[rtIdx].SnrQF >= 100
                && (inRef[rtIdx].TfsQF == (int)QualityFactor.Marginal || inRef[rtIdx].TfsQF == (int)QualityFactor.Good)
                && result.QFa == QualityFactor.Good;

            // Reset Nbp if above MD0
            if (inRef.Length == 1 || inRef[rtIdx].MD < _md0)
            {
                nbp = 0;
            }

            // --- Determine processing mode ---
            if ((inRef.Length == 1 || inRef[rtIdx].MD < _md0) && result.InitGateOk)
            {
                // Initial survey OK but not in lateral or insufficient history
                result.QFr = QualityFactor.Invalid;
                result.CFlag = CorrectionFlag.None;
                result.Dis = inRef[rtIdx].Dis;
                result.Dir = inRef[rtIdx].Dir;
            }
            else if (result.InitGateOk)
            {
                // Sufficient history and initial QF gateway PASS
                ProcessFullQfc(inRef, rtIdx, lastIdx, ref nbp, result);
            }
            else
            {
                // Initial quality gateway FAIL
                result.QFr = QualityFactor.Invalid;
                result.CFlag = CorrectionFlag.Invalid;
                result.Dis = inRef[rtIdx].Dis;
                result.Dir = inRef[rtIdx].Dir;
            }
        }
        catch
        {
            // Error handler: flag execution failure
            int rtIdx = inRef.Length - 1;
            result.ExecOk = false;
            result.QFr = QualityFactor.Invalid;
            result.CFlag = CorrectionFlag.Invalid;
            result.Dis = inRef[rtIdx].Dis;
            result.Dir = inRef[rtIdx].Dir;
        }

        return result;
    }

    private void ProcessFullQfc(QfcInput[] inRef, int rtIdx, int lastIdx, ref int nbp, QfcOutput result)
    {
        // Evaluate pseudo depth estimation
        double estRtPD = inRef[lastIdx].PDLast + inRef[rtIdx].MD - inRef[lastIdx].MD;

        // Calculate rateGB and rateB
        double gbRt = inRef[rtIdx].B / inRef[rtIdx].Dis;
        double gbLast = inRef[lastIdx].B / inRef[lastIdx].Dis;
        double deltaMD = inRef[rtIdx].MD - inRef[lastIdx].MD;
        if (deltaMD == 0.0) deltaMD = 0.001;

        double rateB = (20.0 * Utils.MathHelper.Log10(inRef[lastIdx].B) - 20.0 * Utils.MathHelper.Log10(inRef[rtIdx].B)) / deltaMD;
        double rateGB = (20.0 * Utils.MathHelper.Log10(gbLast) - 20.0 * Utils.MathHelper.Log10(gbRt)) / deltaMD;

        // --- Evaluate QFr (rate quality) ---
        if (rateGB > THRSH_COMBO_RATE_GB && rateB > THRSH_COMBO_RATE_B)
            result.QFr = QualityFactor.Bad;
        else if (rateGB > THRSH_SINGLE_RATE_GB || rateB > THRSH_SINGLE_RATE_B)
            result.QFr = QualityFactor.Marginal;
        else
            result.QFr = QualityFactor.Good;

        // --- Evaluate EOP and QFr-conditional QFC logic ---
        if (nbp >= THRSH_NBP)
        {
            // Unconditional EOP warning – consecutive bad points threshold breached
            result.CFlag = CorrectionFlag.Stop;
            result.Dis = inRef[rtIdx].Dis;
            result.Dir = inRef[rtIdx].Dir;
        }
        else if (result.QFr == QualityFactor.Bad)
        {
            ApplyEopCorrection(inRef, rtIdx, lastIdx, estRtPD, ref nbp, result);
        }
        else if (result.QFr == QualityFactor.Marginal && Math.Abs(inRef[rtIdx].TA) > THRSH_COMBO_TA)
        {
            ApplyEopCorrection(inRef, rtIdx, lastIdx, estRtPD, ref nbp, result);
        }
        else if (Math.Abs(inRef[rtIdx].TA) > THRSH_SINGLE_TA)
        {
            ApplyEopCorrection(inRef, rtIdx, lastIdx, estRtPD, ref nbp, result);
        }
        else if (rateB >= 0.1)
        {
            // Reset NBP (EOP advisory must be permanent after THRSH_NBP)
            nbp = 0;
            if (estRtPD > _tdp - ACTIVATE_METERS_FROM_EOP)
            {
                // Apply distance adjustment using interpolation table
                result.CFlag = CorrectionFlag.Adjusted;
                double disCorrFactor = Interpolation.Linterp(_qfcInterpTable, rateB);
                result.Dis = inRef[rtIdx].Dis / disCorrFactor;
                result.Dir = inRef[rtIdx].Dir;
            }
            else
            {
                result.CFlag = CorrectionFlag.None;
                result.Dis = inRef[rtIdx].Dis;
                result.Dir = inRef[rtIdx].Dir;
            }
        }
        else
        {
            // Good survey on all marks
            if (nbp < THRSH_NBP)
                nbp = 0;
            result.CFlag = CorrectionFlag.None;
            result.Dis = inRef[rtIdx].Dis;
            result.Dir = inRef[rtIdx].Dir;
        }
    }

    /// <summary>
    /// Apply EOP zone correction: use previous station's Dis/Dir if in EOP zone.
    /// </summary>
    private void ApplyEopCorrection(QfcInput[] inRef, int rtIdx, int lastIdx,
        double estRtPD, ref int nbp, QfcOutput result)
    {
        if (estRtPD > _tdp - ACTIVATE_METERS_FROM_EOP)
        {
            if (nbp < THRSH_NBP)
            {
                if (estRtPD > _tdp - 50.0)
                {
                    result.CFlag = CorrectionFlag.Stop;
                    nbp = THRSH_NBP;
                }
                else
                {
                    result.CFlag = CorrectionFlag.Corrected;
                }
                result.Dis = inRef[lastIdx].Dis;
                result.Dir = inRef[lastIdx].Dir;
                nbp++;
            }
            else
            {
                result.CFlag = CorrectionFlag.Stop;
                result.Dis = inRef[rtIdx].Dis;
                result.Dir = inRef[rtIdx].Dir;
            }
        }
        else
        {
            // Not in EOP zone, feedback only
            result.CFlag = CorrectionFlag.None;
            result.Dis = inRef[rtIdx].Dis;
            result.Dir = inRef[rtIdx].Dir;
        }
    }
}
