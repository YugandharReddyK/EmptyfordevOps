using NgrDrillSheetApp.IO;
using NgrDrillSheetApp.Models;
using NgrDrillSheetApp.Processing;

namespace NgrDrillSheetApp;

/// <summary>
/// NGR Drill Sheet Console Application – JSON-based I/O
/// Directional drilling survey processing: QFC, position correlation, H-Section projection.
/// Migrated from VAFI10 NGR DrillSheet VBA application.
///
/// Usage:
///   NgrDrillSheetApp process  --surveys &lt;file.json&gt; --ref-well &lt;file.json&gt; [--config &lt;file.json&gt;] [--out &lt;results.json&gt;]
///   NgrDrillSheetApp export-raw --surveys &lt;file.json&gt; --out &lt;raw.json&gt; [--all]
///   NgrDrillSheetApp help
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("=== NGR Drill Sheet Processor v2.1 (JSON) ===");
        Console.WriteLine("  Migrated from VAFI10 VBA Release 1.9");
        Console.WriteLine();

        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string command = args[0].ToLowerInvariant();
        var opts = ParseArgs(args);

        return command switch
        {
            "process" => RunProcess(opts),
            "export-raw" => RunExportRaw(opts),
            "help" or "--help" or "-h" => PrintUsageReturn(),
            _ => PrintUnknownCommand(command),
        };
    }

    // ========== Commands ==========

    static int RunProcess(Dictionary<string, string> opts)
    {
        if (!ValidateRequired(opts, "--surveys", "--ref-well")) return 1;

        // Load config (from file or defaults)
        var configReader = new ConfigJsonReader();
        var config = opts.TryGetValue("--config", out var cfgPath)
            ? configReader.ReadFromFile(cfgPath)
            : ConfigJsonReader.GetDefaults();

        // Load surveys
        var surveyReader = new SurveyJsonReader();
        Console.WriteLine($"Loading surveys: {opts["--surveys"]}");
        var records = surveyReader.ReadFromFile(opts["--surveys"], config.RmBitOffset);

        if (records.Count == 0)
        {
            Console.Error.WriteLine("No survey records loaded. Aborting.");
            return 1;
        }

        // Load reference well
        var refWellReader = new ReferenceWellJsonReader();
        Console.WriteLine($"Loading reference well: {opts["--ref-well"]}");
        var refWell = refWellReader.ReadFromFile(opts["--ref-well"]);

        // Process
        var processor = new WellDataProcessor(config, refWell);
        var solutions = processor.Process(records);

        // Print summary to console
        PrintSolutionSummary(solutions);

        // Export results to JSON
        string outputPath = opts.TryGetValue("--out", out var outVal)
            ? outVal
            : Path.Combine(Directory.GetCurrentDirectory(), "results.json");

        var exporter = new ResultsJsonExporter();
        exporter.ExportResults(outputPath, config, refWell, records, solutions);

        Console.WriteLine();
        Console.WriteLine($"Done. Full results written to: {Path.GetFullPath(outputPath)}");
        return 0;
    }

    static int RunExportRaw(Dictionary<string, string> opts)
    {
        if (!ValidateRequired(opts, "--surveys", "--out")) return 1;

        double rmBitOffset = opts.TryGetValue("--bit-offset", out var bo) ? double.Parse(bo) : 0.0;
        bool allRecords = opts.ContainsKey("--all");

        var surveyReader = new SurveyJsonReader();
        Console.WriteLine($"Loading surveys: {opts["--surveys"]}");
        var records = surveyReader.ReadFromFile(opts["--surveys"], rmBitOffset);

        var exporter = new ResultsJsonExporter();
        exporter.ExportRawSurveys(opts["--out"], records, goodOnly: !allRecords);
        return 0;
    }

    // ========== Console output ==========

    static void PrintSolutionSummary(List<SolutionRecord> solutions)
    {
        Console.WriteLine();
        Console.WriteLine("=== Solution Summary ===");
        Console.WriteLine($"{"Rec",-5} {"MD",8} {"QFa",-9} {"QFr",-9} {"CFlag",-9} {"HS",8} {"RS",8} {"PsdMD",8} {"AziDiv",-12} {"IncDiv",-12}");
        Console.WriteLine(new string('-', 100));

        foreach (var sol in solutions)
        {
            Console.WriteLine(
                $"{sol.RecordNumber,-5} " +
                $"{sol.MdMwd,8:F2} " +
                $"{sol.QfcQFa.ToDisplayString(),-9} " +
                $"{sol.QfcQFr.ToDisplayString(),-9} " +
                $"{sol.QfcCFlag.ToCorrectionString(),-9} " +
                $"{sol.PcoHS,8:F2} " +
                $"{sol.PcoRS,8:F2} " +
                $"{sol.PcoPsdMd,8:F2} " +
                $"{sol.AziDivCon,-12} " +
                $"{sol.IncDivCon,-12}");
        }
    }

    // ========== Arg parsing ==========

    static Dictionary<string, string> ParseArgs(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                string key = args[i];
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    result[key] = args[i + 1];
                    i++;
                }
                else
                {
                    result[key] = "true"; // Flag-style argument
                }
            }
        }
        return result;
    }

    static bool ValidateRequired(Dictionary<string, string> opts, params string[] required)
    {
        foreach (var key in required)
        {
            if (!opts.ContainsKey(key))
            {
                Console.Error.WriteLine($"Missing required argument: {key}");
                PrintUsage();
                return false;
            }
        }
        return true;
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  NgrDrillSheetApp process    --surveys <file.json> --ref-well <file.json> [options]");
        Console.WriteLine("  NgrDrillSheetApp export-raw --surveys <file.json> --out <raw.json> [--all]");
        Console.WriteLine("  NgrDrillSheetApp help");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  process      Import surveys + reference well, run QFC + position correlation,");
        Console.WriteLine("               export full results to JSON.");
        Console.WriteLine("  export-raw   Export survey records to JSON (Good records only, or --all).");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --surveys <file.json>   Survey input JSON file (required for all commands)");
        Console.WriteLine("  --ref-well <file.json>  Reference well JSON file (required for process)");
        Console.WriteLine("  --config <file.json>    Processing config JSON file (optional; uses defaults)");
        Console.WriteLine("  --out <file.json>       Output file path (default: results.json)");
        Console.WriteLine("  --all                   Include Bad/Unknown records in export-raw");
        Console.WriteLine("  --bit-offset <meters>   Override RM bit offset for export-raw");
        Console.WriteLine();
        Console.WriteLine("Sample data files are provided in the SampleData/ folder.");
    }

    static int PrintUsageReturn() { PrintUsage(); return 0; }

    static int PrintUnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"Unknown command: {cmd}");
        PrintUsage();
        return 1;
    }
}
