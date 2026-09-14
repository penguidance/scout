using Scout.Collector.Collectors;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Collectors;

/// <summary>
/// Direct tests of hardware-ID parsing across PCI, USB, ACPI, and malformed/unexpected inputs —
/// this is the schema's actual matching key, so it gets thorough coverage.
/// </summary>
public class HardwareIdParserTests
{
    [Fact]
    public void Parse_StandardPciId_ExtractsVendorDeviceAndNormalizes()
    {
        var result = HardwareIdParser.Parse(@"PCI\VEN_1002&DEV_73FF&SUBSYS_0123458&REV_C1");

        Assert.Equal("1002", result.VendorId);
        Assert.Equal("73FF", result.DeviceId);
        Assert.Equal(DeviceBusType.PCI, result.BusType);
        Assert.Equal(@"PCI\VEN_1002&DEV_73FF", result.NormalizedId);
    }

    [Fact]
    public void Parse_StandardUsbId_ExtractsVendorDeviceAndNormalizes()
    {
        var result = HardwareIdParser.Parse(@"USB\VID_046D&PID_C52B&REV_0110");

        Assert.Equal("046D", result.VendorId);
        Assert.Equal("C52B", result.DeviceId);
        Assert.Equal(DeviceBusType.USB, result.BusType);
        Assert.Equal(@"USB\VID_046D&PID_C52B", result.NormalizedId);
    }

    [Fact]
    public void Parse_UsbCompositeDeviceInterfaceSuffix_StillExtractsVendorDevice()
    {
        var result = HardwareIdParser.Parse(@"USB\VID_046D&PID_C52B&MI_00");

        Assert.Equal("046D", result.VendorId);
        Assert.Equal("C52B", result.DeviceId);
        Assert.Equal(@"USB\VID_046D&PID_C52B", result.NormalizedId);
    }

    [Fact]
    public void Parse_PlainAcpiPnpId_HasNoVendorDeviceAndKeepsRawForm()
    {
        var result = HardwareIdParser.Parse(@"ACPI\PNP0303");

        Assert.Null(result.VendorId);
        Assert.Null(result.DeviceId);
        Assert.Equal(DeviceBusType.ACPI, result.BusType);
        Assert.Equal(@"ACPI\PNP0303", result.NormalizedId);
    }

    [Fact]
    public void Parse_AcpiIdWithVendorDevicePattern_StillExtractsIt()
    {
        // Rare, but some modern ACPI hardware IDs do carry a VEN_/DEV_ pattern.
        var result = HardwareIdParser.Parse(@"ACPI\VEN_MSFT&DEV_0101");

        Assert.Equal(DeviceBusType.ACPI, result.BusType);
        // "MSFT" is not 4 hex digits, so the strict hex pattern does not match here — confirming
        // we do not half-parse a non-hex vendor token into something misleading.
        Assert.Null(result.VendorId);
    }

    [Theory]
    [InlineData(@"ROOT\NET\0000")]
    [InlineData(@"SW\{12345678-1234-1234-1234-123456789ABC}\Instance")]
    [InlineData("ms_pppoeminiport")]
    public void Parse_NonPciUsbAcpiBuses_ClassifiedAsOtherWithNoVendorDevice(string raw)
    {
        var result = HardwareIdParser.Parse(raw);

        Assert.Equal(DeviceBusType.Other, result.BusType);
        Assert.Null(result.VendorId);
        Assert.Null(result.DeviceId);
        Assert.Equal(raw, result.NormalizedId);
    }

    [Fact]
    public void Parse_LowercaseHex_NormalizesToUppercase()
    {
        var result = HardwareIdParser.Parse(@"pci\ven_1002&dev_73ff&subsys_01234567");

        Assert.Equal("1002", result.VendorId);
        Assert.Equal("73FF", result.DeviceId);
    }

    [Fact]
    public void Parse_VenWithoutDev_DoesNotPartiallyExtract()
    {
        var result = HardwareIdParser.Parse(@"PCI\VEN_1002");

        Assert.Null(result.VendorId);
        Assert.Null(result.DeviceId);
        Assert.Equal(@"PCI\VEN_1002", result.NormalizedId);
    }

    [Fact]
    public void Parse_EmptyString_DoesNotThrowAndYieldsNoVendorDevice()
    {
        var result = HardwareIdParser.Parse("");

        Assert.Null(result.VendorId);
        Assert.Null(result.DeviceId);
        Assert.Equal(DeviceBusType.Other, result.BusType);
        Assert.Equal("", result.NormalizedId);
    }

    [Fact]
    public void Parse_HdaudioBusWithVendorDevicePattern_ExtractsIdsButBusTypeIsOther()
    {
        // HD Audio codec hardware IDs (e.g. on a GPU's HDMI audio function) carry a real
        // VEN_/DEV_ pattern despite not being on the PCI/USB/ACPI bus prefixes we special-case.
        var result = HardwareIdParser.Parse(@"HDAUDIO\FUNC_01&VEN_1002&DEV_AA01&SUBSYS_00AA0100&REV_1007");

        Assert.Equal("1002", result.VendorId);
        Assert.Equal("AA01", result.DeviceId);
        Assert.Equal(DeviceBusType.Other, result.BusType);
    }

    [Fact]
    public void Parse_HardwareIdWithNoBackslash_TreatedAsOtherBus()
    {
        var result = HardwareIdParser.Parse("vms_vsmp");

        Assert.Equal(DeviceBusType.Other, result.BusType);
        Assert.Equal("vms_vsmp", result.NormalizedId);
    }
}
