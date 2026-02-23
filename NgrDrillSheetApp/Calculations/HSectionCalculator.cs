using NgrDrillSheetApp.Utils;

namespace NgrDrillSheetApp.Calculations;

/// <summary>
/// Horizontal Section projection calculator.
/// Projects 3D well positions onto a vertical plane at a user-defined bearing.
/// Maps to VBA: HSection_Perform_Calculate, HSection_REFWELL_Calculate, HSection_TO_Calculate (Module5)
///
/// Algorithm per station:
///   1. horizontalDist = sqrt(North² + East²)
///   2. bearing = atan2(North, East) in degrees
///   3. relativeAngle = -bearing + hSectionBearing
///   4. hSection = cos(relativeAngle) * horizontalDist
/// </summary>
public static class HSectionCalculator
{
    /// <summary>
    /// Result for a single station projected onto the H-Section plane.
    /// </summary>
    public record HSectionPoint(double HSection, double TVD, double North, double East);

    /// <summary>
    /// Compute H-Section projection for a single station (North, East, TVD).
    /// Returns the horizontal distance projected onto the H-Section bearing plane.
    /// </summary>
    public static double ProjectToHSection(double north, double east, double hSectionBearing)
    {
        double horizontalDist = Math.Sqrt(north * north + east * east);
        double bearingDeg = Math.Atan2(north, east) * MathHelper.Deg;
        double relativeAngle = -bearingDeg + hSectionBearing;
        return Math.Cos(relativeAngle * MathHelper.Rad) * horizontalDist;
    }

    /// <summary>
    /// Compute H-Section projections for reference well stations.
    /// Maps to VBA: HSection_REFWELL_Calculate (Module5)
    /// </summary>
    public static List<HSectionPoint> CalculateForReferenceWell(
        IReadOnlyList<(double North, double East, double TVD)> stations,
        double hSectionBearing)
    {
        var results = new List<HSectionPoint>(stations.Count);
        foreach (var s in stations)
        {
            double hSec = ProjectToHSection(s.North, s.East, hSectionBearing);
            results.Add(new HSectionPoint(hSec, s.TVD, s.North, s.East));
        }
        return results;
    }

    /// <summary>
    /// Compute H-Section projections for tie-on solution stations.
    /// Maps to VBA: HSection_TO_Calculate (Module5)
    /// </summary>
    public static List<HSectionPoint> CalculateForSolution(
        IReadOnlyList<(double North, double East, double TVD)> stations,
        double hSectionBearing)
    {
        var results = new List<HSectionPoint>(stations.Count);
        foreach (var s in stations)
        {
            double hSec = ProjectToHSection(s.North, s.East, hSectionBearing);
            results.Add(new HSectionPoint(hSec, s.TVD, s.North, s.East));
        }
        return results;
    }
}
