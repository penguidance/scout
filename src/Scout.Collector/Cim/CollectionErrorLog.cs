using Scout.Core.Models;

namespace Scout.Collector.Cim;

/// <summary>
/// Appends <see cref="CollectionError"/> entries for one profile component ("system",
/// "firmware", "cpu", ...). Used by collectors instead of throwing when a source is unreadable.
/// </summary>
internal sealed class CollectionErrorLog
{
    private readonly ICollection<CollectionError> _sink;
    private readonly string _component;
    private readonly TimeProvider _clock;

    public CollectionErrorLog(ICollection<CollectionError> sink, string component, TimeProvider clock)
    {
        _sink = sink;
        _component = component;
        _clock = clock;
    }

    /// <param name="source">The WMI class/property or registry key that was being read.</param>
    /// <param name="reason">The raw failure reason — no interpretation, just what happened.</param>
    public void Add(string source, string reason) =>
        _sink.Add(new CollectionError
        {
            Component = _component,
            Source = source,
            Message = reason,
            OccurredAt = _clock.GetUtcNow()
        });
}
