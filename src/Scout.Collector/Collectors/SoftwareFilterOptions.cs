namespace Scout.Collector.Collectors;

/// <summary>
/// Configuration for <see cref="SoftwareCollector"/>'s noise filter and category classification.
/// Every list here is a starting point, not a claim of completeness — extend it as real machines
/// turn up entries the defaults miss, the same spirit as the device_id-range entries in
/// data/hardware-compatibility.json.
/// </summary>
public sealed class SoftwareFilterOptions
{
    /// <summary>
    /// A <c>DisplayName</c> containing any of these substrings (case-insensitive) is treated as a
    /// Windows update/hotfix and dropped entirely, not merely categorized — these are not
    /// software a user would recognize as "a program on this machine".
    /// </summary>
    public IReadOnlyList<string> UpdateNameSubstrings { get; init; } =
    [
        "Update for",
        "Security Update",
        "Hotfix for"
    ];

    /// <summary>
    /// When true (default), a <c>DisplayName</c> starting with "KB" followed by digits (the
    /// standard Microsoft hotfix ID format, e.g. "KB5031354") is also treated as an update, in
    /// addition to <see cref="UpdateNameSubstrings"/>.
    /// </summary>
    public bool TreatKbPrefixedNamesAsUpdates { get; init; } = true;

    /// <summary>
    /// <c>DisplayName</c> substrings (case-insensitive) that mark an entry as a shared
    /// runtime/redistributable — kept in the inventory (not dropped), just tagged
    /// <see cref="Core.Models.SoftwareCategory.runtime"/> so a report can fold a dozen VC++
    /// Redistributable entries away without hiding them from someone who wants the full picture.
    /// </summary>
    public IReadOnlyList<string> RuntimeNameKeywords { get; init; } =
    [
        // Not the longer "Visual C++ Redistributable" — real display names put a year range in
        // between (e.g. "Microsoft Visual C++ 2015-2022 Redistributable (x64)"), which that
        // exact phrase would miss entirely.
        "Visual C++",
        ".NET Runtime",
        ".NET Desktop Runtime",
        ".NET Core Runtime",
        ".NET Framework",
        "DirectX",
        "Microsoft Edge WebView2 Runtime",
        "Windows Media Feature Pack",
        "Java(TM)",
        "Java Runtime"
    ];

    /// <summary>
    /// <c>DisplayName</c> substrings (case-insensitive) that mark an entry as a device driver
    /// package — tagged <see cref="Core.Models.SoftwareCategory.driver"/>, checked before
    /// <see cref="RuntimeNameKeywords"/>.
    /// </summary>
    public IReadOnlyList<string> DriverNameKeywords { get; init; } =
    [
        "Driver",
        "Display Adapter"
    ];

    /// <summary>
    /// <c>DisplayName</c> substrings (case-insensitive) for Microsoft-published system utilities
    /// that are neither a typical application nor a runtime/driver — tagged
    /// <see cref="Core.Models.SoftwareCategory.system"/>, checked after the other two lists.
    /// </summary>
    public IReadOnlyList<string> SystemNameKeywords { get; init; } =
    [
        "Update Health Tools",
        "Malicious Software Removal Tool"
    ];
}
