using NgrDrillSheetApp.Models;
using NgrDrillSheetApp.Utils;

namespace NgrDrillSheetApp.Calculations;

/// <summary>
/// Pseudo-drill position calculation and reference well correlation engine.
/// Direct translation of Module3: CalcPosC, delSPos, StepPseudoInput,
/// InitializeAndFirstPseudoInput, LastPseudoInputAndCopyOutput.
/// Uses minimum curvature method for wellbore position calculation.
/// </summary>
public class PositionCalculator
{
    private readonly ReferenceWellData _refWell;
    private readonly List<PseudoDrillRow> _drillRows = new();
    private int _refSurveyFastSearchIndex;

    /// <summary>The calculated pseudo drill rows (read-only access).</summary>
    public IReadOnlyList<PseudoDrillRow> DrillRows => _drillRows;

    public PositionCalculator(ReferenceWellData refWell)
    {
        _refWell = refWell;
        _refSurveyFastSearchIndex = 0;
    }

    /// <summary>
    /// Minimum curvature: compute the delta position vector between two survey stations.
    /// Maps to VBA: Function delSPos (Module3)
    /// </summary>
    public static double[] DeltaPosition(double s1, double inc1, double az1,
                                          double s2, double inc2, double az2)
    {
        double dInc = (inc2 - inc1) * MathHelper.Rad;
        double dAz = (az2 - az1) * MathHelper.Rad;
        double cosDL = Math.Cos(dInc)
            - Math.Sin(inc2 * MathHelper.Rad) * Math.Sin(inc1 * MathHelper.Rad) * (1.0 - Math.Cos(dAz));
        double dl = MathHelper.Acos(cosDL);

        double f = dl > 0.001 ? Math.Tan(0.5 * dl) / dl : 0.5;

        double dN = (s2 - s1) * f * (Math.Sin(inc2 * MathHelper.Rad) * Math.Cos(az2 * MathHelper.Rad)
            + Math.Sin(inc1 * MathHelper.Rad) * Math.Cos(az1 * MathHelper.Rad));
        double dE = (s2 - s1) * f * (Math.Sin(inc2 * MathHelper.Rad) * Math.Sin(az2 * MathHelper.Rad)
            + Math.Sin(inc1 * MathHelper.Rad) * Math.Sin(az1 * MathHelper.Rad));
        double dV = (s2 - s1) * f * (Math.Cos(inc2 * MathHelper.Rad) + Math.Cos(inc1 * MathHelper.Rad));

        return new[] { dN, dE, dV };
    }

    /// <summary>
    /// Initialize the pseudo-drill with the first survey station and tie-on coordinates.
    /// Maps to VBA: InitializeAndFirstPseudoInput (Module3)
    /// </summary>
    public void InitializeFirstStation(
        double depthMWD, double incMWD, double azMWD,
        double qfcDis, double qfcDir,
        double tieOnNorth, double tieOnEast, double tieOnTvd)
    {
        _drillRows.Clear();
        _refSurveyFastSearchIndex = 0;

        var row = new PseudoDrillRow
        {
            DepthMWD = depthMWD,
            IncMWD = incMWD,
            AzMWD = azMWD,
            RangeDist = qfcDis,
            RangeDir = qfcDir,
            NorthMWD = tieOnNorth,
            EastMWD = tieOnEast,
            VertMWD = tieOnTvd,
            NorthCor = tieOnNorth,
            EastCor = tieOnEast,
            VertCor = tieOnTvd,
        };

        // Tie-on: set TO positions = initial positions
        row.DepthTO = depthMWD;
        row.IncTO = incMWD;
        row.AzTO = azMWD;
        row.NorthTO = tieOnNorth;
        row.EastTO = tieOnEast;
        row.VertTO = tieOnTvd;

        // Range HS/RS
        row.RangeHS = -qfcDis * Math.Cos(qfcDir * MathHelper.Rad);
        row.RangeRS = -qfcDis * Math.Sin(qfcDir * MathHelper.Rad);

        // Position correlation for first station
        var posC = CalcPosC(true, 0, tieOnNorth, tieOnEast, tieOnTvd, row.RangeHS, row.RangeRS);
        row.NorthCor = posC.NorthCor;
        row.EastCor = posC.EastCor;
        row.VertCor = posC.VertCor;
        row.DepthI = posC.DepthI;
        row.IncI = posC.IncI;
        row.AzI = posC.AzI;
        row.NorthI = posC.NorthI;
        row.EastI = posC.EastI;
        row.TvdI = posC.TvdI;
        row.MinDist = posC.MinDist;
        row.TotErr = posC.TotErr;
        row.NErrCol = posC.NorthErr;
        row.EErrCol = posC.EastErr;
        row.VErrCol = posC.VertErr;

        _drillRows.Add(row);
    }

