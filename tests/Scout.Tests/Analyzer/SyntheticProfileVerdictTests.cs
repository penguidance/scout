using Scout.Analyzer;
using Scout.Analyzer.Analysis;
using Scout.Analyzer.Compatibility;
using Scout.Core.Serialization;
using Xunit;

namespace Scout.Tests.Analyzer;

/// <summary>
/// Verdict regression tests against the three hand-authored, schema-perfect synthetic profiles
/// in docs/samples/ (see Fixtures/SyntheticProfiles.cs for how they were built) — each one
/// exercises a specific real-world compatibility scenario the real reference machine (a clean
/// AMD Ryzen 5600X build) never would.
/// </summary>
public class SyntheticProfileVerdictTests
{
    private static string SamplePath(string fileName) => Path.Combine(AppContext.BaseDirectory, "samples", fileName);

    private static MachineAnalyzer CreateAnalyzer() =>
        new(new CompatibilityMatcher(CompatibilityDatabase.LoadEmbedded()));

    private static Scout.Core.Models.MachineProfile LoadProfile(string fileName) =>
        ProfileJsonSerializer.Deserialize(File.ReadAllText(SamplePath(fileName)));

    [Fact]
    public void OptimusLaptop_NvidiaProprietaryDriverNeeded_ReturnsNeedsAttention()
    {
        var profile = LoadProfile("synthetic-optimus-laptop.json");

        var result = CreateAnalyzer().Analyze(profile);

        Assert.Equal(Verdict.NeedsAttention, result.Verdict);

        var gpu = Assert.Single(result.Devices, d => d.Device.VendorId == "10DE");
        Assert.Equal(SupportLevel.Proprietary, gpu.Match.Support);

        // The hybrid Intel iGPU + discrete NVIDIA combination should be recognized as such.
        Assert.Equal(Scout.Core.Models.GpuLayout.Hybrid, profile.GpuTopology.Layout);
    }

    [Fact]
    public void BroadcomMacBook_PartialWifiSupport_ReturnsNeedsAttentionOrBlocked()
    {
        var profile = LoadProfile("synthetic-broadcom-macbook.json");

        var result = CreateAnalyzer().Analyze(profile);

        // The task accepts either outcome — what matters is that the Broadcom chip's harder
        // firmware situation is NOT silently downgraded to a plain "install firmware" MinorIssues.
        Assert.True(
            result.Verdict is Verdict.NeedsAttention or Verdict.Blocked,
            $"Expected NeedsAttention or Blocked, got {result.Verdict}.");

        var wifi = Assert.Single(result.Devices, d => d.Device.VendorId == "14E4" && d.Device.DeviceId == "43BA");
        Assert.Equal(MatchLevel.Exact, wifi.Match.Level);
        Assert.True(wifi.Match.Support is SupportLevel.Partial or SupportLevel.Unsupported);
    }

    [Fact]
    public void OldRadeonDesktop_AllDevicesResolveNatively_ButSystemConstraintsFloorItAtMinorIssues()
    {
        var profile = LoadProfile("synthetic-old-radeon.json");

        var result = CreateAnalyzer().Analyze(profile);

        // Device-wise this machine is perfectly clean (see the dedicated test below); the verdict
        // is not Ready because of two system-wide findings — v2 CPU feature level and Legacy boot
        // mode — not because of anything wrong with the hardware itself.
        Assert.Equal(Verdict.MinorIssues, result.Verdict);
        Assert.All(result.Devices, d => Assert.Equal(SupportLevel.Native, d.Match.Support));

        Assert.Contains(result.SystemConstraints, c => c.Kind == SystemConstraintKind.LowX86FeatureLevel);
        Assert.Contains(result.SystemConstraints, c => c.Kind == SystemConstraintKind.LegacyBootMode);
        Assert.DoesNotContain(result.SystemConstraints, c => c.Kind == SystemConstraintKind.LowDiskSpace);
        Assert.DoesNotContain(result.SystemConstraints, c => c.Kind == SystemConstraintKind.LowMemory);
    }

    /// <summary>
    /// The actual regression this profile exists to guard: without the device_id-range rule,
    /// the 1002 vendor-wide fallback would match this GCN 1.0 card to "amdgpu" — the wrong
    /// driver for this generation — while still (wrongly, for the wrong reason) reporting
    /// SupportLevel.Native. A verdict-only check would not have caught that; only the specific
    /// driver name does.
    /// </summary>
    [Fact]
    public void OldRadeonDesktop_Hd7750_MatchesRadeonDriverViaRangeNotAmdgpuViaVendorFallback()
    {
        var profile = LoadProfile("synthetic-old-radeon.json");

        var result = CreateAnalyzer().Analyze(profile);

        var gpu = Assert.Single(result.Devices, d => d.Device.VendorId == "1002" && d.Device.DeviceId == "683F");
        Assert.Equal(MatchLevel.RangeMatch, gpu.Match.Level);
        Assert.Equal("radeon", gpu.Match.Entry!.KernelDriver);
        Assert.NotEqual("amdgpu", gpu.Match.Entry.KernelDriver);
    }
}
