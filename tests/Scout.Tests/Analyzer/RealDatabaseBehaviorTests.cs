using Scout.Analyzer.Compatibility;
using Scout.Collector.Collectors;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Analyzer;

/// <summary>
/// Behavior tests against the real, committed seed database (not a small fixture) — these lock
/// in specific, previously-wrong outcomes that were fixed by hand after being observed for real.
/// </summary>
public class RealDatabaseBehaviorTests
{
    private static CompatibilityMatcher CreateMatcher() => new(CompatibilityDatabase.LoadEmbedded());

    [Fact]
    public void Match_OldGcn1RadeonCard_UsesRadeonDriverNotAmdgpu()
    {
        // Radeon HD 7750 (Cape Verde, GCN 1.0 / "Southern Islands"). The vendor-wide 1002 rule
        // says amdgpu, which is wrong for this generation — amdgpu's Southern Islands support is
        // experimental and off by default; the driver Linux actually uses by default is radeon.
        var device = TestDevices.Create("1002", "683F", DeviceBusType.PCI, friendlyName: "AMD Radeon HD 7750", deviceClass: "Display");

        var result = CreateMatcher().Match(device);

        Assert.Equal(MatchLevel.RangeMatch, result.Level);
        Assert.Equal("radeon", result.Entry!.KernelDriver);
        Assert.Equal(SupportLevel.Native, result.Support);
    }

    [Fact]
    public void Match_ModernAmdGpu_StillUsesAmdgpuViaVendorFallback_NotSwallowedByTheOldRadeonRange()
    {
        var device = TestDevices.Create("1002", "7340", DeviceBusType.PCI, friendlyName: "AMD Radeon RX 5500 XT", deviceClass: "Display");

        var result = CreateMatcher().Match(device);

        Assert.Equal(MatchLevel.VendorFallback, result.Level);
        Assert.Equal("amdgpu", result.Entry!.KernelDriver);
    }

    [Fact]
    public void Match_UsbRootHub_IsNativeNotUnknown()
    {
        var device = TestDevices.Create(HardwareIdParser.UsbRootHubVendorId, null, DeviceBusType.USB, friendlyName: "USB Root Hub (USB 3.0)", deviceClass: "USB");

        var result = CreateMatcher().Match(device);

        Assert.Equal(MatchLevel.VendorFallback, result.Level);
        Assert.Equal(SupportLevel.Native, result.Support);
    }

    [Fact]
    public void Parse_UsbRootHubVariants_AllResolveToTheSyntheticVendorId()
    {
        Assert.Equal(HardwareIdParser.UsbRootHubVendorId, HardwareIdParser.Parse(@"USB\ROOT_HUB30").VendorId);
        Assert.Equal(HardwareIdParser.UsbRootHubVendorId, HardwareIdParser.Parse(@"USB\ROOT_HUB20").VendorId);
        Assert.Equal(HardwareIdParser.UsbRootHubVendorId, HardwareIdParser.Parse(@"USB\ROOT_HUB").VendorId);
    }

    [Fact]
    public void Match_AmdGpuHdmiAudio_HasAnExactEntryWithAUserFriendlyDisplayName()
    {
        var device = TestDevices.Create("1002", "AA01", DeviceBusType.Other, friendlyName: "AMD High Definition Audio Device", deviceClass: "MEDIA");

        var result = CreateMatcher().Match(device);

        Assert.Equal(MatchLevel.Exact, result.Level);
        Assert.Equal(SupportLevel.Native, result.Support);
        Assert.Equal("HDMI/DisplayPort ses çıkışı", result.Entry!.DisplayName);
        Assert.Equal("snd_hda_intel", result.Entry.KernelDriver); // the real module name is still kept, just not shown as the primary label
    }
}
