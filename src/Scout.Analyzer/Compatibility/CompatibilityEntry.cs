using System.Text.Json.Serialization;

namespace Scout.Analyzer.Compatibility;

/// <summary>
/// One row of the hardware compatibility database (data/hardware-compatibility.json).
/// </summary>
public sealed class CompatibilityEntry
{
    /// <summary>
    /// PCI/USB vendor ID, hex, e.g. "1002". Matched case-insensitively. Also used, by
    /// convention, for a handful of synthetic non-hex IDs (e.g. "USBROOTHUB") that
    /// Scout.Collector's hardware-ID parser assigns to generic USB infrastructure PnP nodes
    /// (root hubs) that carry no real VID/PID of their own — see
    /// docs/schema/profile-v0.1.md for why.
    /// </summary>
    [JsonPropertyName("vendor_id")]
    public required string VendorId { get; init; }

    /// <summary>
    /// PCI/USB device ID, hex, e.g. "73FF". Null means this entry applies to the whole vendor
    /// (a "vendor-wide" rule) — see <see cref="DeviceClass"/> for how such rules disambiguate
    /// vendors that make several unrelated kinds of hardware under one vendor ID. Ignored when
    /// <see cref="DeviceIdRangeStart"/>/<see cref="DeviceIdRangeEnd"/> are set.
    /// </summary>
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; init; }

    /// <summary>
    /// Inclusive hex device_id range (both bounds required together) for when a vendor's device
    /// IDs cluster by hardware generation and a single vendor-wide rule would give the wrong
    /// answer for part of the range. The motivating case: AMD/ATI vendor 1002 as a whole maps to
    /// the modern <c>amdgpu</c> driver, but the Southern Islands generation (GCN 1.0, e.g.
    /// Radeon HD 7750) needs the older <c>radeon</c> driver instead — see the Southern Islands
    /// entry in data/hardware-compatibility.json and docs/schema/profile-v0.1.md. Checked by
    /// <see cref="CompatibilityMatcher"/> after an exact <see cref="DeviceId"/> match and before
    /// any vendor-wide (device_id-less) fallback, and reported as
    /// <see cref="MatchLevel.RangeMatch"/> rather than <see cref="MatchLevel.Exact"/> or
    /// <see cref="MatchLevel.VendorFallback"/> — a deliberate, generation-scoped classification
    /// is neither a single specific device lookup nor an unscoped guess.
    /// </summary>
    [JsonPropertyName("device_id_range_start")]
    public string? DeviceIdRangeStart { get; init; }

    [JsonPropertyName("device_id_range_end")]
    public string? DeviceIdRangeEnd { get; init; }

    /// <summary>
    /// Optional PNPClass-style scope (e.g. "Display", "Net") for a vendor-wide rule
    /// (<see cref="DeviceId"/> null). A vendor like Intel ships GPUs, NICs, Wi-Fi and chipset
    /// USB controllers all under vendor ID 8086 with very different support stories, so a bare
    /// vendor-wide rule would be misleading; scoping it to a device class lets several vendor-wide
    /// rules coexist for the same vendor without colliding. Ignored when <see cref="DeviceId"/>
    /// is set, since an exact device ID is already unambiguous.
    /// </summary>
    [JsonPropertyName("device_class")]
    public string? DeviceClass { get; init; }

    /// <summary>The Linux kernel module/driver name, e.g. "amdgpu", "r8169".</summary>
    [JsonPropertyName("kernel_driver")]
    public required string KernelDriver { get; init; }

    /// <summary>
    /// A plain-language label shown instead of <see cref="KernelDriver"/> wherever the raw
    /// module name would confuse a reader (e.g. "HDMI/DisplayPort ses çıkışı" instead of
    /// "snd_hda_intel"). Optional — most entries do not need one; falls back to
    /// <see cref="KernelDriver"/> when null.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("support")]
    public required SupportLevel Support { get; init; }

    /// <summary>Earliest kernel version this is known to work from, e.g. "5.4". Best-effort guidance, not a hard guarantee.</summary>
    [JsonPropertyName("min_kernel")]
    public string? MinKernel { get; init; }

    /// <summary>Short, user-facing explanation.</summary>
    [JsonPropertyName("notes")]
    public required string Notes { get; init; }
}
