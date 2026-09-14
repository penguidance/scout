using Microsoft.Management.Infrastructure;
using Scout.Collector.Cim;
using Scout.Core.Models;

namespace Scout.Collector.Collectors;

/// <summary>
/// Collects <see cref="MemoryInfo"/> from Win32_PhysicalMemory (one instance per populated
/// DIMM/SODIMM slot). Win32_PhysicalMemoryArray is also queried, but only to tell apart two very
/// different reasons Win32_PhysicalMemory might report zero modules: a real collection failure,
/// versus a virtualized/BIOS environment that simply does not expose per-module SMBIOS Type 17
/// data (a well-known limitation on some VMs) even though a memory array clearly exists.
/// </summary>
public sealed class MemoryCollector : ISectionCollector<MemoryInfo>
{
    private const string ModuleSource = "Win32_PhysicalMemory";
    private const string ArraySource = "Win32_PhysicalMemoryArray";

    // SMBIOS Type 17 "Memory Type" codes we bother decoding to a friendly label; anything else
    // keeps its raw numeric code (as a string) rather than being silently dropped.
    private static readonly Dictionary<uint, string> MemoryTypeNames = new()
    {
        [20] = "DDR",
        [21] = "DDR2",
        [24] = "DDR3",
        [26] = "DDR4",
        [34] = "DDR5"
    };

    private readonly ICimQueryExecutor _cim;
    private readonly TimeProvider _clock;

    public MemoryCollector(ICimQueryExecutor cim, TimeProvider? clock = null)
    {
        _cim = cim ?? throw new ArgumentNullException(nameof(cim));
        _clock = clock ?? TimeProvider.System;
    }

    public string ComponentName => "memory";

    public MemoryInfo Collect(ICollection<CollectionError> errors)
    {
        var log = new CollectionErrorLog(errors, ComponentName, _clock);

        IReadOnlyList<CimInstance> moduleInstances;
        try
        {
            moduleInstances = _cim.QueryInstances("root/cimv2", ModuleSource);
        }
        catch (Exception ex)
        {
            log.Add(ModuleSource, ex.Message);
            moduleInstances = [];
        }

        var modules = moduleInstances.Select(BuildModule).ToList();

        long? totalBytes;
        if (moduleInstances.Count > 0)
        {
            totalBytes = SumCapacities(modules, log);
        }
        else
        {
            totalBytes = null;
            LogEmptyModulesReason(log);
        }

        return new MemoryInfo { TotalBytes = totalBytes, Modules = modules };
    }

    private static long? SumCapacities(IReadOnlyList<MemoryModule> modules, CollectionErrorLog log)
    {
        if (modules.Any(m => m.CapacityBytes is null))
        {
            log.Add(
                $"{ModuleSource}.Capacity",
                "En az bir modülün kapasitesi okunamadı; total_bytes eksik/güvenilmez olabilir.");
        }

        // Sum only the modules we could actually read; an unreadable module contributes nothing
        // rather than silently being treated as 0 bytes of real capacity.
        var readable = modules.Where(m => m.CapacityBytes is not null).ToList();
        return readable.Count > 0 ? readable.Sum(m => m.CapacityBytes!.Value) : null;
    }

    private void LogEmptyModulesReason(CollectionErrorLog log)
    {
        try
        {
            var arrayInstances = _cim.QueryInstances("root/cimv2", ArraySource);
            log.Add(
                ModuleSource,
                arrayInstances.Count > 0
                    ? "0 örnek döndürdü; bellek dizisi (Win32_PhysicalMemoryArray) mevcut olduğundan bu muhtemelen sanallaştırılmış bir ortamın SMBIOS Type 17 verisi sunmamasından kaynaklanıyor."
                    : "0 örnek döndürdü.");
        }
        catch (Exception ex)
        {
            log.Add(ArraySource, ex.Message);
            log.Add(ModuleSource, "0 örnek döndürdü.");
        }
    }

    private static MemoryModule BuildModule(CimInstance instance)
    {
        var (memoryType, memoryTypeRaw) = DecodeMemoryType(instance);

        return new MemoryModule
        {
            CapacityBytes = instance.GetInt64("Capacity"),
            RatedSpeedMhz = instance.GetUInt32("Speed") is { } speed ? (int)speed : null,
            ConfiguredSpeedMhz = instance.GetUInt32("ConfiguredClockSpeed") is { } configured ? (int)configured : null,
            Manufacturer = instance.GetString("Manufacturer"),
            PartNumber = instance.GetString("PartNumber"),
            MemoryType = memoryType,
            MemoryTypeRaw = memoryTypeRaw
        };
    }

    /// <summary>
    /// Deliberately reads SMBIOSMemoryType, not the older MemoryType property: on modern Windows,
    /// Win32_PhysicalMemory.MemoryType is unreliable and commonly just reports 0 regardless of
    /// the module's real type, while SMBIOSMemoryType carries the actual SMBIOS Type 17 code.
    /// </summary>
    private static (string? Type, string? Raw) DecodeMemoryType(CimInstance instance)
    {
        var code = instance.GetUInt32("SMBIOSMemoryType");
        if (code is null) return (null, null);

        var raw = code.Value.ToString();
        // An unrecognized code is a real, if incomplete, observation — never presented as if it
        // were a resolved label. See MemoryType/MemoryTypeRaw remarks.
        return MemoryTypeNames.TryGetValue(code.Value, out var name) ? (name, raw) : (null, raw);
    }
}
