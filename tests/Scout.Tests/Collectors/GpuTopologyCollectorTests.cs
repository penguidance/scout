using Scout.Collector.Collectors;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Collectors;

public class GpuTopologyCollectorTests
{
    private static DeviceInfo Gpu(string vendorId, string deviceId, string friendlyName, string driverVersion = "1.0") => new()
    {
        HardwareId = $@"PCI\VEN_{vendorId}&DEV_{deviceId}",
        HardwareIdRaw = $@"PCI\VEN_{vendorId}&DEV_{deviceId}&SUBSYS_00000000&REV_00",
        VendorId = vendorId,
        DeviceId = deviceId,
        BusType = DeviceBusType.PCI,
        CompatibleIds = [],
        FriendlyName = friendlyName,
        Class = "Display",
        DriverVersion = driverVersion,
        Status = DeviceStatus.OK
    };

    private static DeviceInfo NonGpu() => new()
    {
        HardwareId = @"PCI\VEN_8086&DEV_15F2",
        HardwareIdRaw = @"PCI\VEN_8086&DEV_15F2",
        VendorId = "8086",
        DeviceId = "15F2",
        BusType = DeviceBusType.PCI,
        CompatibleIds = [],
        FriendlyName = "Intel Ethernet Adapter",
        Class = "Net",
        Status = DeviceStatus.OK
    };

    [Fact]
    public void Collect_IgnoresNonDisplayDevices()
    {
        var devices = new List<DeviceInfo> { NonGpu(), Gpu("10DE", "2504", "NVIDIA GeForce RTX 3060") };

        var result = new GpuTopologyCollector(devices).Collect([]);

        Assert.Single(result.Gpus);
    }

    [Fact]
    public void Collect_WithOneGpu_ReportsSingleLayout()
    {
        var devices = new List<DeviceInfo> { Gpu("1002", "7340", "AMD Radeon RX 5500 XT") };

        var result = new GpuTopologyCollector(devices).Collect([]);

        Assert.Equal(GpuLayout.Single, result.Layout);
        Assert.Equal("1002", result.Gpus[0].VendorId);
        Assert.Equal("7340", result.Gpus[0].DeviceId);
        Assert.Equal("AMD Radeon RX 5500 XT", result.Gpus[0].FriendlyName);
    }

    [Fact]
    public void Collect_WithNoGpus_ReportsNullLayout()
    {
        var result = new GpuTopologyCollector([NonGpu()]).Collect([]);

        Assert.Empty(result.Gpus);
        Assert.Null(result.Layout);
    }

    [Fact]
    public void Collect_WithIntegratedAndDiscrete_ReportsHybridLayout()
    {
        var devices = new List<DeviceInfo>
        {
            Gpu("8086", "9BC4", "Intel(R) UHD Graphics 630"),
            Gpu("10DE", "2504", "NVIDIA GeForce RTX 3060")
        };

        var result = new GpuTopologyCollector(devices).Collect([]);

        Assert.Equal(GpuLayout.Hybrid, result.Layout);
        Assert.Equal(2, result.Gpus.Count);
    }

    [Fact]
    public void Collect_WithTwoDiscreteGpus_ReportsMultiDiscreteLayout()
    {
        var devices = new List<DeviceInfo>
        {
            Gpu("10DE", "2504", "NVIDIA GeForce RTX 3060"),
            Gpu("10DE", "2482", "NVIDIA GeForce RTX 3070")
        };

        var result = new GpuTopologyCollector(devices).Collect([]);

        Assert.Equal(GpuLayout.MultiDiscrete, result.Layout);
    }

    [Fact]
    public void Collect_WithTwoUnclassifiableGpus_ReportsNullLayoutRatherThanGuessing()
    {
        var devices = new List<DeviceInfo>
        {
            Gpu("1AF4", "1050", "Red Hat Virtio GPU"),
            Gpu("1B36", "0100", "QEMU Standard VGA")
        };

        var result = new GpuTopologyCollector(devices).Collect([]);

        Assert.Null(result.Layout);
    }
}
