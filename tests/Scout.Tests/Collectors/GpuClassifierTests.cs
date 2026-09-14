using Scout.Collector.Collectors;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Collectors;

public class GpuClassifierTests
{
    private static GpuInfo Gpu(string vendorId, string friendlyName) => new()
    {
        HardwareId = $@"PCI\VEN_{vendorId}&DEV_0000",
        VendorId = vendorId,
        DeviceId = "0000",
        FriendlyName = friendlyName
    };

    [Theory]
    [InlineData("8086", "Intel(R) UHD Graphics 630")]
    [InlineData("1002", "AMD Radeon(TM) Graphics")]
    [InlineData("1002", "AMD Radeon Vega Graphics")]
    public void IsLikelyIntegrated_KnownIntegratedPatterns_ReturnsTrue(string vendorId, string name)
    {
        Assert.True(GpuClassifier.IsLikelyIntegrated(Gpu(vendorId, name)));
        Assert.False(GpuClassifier.IsLikelyDiscrete(Gpu(vendorId, name)));
    }

    [Theory]
    [InlineData("10DE", "NVIDIA GeForce RTX 3060")]
    [InlineData("1002", "AMD Radeon RX 5500 XT")]
    [InlineData("1002", "AMD Radeon HD 7970")]
    [InlineData("8086", "Intel(R) Arc A770 Graphics")]
    public void IsLikelyDiscrete_KnownDiscretePatterns_ReturnsTrue(string vendorId, string name)
    {
        Assert.True(GpuClassifier.IsLikelyDiscrete(Gpu(vendorId, name)));
        Assert.False(GpuClassifier.IsLikelyIntegrated(Gpu(vendorId, name)));
    }

    [Fact]
    public void IntelArc_IsDiscreteNotIntegrated_DespiteSharingIntelVendorIdWithIgpus()
    {
        var arc = Gpu("8086", "Intel(R) Arc A770 Graphics");

        Assert.True(GpuClassifier.IsLikelyDiscrete(arc));
        Assert.False(GpuClassifier.IsLikelyIntegrated(arc));
    }

    [Theory]
    [InlineData("1AF4", "Red Hat Virtio GPU")]
    [InlineData("1B36", "QEMU Standard VGA")]
    public void UnrecognizedVendorOrName_ClassifiedAsNeitherRatherThanGuessed(string vendorId, string name)
    {
        var gpu = Gpu(vendorId, name);

        Assert.False(GpuClassifier.IsLikelyIntegrated(gpu));
        Assert.False(GpuClassifier.IsLikelyDiscrete(gpu));
    }
}
