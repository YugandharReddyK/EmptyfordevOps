using System.Text.Json;
using NgrDrillSheetApp.Models;

namespace NgrDrillSheetApp.IO;

/// <summary>
/// Reads processing configuration from a JSON file.
///
/// Expected JSON structure:
/// {
///   "md0": 2500.0,
///   "rmBitOffset": 15.24,
///   "dirSensorOffset": 0.0,
///   "interpolationEnabled": false,
///   "hSectionBearing": 285.0,
///   "qfcInterpTable": [ { "rateB": 0.0, "factor": 1.0 }, ... ]
/// }
/// </summary>
public class ConfigJsonReader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Read processing config from a JSON file.
    /// </summary>
    public WellProcessingConfig ReadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Config JSON file not found: {filePath}");
            return GetDefaults();
        }

        var json = File.ReadAllText(filePath);
        var doc = JsonSerializer.Deserialize<ConfigDocument>(json, JsonOpts);

        if (doc == null)
        {
            Console.Error.WriteLine("Failed to parse config JSON. Using defaults.");
            return GetDefaults();
        }

        var config = new WellProcessingConfig
        {
            MD0 = doc.Md0,
            RmBitOffset = doc.RmBitOffset,
            DirSensorOffset = doc.DirSensorOffset,
            InterpolationEnabled = doc.InterpolationEnabled,
            HSectionBearing = doc.HSectionBearing,
        };

        // Convert QFC table
        if (doc.QfcInterpTable != null && doc.QfcInterpTable.Count > 0)
        {
            config.QfcInterpTable = doc.QfcInterpTable
                .Select(r => (RateB: r.RateB, Factor: r.Factor))
                .ToList();
        }
        else
        {
            config.QfcInterpTable = GetDefaultQfcTable();
        }

        Console.WriteLine($"  Config loaded: MD0={config.MD0}, BitOffset={config.RmBitOffset}, HSec={config.HSectionBearing}°");
        return config;
    }

    /// <summary>
    /// Returns a default configuration (useful if no config file is provided).
    /// </summary>
    public static WellProcessingConfig GetDefaults()
    {
        return new WellProcessingConfig
        {
            MD0 = 0,
            RmBitOffset = 0,
            DirSensorOffset = 0,
            InterpolationEnabled = false,
            HSectionBearing = 0,
            QfcInterpTable = GetDefaultQfcTable(),
        };
    }

    private static List<(double RateB, double Factor)> GetDefaultQfcTable()
    {
        return new List<(double, double)>
        {
            (0.00, 1.00), (0.05, 1.02), (0.10, 1.05), (0.15, 1.10),
            (0.20, 1.18), (0.25, 1.28), (0.30, 1.40), (0.40, 1.70),
            (0.50, 2.10), (0.60, 2.60), (0.80, 3.80), (1.00, 5.50),
        };
    }

    // --- JSON deserialization DTOs ---

    private class ConfigDocument
    {
        public string? Description { get; set; }
        public double Md0 { get; set; }
        public double RmBitOffset { get; set; }
        public double DirSensorOffset { get; set; }
        public bool InterpolationEnabled { get; set; }
        public double HSectionBearing { get; set; }
        public List<QfcTableRow>? QfcInterpTable { get; set; }
    }

    private class QfcTableRow
    {
        public double RateB { get; set; }
        public double Factor { get; set; }
    }
}
