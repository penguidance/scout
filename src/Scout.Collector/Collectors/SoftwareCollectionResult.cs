using Scout.Core.Models;

namespace Scout.Collector.Collectors;

/// <summary>
/// Output of <see cref="SoftwareCollector"/>: the surviving entries plus how many were filtered
/// out as noise. Two profile fields (<c>software</c> and <c>software_filtered_count</c>) come
/// from one collection pass, so this carries both rather than forcing a second pass or a
/// collector-held-state workaround.
/// </summary>
public sealed class SoftwareCollectionResult
{
    public required IReadOnlyList<SoftwareEntry> Entries { get; init; }

    /// <summary>See <see cref="MachineProfile.SoftwareFilteredCount"/>.</summary>
    public required int FilteredCount { get; init; }
}
