using Scout.Core.Models;

namespace Scout.Tests.Analyzer;

/// <summary>Small factory for building minimal <see cref="DeviceInfo"/> fixtures in tests.</summary>
internal static class TestDevices
{
    public static DeviceInfo Create(
        string? vendorId,
        string? deviceId,
        DeviceBusType busType,
        string friendlyName = "Test Device",
        string deviceClass = "Net",
        IReadOnlyList<string>? compatibleIds = null,
        DeviceStatus? status = DeviceStatus.OK)
    {
        var rawSuffix = vendorId is not null
            ? $@"VEN_{vendorId}&DEV_{deviceId ?? "0000"}"
            : friendlyName.Replace(" ", "");

        return new DeviceInfo
        {
            HardwareId = vendorId is not null ? $@"{busType}\VEN_{vendorId}&DEV_{deviceId ?? "0000"}" : rawSuffix,
            HardwareIdRaw = vendorId is not null ? $@"{busType}\VEN_{vendorId}&DEV_{deviceId ?? "0000"}&REV_00" : rawSuffix,
            VendorId = vendorId,
            DeviceId = deviceId,
            BusType = busType,
            CompatibleIds = compatibleIds ?? [],
            FriendlyName = friendlyName,
            Class = deviceClass,
            Status = status
        };
    }
}
