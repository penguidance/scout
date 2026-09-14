namespace Scout.Analyzer.Compatibility;

/// <summary>Where a <see cref="CompatibilityMatch"/> came from — see <see cref="CompatibilityMatcher"/>.</summary>
public enum MatchLevel
{
    /// <summary>An entry matched on vendor_id + device_id exactly.</summary>
    Exact,

    /// <summary>
    /// No exact device match, but the device_id fell inside a declared generation range for this
    /// vendor (see <see cref="CompatibilityEntry.DeviceIdRangeStart"/>). More specific than a
    /// vendor-wide fallback — it is scoped to a particular hardware generation, not "anything
    /// from this vendor" — but still not a single-device lookup.
    /// </summary>
    RangeMatch,

    /// <summary>No exact device or range match; fell back to a vendor-wide entry (device_id null in the database).</summary>
    VendorFallback,

    /// <summary>No entry at all — never guessed at.</summary>
    None
}
