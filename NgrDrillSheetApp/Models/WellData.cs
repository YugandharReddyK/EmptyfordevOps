namespace NgrDrillSheetApp.Models;

/// <summary>
/// A single reference well survey station (Depth, Inc, Azi, TVD, North, East).
/// Maps to PrivRefSheet columns (Module1/Module3).
/// </summary>
public class ReferenceWellStation
{
    public int RecordNumber { get; set; }
    public double MeasuredDepth { get; set; }
    public double Inclination { get; set; }
    public double Azimuth { get; set; }
    public double TVD { get; set; }
    public double North { get; set; }
    public double East { get; set; }
}

/// <summary>
/// Complete reference well data including the survey path and tie-on point.
/// </summary>
public class ReferenceWellData
{
    /// <summary>Path to the source file</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Header text from the survey file</summary>
    public string HeaderText { get; set; } = string.Empty;

    /// <summary>Total Depth Point – last station depth of the reference well</summary>
    public double TDP { get; set; }

    /// <summary>Tie-on coordinates (North, East, TVD)</summary>
    public double TieOnNorth { get; set; }
    public double TieOnEast { get; set; }
    public double TieOnTVD { get; set; }

    /// <summary>Ordered list of survey stations</summary>
    public List<ReferenceWellStation> Stations { get; set; } = new();
}

/// <summary>
/// A row in the pseudo-drill worksheet (PrivPsdDrill).
/// Maps to Module3 column definitions.
/// </summary>
public class PseudoDrillRow
{
    public double DepthMWD { get; set; }
    public double IncMWD { get; set; }
    public double AzMWD { get; set; }
    public double NorthMWD { get; set; }
    public double EastMWD { get; set; }
    public double VertMWD { get; set; }
    public double RangeDist { get; set; }
    public double RangeDir { get; set; }
    public double RangeHS { get; set; }
    public double RangeRS { get; set; }
    public double DepthTO { get; set; }
    public double IncTO { get; set; }
    public double AzTO { get; set; }
    public double NorthTO { get; set; }
    public double EastTO { get; set; }
    public double VertTO { get; set; }
    public double DepthI { get; set; }
    public double IncI { get; set; }
    public double AzI { get; set; }
    public double NorthI { get; set; }
    public double EastI { get; set; }
    public double TvdI { get; set; }
    public double MinDist { get; set; }
    public double TotErr { get; set; }
    public double NErrCol { get; set; }
    public double EErrCol { get; set; }
    public double VErrCol { get; set; }
    public double NorthCor { get; set; }
    public double EastCor { get; set; }
    public double VertCor { get; set; }
    public double DepthRev { get; set; }
    public double IncRev { get; set; }
    public double AzRev { get; set; }
}

/// <summary>
/// Holds the overall well processing configuration.
/// </summary>
public class WellProcessingConfig
{
    /// <summary>MD0 – start of lateral section</summary>
    public double MD0 { get; set; }

    /// <summary>Directional sensor offset (for interpolation mode)</summary>
    public double DirSensorOffset { get; set; }

    /// <summary>Whether interpolation/extrapolation mode is enabled</summary>
    public bool InterpolationEnabled { get; set; }

    /// <summary>RM tool bit-to-sensor offset</summary>
    public double RmBitOffset { get; set; }

    /// <summary>QFC interpolation lookup table (2-column: rateB → correction factor)</summary>
    public List<(double RateB, double Factor)> QfcInterpTable { get; set; } = new();

    /// <summary>H-Section bearing angle (degrees)</summary>
    public double HSectionBearing { get; set; }
}

/// <summary>
/// Result of position correlation for a single survey station.
/// </summary>
public class PositionCorrelationResult
{
    public double NorthCor { get; set; }
    public double EastCor { get; set; }
    public double VertCor { get; set; }

    // The following are populated when POut=1 in CalcPosC
    public double DepthI { get; set; }
    public double IncI { get; set; }
    public double AzI { get; set; }
    public double NorthI { get; set; }
    public double EastI { get; set; }
    public double TvdI { get; set; }
    public double MinDist { get; set; }
    public double TotErr { get; set; }
    public double NorthErr { get; set; }
    public double EastErr { get; set; }
    public double VertErr { get; set; }
}
