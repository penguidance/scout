using Scout.Collector.Collectors;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Collectors;

public class DeviceCollectorTests
{
    private static Microsoft.Management.Infrastructure.CimInstance PnPEntity(
        string pnpClass,
        string[] hardwareIds,
        string name = "Test Device",
        string deviceId = "DEV1",
        uint? configManagerErrorCode = 0) =>
        FakeCimQueryExecutor.Instance(
            "Win32_PnPEntity",
            ("PNPClass", pnpClass),
            ("HardwareID", hardwareIds),
            ("Name", name),
            ("DeviceID", deviceId),
            ("ConfigManagerErrorCode", configManagerErrorCode));

    [Fact]
    public void Collect_FiltersToRelevantClassesOnly()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_PnPEntity",
            PnPEntity("Display", [@"PCI\VEN_1002&DEV_7340"], deviceId: "D1"),
            PnPEntity("Volume", [@"STORAGE\Volume\1"], deviceId: "D2"), // not in the default allow-list
            PnPEntity("Net", [@"PCI\VEN_8086&DEV_15F2"], deviceId: "D3"));
        cim.SetInstances("root/cimv2", "Win32_PnPSignedDriver");

        var result = new DeviceCollector(cim).Collect([]);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, d => d.Class == "Volume");
    }

    [Fact]
    public void Collect_MapsHardwareIdVendorDeviceBusTypeAndCompatibleIds()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_PnPEntity", PnPEntity(
            "Display",
            [
                @"PCI\VEN_1002&DEV_7340&SUBSYS_059B1043&REV_C5",
                @"PCI\VEN_1002&DEV_7340&SUBSYS_059B1043",
                @"PCI\VEN_1002&DEV_7340&CC_030000"
            ],
            name: "AMD Radeon RX 5500 XT"));
        cim.SetInstances("root/cimv2", "Win32_PnPSignedDriver");

        var device = Assert.Single(new DeviceCollector(cim).Collect([]));

        Assert.Equal(@"PCI\VEN_1002&DEV_7340", device.HardwareId);
        Assert.Equal(@"PCI\VEN_1002&DEV_7340&SUBSYS_059B1043&REV_C5", device.HardwareIdRaw);
        Assert.Equal("1002", device.VendorId);
        Assert.Equal("7340", device.DeviceId);
        Assert.Equal(DeviceBusType.PCI, device.BusType);
        Assert.Equal(
            [@"PCI\VEN_1002&DEV_7340&SUBSYS_059B1043", @"PCI\VEN_1002&DEV_7340&CC_030000"],
            device.CompatibleIds);
        Assert.Equal("AMD Radeon RX 5500 XT", device.FriendlyName);
    }

    [Fact]
    public void Collect_JoinsDriverInfoByDeviceId()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_PnPEntity", PnPEntity(
            "Net", [@"PCI\VEN_8086&DEV_15F2"], deviceId: @"PCI\VEN_8086&DEV_15F2\4&1234&0&0019"));
        cim.SetInstances("root/cimv2", "Win32_PnPSignedDriver", FakeCimQueryExecutor.Instance(
            "Win32_PnPSignedDriver",
            ("DeviceID", @"PCI\VEN_8086&DEV_15F2\4&1234&0&0019"),
            ("DriverProvider", "Intel"),
            ("DriverVersion", "12.19.2.61"),
            ("DriverDate", new DateTime(2025, 3, 1))));

        var device = Assert.Single(new DeviceCollector(cim).Collect([]));

        Assert.Equal("Intel", device.DriverProvider);
        Assert.Equal("12.19.2.61", device.DriverVersion);
        Assert.Equal(new DateOnly(2025, 3, 1), device.DriverDate);
    }

    [Theory]
    [InlineData((uint)0, DeviceStatus.OK)]
    [InlineData((uint)22, DeviceStatus.Error)]
    [InlineData((uint)10, DeviceStatus.Error)]
    public void Collect_MapsConfigManagerErrorCodeToStatus(uint code, DeviceStatus expected)
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_PnPEntity", PnPEntity(
            "Net", [@"PCI\VEN_8086&DEV_15F2"], configManagerErrorCode: code));
        cim.SetInstances("root/cimv2", "Win32_PnPSignedDriver");

        var device = Assert.Single(new DeviceCollector(cim).Collect([]));

        Assert.Equal(expected, device.Status);
    }

    [Fact]
    public void Collect_WhenConfigManagerErrorCodeMissing_LeavesStatusNullAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_PnPEntity", PnPEntity(
            "Net", [@"PCI\VEN_8086&DEV_15F2"], configManagerErrorCode: null));
        cim.SetInstances("root/cimv2", "Win32_PnPSignedDriver");

        var errors = new List<CollectionError>();
        var device = Assert.Single(new DeviceCollector(cim).Collect(errors));

        Assert.Null(device.Status);
        Assert.Contains(errors, e => e.Component == "devices" && e.Source.Contains("ConfigManagerErrorCode"));
    }

    [Fact]
    public void Collect_SkipsDevicesWithNoHardwareIdAndLogsACount()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_PnPEntity",
            PnPEntity("Net", []), // empty HardwareID array
            PnPEntity("Net", [@"PCI\VEN_8086&DEV_15F2"]));
        cim.SetInstances("root/cimv2", "Win32_PnPSignedDriver");

        var errors = new List<CollectionError>();
        var result = new DeviceCollector(cim).Collect(errors);

        Assert.Single(result);
        Assert.Contains(errors, e => e.Component == "devices" && e.Message.Contains("1"));
    }

    [Fact]
    public void Collect_WhenPnPEntityQueryThrows_ReturnsEmptyAndLogsError()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstanceQueryFailure("root/cimv2", "Win32_PnPEntity", new InvalidOperationException("bağlantı koptu"));

        var errors = new List<CollectionError>();
        var result = new DeviceCollector(cim).Collect(errors);

        Assert.Empty(result);
        Assert.Contains(errors, e => e.Component == "devices" && e.Source == "Win32_PnPEntity");
    }

    [Fact]
    public void Collect_WithCustomRelevantClasses_OverridesTheDefaultAllowList()
    {
        var cim = new FakeCimQueryExecutor();
        cim.SetInstances("root/cimv2", "Win32_PnPEntity",
            PnPEntity("Display", [@"PCI\VEN_1002&DEV_7340"]),
            PnPEntity("Volume", [@"STORAGE\Volume\1"]));
        cim.SetInstances("root/cimv2", "Win32_PnPSignedDriver");

        var customClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Volume" };
        var result = new DeviceCollector(cim, customClasses).Collect([]);

        var device = Assert.Single(result);
        Assert.Equal("Volume", device.Class);
    }
}
