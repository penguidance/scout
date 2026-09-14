// Scout.Collector — entry point.
//
// Collects the "system", "firmware", "cpu", "memory", "os", "storage", "devices", "gpu_topology"
// and "software" blocks of the machine profile (see docs/schema/profile-v0.1.md), plus
// machine_id, via Microsoft.Management.Infrastructure (CIM/WS-Man) — never the legacy
// System.Management (DCOM) stack — so the same collector code can later point at a remote
// machine simply by giving CimSession.Create a different computer name.
//
// gpu_topology is derived from the already-collected "devices" list (its Display-class entries),
// not from a separate WMI query.
//
// "software" is inventory only in this iteration — no Linux-equivalent mapping happens here, and
// nothing about it is shown in --report's HTML output yet (see SoftwareCollector).
//
// The remaining profile block (peripherals) is not implemented yet in this iteration. Rather than
// silently emitting empty-but-plausible data for it, it is flagged with a collection_errors entry
// so the output is honest about what was actually observed.
//
// Privacy default: hostname and serial_number are NOT collected unless --include-identifiers is
// passed. When it is not, that is recorded as a deliberate redaction in privacy.redacted_fields —
// not as a collection failure.
//
// --report <file.html> runs the profile straight through Scout.Analyzer + Scout.Reporter and
// writes the single-file HTML compatibility report. In that mode stdout gets a short summary
// (verdict, device counts, where the report was written — see ReportSummaryFormatter) instead of
// the raw JSON profile; pass --output as well to still get the JSON, written to a file.

using System.Reflection;
using Microsoft.Management.Infrastructure;
using Scout.Analyzer;
using Scout.Analyzer.Compatibility;
using Scout.Collector.Cim;
using Scout.Collector.Collectors;
using Scout.Core.Models;
using Scout.Core.Serialization;
using Scout.Reporter;

ParsedArguments arguments;
try
{
    arguments = ParseArguments(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

var errors = new List<CollectionError>();
var clock = TimeProvider.System;

// null = local machine. Pass a hostname here (plus CimSessionOptions for auth/transport) to
// collect from a remote target over WS-Man once that scenario is wired up.
using var session = CimSession.Create(null);
var cim = new CimSessionQueryExecutor(session);

var machineId = new MachineIdCollector(cim, clock).Collect(errors);
var systemInfo = new SystemCollector(cim, arguments.IncludeIdentifiers, clock).Collect(errors);
var firmwareInfo = new FirmwareCollector(cim, clock).Collect(errors);
var cpuInfo = new CpuCollector(cim, clock).Collect(errors);
var memoryInfo = new MemoryCollector(cim, clock).Collect(errors);
var osInfo = new OsCollector(cim, clock).Collect(errors);
var storageInfo = new StorageCollector(cim, clock).Collect(errors);
var devices = new DeviceCollector(cim, clock: clock).Collect(errors);
var gpuTopology = new GpuTopologyCollector(devices).Collect(errors);
var software = new SoftwareCollector(cim, clock: clock).Collect(errors);

MarkNotImplementedYet(errors, clock, "peripherals");

var redactedFields = new List<string>();
if (!arguments.IncludeIdentifiers)
{
    redactedFields.Add("system.hostname");
    redactedFields.Add("system.serial_number");
}

var profile = new MachineProfile
{
    SchemaVersion = "0.1",
    CollectorVersion = GetCollectorVersion(),
    CollectedAt = clock.GetUtcNow(),
    MachineId = machineId,
    Privacy = new PrivacyInfo
    {
        HostnameIncluded = systemInfo.Hostname is not null,
        UsernameIncluded = false,
        SerialNumbersIncluded = systemInfo.SerialNumber is not null,
        RedactedFields = redactedFields
    },
    System = systemInfo,
    Firmware = firmwareInfo,
    Cpu = cpuInfo,
    Memory = memoryInfo,
    Storage = storageInfo,
    Devices = devices,
    GpuTopology = gpuTopology,
    Os = osInfo,
    Software = software.Entries,
    SoftwareFilteredCount = software.FilteredCount,
    Peripherals = [],
    CollectionErrors = errors
};

var json = ProfileJsonSerializer.Serialize(profile);

if (arguments.OutputPath is not null)
{
    File.WriteAllText(arguments.OutputPath, json);
}

if (arguments.ReportPath is not null)
{
    // --report gets the short summary below instead of the raw JSON dump: someone asking for a
    // human-readable report wants a verdict on their terminal, not a profile they did not ask to
    // see (still available via --output if they also want the raw data).
    var analyzer = new MachineAnalyzer(new CompatibilityMatcher(CompatibilityDatabase.LoadEmbedded()));
    var analysis = analyzer.Analyze(profile);
    var html = new ReportGenerator().Generate(profile, analysis);
    File.WriteAllText(arguments.ReportPath, html);

    foreach (var line in ReportSummaryFormatter.BuildLines(analysis, arguments.ReportPath))
    {
        Console.WriteLine(line);
    }
}
else
{
    Console.WriteLine(json);
}

return 0;

static ParsedArguments ParseArguments(string[] args)
{
    string? outputPath = null;
    string? reportPath = null;
    var includeIdentifiers = false;

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

            case "--report":
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException("--report bir dosya yolu bekler.");
                }

                reportPath = args[++i];
                break;

            case "--include-identifiers":
                includeIdentifiers = true;
                break;

            default:
                throw new ArgumentException($"Bilinmeyen argüman: {args[i]}");
        }
    }

    return new ParsedArguments(outputPath, reportPath, includeIdentifiers);
}

static void MarkNotImplementedYet(List<CollectionError> errors, TimeProvider clock, params string[] components)
{
    foreach (var component in components)
    {
        errors.Add(new CollectionError
        {
            Component = component,
            Source = "Scout.Collector",
            Message = "Bu bölüm için toplama mantığı henüz uygulanmadı.",
            OccurredAt = clock.GetUtcNow()
        });
    }
}

static string GetCollectorVersion()
{
    var version = Assembly.GetExecutingAssembly().GetName().Version;
    return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
}

internal readonly record struct ParsedArguments(string? OutputPath, string? ReportPath, bool IncludeIdentifiers);
