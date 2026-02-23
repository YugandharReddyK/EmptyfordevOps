namespace NgrDrillSheetApp.Models;

/// <summary>
/// A single survey record as imported from an NGR CSV file or manual entry.
/// Maps to the columns in PrivDbSheet / DbQuerySheet (Module1).
/// </summary>
public class SurveyRecord
{
    /// <summary>File identifier or manual entry ID</summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>Good/Bad/Unclassified flag ("G", "B", "U")</summary>
    public string GBU { get; set; } = "U";

    /// <summary>Record number (sequential)</summary>
    public int RecordNumber { get; set; }

    /// <summary>Timestamp (Unix epoch seconds in CSV, DateTime here)</summary>
    public DateTime Timestamp { get; set; }

    // --- Raw imported values ---

    /// <summary>Measured depth at bit (raw, before offset)</summary>
    public double MdBit { get; set; }

    /// <summary>Azimuth (degrees)</summary>
    public double Azimuth { get; set; }

    /// <summary>Inclination (degrees)</summary>
    public double Inclination { get; set; }

    /// <summary>Ranging distance</summary>
    public double Distance { get; set; }

    /// <summary>Ranging direction</summary>
    public double Direction { get; set; }

    /// <summary>SNR quality factor value</summary>
    public double SnrQfValue { get; set; }

    /// <summary>SNR quality factor text</summary>
    public string SnrQfText { get; set; } = string.Empty;

    /// <summary>TFS quality factor value</summary>
    public double TfsQfValue { get; set; }

    /// <summary>TFS quality factor text</summary>
    public string TfsQfText { get; set; } = string.Empty;

    /// <summary>Tool-face angle</summary>
    public double ToolAngle { get; set; }

    /// <summary>Magnetic field strength (H)</summary>
    public double HField { get; set; }

    /// <summary>Percent max H field</summary>
    public double PctMaxH { get; set; }

    // --- Edited / working values (initially cloned from raw) ---

    /// <summary>Working measured depth at MWD (after bit offset applied)</summary>
    public double EditedMdMwd { get; set; }

    /// <summary>Working azimuth</summary>
    public double EditedAzimuth { get; set; }

    /// <summary>Working inclination</summary>
    public double EditedInclination { get; set; }

    /// <summary>Working distance</summary>
    public double EditedDistance { get; set; }

    /// <summary>Working direction</summary>
    public double EditedDirection { get; set; }

    /// <summary>Whether this record has been manually edited ("Y"/"N")</summary>
    public string EditedFlag { get; set; } = "N";

    /// <summary>Initialize working values from raw values (after offset)</summary>
    public void InitializeEditedValues(double bitOffset)
    {
        EditedMdMwd = MdBit - bitOffset;
        EditedAzimuth = Azimuth;
        EditedInclination = Inclination;
        EditedDistance = Distance;
        EditedDirection = Direction;
    }
}
