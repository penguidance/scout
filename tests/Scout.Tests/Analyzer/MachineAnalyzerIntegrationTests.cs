using Scout.Analyzer;
using Scout.Analyzer.Analysis;
using Scout.Analyzer.Compatibility;
using Scout.Core.Serialization;
using Xunit;

namespace Scout.Tests.Analyzer;

/// <summary>
/// End-to-end tests against the real reference profiles in docs/samples/ — the first machines
/// this engine (and, later, its report) is meant to make sense of.
/// </summary>
public class MachineAnalyzerIntegrationTests
{
    private static string SamplePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "samples", fileName);

    private static MachineAnalyzer CreateAnalyzer() =>
        new(new CompatibilityMatcher(CompatibilityDatabase.LoadEmbedded()));

    [Fact]
    public void Analyze_AdminSample_ReturnsReadyVerdict()
    {
        var profile = ProfileJsonSerializer.Deserialize(File.ReadAllText(SamplePath("ryzen-5600x-msi-b450-admin.json")));

        var result = CreateAnalyzer().Analyze(profile);

        Assert.Equal(Verdict.Ready, result.Verdict);
    }

    [Fact]
    public void Analyze_AdminSample_MatchesTheRx5500XtAsNativeAmdgpu()
    {
        var profile = ProfileJsonSerializer.Deserialize(File.ReadAllText(SamplePath("ryzen-5600x-msi-b450-admin.json")));

        var result = CreateAnalyzer().Analyze(profile);

        var gpu = Assert.Single(result.Devices, d => d.Device.VendorId == "1002" && d.Device.DeviceId == "7340");
        Assert.Equal("AMD Radeon RX 5500 XT", gpu.Device.FriendlyName);
        Assert.Equal("amdgpu", gpu.Match.Entry?.KernelDriver);
        Assert.Equal(SupportLevel.Native, gpu.Match.Support);
    }

    [Fact]
    public void Analyze_AdminSample_FiltersOutTheKnownNoiseDevices()
    {
        var profile = ProfileJsonSerializer.Deserialize(File.ReadAllText(SamplePath("ryzen-5600x-msi-b450-admin.json")));

        var result = CreateAnalyzer().Analyze(profile);

        Assert.DoesNotContain(result.Devices, d => d.Device.FriendlyName.Contains("WAN Miniport"));
        Assert.DoesNotContain(result.Devices, d => d.Device.FriendlyName.Contains("Hyper-V"));
        Assert.True(result.DevicesRemovedForRelevance > 0);
    }

    [Fact]
    public void Analyze_UserModeSample_AlsoProducesAResultWithoutThrowing()
    {
        // The non-admin capture has fewer readable fields (BitLocker/TPM access denied) but
        // should still analyze cleanly — it exercises the exact same device list.
        var profile = ProfileJsonSerializer.Deserialize(File.ReadAllText(SamplePath("ryzen-5600x-msi-b450-user.json")));

        var result = CreateAnalyzer().Analyze(profile);

        Assert.NotEmpty(result.Devices);
    }
}
