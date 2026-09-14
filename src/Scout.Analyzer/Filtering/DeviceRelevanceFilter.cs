using Scout.Core.Models;

namespace Scout.Analyzer.Filtering;

/// <summary>
/// Strips the noise out of a profile's <c>devices[]</c> list before compatibility matching: WAN
/// miniports, Hyper-V virtual adapters, "Microsoft Print to PDF", Root\-enumerated software
/// devices, and repeated HID collections for the same physical device are not meaningful for a
/// Windows→Linux hardware compatibility assessment.
/// </summary>
public sealed class DeviceRelevanceFilter
{
    private readonly DeviceRelevanceOptions _options;

    public DeviceRelevanceFilter(DeviceRelevanceOptions? options = null)
    {
        _options = options ?? new DeviceRelevanceOptions();
    }

    public DeviceRelevanceResult Filter(IReadOnlyList<DeviceInfo> devices)
    {
        var kept = new List<DeviceInfo>(devices.Count);
        var removedForRelevance = 0;

        foreach (var device in devices)
        {
            var busIsInherentlyRelevant = _options.InherentlyRelevantBusTypes.Contains(device.BusType);
            if (!busIsInherentlyRelevant && device.VendorId is null)
            {
                removedForRelevance++;
                continue;
            }

            kept.Add(device);
        }

        var relevant = kept;
        var removedForDuplication = 0;
        if (_options.DeduplicateByVendorAndDevice)
        {
            (relevant, removedForDuplication) = Deduplicate(kept);
        }

        return new DeviceRelevanceResult
        {
            RelevantDevices = relevant,
            RemovedForRelevance = removedForRelevance,
            RemovedForDuplication = removedForDuplication
        };
    }

    /// <summary>
    /// Collapses devices sharing a vendor_id+device_id pair into one, keeping whichever
    /// friendly_name is longest as a simple, deterministic proxy for "most descriptive". Devices
    /// missing either ID (nothing to key on) are never deduplicated against each other.
    /// </summary>
    private static (List<DeviceInfo> Result, int RemovedCount) Deduplicate(IReadOnlyList<DeviceInfo> devices)
    {
        var byKey = new Dictionary<(string VendorId, string DeviceId), DeviceInfo>();
        var unkeyed = new List<DeviceInfo>();
        var removed = 0;

        foreach (var device in devices)
        {
            if (device.VendorId is null || device.DeviceId is null)
            {
                unkeyed.Add(device);
                continue;
            }

            var key = (device.VendorId.ToUpperInvariant(), device.DeviceId.ToUpperInvariant());
            if (byKey.TryGetValue(key, out var existing))
            {
                removed++;
                if (device.FriendlyName.Length > existing.FriendlyName.Length)
                {
                    byKey[key] = device;
                }
            }
            else
            {
                byKey[key] = device;
            }
        }

        var result = new List<DeviceInfo>(byKey.Count + unkeyed.Count);
        result.AddRange(byKey.Values);
        result.AddRange(unkeyed);
        return (result, removed);
    }
}
