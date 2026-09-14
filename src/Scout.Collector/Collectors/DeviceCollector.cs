using Microsoft.Management.Infrastructure;
using Scout.Collector.Cim;
using Scout.Core.Models;

namespace Scout.Collector.Collectors;

/// <summary>
/// Collects <see cref="DeviceInfo"/> entries from Win32_PnPEntity (joined to
/// Win32_PnPSignedDriver for driver details), filtered to <see cref="RelevantDeviceClasses"/> —
/// a typical machine enumerates thousands of PnP entities, and dumping all of them would not be
/// useful for Windows→Linux compatibility triage. See docs/schema/profile-v0.1.md.
/// </summary>
public sealed class DeviceCollector : ISectionCollector<IReadOnlyList<DeviceInfo>>
{
    private const string PnPEntitySource = "Win32_PnPEntity";
    private const string SignedDriverSource = "Win32_PnPSignedDriver";

    private readonly ICimQueryExecutor _cim;
    private readonly IReadOnlySet<string> _relevantClasses;
    private readonly TimeProvider _clock;

    public DeviceCollector(ICimQueryExecutor cim, IReadOnlySet<string>? relevantClasses = null, TimeProvider? clock = null)
    {
        _cim = cim ?? throw new ArgumentNullException(nameof(cim));
        _relevantClasses = relevantClasses ?? RelevantDeviceClasses.Default;
        _clock = clock ?? TimeProvider.System;
    }

    public string ComponentName => "devices";

    public IReadOnlyList<DeviceInfo> Collect(ICollection<CollectionError> errors)
    {
        var log = new CollectionErrorLog(errors, ComponentName, _clock);

        IReadOnlyList<CimInstance> entities;
        try
        {
            entities = _cim.QueryInstances("root/cimv2", PnPEntitySource);
        }
        catch (Exception ex)
        {
            log.Add(PnPEntitySource, ex.Message);
            return [];
        }

        var driversByDeviceId = FetchDriversByDeviceId(log);

        var results = new List<DeviceInfo>();
        var skippedNoHardwareId = 0;

        foreach (var entity in entities)
        {
            var pnpClass = entity.GetString("PNPClass");
            if (pnpClass is null || !_relevantClasses.Contains(pnpClass)) continue;

            var hardwareIds = entity.GetStringArray("HardwareID");
            if (hardwareIds is not { Length: > 0 })
            {
                // No hardware ID at all means no usable matching key — nothing worth emitting.
                skippedNoHardwareId++;
                continue;
            }

            var rawHardwareId = hardwareIds[0];
            var parsed = HardwareIdParser.Parse(rawHardwareId);
            var driver = FindDriver(entity, driversByDeviceId);

            results.Add(new DeviceInfo
            {
                HardwareId = parsed.NormalizedId,
                HardwareIdRaw = rawHardwareId,
                VendorId = parsed.VendorId,
                DeviceId = parsed.DeviceId,
                BusType = parsed.BusType,
                CompatibleIds = hardwareIds.Skip(1).ToList(),
                FriendlyName = entity.RequireString("Name", PnPEntitySource, log),
                Class = pnpClass,
                DriverProvider = driver?.GetString("DriverProvider"),
                DriverVersion = driver?.GetString("DriverVersion"),
                DriverDate = driver?.GetDateOnly("DriverDate"),
                Status = MapStatus(entity, log)
            });
        }

        if (skippedNoHardwareId > 0)
        {
            log.Add(PnPEntitySource, $"{skippedNoHardwareId} ilgili sınıftaki cihaz HardwareID bilgisi olmadığı için atlandı.");
        }

        return results;
    }

    private static CimInstance? FindDriver(CimInstance entity, IReadOnlyDictionary<string, CimInstance> driversByDeviceId)
    {
        var deviceId = entity.GetString("DeviceID");
        return deviceId is not null && driversByDeviceId.TryGetValue(deviceId, out var driver) ? driver : null;
    }

    /// <summary>Fetches every signed driver once and indexes it by DeviceID, to avoid one CIM round-trip per device.</summary>
    private IReadOnlyDictionary<string, CimInstance> FetchDriversByDeviceId(CollectionErrorLog log)
    {
        try
        {
            var drivers = _cim.QueryInstances("root/cimv2", SignedDriverSource);
            var result = new Dictionary<string, CimInstance>(StringComparer.OrdinalIgnoreCase);
            foreach (var driver in drivers)
            {
                var deviceId = driver.GetString("DeviceID");
                if (deviceId is not null)
                {
                    result[deviceId] = driver;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            log.Add(SignedDriverSource, ex.Message);
            return new Dictionary<string, CimInstance>();
        }
    }

    /// <summary>
    /// 0 = working properly (OK); any other ConfigManagerErrorCode = some kind of problem
    /// (Error). This does not yet distinguish the ~30 documented CM_PROB_* codes (e.g. "disabled
    /// by user" vs. "drivers not installed") — a coarser but honest v0.1 mapping.
    /// </summary>
    private static DeviceStatus? MapStatus(CimInstance entity, CollectionErrorLog log)
    {
        var code = entity.GetUInt32("ConfigManagerErrorCode");
        if (code is null)
        {
            log.Add($"{PnPEntitySource}.ConfigManagerErrorCode", "Alan okunamadı.");
            return null;
        }

        return code == 0 ? DeviceStatus.OK : DeviceStatus.Error;
    }
}
