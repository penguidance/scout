using Scout.Core.Models;

namespace Scout.Analyzer.Filtering;

/// <summary>Configuration for <see cref="DeviceRelevanceFilter"/>.</summary>
public sealed class DeviceRelevanceOptions
{
    /// <summary>
    /// Bus types treated as inherently worth keeping regardless of whether a vendor_id could be
    /// extracted (e.g. a USB root hub with no VID_/PID_ in its hardware ID is still real,
    /// physical USB hardware). Devices on any other bus are kept only when they do have a
    /// vendor_id — see <see cref="DeviceRelevanceFilter"/>.
    /// </summary>
    public IReadOnlySet<DeviceBusType> InherentlyRelevantBusTypes { get; init; } =
        new HashSet<DeviceBusType> { DeviceBusType.PCI, DeviceBusType.USB };

    /// <summary>
    /// When true (default), devices sharing the same vendor_id+device_id pair (e.g. several HID
    /// collections reported for one physical keyboard) are collapsed into a single entry.
    /// </summary>
    public bool DeduplicateByVendorAndDevice { get; init; } = true;
}
