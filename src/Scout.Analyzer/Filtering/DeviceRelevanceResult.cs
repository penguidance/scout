using Scout.Core.Models;

namespace Scout.Analyzer.Filtering;

/// <summary>Result of running <see cref="DeviceRelevanceFilter"/> over a profile's device list.</summary>
public sealed class DeviceRelevanceResult
{
    public required IReadOnlyList<DeviceInfo> RelevantDevices { get; init; }

    /// <summary>Devices dropped because they carry no usable signal — no PCI/USB bus and no vendor_id.</summary>
    public required int RemovedForRelevance { get; init; }

    /// <summary>Devices dropped because they duplicated an already-kept vendor_id+device_id pair.</summary>
    public required int RemovedForDuplication { get; init; }
}
