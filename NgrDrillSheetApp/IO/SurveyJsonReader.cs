using System.Text.Json;
using System.Text.Json.Serialization;
using NgrDrillSheetApp.Models;

namespace NgrDrillSheetApp.IO;

/// <summary>
/// Reads NGR survey records from a JSON file.
/// Replaces the former CSV-based NgrCsvImporter.
///
/// Expected JSON structure:
/// {
///   "ngrDataVersion": 1,
///   "surveys": [ { "fileId": "...", "mdBit": 3050.5, ... }, ... ]
/// }
/// </summary>
public class SurveyJsonReader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Import survey records from a JSON file.
    /// </summary>
    public List<SurveyRecord> ReadFromFile(string filePath, double rmBitOffset = 0.0)
    {
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Survey JSON file not found: {filePath}");
            return new List<SurveyRecord>();
        }

        var json = File.ReadAllText(filePath);
        var doc = JsonSerializer.Deserialize<SurveyInputDocument>(json, JsonOpts);

        if (doc?.Surveys == null || doc.Surveys.Count == 0)
        {
            Console.Error.WriteLine("No survey records found in JSON file.");
            return new List<SurveyRecord>();
        }

        var records = new List<SurveyRecord>(doc.Surveys.Count);

        foreach (var s in doc.Surveys)
        {
            var rec = new SurveyRecord
            {
                FileId = s.FileId ?? string.Empty,
                RecordNumber = s.RecordNumber,
                GBU = s.Gbu ?? "U",
                MdBit = s.MdBit - rmBitOffset,
                Azimuth = s.Azimuth,
                Inclination = s.Inclination,
                Distance = s.Distance,
                Direction = s.Direction,
                SnrQfValue = s.SnrQfValue,
                SnrQfText = s.SnrQfText ?? string.Empty,
                TfsQfValue = s.TfsQfValue,
                TfsQfText = s.TfsQfText ?? string.Empty,
                ToolAngle = s.ToolAngle,
                HField = s.HField,
                PctMaxH = s.PctMaxH,
            };

            // Parse timestamp
            rec.Timestamp = s.Timestamp != default ? s.Timestamp : DateTime.MinValue;

            // Initialize edited/working values from raw values
            rec.InitializeEditedValues(rmBitOffset);
            records.Add(rec);
        }

        Console.WriteLine($"  Imported {records.Count} survey record(s) from {Path.GetFileName(filePath)}");
        return records;
    }

    // --- JSON deserialization DTOs ---

    private class SurveyInputDocument
    {
        public int NgrDataVersion { get; set; }
        public string? Description { get; set; }
        public List<SurveyInputRow> Surveys { get; set; } = new();
    }

    private class SurveyInputRow
    {
        public string? FileId { get; set; }
        public int RecordNumber { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Gbu { get; set; }
        public double MdBit { get; set; }
        public double Azimuth { get; set; }
        public double Inclination { get; set; }
        public double Distance { get; set; }
        public double Direction { get; set; }
        public double SnrQfValue { get; set; }
        public string? SnrQfText { get; set; }
        public double TfsQfValue { get; set; }
        public string? TfsQfText { get; set; }
        public double ToolAngle { get; set; }
        public double HField { get; set; }
        public double PctMaxH { get; set; }
    }
}
