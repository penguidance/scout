using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>Declares which personal/identifying fields were included or redacted for this run.</summary>
public sealed class PrivacyInfo
{
    [JsonPropertyName("hostname_included")]
    public required bool HostnameIncluded { get; init; }

    [JsonPropertyName("username_included")]
    public required bool UsernameIncluded { get; init; }

    [JsonPropertyName("serial_numbers_included")]
    public required bool SerialNumbersIncluded { get; init; }

    /// <summary>Field paths (e.g. "system.serial_number") deliberately left null/omitted this run.</summary>
    [JsonPropertyName("redacted_fields")]
    public required IReadOnlyList<string> RedactedFields { get; init; }
}
