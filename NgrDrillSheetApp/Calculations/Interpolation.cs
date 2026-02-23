namespace NgrDrillSheetApp.Calculations;

/// <summary>
/// Linear interpolation / extrapolation functions.
/// Maps to VBA: Linterp (Module2) and LinterpSyncRng (Module1).
/// </summary>
public static class Interpolation
{
    /// <summary>
    /// Linear interpolation from a 2-column table (x in column 0, y in column 1).
    /// Extrapolates beyond the table boundaries.
    /// Maps to VBA: Public Function Linterp(r As Range, X As Double) As Double (Module2)
    /// </summary>
    /// <param name="table">List of (x, y) pairs, assumed sorted ascending by x.</param>
    /// <param name="x">The x value to interpolate at.</param>
    public static double Linterp(IReadOnlyList<(double X, double Y)> table, double x)
    {
        int n = table.Count;
        if (n < 2) return 0.0;

        int l1, l2;

        if (x < table[0].X)
        {
            // x < xmin, extrapolate from first two points
            l1 = 0;
            l2 = 1;
        }
        else if (x > table[n - 1].X)
        {
            // x > xmax, extrapolate from last two points
            l1 = n - 2;
            l2 = n - 1;
        }
        else
        {
            // Search for the bracketing interval
            for (int i = 0; i < n; i++)
            {
                if (Math.Abs(table[i].X - x) < 1e-15)
                {
                    // Exact match
                    return table[i].Y;
                }
                if (table[i].X > x)
                {
                    l1 = i;
                    l2 = i - 1;
                    return Interp(table, l1, l2, x);
                }
            }
            // Fallback (shouldn't reach here)
            l1 = n - 2;
            l2 = n - 1;
        }

        return Interp(table, l1, l2, x);
    }

    private static double Interp(IReadOnlyList<(double X, double Y)> table, int l1, int l2, double x)
    {
        double denom = table[l2].X - table[l1].X;
        if (Math.Abs(denom) < 1e-15)
        {
            // Avoid division by zero, average
            return 0.5 * (table[l1].Y + table[l2].Y);
        }
        return table[l1].Y + (table[l2].Y - table[l1].Y) * (x - table[l1].X) / denom;
    }

    /// <summary>
    /// Linear interpolation using two synchronized arrays (one for x-values, one for y-values).
    /// Extrapolates beyond boundaries.
    /// Maps to VBA: Public Function LinterpSyncRng (Module1)
    /// </summary>
    /// <param name="xValues">X values (e.g., measured depths), must be same length as yValues.</param>
    /// <param name="yValues">Y values (e.g., azimuths or inclinations).</param>
    /// <param name="x">The x value to interpolate at.</param>
    public static double LinterpSyncRange(
        IReadOnlyList<double> xValues,
        IReadOnlyList<double> yValues,
        double x)
    {
        int n = xValues.Count;
        if (n != yValues.Count || n < 2) return 0.0;

        int l1, l2;

        if (x < xValues[0])
        {
            // Extrapolate below
            l1 = 0;
            l2 = 1;
        }
        else if (x > xValues[n - 1])
        {
            // Extrapolate above
            l1 = n - 2;
            l2 = n - 1;
        }
        else
        {
            for (int i = 0; i < n; i++)
            {
                if (Math.Abs(xValues[i] - x) < 1e-15)
                {
                    return yValues[i]; // Exact match
                }
                if (xValues[i] > x)
                {
                    l1 = i;
                    l2 = i - 1;
                    return InterpSync(xValues, yValues, l1, l2, x);
                }
            }
            l1 = n - 2;
            l2 = n - 1;
        }

        return InterpSync(xValues, yValues, l1, l2, x);
    }

    private static double InterpSync(
        IReadOnlyList<double> xValues,
        IReadOnlyList<double> yValues,
        int l1, int l2, double x)
    {
        double denom = xValues[l2] - xValues[l1];
        if (Math.Abs(denom) < 1e-15)
        {
            return 0.5 * (yValues[l1] + yValues[l2]);
        }
        return yValues[l1] + (yValues[l2] - yValues[l1]) * (x - xValues[l1]) / denom;
    }
}
