using System.Text.RegularExpressions;
using Microsoft.Management.Infrastructure;
using Scout.Collector.Cim;
using Scout.Core.Models;

namespace Scout.Collector.Collectors;

/// <summary>
/// Collects <see cref="CpuInfo"/> from Win32_Processor — see docs/schema/profile-v0.1.md —
/// "Kaynak eşlemesi".
/// </summary>
public sealed partial class CpuCollector : ISectionCollector<CpuInfo>
{
    private const string Source = "Win32_Processor";

    private readonly ICimQueryExecutor _cim;
    private readonly TimeProvider _clock;

    public CpuCollector(ICimQueryExecutor cim, TimeProvider? clock = null)
    {
        _cim = cim ?? throw new ArgumentNullException(nameof(cim));
        _clock = clock ?? TimeProvider.System;
    }

    public string ComponentName => "cpu";

    public CpuInfo Collect(ICollection<CollectionError> errors)
    {
        var log = new CollectionErrorLog(errors, ComponentName, _clock);
        var processor = CimInstanceFetcher.TryGetFirst(_cim, "root/cimv2", "Win32_Processor", log);

        var vendor = processor.RequireString("Manufacturer", Source, log);
        var architecture = MapArchitecture(processor, log);

        return new CpuInfo
        {
            Vendor = vendor,
            Model = processor.RequireString("Name", Source, log),
            PhysicalCores = processor.RequireInt("NumberOfCores", Source, log),
            LogicalProcessors = processor.RequireInt("NumberOfLogicalProcessors", Source, log),
            Architecture = architecture,
            X86_64FeatureLevel = architecture == CpuArchitecture.x86_64
                ? DeriveFeatureLevel(processor, vendor, log)
                : null,
            VirtualizationFirmwareEnabled = processor.GetBool("VirtualizationFirmwareEnabled")
        };
    }

    /// <summary>
    /// Normalizes the raw Win32_Processor.Architecture code into the schema's
    /// <see cref="CpuArchitecture"/> category — a coding→label conversion, not a compatibility
    /// judgment. Null means no signal at all (source unreadable);
    /// <see cref="CpuArchitecture.Unknown"/> means a real code was read but not recognized.
    /// </summary>
    private static CpuArchitecture? MapArchitecture(CimInstance? processor, CollectionErrorLog log)
    {
        if (processor is null) return null;

        var code = processor.GetUInt32("Architecture");
        if (code is null)
        {
            log.Add($"{Source}.Architecture", "Mimari kodu okunamadı.");
            return null;
        }

        // https://learn.microsoft.com/windows/win32/cimwin32prov/win32-processor — Architecture
        return code switch
        {
            0 => CpuArchitecture.x86,
            9 => CpuArchitecture.x86_64,
            12 => CpuArchitecture.arm64,
            _ => CpuArchitecture.Unknown
        };
    }

    /// <summary>
    /// Derives the x86-64 microarchitecture feature level from the CPU's vendor and its raw
    /// CPUID family/model, parsed out of Win32_Processor.Description (e.g. "AMD64 Family 25
    /// Model 33 Stepping 2") — never from CPUID probing, so this also works against a remote
    /// target reachable only through CIM/WS-Man. See <see cref="X86FeatureLevelClassifier"/>.
    /// </summary>
    private static X86FeatureLevel? DeriveFeatureLevel(CimInstance? processor, string vendor, CollectionErrorLog log)
    {
        const string source = $"{Source}.Description";

        var description = processor.GetString("Description");
        if (description is null || !TryParseFamilyModel(description, out var family, out var model))
        {
            log.Add(source, "CPU ailesi/modeli 'Description' alanından ayrıştırılamadı.");
            return null;
        }

        var level = X86FeatureLevelClassifier.Classify(vendor, family, model);
        if (level is null)
        {
            log.Add(
                source,
                $"Bilinmeyen CPU ailesi/modeli için mikromimari seviyesi eşlemesi yok (vendor={vendor}, family={family}, model={model}).");
        }

        return level;
    }

    private static bool TryParseFamilyModel(string description, out int family, out int model)
    {
        var match = FamilyModelPattern().Match(description);
        if (!match.Success)
        {
            family = 0;
            model = 0;
            return false;
        }

        family = int.Parse(match.Groups[1].Value);
        model = int.Parse(match.Groups[2].Value);
        return true;
    }

    [GeneratedRegex(@"Family\s+(\d+)\s+Model\s+(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex FamilyModelPattern();
}
