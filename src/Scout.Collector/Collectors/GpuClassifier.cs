using Scout.Core.Models;

namespace Scout.Collector.Collectors;

/// <summary>
/// Best-effort integrated-vs-discrete GPU classification from vendor_id and friendly-name text.
/// Windows has no clean CIM signal for this (Win32_VideoController does not distinguish them
/// either), so this is a documented-blind-spots heuristic, not an authoritative source — when it
/// cannot classify a GPU confidently, callers should treat that GPU as unclassified rather than
/// guessing further.
/// </summary>
internal static class GpuClassifier
{
    private const string IntelVendorId = "8086";
    private const string NvidiaVendorId = "10DE";
    private const string AmdVendorId = "1002";

    private static readonly string[] AmdIntegratedNameHints = ["radeon(tm) graphics", "radeon graphics", "vega graphics"];
    private static readonly string[] AmdDiscreteNameHints = ["radeon rx", "radeon hd", "radeon pro", "radeon vii", "radeon r5", "radeon r7", "radeon r9"];

    /// <remarks>
    /// Blind spot: Intel's discrete Arc cards also report vendor 8086, same as Intel's
    /// integrated graphics — distinguished here only by the name containing "Arc".
    /// </remarks>
    public static bool IsLikelyIntegrated(GpuInfo gpu)
    {
        if (gpu.VendorId == IntelVendorId && !NameContains(gpu, "arc")) return true;
        if (gpu.VendorId == AmdVendorId && NameContainsAny(gpu, AmdIntegratedNameHints)) return true;
        return false;
    }

    /// <remarks>
    /// NVIDIA has never shipped a consumer integrated GPU, so any NVIDIA-vendor device is
    /// treated as discrete outright. AMD discrete cards are recognized by common Radeon
    /// product-line keywords — not an exhaustive list.
    /// </remarks>
    public static bool IsLikelyDiscrete(GpuInfo gpu)
    {
        if (gpu.VendorId == NvidiaVendorId) return true;
        if (gpu.VendorId == IntelVendorId && NameContains(gpu, "arc")) return true;
        if (gpu.VendorId == AmdVendorId && NameContainsAny(gpu, AmdDiscreteNameHints)) return true;
        return false;
    }

    private static bool NameContains(GpuInfo gpu, string needle) =>
        gpu.FriendlyName.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static bool NameContainsAny(GpuInfo gpu, IReadOnlyList<string> needles)
    {
        foreach (var needle in needles)
        {
            if (NameContains(gpu, needle)) return true;
        }

        return false;
    }
}
