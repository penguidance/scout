using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>
/// One installed-software entry — inventory only, no Linux-equivalent mapping (that is a later
/// Analyzer concern). Read from a registry Uninstall key or, for <see cref="SoftwareSource.StorePackage"/>,
/// Win32_InstalledStoreProgram. Win32_Product is never used as a source — see
/// docs/schema/profile-v0.1.md for why.
/// </summary>
public sealed class SoftwareEntry
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; init; }

    [JsonPropertyName("install_date")]
    public DateOnly? InstallDate { get; init; }

    /// <summary>
    /// From the registry's <c>EstimatedSize</c> value (kibibytes) converted to bytes. Null when
    /// absent — most installers never set this value at all, which is a normal, common fact
    /// about the entry, not a read failure; never present for a <see cref="SoftwareSource.StorePackage"/>
    /// entry (no equivalent field exists there).
    /// </summary>
    [JsonPropertyName("estimated_size_bytes")]
    public long? EstimatedSizeBytes { get; init; }

    /// <summary>Kept for diagnostics only. Never executed by Collector or Analyzer.</summary>
    [JsonPropertyName("uninstall_string")]
    public string? UninstallString { get; init; }

    /// <summary>
    /// Null for a non-registry source (<see cref="SoftwareSource.StorePackage"/>) — the concept of
    /// a 32-bit/64-bit registry view does not apply there at all, not merely "unread".
    /// </summary>
    [JsonPropertyName("registry_view")]
    public RegistryView? RegistryView { get; init; }

    /// <summary>
    /// The Uninstall subkey's own name — for an MSI-installed program this is the ProductCode
    /// GUID (e.g. <c>"{90160000-008C-0409-1000-0000000FF1CE}"</c>), for most EXE-based installers
    /// it is a name the installer author chose. Unlike <see cref="Name"/>, this is set once at
    /// install time and is never re-translated if Windows' display language changes later — a far
    /// more stable identity signal than <see cref="Name"/> for software whose <c>DisplayName</c>
    /// is localized (see <see cref="Compatibility.SoftwareMatcher"/>). Null for a non-registry
    /// source (<see cref="SoftwareSource.StorePackage"/>) — no equivalent concept there.
    /// </summary>
    [JsonPropertyName("registry_key_name")]
    public string? RegistryKeyName { get; init; }

    /// <summary>
    /// The registry's <c>InstallLocation</c> value (an install folder path), when the installer
    /// set one. Collected because a path is usually not re-translated by display language either
    /// (e.g. "Program Files" stays the literal NTFS folder name across Windows UI languages), but
    /// not yet used by <see cref="Compatibility.SoftwareMatcher"/> — kept for future matching
    /// heuristics and for a human comparing profiles by hand. Most installers never set it, which
    /// is a normal, common fact about the entry, not a read failure.
    /// </summary>
    [JsonPropertyName("install_location")]
    public string? InstallLocation { get; init; }

    /// <summary>Which registry hive (or non-registry source) this entry was read from — see <see cref="SoftwareSource"/>.</summary>
    [JsonPropertyName("source")]
    public required SoftwareSource Source { get; init; }

    /// <summary>Coarse category from <c>SoftwareCollector</c>'s (configurable) keyword rules — see <see cref="SoftwareCategory"/>.</summary>
    [JsonPropertyName("category")]
    public required SoftwareCategory Category { get; init; }

    /// <summary>
    /// Mirrors the raw registry "SystemComponent" value for registry-sourced entries. An entry
    /// with this flag set to 1 is dropped by the collector entirely (see docs/schema), so every
    /// entry that actually reaches the profile has this false; kept as a field (rather than
    /// removed now that filtering happens earlier) so a re-analysis of an already-collected
    /// profile never needs the raw registry data back. Always false for a
    /// <see cref="SoftwareSource.StorePackage"/> entry (no equivalent flag exists there).
    /// </summary>
    [JsonPropertyName("system_component")]
    public required bool SystemComponent { get; init; }
}
