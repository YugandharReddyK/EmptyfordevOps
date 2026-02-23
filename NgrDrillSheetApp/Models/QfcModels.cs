namespace NgrDrillSheetApp.Models;

/// <summary>
/// Input to the QFC (Quality Factor Calculation) engine.
/// Maps to VBA: Public Type QfcInput (Module2)
/// </summary>
public class QfcInput
{
    /// <summary>Measured Depth</summary>
    public double MD { get; set; }

    /// <summary>Previous pseudo depth (caller enters anything for first station)</summary>
    public double PDLast { get; set; }

    /// <summary>Distance (ranging)</summary>
    public double Dis { get; set; }

    /// <summary>Direction (ranging)</summary>
    public double Dir { get; set; }

    /// <summary>Magnetic field strength (b-field)</summary>
    public double B { get; set; }

    /// <summary>Tool-face angle</summary>
    public double TA { get; set; }

    /// <summary>SNR quality factor (decoded floor-of-range SNR value)</summary>
    public int SnrQF { get; set; }

    /// <summary>TFS quality factor (conforms to RM Tool's QF Numeric Defines)</summary>
    public int TfsQF { get; set; }
}

/// <summary>
/// Output from the QFC engine.
/// Maps to VBA: Public Type QfcOutput (Module2)
/// </summary>
public class QfcOutput
{
    /// <summary>Output distance (possibly corrected)</summary>
    public double Dis { get; set; }

    /// <summary>Output direction (possibly corrected)</summary>
    public double Dir { get; set; }

    /// <summary>Correction flag indicating what action was taken</summary>
    public CorrectionFlag CFlag { get; set; }

    /// <summary>Quality factor for amplitude</summary>
    public QualityFactor QFa { get; set; }

    /// <summary>Quality factor for rate</summary>
    public QualityFactor QFr { get; set; }

    /// <summary>Quality factor for tilt</summary>
    public QualityFactor QFt { get; set; }

    /// <summary>
    /// Whether this survey passed the initial quality gateway.
    /// Caller must use this to determine which surveys are viable for QFC input history.
    /// </summary>
    public bool InitGateOk { get; set; }

    /// <summary>
    /// Whether QFC execution succeeded without errors (e.g., div/0).
    /// If false, output values should not be trusted.
    /// </summary>
    public bool ExecOk { get; set; } = true;
}