    /// <summary>
    /// Process an intermediate survey station.
    /// Maps to VBA: StepPseudoInput (Module3)
    /// </summary>
    public void StepStation(double depthMWD, double incMWD, double azMWD,
                            double qfcDis, double qfcDir)
    {
        if (_drillRows.Count == 0) return;

        var prev = _drillRows[^1];
        int indexD = _drillRows.Count; // 0-based index of the new row

        var row = new PseudoDrillRow
        {
            DepthMWD = depthMWD,
            IncMWD = incMWD,
            AzMWD = azMWD,
            RangeDist = qfcDis,
            RangeDir = qfcDir,
        };

        // Range HS/RS
        row.RangeHS = -qfcDis * Math.Cos(qfcDir * MathHelper.Rad);
        row.RangeRS = -qfcDis * Math.Sin(qfcDir * MathHelper.Rad);

        // Use previous AZI from tie-on (most accurate known in RT) instead of PCD-C AZIs
        double azEstP = prev.AzTO;

        // Minimum curvature for MWD position
        double[] delPos = DeltaPosition(prev.DepthMWD, prev.IncMWD, azEstP,
                                         depthMWD, incMWD, azEstP);
        row.NorthMWD = prev.NorthCor + delPos[0];
        row.EastMWD = prev.EastCor + delPos[1];
        row.VertMWD = prev.VertCor + delPos[2];

        // Position correlation (first pass) to get estimated position
        var posEst = CalcPosC(false, indexD, row.NorthMWD, row.EastMWD, row.VertMWD,
                              row.RangeHS, row.RangeRS);
        double azTO;
        if (posEst.BracketFound)
        {

            // Calculate tie-on azimuth
            azTO = MathHelper.Atan2(posEst.NorthCor - prev.NorthCor,
                                       posEst.EastCor - prev.EastCor) * MathHelper.Deg;
            if (azTO < 0.0) azTO += 360.0;
        }
        else
        {
            azTO = MathHelper.Atan2(-prev.NorthCor, -prev.EastCor) * MathHelper.Deg;
            if (azTO < 0.0) azTO += 360.0;
        }
        // Minimum curvature with tie-on azimuth
        double azTOP = prev.AzTO;
        delPos = DeltaPosition(prev.DepthMWD, prev.IncMWD, azTOP,
                                   depthMWD, incMWD, azTO);

        row.DepthTO = depthMWD;
        row.IncTO = incMWD;
        row.AzTO = azTO;
        row.NorthTO = prev.NorthCor + delPos[0];
        row.EastTO = prev.EastCor + delPos[1];
        row.VertTO = prev.VertCor + delPos[2];

        // Position correlation (second pass) – writes detail
        var posCorr = CalcPosC(true, indexD, row.NorthTO, row.EastTO, row.VertTO,
                               row.RangeHS, row.RangeRS);
        row.NorthCor = posCorr.NorthCor;
        row.EastCor = posCorr.EastCor;
        row.VertCor = posCorr.VertCor;
        row.DepthI = posCorr.DepthI;
        row.IncI = posCorr.IncI;
        row.AzI = posCorr.AzI;
        row.NorthI = posCorr.NorthI;
        row.EastI = posCorr.EastI;
        row.TvdI = posCorr.TvdI;
        row.MinDist = posCorr.MinDist;
        row.TotErr = posCorr.TotErr;
        row.NErrCol = posCorr.NorthErr;
        row.EErrCol = posCorr.EastErr;
        row.VErrCol = posCorr.VertErr;

        // Back-calculation of depth/inc/az from corrected positions
        CalculateBackProjection(indexD, row);

        _drillRows.Add(row);
    }

