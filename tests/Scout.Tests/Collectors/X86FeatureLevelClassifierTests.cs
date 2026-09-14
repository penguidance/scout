using Scout.Collector.Collectors;
using Scout.Core.Models;
using Xunit;

namespace Scout.Tests.Collectors;

/// <summary>Direct tests of the vendor+family+model → x86-64 feature level mapping table.</summary>
public class X86FeatureLevelClassifierTests
{
    [Theory]
    [InlineData(0x17, X86FeatureLevel.v3)] // Zen / Zen+
    [InlineData(0x18, X86FeatureLevel.v3)] // Zen (APU line)
    [InlineData(0x19, X86FeatureLevel.v3)] // Zen 3 / Zen 4
    [InlineData(0x1A, X86FeatureLevel.v3)] // Zen 5 and later, by the ">=" rule
    public void Classify_Amd_ZenAndLaterFamilies_AreV3(int family, X86FeatureLevel expected)
    {
        Assert.Equal(expected, X86FeatureLevelClassifier.Classify("AuthenticAMD", family, model: 1));
    }

    [Theory]
    [InlineData(0x10)] // K10
    [InlineData(0x12)]
    [InlineData(0x14)] // Bobcat
    [InlineData(0x15)] // Bulldozer/Piledriver/Steamroller/Excavator
    [InlineData(0x16)] // Jaguar/Puma
    public void Classify_Amd_PreZenKnownFamilies_AreV2(int family)
    {
        Assert.Equal(X86FeatureLevel.v2, X86FeatureLevelClassifier.Classify("AuthenticAMD", family, model: 1));
    }

    [Fact]
    public void Classify_Amd_UnknownOldFamily_ReturnsNull()
    {
        Assert.Null(X86FeatureLevelClassifier.Classify("AuthenticAMD", family: 0x0F, model: 1));
    }

    [Theory]
    [InlineData(0x3C)] // Haswell
    [InlineData(0x3F)] // Haswell-E
    [InlineData(0x3D)] // Broadwell
    [InlineData(0x4E)] // Skylake
    [InlineData(0x5E)] // Skylake
    [InlineData(0x8E)] // Kaby Lake / Coffee Lake / Comet Lake
    [InlineData(0x9E)]
    [InlineData(0x97)] // Alder Lake
    [InlineData(0xB7)] // Raptor Lake
    public void Classify_Intel_HaswellOrLaterModels_AreV3(int model)
    {
        Assert.Equal(X86FeatureLevel.v3, X86FeatureLevelClassifier.Classify("GenuineIntel", family: 6, model));
    }

    [Theory]
    [InlineData(0x1A)] // Nehalem
    [InlineData(0x2A)] // Sandy Bridge
    [InlineData(0x2D)] // Sandy Bridge-E
    [InlineData(0x3A)] // Ivy Bridge
    [InlineData(0x3E)] // Ivy Bridge-E
    public void Classify_Intel_PreHaswellKnownModels_AreV2(int model)
    {
        Assert.Equal(X86FeatureLevel.v2, X86FeatureLevelClassifier.Classify("GenuineIntel", family: 6, model));
    }

    [Fact]
    public void Classify_Intel_UnknownModel_ReturnsNull()
    {
        Assert.Null(X86FeatureLevelClassifier.Classify("GenuineIntel", family: 6, model: 250));
    }

    [Fact]
    public void Classify_Intel_NonFamily6_ReturnsNull()
    {
        // Family 6 covers the relevant Core-era line; anything else (e.g. legacy Itanium) is out of scope.
        Assert.Null(X86FeatureLevelClassifier.Classify("GenuineIntel", family: 7, model: 60));
    }

    [Fact]
    public void Classify_UnrecognizedVendor_ReturnsNull()
    {
        Assert.Null(X86FeatureLevelClassifier.Classify("CentaurHauls", family: 6, model: 15));
    }
}
