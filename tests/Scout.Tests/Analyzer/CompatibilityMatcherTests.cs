using Scout.Analyzer.Compatibility;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Analyzer;

public class CompatibilityMatcherTests
{
    private const string Database = """
        [
          {
            "vendor_id": "1002",
            "device_id": "7340",
            "kernel_driver": "amdgpu",
            "support": "native",
            "notes": { "en": "exact AMD RX 5500 XT entry" }
          },
          {
            "vendor_id": "1002",
            "device_id": null,
            "device_class": "Display",
            "kernel_driver": "amdgpu-generic",
            "support": "native",
            "notes": { "en": "AMD display vendor fallback" }
          },
          {
            "vendor_id": "8086",
            "device_id": null,
            "device_class": "Display",
            "kernel_driver": "i915",
            "support": "native",
            "notes": { "en": "Intel display vendor fallback" }
          },
          {
            "vendor_id": "8086",
            "device_id": null,
            "device_class": "Net",
            "kernel_driver": "iwlwifi",
            "support": "firmware_required",
            "notes": { "en": "Intel network vendor fallback" }
          },
          {
            "vendor_id": "14E4",
            "device_id": null,
            "device_class": null,
            "kernel_driver": "brcmfmac",
            "support": "firmware_required",
            "notes": { "en": "Broadcom class-less vendor fallback" }
          }
        ]
        """;

    private static CompatibilityMatcher CreateMatcher() =>
        new(CompatibilityDatabase.FromJson(Database));

    [Fact]
    public void Match_ExactVendorAndDevice_ReturnsExactLevel()
    {
        var device = TestDevices.Create("1002", "7340", DeviceBusType.PCI, deviceClass: "Display");

        var result = CreateMatcher().Match(device);

        Assert.Equal(MatchLevel.Exact, result.Level);
        Assert.Equal(SupportLevel.Native, result.Support);
        Assert.Equal("amdgpu", result.Entry!.KernelDriver);
    }

    [Fact]
    public void Match_ExactEntryTakesPriorityOverVendorWideEntry()
    {
        // 1002 has both an exact 7340 entry and a class-scoped Display fallback; exact must win.
        var device = TestDevices.Create("1002", "7340", DeviceBusType.PCI, deviceClass: "Display");

        var result = CreateMatcher().Match(device);

        Assert.Equal("amdgpu", result.Entry!.KernelDriver); // not "amdgpu-generic"
    }

    [Fact]
    public void Match_NoExactMatch_FallsBackToClassScopedVendorRule()
    {
        var device = TestDevices.Create("1002", "9999", DeviceBusType.PCI, deviceClass: "Display");

        var result = CreateMatcher().Match(device);

        Assert.Equal(MatchLevel.VendorFallback, result.Level);
        Assert.Equal("amdgpu-generic", result.Entry!.KernelDriver);
    }

    [Fact]
    public void Match_SameVendorDifferentClass_UsesTheMatchingClassScopedRuleNotTheOther()
    {
        var wifi = TestDevices.Create("8086", "1234", DeviceBusType.PCI, deviceClass: "Net");
        var gpu = TestDevices.Create("8086", "5678", DeviceBusType.PCI, deviceClass: "Display");

        var wifiResult = CreateMatcher().Match(wifi);
        var gpuResult = CreateMatcher().Match(gpu);

        Assert.Equal("iwlwifi", wifiResult.Entry!.KernelDriver);
        Assert.Equal(SupportLevel.FirmwareRequired, wifiResult.Support);
        Assert.Equal("i915", gpuResult.Entry!.KernelDriver);
        Assert.Equal(SupportLevel.Native, gpuResult.Support);
    }

    [Fact]
    public void Match_ClasslessVendorRule_AppliesRegardlessOfDeviceClass()
    {
        var device = TestDevices.Create("14E4", "4360", DeviceBusType.PCI, deviceClass: "Net");

        var result = CreateMatcher().Match(device);

        Assert.Equal(MatchLevel.VendorFallback, result.Level);
        Assert.Equal("brcmfmac", result.Entry!.KernelDriver);
    }

    [Fact]
    public void Match_UnknownVendor_ReturnsNoneAndUnknownNeverGuessed()
    {
        var device = TestDevices.Create("FFFF", "0001", DeviceBusType.PCI);

        var result = CreateMatcher().Match(device);

        Assert.Equal(MatchLevel.None, result.Level);
        Assert.Equal(SupportLevel.Unknown, result.Support);
        Assert.Null(result.Entry);
    }

    [Fact]
    public void Match_DeviceWithNoVendorId_ReturnsNoneWithoutLookingAtAnything()
    {
        var device = TestDevices.Create(null, null, DeviceBusType.ACPI, friendlyName: "ACPI\\PNP0303");

        var result = CreateMatcher().Match(device);

        Assert.Equal(MatchLevel.None, result.Level);
        Assert.Equal(SupportLevel.Unknown, result.Support);
    }

    [Fact]
    public void Match_KnownVendorButClassDoesNotMatchAnyScopedRuleAndNoClasslessRuleExists_ReturnsNone()
    {
        // 8086 only has Display/Net scoped rules in this fixture DB — a Biometric-class Intel
        // device should not silently inherit either of them.
        var device = TestDevices.Create("8086", "AAAA", DeviceBusType.PCI, deviceClass: "Biometric");

        var result = CreateMatcher().Match(device);

        Assert.Equal(MatchLevel.None, result.Level);
        Assert.Equal(SupportLevel.Unknown, result.Support);
    }
}