    /// <summary>
    /// Process the last station (same as Step, then compute final back-projection).
    /// Maps to VBA: LastPseudoInputAndCopyOutput (Module3)
    /// </summary>
    public void FinalizeLastStation(double depthMWD, double incMWD, double azMWD,
                                     double qfcDis, double qfcDir)
    {
        StepStation(depthMWD, incMWD, azMWD, qfcDis, qfcDir);

        if (_drillRows.Count < 2) return;

        int indexD = _drillRows.Count - 1;
        var curr = _drillRows[indexD];
        var prev = _drillRows[indexD - 1];

        double[] r01 = {
            curr.NorthCor - prev.NorthCor,
            curr.EastCor  - prev.EastCor,
            curr.VertCor  - prev.VertCor
        };
        double dMD = Math.Sqrt(r01[0] * r01[0] + r01[1] * r01[1] + r01[2] * r01[2]);

        if (dMD != 0.0)
        {
            double u1N = r01[0] / dMD;
            double u1E = r01[1] / dMD;
            double u1V = r01[2] / dMD;
            curr.IncRev = MathHelper.Atan2(u1V, Math.Sqrt(u1N * u1N + u1E * u1E)) * MathHelper.Deg;
            curr.AzRev = MathHelper.Atan2(u1N, u1E) * MathHelper.Deg;
            if (curr.AzRev < 0.0) curr.AzRev += 360.0;
            curr.DepthRev = prev.DepthRev + dMD;
        }
    }

    /// <summary>
    /// Get the pseudo depth at a given solution row index (for QFC PDLast).
    /// Maps to VBA: GetLastPseudoDepth (Module3)
    /// </summary>
    public double GetPseudoDepth(int index)
    {
        if (index >= 0 && index < _drillRows.Count)
            return _drillRows[index].DepthI;
        return 0.0;
    }

