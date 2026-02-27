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

        return 0;
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
            //$"{sol.QfcQFa.ToDisplayString(),-9} " +
            //$"{sol.QfcQFr.ToDisplayString(),-9} " +
            //$"{sol.QfcCFlag.ToCorrectionString(),-9} " +
            //$"{sol.PcoHS,8:F2} " +
            //$"{sol.PcoRS,8:F2} " +
            // $"{sol.PcoToMd,8:F2} " +
            //$"{sol.PcoToInc,8:F2} " +
            //$"{sol.PcoToAzi,8:F2} " +
            //$"{sol.PcoToNorth,8:F2} " +
            //$"{sol.PcoToEast,8:F2} " +
            //$"{sol.PcoToTvd,8:F2} " +
            //$"{sol.PcoPsdMd,8:F2} " +
            //$"{sol.PcoPsdInc,8:F2} " +
            //$"{sol.PcoPsdAzi,8:F2} " +
            //$"{sol.PcoPsdNorth,8:F2} " +
            //$"{sol.PcoPsdEast,8:F2} " +
            //$"{sol.PcoPsdTvd,8:F2} " +
            //$"{sol.PcoMinDist,8:F2} " +
            //$"{sol.PcoErrTotal,8:F2} " +
            //$"{sol.PcoErrNorth,8:F2} " +
            //$"{sol.PcoErrEast,8:F2} " +
            //$"{sol.PcoErrTvd,8:F2} " +
            //$"{sol.PcoMdBack,8:F2} " +
            //$"{sol.PcoIncBack,8:F2} " +
            //$"{sol.PcoAzBack,8:F2} " +
            //$"{sol.AziRefDiff,8:F2} " +
            //$"{sol.IncRefDiff,8:F2} " +
            $"{sol.AziDivCon,-12} " +
            $"{sol.IncDivCon,-12} " +
            $"{sol.HsRefRef,-12} " +
            $"{sol.RsRefRef,-12}"
            );
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

        Console.WriteLine("Enter 1 for Processor,2 for export-raw json, 3 for help");
        int input = int.Parse(Console.ReadLine());

        string projectRoot = Path.GetFullPath(Path.Combine(
       AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));

        string samplesDir = Path.Combine(projectRoot, "SampleData");
        switch (input)
        {
            case 1:
                string[] args1 = { "process",
                "--surveys",  Path.Combine(samplesDir, "ngr-surveys.json"),
                "--ref-well", Path.Combine(samplesDir, "reference-well.json"),
                "--config",   Path.Combine(samplesDir, "config.json"),
                "--out",      Path.Combine(samplesDir, "results.json") };
                var opts1 = ParseArgs(args1);
                RunProcess(opts1);
                break;
            case 2:
                string[] args2 = { "export-raw",
                "--surveys", Path.Combine(samplesDir, "ngr-surveys.json"),
                "--out",     Path.Combine(samplesDir, "raw-surveys.json") };
                var opts2 = ParseArgs(args2);
                RunExportRaw(opts2);
                break;
            case 3:
                PrintUsageReturn();
                break;
            default:
                PrintUnknownCommand(input + "");
                break;
        }
    }

    static int PrintUsageReturn() { PrintUsage(); return 0; }

    static int PrintUnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"Unknown command: {cmd}");
        PrintUsage();
        return 1;
    }
}
