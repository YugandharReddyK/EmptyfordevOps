using System.Text.Json;
using System.Text.Json.Serialization;
using NgrDrillSheetApp.Calculations;
using NgrDrillSheetApp.Models;
using NgrDrillSheetApp.Utils;

namespace NgrDrillSheetApp.IO;

/// <summary>
/// Exports processing results (solutions, H-Section, survey summary) to JSON.
/// Replaces the former CrdPrxExporter and RawCsvExporter.
/// </summary>
public class ResultsJsonExporter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Export the full processing results to a JSON file.
    /// </summary>
    public void ExportResults(
        string outputPath,
        WellProcessingConfig config,
        ReferenceWellData refWell,
        List<SurveyRecord> allRecords,
        List<SolutionRecord> solutions)
    {
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var doc = new ResultsDocument
        {
            ProcessedAt = DateTime.UtcNow,
            AppVersion = "2.0 (migrated from VAFI10 VBA 1.9)",
            Config = BuildConfigSection(config),
            ReferenceWellSummary = BuildRefWellSummary(refWell),
            SurveySummary = BuildSurveySummary(allRecords),
            Solutions = solutions.Select(BuildSolutionRow).ToList(),
        };

        // Optionally compute H-Section projections
        if (config.HSectionBearing != 0.0 && solutions.Count > 0)
        {
            doc.HSection = BuildHSection(solutions, refWell, config.HSectionBearing);
        }

        var json = JsonSerializer.Serialize(doc, JsonOpts);
        File.WriteAllText(outputPath, json);

        Console.WriteLine($"  Results exported to {Path.GetFileName(outputPath)} ({solutions.Count} solution records)");
    }

    /// <summary>
    /// Export just the raw survey records to a JSON file.
    /// </summary>
    public void ExportRawSurveys(
        string outputPath,
        List<SurveyRecord> records,
        bool goodOnly = true)
    {
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var filtered = goodOnly
            ? records.Where(r => string.Equals(r.GBU, "G", StringComparison.OrdinalIgnoreCase)).ToList()
            : records;

        var doc = new RawSurveyExport
        {
            ExportedAt = DateTime.UtcNow,
            TotalRecords = records.Count,
            ExportedRecords = filtered.Count(),
            GoodOnly = goodOnly,
            Surveys = filtered.Select(r => new RawSurveyRow
            {
                FileId = r.FileId,
                RecordNumber = r.RecordNumber,
                Timestamp = r.Timestamp != default ? r.Timestamp : null,
                Gbu = r.GBU,
                MdBit = Math.Round(r.MdBit, 4),
                Azimuth = Math.Round(r.Azimuth, 4),
                Inclination = Math.Round(r.Inclination, 4),
                Distance = Math.Round(r.Distance, 4),
                Direction = Math.Round(r.Direction, 4),
                SnrQfValue = r.SnrQfValue,
                SnrQfText = r.SnrQfText,
                TfsQfValue = r.TfsQfValue,
                TfsQfText = r.TfsQfText,
                ToolAngle = Math.Round(r.ToolAngle, 4),
                HField = Math.Round(r.HField, 4),
                PctMaxH = Math.Round(r.PctMaxH, 4),
            }).ToList(),
        };

        var json = JsonSerializer.Serialize(doc, JsonOpts);
        File.WriteAllText(outputPath, json);

        Console.WriteLine($"  Raw surveys exported to {Path.GetFileName(outputPath)} ({filtered.Count()} records)");
    }

    // ========== Private builders ==========

    private static ConfigSection BuildConfigSection(WellProcessingConfig cfg) => new()
    {
        Md0 = cfg.MD0,
        RmBitOffset = cfg.RmBitOffset,
        DirSensorOffset = cfg.DirSensorOffset,
        InterpolationEnabled = cfg.InterpolationEnabled,
        HSectionBearing = cfg.HSectionBearing,
        QfcTableEntries = cfg.QfcInterpTable.Count,
    };

    private static RefWellSummary BuildRefWellSummary(ReferenceWellData rw) => new()
    {
        StationCount = rw.Stations.Count,
        TotalDepthPoint = rw.TDP,
        TieOnNorth = rw.TieOnNorth,
        TieOnEast = rw.TieOnEast,
        TieOnTVD = rw.TieOnTVD,
    };

    private static SurveySummary BuildSurveySummary(List<SurveyRecord> all) => new()
    {
        TotalRecords = all.Count,
        GoodRecords = all.Count(r => string.Equals(r.GBU, "G", StringComparison.OrdinalIgnoreCase)),
        BadRecords = all.Count(r => string.Equals(r.GBU, "B", StringComparison.OrdinalIgnoreCase)),
        UnknownRecords = all.Count(r => string.Equals(r.GBU, "U", StringComparison.OrdinalIgnoreCase)),
    };

    private static SolutionRow BuildSolutionRow(SolutionRecord sol) => new()
    {
        RecordNumber = sol.RecordNumber,
        FileId = sol.FileId,
        Timestamp = sol.Timestamp != default ? sol.Timestamp : null,

        // Input survey
        MdMwd = Math.Round(sol.MdMwd, 4),
        Azimuth = Math.Round(sol.Azimuth, 4),
        Inclination = Math.Round(sol.Inclination, 4),
        Distance = Math.Round(sol.Distance, 4),
        Direction = Math.Round(sol.Direction, 4),
        HField = Math.Round(sol.HField, 4),
        ToolAngle = Math.Round(sol.ToolAngle, 4),

        // QFC
        QfcDis = Math.Round(sol.QfcDis, 4),
        QfcDir = Math.Round(sol.QfcDir, 4),
        QfcCFlag = sol.QfcCFlag.ToCorrectionString(),
        QfcQFa = sol.QfcQFa.ToDisplayString(),
        QfcQFr = sol.QfcQFr.ToDisplayString(),
        QfcQFt = sol.QfcQFt.ToDisplayString(),
        QfcInitGateOk = sol.QfcInitGateOk,
        QfcExecOk = sol.QfcExecOk,

        // Position correlation – Tie-on
        PcoToMd = Math.Round(sol.PcoToMd, 4),
        PcoToInc = Math.Round(sol.PcoToInc, 4),
        PcoToAzi = Math.Round(sol.PcoToAzi, 4),
        PcoToNorth = Math.Round(sol.PcoToNorth, 4),
        PcoToEast = Math.Round(sol.PcoToEast, 4),
        PcoToTvd = Math.Round(sol.PcoToTvd, 4),

        // Position correlation – Pseudo drill
        PcoPsdMd = Math.Round(sol.PcoPsdMd, 4),
        PcoPsdInc = Math.Round(sol.PcoPsdInc, 4),
        PcoPsdAzi = Math.Round(sol.PcoPsdAzi, 4),
        PcoPsdNorth = Math.Round(sol.PcoPsdNorth, 4),
        PcoPsdEast = Math.Round(sol.PcoPsdEast, 4),
        PcoPsdTvd = Math.Round(sol.PcoPsdTvd, 4),

        // Ranging
        PcoHS = Math.Round(sol.PcoHS, 4),
        PcoRS = Math.Round(sol.PcoRS, 4),
        RadialSeparation = Math.Round(MathHelper.GetRadialSeparation(sol.PcoHS, sol.PcoRS), 4),

        // Errors
        PcoMinDist = Math.Round(sol.PcoMinDist, 4),
        PcoErrTotal = Math.Round(sol.PcoErrTotal, 4),
        PcoErrNorth = Math.Round(sol.PcoErrNorth, 4),
        PcoErrEast = Math.Round(sol.PcoErrEast, 4),
        PcoErrTvd = Math.Round(sol.PcoErrTvd, 4),

        // Back-projection
        PcoMdBack = Math.Round(sol.PcoMdBack, 4),
        PcoIncBack = Math.Round(sol.PcoIncBack, 4),
        PcoAzBack = Math.Round(sol.PcoAzBack, 4),

        // Post-process
        AziRefDiff = Math.Round(sol.AziRefDiff, 4),
        IncRefDiff = Math.Round(sol.IncRefDiff, 4),
        AziDivCon = sol.AziDivCon,
        IncDivCon = sol.IncDivCon,
        HsRefRef = sol.HsRefRef,
        RsRefRef = sol.RsRefRef,
    };

    private static HSectionResult BuildHSection(
        List<SolutionRecord> solutions,
        ReferenceWellData refWell,
        double bearing)
    {
        var refStations = refWell.Stations
            .Select(s => (s.North, s.East, s.TVD))
            .ToList();
        var refProjections = HSectionCalculator.CalculateForReferenceWell(refStations, bearing);

        var solStations = solutions
            .Select(s => (s.PcoToNorth, s.PcoToEast, s.PcoToTvd))
            .ToList();
        var solProjections = HSectionCalculator.CalculateForSolution(solStations, bearing);

        return new HSectionResult
        {
            Bearing = bearing,
            ReferenceWellProjections = refProjections
                .Select(p => new HSectionPoint { HSection = Math.Round(p.HSection, 4), TVD = Math.Round(p.TVD, 4) })
                .ToList(),
            SolutionProjections = solProjections
                .Select((p, i) => new HSectionSolutionPoint
                {
                    RecordNumber = solutions[i].RecordNumber,
                    HSection = Math.Round(p.HSection, 4),
                    TVD = Math.Round(p.TVD, 4),
                })
                .ToList(),
        };
    }

    // ========== Output DTOs ==========

    private class ResultsDocument
    {
        public DateTime ProcessedAt { get; set; }
        public string AppVersion { get; set; } = string.Empty;
        public ConfigSection Config { get; set; } = new();
        public RefWellSummary ReferenceWellSummary { get; set; } = new();
        public SurveySummary SurveySummary { get; set; } = new();
        public List<SolutionRow> Solutions { get; set; } = new();
        public HSectionResult? HSection { get; set; }
    }

    private class ConfigSection
    {
        public double Md0 { get; set; }
        public double RmBitOffset { get; set; }
        public double DirSensorOffset { get; set; }
        public bool InterpolationEnabled { get; set; }
        public double HSectionBearing { get; set; }
        public int QfcTableEntries { get; set; }
    }

    private class RefWellSummary
    {
        public int StationCount { get; set; }
        public double TotalDepthPoint { get; set; }
        public double TieOnNorth { get; set; }
        public double TieOnEast { get; set; }
        public double TieOnTVD { get; set; }
    }

    private class SurveySummary
    {
        public int TotalRecords { get; set; }
        public int GoodRecords { get; set; }
        public int BadRecords { get; set; }
        public int UnknownRecords { get; set; }
    }

    private class SolutionRow
    {
        public int RecordNumber { get; set; }
        public string FileId { get; set; } = string.Empty;
        public DateTime? Timestamp { get; set; }

        // Input survey
        public double MdMwd { get; set; }
        public double Azimuth { get; set; }
        public double Inclination { get; set; }
        public double Distance { get; set; }
        public double Direction { get; set; }
        public double HField { get; set; }
        public double ToolAngle { get; set; }

        // QFC
        public double QfcDis { get; set; }
        public double QfcDir { get; set; }
        public string QfcCFlag { get; set; } = string.Empty;
        public string QfcQFa { get; set; } = string.Empty;
        public string QfcQFr { get; set; } = string.Empty;
        public string QfcQFt { get; set; } = string.Empty;
        public bool QfcInitGateOk { get; set; }
        public bool QfcExecOk { get; set; }

        // Position correlation – Tie-on
        public double PcoToMd { get; set; }
        public double PcoToInc { get; set; }
        public double PcoToAzi { get; set; }
        public double PcoToNorth { get; set; }
        public double PcoToEast { get; set; }
        public double PcoToTvd { get; set; }

        // Position correlation – Pseudo drill
        public double PcoPsdMd { get; set; }
        public double PcoPsdInc { get; set; }
        public double PcoPsdAzi { get; set; }
        public double PcoPsdNorth { get; set; }
        public double PcoPsdEast { get; set; }
        public double PcoPsdTvd { get; set; }

        // Ranging
        public double PcoHS { get; set; }
        public double PcoRS { get; set; }
        public double RadialSeparation { get; set; }

        // Errors
        public double PcoMinDist { get; set; }
        public double PcoErrTotal { get; set; }
        public double PcoErrNorth { get; set; }
        public double PcoErrEast { get; set; }
        public double PcoErrTvd { get; set; }

        // Back-projection
        public double PcoMdBack { get; set; }
        public double PcoIncBack { get; set; }
        public double PcoAzBack { get; set; }

        // Post-process
        public double AziRefDiff { get; set; }
        public double IncRefDiff { get; set; }
        public string AziDivCon { get; set; } = string.Empty;
        public string IncDivCon { get; set; } = string.Empty;
        public string HsRefRef { get; set; } = string.Empty;
        public string RsRefRef { get; set; } = string.Empty;
    }

    private class HSectionResult
    {
        public double Bearing { get; set; }
        public List<HSectionPoint> ReferenceWellProjections { get; set; } = new();
        public List<HSectionSolutionPoint> SolutionProjections { get; set; } = new();
    }

    private class HSectionPoint
    {
        public double HSection { get; set; }
        public double TVD { get; set; }
    }

    private class HSectionSolutionPoint
    {
        public int RecordNumber { get; set; }
        public double HSection { get; set; }
        public double TVD { get; set; }
    }

    private class RawSurveyExport
    {
        public DateTime ExportedAt { get; set; }
        public int TotalRecords { get; set; }
        public int ExportedRecords { get; set; }
        public bool GoodOnly { get; set; }
        public List<RawSurveyRow> Surveys { get; set; } = new();
    }

    private class RawSurveyRow
    {
        public string FileId { get; set; } = string.Empty;
        public int RecordNumber { get; set; }
        public DateTime? Timestamp { get; set; }
        public string Gbu { get; set; } = string.Empty;
        public double MdBit { get; set; }
        public double Azimuth { get; set; }
        public double Inclination { get; set; }
        public double Distance { get; set; }
        public double Direction { get; set; }
        public double SnrQfValue { get; set; }
        public string SnrQfText { get; set; } = string.Empty;
        public double TfsQfValue { get; set; }
        public string TfsQfText { get; set; } = string.Empty;
        public double ToolAngle { get; set; }
        public double HField { get; set; }
        public double PctMaxH { get; set; }
    }
}
