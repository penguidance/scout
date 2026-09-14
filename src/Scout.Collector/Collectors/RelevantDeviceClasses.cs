namespace Scout.Collector.Collectors;

/// <summary>
/// PNPClass values worth surfacing for Windows→Linux compatibility triage out of the thousands
/// of PnP entities a typical machine enumerates. A plain data set, not a hard-coded switch in
/// <see cref="DeviceCollector"/> — extend it by adding a name, no other code changes needed.
/// </summary>
public static class RelevantDeviceClasses
{
    public static readonly IReadOnlySet<string> Default = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Display",
        "Net",
        "Media",
        "Bluetooth",
        "USB",
        "SCSIAdapter",
        "Biometric",
        "Camera",
        "PrintQueue",
        "HIDClass",
        "SmartCardReader"
    };
}
