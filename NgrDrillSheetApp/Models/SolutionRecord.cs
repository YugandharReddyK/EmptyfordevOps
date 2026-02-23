namespace NgrDrillSheetApp.Models;

/// <summary>
/// A single row in the solution sheet, containing the full processed result
/// for one survey station. Maps to SolnQuerySheet columns (Module1).
/// </summary>
public class SolutionRecord
{
    // --- Identity (copied from SurveyRecord) ---
    public string FileId { get; set; } = string.Empty;
    public string GBU { get; set; } = "G";
    public int RecordNumber { get; set; }
    public DateTime Timestamp { get; set; }

    // --- Input survey data ---
    public double MdMwd { get; set; }
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

    // --- QFC outputs ---
    public double QfcDis { get; set; }
    public double QfcDir { get; set; }
    public CorrectionFlag QfcCFlag { get; set; }
    public QualityFactor QfcQFa { get; set; }
    public QualityFactor QfcQFr { get; set; }
    public QualityFactor QfcQFt { get; set; }
    public bool QfcInitGateOk { get; set; }
    public bool QfcExecOk { get; set; }

    // --- Pseudo Correlation Outputs (PCO) ---
    public double PcoHS { get; set; }
    public double PcoRS { get; set; }
    public double PcoToMd { get; set; }
    public double PcoToInc { get; set; }
    public double PcoToAzi { get; set; }
    public double PcoToNorth { get; set; }
    public double PcoToEast { get; set; }
    public double PcoToTvd { get; set; }
    public double PcoPsdMd { get; set; }
    public double PcoPsdInc { get; set; }
    public double PcoPsdAzi { get; set; }
    public double PcoPsdNorth { get; set; }
    public double PcoPsdEast { get; set; }
    public double PcoPsdTvd { get; set; }
    public double PcoMinDist { get; set; }
    public double PcoErrTotal { get; set; }
    public double PcoErrNorth { get; set; }
    public double PcoErrEast { get; set; }
    public double PcoErrTvd { get; set; }
    public double PcoMdBack { get; set; }
    public double PcoIncBack { get; set; }
    public double PcoAzBack { get; set; }

    // --- Post-process outputs ---
    public double AziRefDiff { get; set; }
    public double IncRefDiff { get; set; }
    public string AziDivCon { get; set; } = string.Empty;    // "Converging"/"Diverging"/"Parallel"
    public string IncDivCon { get; set; } = string.Empty;    // "Converging"/"Diverging"/"Parallel"
    public string HsRefRef { get; set; } = string.Empty;     // "Above"/"Below"/"In Line"
    public string RsRefRef { get; set; } = string.Empty;     // "Left"/"Right"/"In Line"

    /// <summary>Populate from a survey record's edited values.</summary>
    public void CopyFromSurveyRecord(SurveyRecord rec)
    {
        FileId = rec.FileId;
        GBU = rec.GBU;
        RecordNumber = rec.RecordNumber;
        Timestamp = rec.Timestamp;
        MdMwd = rec.EditedMdMwd;
        Azimuth = rec.EditedAzimuth;
        Inclination = rec.EditedInclination;
        Distance = rec.EditedDistance;
        Direction = rec.EditedDirection;
        SnrQfValue = rec.SnrQfValue;
        SnrQfText = rec.SnrQfText;
        TfsQfValue = rec.TfsQfValue;
        TfsQfText = rec.TfsQfText;
        ToolAngle = rec.ToolAngle;
        HField = rec.HField;
        PctMaxH = rec.PctMaxH;
    }
}
