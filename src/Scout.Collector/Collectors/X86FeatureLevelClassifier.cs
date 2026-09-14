using Scout.Core.Models;

namespace Scout.Collector.Collectors;

/// <summary>
/// Best-effort classification of the x86-64 microarchitecture feature level (v1-v4, see
/// docs/schema/profile-v0.1.md) from a CPU's vendor plus its CPUID family/model — not from
/// CPUID probing itself, so the same code works against a remote target read only through CIM.
/// </summary>
/// <remarks>
/// Family/model here are the raw CPUID values (as reported in Win32_Processor.Description,
/// e.g. "AMD64 Family 25 Model 33 Stepping 2"), not Win32_Processor.Family — that property is a
/// separate WMI "well-known value" marketing enumeration, not the CPUID family nibble, and is
/// not used here.
/// <para>
/// Only vendor/family/model combinations deliberately curated below are classified; anything
/// else returns null so the caller can log a collection_errors entry instead of guessing.
/// </para>
/// </remarks>
internal static class X86FeatureLevelClassifier
{
    // AMD family numbers (CPUID, decimal) at/after Zen, which brought AVX2/BMI2/FMA/etc. (v3).
    private const int AmdZenFamily = 0x17;

    // Older AMD families we recognize but that predate the v3 feature set (v2: at least SSE4.2 +
    // POPCNT, from the Bulldozer/Jaguar generation onward).
    private static readonly HashSet<int> AmdOlderKnownFamilies = [0x10, 0x12, 0x14, 0x15, 0x16];

    // Intel family 6 models at/after Haswell (2013), which introduced AVX2/BMI1/BMI2/FMA (v3).
    private static readonly HashSet<int> IntelHaswellOrLaterModels =
    [
        0x3C, 0x3F, 0x45, 0x46, // Haswell
        0x3D, 0x47, 0x4F, 0x56, // Broadwell
        0x4E, 0x5E, // Skylake
        0x8E, 0x9E, // Kaby Lake / Coffee Lake / Comet Lake (model reused across steppings)
        0x66, // Cannon Lake
        0x6A, 0x6C, // Ice Lake-SP / Ice Lake-X (server)
        0x7D, 0x7E, // Ice Lake (client)
        0x8C, 0x8D, // Tiger Lake
        0xA7, // Rocket Lake
        0x97, 0x9A, // Alder Lake
        0xB7, 0xBA, 0xBF, // Raptor Lake
        0xAC, 0xAD // Meteor Lake
    ];

    // Intel family 6 models we recognize but that predate Haswell (v2 at best: up to SSE4.2).
    private static readonly HashSet<int> IntelOlderKnownModels =
    [
        0x1A, 0x1E, 0x1F, 0x2E, // Nehalem
        0x25, 0x2C, 0x2F, // Westmere
        0x2A, 0x2D, // Sandy Bridge
        0x3A, 0x3E // Ivy Bridge
    ];

    public static X86FeatureLevel? Classify(string vendor, int family, int model) => vendor switch
    {
        "AuthenticAMD" => ClassifyAmd(family),
        "GenuineIntel" => ClassifyIntel(family, model),
        _ => null
    };

    private static X86FeatureLevel? ClassifyAmd(int family)
    {
        if (family >= AmdZenFamily) return X86FeatureLevel.v3;
        if (AmdOlderKnownFamilies.Contains(family)) return X86FeatureLevel.v2;
        return null;
    }

    private static X86FeatureLevel? ClassifyIntel(int family, int model)
    {
        if (family != 6) return null; // Family 6 covers the relevant Core-era client/server line.

        if (IntelHaswellOrLaterModels.Contains(model)) return X86FeatureLevel.v3;
        if (IntelOlderKnownModels.Contains(model)) return X86FeatureLevel.v2;
        return null;
    }
}