    /// <summary>
    /// Position correlation: find the closest point on the reference well to the drill position,
    /// compute ranging errors, and return corrected position.
    /// Maps to VBA: Function CalcPosC (Module3)
    /// </summary>
    private PositionCorrelationResult CalcPosC(bool writeDetail, int stationIndex,
        double drillNorth, double drillEast, double drillVert,
        double rangeHS, double rangeRS)
    {
        var result = new PositionCorrelationResult();
        result.BracketFound = false;
        var stations = _refWell.Stations;
        if (stations.Count < 2) return result;

        int startIdx = Math.Max(0, _refSurveyFastSearchIndex - 1);
        int pConP = 0, pConC;
        bool fastSearch = true;
        int index = startIdx;

        while (index < stations.Count || fastSearch)
        {
            var station = stations[index];
            double refInc = station.Inclination;
            double refAz = station.Azimuth;
            double refNorth = station.North;
            double refEast = station.East;
            double refVert = station.TVD;

            double dirC1 = Math.Sin(refInc * MathHelper.Rad) * Math.Cos(refAz * MathHelper.Rad);
            double dirC2 = Math.Sin(refInc * MathHelper.Rad) * Math.Sin(refAz * MathHelper.Rad);
            double dirC3 = Math.Cos(refInc * MathHelper.Rad);
            double planeC = (drillNorth - refNorth) * dirC1
                          + (drillEast - refEast) * dirC2
                          + (drillVert - refVert) * dirC3;

            pConC = planeC > 0 ? 1 : -1;

            if (Math.Abs(pConP - pConC) == 2 && index > 0)
            {
                // Found the bracket – interpolate on reference well
                var stationP = stations[index - 1];
                double refDepthP = stationP.MeasuredDepth;
                double refIncP = stationP.Inclination;
                double refAzP = stationP.Azimuth;
                double refNorthP = stationP.North;
                double refEastP = stationP.East;
                double refVertP = stationP.TVD;
                double refDepth = station.MeasuredDepth;
                result.BracketFound = true;

                double dirCP1 = Math.Sin(refIncP * MathHelper.Rad) * Math.Cos(refAzP * MathHelper.Rad);
                double dirCP2 = Math.Sin(refIncP * MathHelper.Rad) * Math.Sin(refAzP * MathHelper.Rad);
                double dirCP3 = Math.Cos(refIncP * MathHelper.Rad);
                double planeCP = (drillNorth - refNorthP) * dirCP1
                               + (drillEast - refEastP) * dirCP2
                               + (drillVert - refVertP) * dirCP3;

                double cDL = dirC1 * dirCP1 + dirC2 * dirCP2 + dirC3 * dirCP3;
                cDL = Math.Clamp(cDL, -1.0, 1.0);
                double dlAng = MathHelper.Acos(cDL);

                double depthI, incI, azI, northI, eastI, vertI, f;

                if (dlAng < 0.001)
                {
                    depthI = refDepthP + planeCP * (refDepth - refDepthP) / (planeCP - planeC);
                    incI = 0.5 * (refInc + refIncP);
                    azI = 0.5 * (refAz + refAzP);
                    f = 0.5;
                }
                else
                {
                    double sDL = Math.Sin(dlAng);
                    double rad1 = (dirCP1 * cDL - dirC1) / sDL;
                    double rad2 = (dirCP2 * cDL - dirC2) / sDL;
                    double rad3 = (dirCP3 * cDL - dirC3) / sDL;
                    double temp = (drillNorth - refNorthP) * rad1
                                + (drillEast - refEastP) * rad2
                                + (drillVert - refVertP) * rad3
                                + (refDepth - refDepthP) / dlAng;
                    double dlI = MathHelper.Atan2(temp, planeCP);

                    double dirCI1 = dirCP1 * Math.Cos(dlI) - rad1 * Math.Sin(dlI);
                    double dirCI2 = dirCP2 * Math.Cos(dlI) - rad2 * Math.Sin(dlI);
                    double dirCI3 = dirCP3 * Math.Cos(dlI) - rad3 * Math.Sin(dlI);
                    incI = MathHelper.Atan2(dirCI3, Math.Sqrt(dirCI1 * dirCI1 + dirCI2 * dirCI2)) * MathHelper.Deg;
                    azI = MathHelper.Atan2(dirCI1, dirCI2) * MathHelper.Deg;
                    if (azI < 0.0) azI += 360.0;
                    depthI = refDepthP + dlI / dlAng * (refDepth - refDepthP);
                    f = Math.Tan(0.5 * dlI) / dlI;
                }

                northI = refNorthP + (depthI - refDepthP) * f
                    * (Math.Sin(incI * MathHelper.Rad) * Math.Cos(azI * MathHelper.Rad)
                     + Math.Sin(refIncP * MathHelper.Rad) * Math.Cos(refAzP * MathHelper.Rad));
                eastI = refEastP + (depthI - refDepthP) * f
                    * (Math.Sin(incI * MathHelper.Rad) * Math.Sin(azI * MathHelper.Rad)
                     + Math.Sin(refIncP * MathHelper.Rad) * Math.Sin(refAzP * MathHelper.Rad));
                vertI = refVertP + (depthI - refDepthP) * f
                    * (Math.Cos(incI * MathHelper.Rad) + Math.Cos(refIncP * MathHelper.Rad));

                double nDiff = drillNorth - northI;
                double eDiff = drillEast - eastI;
                double vDiff = drillVert - vertI;
                double drillHS = nDiff * Math.Cos(incI * MathHelper.Rad) * Math.Cos(azI * MathHelper.Rad)
                               + eDiff * Math.Cos(incI * MathHelper.Rad) * Math.Sin(azI * MathHelper.Rad)
                               - vDiff * Math.Sin(incI * MathHelper.Rad);
                double drillRS = -nDiff * Math.Sin(azI * MathHelper.Rad) + eDiff * Math.Cos(azI * MathHelper.Rad);
                double minDist = Math.Sqrt(nDiff * nDiff + eDiff * eDiff + vDiff * vDiff);

                double rangeN = rangeHS * Math.Cos(incI * MathHelper.Rad) * Math.Cos(azI * MathHelper.Rad)
                              - rangeRS * Math.Sin(azI * MathHelper.Rad);
                double rangeE = rangeHS * Math.Cos(incI * MathHelper.Rad) * Math.Sin(azI * MathHelper.Rad)
                              + rangeRS * Math.Cos(azI * MathHelper.Rad);
                double rangeV = -rangeHS * Math.Sin(incI * MathHelper.Rad);

                double northErr = nDiff - rangeN;
                double eastErr = eDiff - rangeE;
                double vertErr = vDiff - rangeV;
                double totErr = Math.Sqrt(northErr * northErr + eastErr * eastErr + vertErr * vertErr);

                result.NorthCor = drillNorth - northErr;
                result.EastCor = drillEast - eastErr;
                result.VertCor = drillVert - vertErr;

                if (writeDetail)
                {
                    result.DepthI = depthI;
                    result.IncI = incI;
                    result.AzI = azI;
                    result.NorthI = northI;
                    result.EastI = eastI;
                    result.TvdI = vertI;
                    result.MinDist = minDist;
                    result.TotErr = totErr;
                    result.NorthErr = northErr;
                    result.EastErr = eastErr;
                    result.VertErr = vertErr;
                }

                _refSurveyFastSearchIndex = index;
                return result;
            }
            else
            {
                pConP = pConC;
            }

            if (index >= stations.Count - 1)
            {
                if (fastSearch)
                {
                    // Restart full search from beginning
                    index = 0;
                    fastSearch = false;
                    pConP = 0;
                    pConC = 0;
                }
                else
                {
                    break;
                }
            }
            else
            {
                index++;
            }
        }

        // If no bracket found, return drill position uncorrected
        result.NorthCor = drillNorth;
        result.EastCor = drillEast;
        result.VertCor = drillVert;
        return result;
    }

