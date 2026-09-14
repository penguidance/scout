using Scout.Core.Models;

namespace Scout.Collector.Collectors;

/// <summary>
/// A collector for one top-level machine-profile block. Implementations never throw for a
/// single unreadable field — they record it as a <see cref="CollectionError"/> (component +
/// reason) and leave the corresponding profile field null/default instead. A single missing
/// source must never abort the whole collection run.
/// </summary>
public interface ISectionCollector<out TSection>
{
    /// <summary>Component name used as <see cref="CollectionError.Section"/> for this block.</summary>
    string ComponentName { get; }

    /// <param name="errors">Sink that failures for this block are appended to.</param>
    TSection Collect(ICollection<CollectionError> errors);
}
