using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>
/// A raw collection failure for one section of the profile. Carries no interpretation or
/// remediation advice — just what Collector observed when a source could not be read.
/// </summary>
public sealed class CollectionError
{
    /// <summary>Top-level profile component where the failure occurred, e.g. "storage".</summary>
    [JsonPropertyName("component")]
    public required string Component { get; init; }

    /// <summary>The WMI class or registry key that was being queried.</summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>The raw exception/error message.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("occurred_at")]
    public required DateTimeOffset OccurredAt { get; init; }
}
