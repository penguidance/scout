using Scout.Analyzer.Filtering;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Analyzer;

public class DeviceRelevanceFilterTests
{
    [Fact]
    public void Filter_RemovesNonPciUsbDevicesWithNoVendorId()
    {
        var devices = new List<DeviceInfo>
        {
            TestDevices.Create(null, null, DeviceBusType.Other, friendlyName: "WAN Miniport (PPPOE)", deviceClass: "Net"),
            TestDevices.Create(null, null, DeviceBusType.Other, friendlyName: "Hyper-V Virtual Switch Extension Adapter", deviceClass: "Net"),
            TestDevices.Create(null, null, DeviceBusType.Other, friendlyName: "Microsoft Print to PDF", deviceClass: "PrintQueue"),
            TestDevices.Create("1002", "7340", DeviceBusType.PCI, friendlyName: "AMD Radeon RX 5500 XT", deviceClass: "Display")
        };

        var result = new DeviceRelevanceFilter().Filter(devices);

        var kept = Assert.Single(result.RelevantDevices);
        Assert.Equal("AMD Radeon RX 5500 XT", kept.FriendlyName);
        Assert.Equal(3, result.RemovedForRelevance);
    }

    [Fact]
    public void Filter_KeepsPciUsbDevicesEvenWithoutAVendorId()
    {
        // e.g. a USB root hub, whose hardware ID has no VID_/PID_ pattern.
        var devices = new List<DeviceInfo>
        {
            TestDevices.Create(null, null, DeviceBusType.USB, friendlyName: "USB Root Hub (USB 3.0)", deviceClass: "USB")
        };

        var result = new DeviceRelevanceFilter().Filter(devices);

        Assert.Single(result.RelevantDevices);
        Assert.Equal(0, result.RemovedForRelevance);
    }

    [Fact]
    public void Filter_KeepsAcpiOrOtherBusDevicesThatDoHaveAVendorId()
    {
        // e.g. an HD Audio codec on a GPU, enumerated on the HDAUDIO bus (classified as "Other")
        // but still carrying a real vendor_id/device_id worth assessing.
        var devices = new List<DeviceInfo>
        {
            TestDevices.Create("1002", "AA01", DeviceBusType.Other, friendlyName: "AMD High Definition Audio Device", deviceClass: "MEDIA")
        };

        var result = new DeviceRelevanceFilter().Filter(devices);

        Assert.Single(result.RelevantDevices);
        Assert.Equal(0, result.RemovedForRelevance);
    }

    [Fact]
    public void Filter_CollapsesRepeatedHidCollectionsOfTheSamePhysicalDeviceKeepingLongestName()
    {
        var devices = new List<DeviceInfo>
        {
            TestDevices.Create("046D", "C336", DeviceBusType.Other, friendlyName: "HID uyumlu aygıt", deviceClass: "HIDClass"),
            TestDevices.Create("046D", "C336", DeviceBusType.USB, friendlyName: "Logitech G213 Gaming Keyboard", deviceClass: "USB"),
            TestDevices.Create("046D", "C336", DeviceBusType.Other, friendlyName: "HID", deviceClass: "HIDClass")
        };

        var result = new DeviceRelevanceFilter().Filter(devices);

        var kept = Assert.Single(result.RelevantDevices);
        Assert.Equal("Logitech G213 Gaming Keyboard", kept.FriendlyName);
        Assert.Equal(2, result.RemovedForDuplication);
    }

    [Fact]
    public void Filter_DoesNotDeduplicateDevicesMissingEitherId()
    {
        var devices = new List<DeviceInfo>
        {
            TestDevices.Create(null, null, DeviceBusType.USB, friendlyName: "USB Root Hub (USB 3.0)", deviceClass: "USB"),
            TestDevices.Create(null, null, DeviceBusType.USB, friendlyName: "USB Root Hub (USB 3.0)", deviceClass: "USB")
        };

        var result = new DeviceRelevanceFilter().Filter(devices);

        Assert.Equal(2, result.RelevantDevices.Count);
        Assert.Equal(0, result.RemovedForDuplication);
    }

    [Fact]
    public void Filter_WithDeduplicationDisabled_KeepsAllDuplicates()
    {
        var devices = new List<DeviceInfo>
        {
            TestDevices.Create("046D", "C336", DeviceBusType.USB, friendlyName: "A"),
            TestDevices.Create("046D", "C336", DeviceBusType.USB, friendlyName: "B")
        };

        var options = new DeviceRelevanceOptions { DeduplicateByVendorAndDevice = false };
        var result = new DeviceRelevanceFilter(options).Filter(devices);

        Assert.Equal(2, result.RelevantDevices.Count);
        Assert.Equal(0, result.RemovedForDuplication);
    }

    [Fact]
    public void Filter_WithCustomInherentlyRelevantBusTypes_HonorsTheOverride()
    {
        var devices = new List<DeviceInfo>
        {
            TestDevices.Create(null, null, DeviceBusType.ACPI, friendlyName: "ACPI\\PNP0303", deviceClass: "System")
        };

        var options = new DeviceRelevanceOptions
        {
            InherentlyRelevantBusTypes = new HashSet<DeviceBusType> { DeviceBusType.ACPI }
        };
        var result = new DeviceRelevanceFilter(options).Filter(devices);

        Assert.Single(result.RelevantDevices);
    }
}
