namespace NgrDrillSheetApp.Utils;

/// <summary>
/// Mathematical constants and helper functions used throughout the drill sheet calculations.
/// Maps to Module3 constants and various helper functions across VBA modules.
/// </summary>
public static class MathHelper
{
    public const double PI = Math.PI;
    public const double PI_2 = Math.PI / 2.0;

    /// <summary>Degrees to radians conversion factor. VBA: Rad = 0.017453292</summary>
    public const double Rad = Math.PI / 180.0;

    /// <summary>Radians to degrees conversion factor. VBA: Deg = 57.29577951</summary>
    public const double Deg = 180.0 / Math.PI;

    /// <summary>
    /// Log base 10. VBA's Log() is natural log, so the original used Log(val)/Log(10).
    /// Maps to VBA: VBLog10 (Module2)
    /// </summary>
    public static double Log10(double value) => Math.Log10(value);

    /// <summary>
    /// Atan2 matching Excel/VBA Application.Atan2 convention.
    /// NOTE: Excel's ATAN2(x, y) has reversed parameter order vs Math.Atan2(y, x).
    /// VBA calls Application.Atan2(x, y) which returns atan(y/x).
    /// </summary>
    public static double Atan2(double x, double y) => Math.Atan2(y, x);

    /// <summary>
    /// Acos with clamping to [-1, 1] to prevent NaN from floating point drift.
    /// </summary>
    public static double Acos(double value)
    {
        if (value > 1.0) value = 1.0;
        if (value < -1.0) value = -1.0;
        return Math.Acos(value);
    }

    /// <summary>
    /// Asin with clamping to [-1, 1].
    /// </summary>
    public static double Asin(double value)
    {
        if (value > 1.0) value = 1.0;
        if (value < -1.0) value = -1.0;
        return Math.Asin(value);
    }

    /// <summary>
    /// Unwraps a drill angle relative to a reference angle to handle the 0/360 boundary.
    /// Maps to VBA: RelativeAngleUnwrapper (Module1)
    /// </summary>
    public static double RelativeAngleUnwrapper(double refAngle, double drillAngle)
    {
        double result = drillAngle;
        double oppAngle = refAngle - 180.0;
        if (oppAngle < 0.0) oppAngle += 360.0;

        if (refAngle > oppAngle)
        {
            if (drillAngle > refAngle || drillAngle < oppAngle)
            {
                if (refAngle > 180.0)
                {
                    if (drillAngle < oppAngle) result = drillAngle + 360.0;
                }
                else if (refAngle < 180.0)
                {
                    if (drillAngle > oppAngle) result = drillAngle - 360.0;
                }
            }
        }
        else // refAngle < oppAngle
        {
            if (drillAngle > oppAngle || drillAngle < refAngle)
            {
                if (refAngle > 180.0)
                {
                    if (drillAngle < oppAngle) result = drillAngle + 360.0;
                }
                else if (refAngle < 180.0)
                {
                    if (drillAngle > oppAngle) result = drillAngle - 360.0;
                }
            }
        }

        return result;
    }

    /// <summary>Return the greater of two doubles. Maps to VBA: RetGreaterDouble (Module5)</summary>
    public static double Max(double a, double b) => a >= b ? a : b;

    /// <summary>Return the lesser of two doubles. Maps to VBA: RetLesserDouble (Module5)</summary>
    public static double Min(double a, double b) => a <= b ? a : b;

    /// <summary>
    /// Compute radial separation from HS and RS components.
    /// Maps to VBA: GetRangingRadialSeparation (Module1)
    /// </summary>
    public static double GetRadialSeparation(double hs, double rs)
        => Math.Sqrt(hs * hs + rs * rs);
}
