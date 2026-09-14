// Scout.Reporter — standalone entry point.
//
// Turns an already-collected machine profile JSON into the single-file HTML report, without
// needing Scout.Collector or a live machine at all — useful for regenerating a report after the
// compatibility database or the report template changes, or for building one from a profile
// someone else collected and sent over.
//
// Usage: Scout.Reporter <profile.json> [--output <report.html>]
// If --output is omitted, the report is written next to the input file with a .html extension.

using Scout.Analyzer;
using Scout.Analyzer.Compatibility;
using Scout.Core.Serialization;
using Scout.Reporter;

if (args.Length == 0)
{
    Console.Error.WriteLine("Kullanım: Scout.Reporter <profile.json> [--output <rapor.html>]");
    return 1;
}

string profilePath;
string? outputPath = null;

try
{
    (profilePath, outputPath) = ParseArguments(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

if (!File.Exists(profilePath))
{
    Console.Error.WriteLine($"Profil dosyası bulunamadı: {profilePath}");
    return 1;
}

outputPath ??= Path.ChangeExtension(profilePath, ".html");

var profile = ProfileJsonSerializer.Deserialize(File.ReadAllText(profilePath));
var analyzer = new MachineAnalyzer(new CompatibilityMatcher(CompatibilityDatabase.LoadEmbedded()));
var analysis = analyzer.Analyze(profile);

var html = new ReportGenerator().Generate(profile, analysis);
File.WriteAllText(outputPath, html);

Console.WriteLine($"Rapor yazıldı: {outputPath}");
return 0;

static (string ProfilePath, string? OutputPath) ParseArguments(string[] args)
{
    string? profilePath = null;
    string? outputPath = null;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--output" or "-o":
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException("--output bir dosya yolu bekler.");
                }

                outputPath = args[++i];
                break;

            default:
                if (profilePath is not null)
                {
                    throw new ArgumentException($"Bilinmeyen argüman: {args[i]}");
                }

                profilePath = args[i];
                break;
        }
    }

    if (profilePath is null)
    {
        throw new ArgumentException("Bir profil JSON dosyası yolu vermelisin.");
    }

    return (profilePath, outputPath);
}