    /// <summary>
    /// Back-calculate depth/inc/az from the corrected 3D positions.
    /// Maps to the back-projection logic in VBA StepPseudoInput (Module3).
    /// </summary>
    private void CalculateBackProjection(int indexD, PseudoDrillRow row)
    {
        if (indexD == 1)
        {
            // First step: simple vector from station 0 to station 1
            var prev = _drillRows[0];
            double[] r12 = {
                row.NorthCor - prev.NorthCor,
                row.EastCor  - prev.EastCor,
                row.VertCor  - prev.VertCor
            };
            double dMD = Math.Sqrt(r12[0] * r12[0] + r12[1] * r12[1] + r12[2] * r12[2]);
            if (dMD == 0) return;

            double u1N = r12[0] / dMD, u1E = r12[1] / dMD, u1V = r12[2] / dMD;
            prev.DepthRev = prev.DepthMWD;
            prev.IncRev = MathHelper.Atan2(u1V, Math.Sqrt(u1N * u1N + u1E * u1E)) * MathHelper.Deg;
            prev.AzRev = MathHelper.Atan2(u1N, u1E) * MathHelper.Deg;
            if (prev.AzRev < 0) prev.AzRev += 360.0;
        }
        else if (indexD > 1)
        {
            // Three-point back-projection
            var pos0 = _drillRows[indexD - 2];
            var pos1 = _drillRows[indexD - 1];

            double[] p0 = { pos0.NorthCor, pos0.EastCor, pos0.VertCor };
            double[] p1 = { pos1.NorthCor, pos1.EastCor, pos1.VertCor };
            double[] p2 = { row.NorthCor, row.EastCor, row.VertCor };

            double dist01 = 0, dist02 = 0, dist12 = 0;
            double[] r01 = new double[3], r02 = new double[3], r12 = new double[3];
            double[] u01 = new double[3], u02 = new double[3], u12 = new double[3];

            for (int i = 0; i < 3; i++)
            {
                r01[i] = p1[i] - p0[i];
                r02[i] = p2[i] - p0[i];
                r12[i] = p2[i] - p1[i];
                dist01 += r01[i] * r01[i];
                dist02 += r02[i] * r02[i];
                dist12 += r12[i] * r12[i];
            }
            dist01 = Math.Sqrt(dist01);
            dist02 = Math.Sqrt(dist02);
            dist12 = Math.Sqrt(dist12);

            if (dist01 == 0 || dist02 == 0 || dist12 == 0) return;

            for (int i = 0; i < 3; i++)
            {
                u01[i] = r01[i] / dist01;
                u02[i] = r02[i] / dist02;
                u12[i] = r12[i] / dist12;
            }

            double[] u1 = new double[3];
            for (int i = 0; i < 3; i++)
                u1[i] = dist12 / dist02 * u01[i] + dist01 / dist02 * u12[i];

            double cosA = u12[0] * u02[0] + u12[1] * u02[1] + u12[2] * u02[2];
            double[] u0 = new double[3];
            for (int i = 0; i < 3; i++)
                u0[i] = 2.0 * u01[i] * cosA - u1[i];

            double[] cProd = {
                u0[1] * u1[2] - u0[2] * u1[1],
                -(u0[0] * u1[2] - u0[2] * u1[0]),
                u0[0] * u1[1] - u0[1] * u1[0]
            };
            double sAlpha = Math.Sqrt(cProd[0] * cProd[0] + cProd[1] * cProd[1] + cProd[2] * cProd[2]);
            double alpha = MathHelper.Asin(sAlpha);

            double dMD;
            if (Math.Abs(alpha) < 0.0001)
                dMD = dist01;
            else
                dMD = dist01 * 0.5 * alpha / Math.Sin(0.5 * alpha);

            pos1.IncRev = MathHelper.Atan2(u1[2], Math.Sqrt(u1[0] * u1[0] + u1[1] * u1[1])) * MathHelper.Deg;
            pos1.AzRev = MathHelper.Atan2(u1[0], u1[1]) * MathHelper.Deg;
            if (pos1.AzRev < 0) pos1.AzRev += 360.0;
            pos1.DepthRev = pos0.DepthRev + dMD;
        }
    }
}
