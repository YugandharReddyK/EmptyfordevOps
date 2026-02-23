using System.Text.Json;
using NgrDrillSheetApp.Calculations;
using NgrDrillSheetApp.Models;

namespace NgrDrillSheetApp.IO;

/// <summary>
/// Reads reference well data from a JSON file.
/// Replaces the former .ut CSV-based ReferenceWellReader.
///
/// Expected JSON structure:
/// {
///   "headerText": "...",
///   "tieOnNorth": 0.0, "tieOnEast": 0.0, "tieOnTVD": 0.0,
///   "stations": [ { "measuredDepth": 0, "inclination": 0, "azimuth": 0 }, ... ]
/// }
/// </summary>
public class ReferenceWellJsonReader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Read a reference well from a JSON file.
    /// North/East/TVD are computed from MD/Inc/Azi using minimum curvature.
    /// </summary>
    public ReferenceWellData ReadFromFile(string filePath)
    {
        var result = new ReferenceWellData();

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Reference well JSON file not found: {filePath}");
            return result;
        }

        var json = File.ReadAllText(filePath);
        var doc = JsonSerializer.Deserialize<RefWellDocument>(json, JsonOpts);

        if (doc == null || doc.Stations == null || doc.Stations.Count == 0)
        {
            Console.Error.WriteLine("No stations found in reference well JSON.");
            return result;
        }

        result.FilePath = filePath;
        result.HeaderText = doc.HeaderText ?? string.Empty;

        // Compute cumulative North, East, TVD using minimum curvature
        double prevMD = 0, prevInc = 0, prevAz = 0;
        double cumNorth = 0, cumEast = 0, cumTvd = 0;

        for (int i = 0; i < doc.Stations.Count; i++)
        {
            var s = doc.Stations[i];

            if (i > 0)
            {
                var delta = PositionCalculator.DeltaPosition(
                    prevMD, prevInc, prevAz, s.MeasuredDepth, s.Inclination, s.Azimuth);
                cumNorth += delta[0];
                cumEast += delta[1];
                cumTvd += delta[2];
            }

            result.Stations.Add(new ReferenceWellStation
            {
                RecordNumber = i + 1,
                MeasuredDepth = s.MeasuredDepth,
                Inclination = s.Inclination,
                Azimuth = s.Azimuth,
                North = cumNorth,
                East = cumEast,
                TVD = cumTvd,
            });

            prevMD = s.MeasuredDepth;
            prevInc = s.Inclination;
            prevAz = s.Azimuth;
        }

        // TDP = last station depth
        if (result.Stations.Count > 0)
            result.TDP = result.Stations[^1].MeasuredDepth;

        // Tie-on coordinates: use JSON values if non-zero, otherwise default to first station
        if (doc.TieOnNorth != 0 || doc.TieOnEast != 0 || doc.TieOnTVD != 0)
        {
            result.TieOnNorth = doc.TieOnNorth;
            result.TieOnEast = doc.TieOnEast;
            result.TieOnTVD = doc.TieOnTVD;
        }
        else if (result.Stations.Count > 0)
        {
            var first = result.Stations[0];
            result.TieOnNorth = first.North;
            result.TieOnEast = first.East;
            result.TieOnTVD = first.TVD;
        }

        Console.WriteLine($"  Loaded {result.Stations.Count} reference well stations, TDP = {result.TDP:F2} m");
        return result;
    }

    // --- JSON deserialization DTOs ---

    private class RefWellDocument
    {
        public string? Description { get; set; }
        public string? HeaderText { get; set; }
        public double TieOnNorth { get; set; }
        public double TieOnEast { get; set; }
        public double TieOnTVD { get; set; }
        public List<RefWellStationDto> Stations { get; set; } = new();
    }

    private class RefWellStationDto
    {
        public double MeasuredDepth { get; set; }
        public double Inclination { get; set; }
        public double Azimuth { get; set; }
    }
}
