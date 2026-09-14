using System.Text.Json.Serialization;

namespace Scout.Core.Models;

/// <summary>TPM presence/state, from the Win32_Tpm WMI class.</summary>
public sealed class TpmInfo
{
    /// <summary>
    /// true = confirmed present, false = confirmed absent (the query succeeded and returned no
    /// TPM instance), null = could not be determined (e.g. access denied). Never defaults to
    /// false just because the read failed — "unreadable" and "absent" are different facts.
    /// </summary>
    // JsonIgnoreCondition.Never overrides ProfileJsonSerializer's global WhenWritingNull: a
    // `required` member must always be present as a JSON key (even as `null`) or deserialization
    // fails with "missing required properties" — letting the global null-omission rule apply
    // here would make Collector's own output unreadable by its own deserializer.
    [JsonPropertyName("present")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required bool? Present { get; init; }

    /// <summary>
    /// The TPM specification version, e.g. "2.0" — the first comma-separated component of the
    /// raw <c>Win32_Tpm.SpecVersion</c> value. Null if absent/unparsable; see
    /// <see cref="SpecVersionRaw"/>, which is never dropped even when this is null.
    /// </summary>
    [JsonPropertyName("spec_version")]
    public string? SpecVersion { get; init; }

    /// <summary>
    /// The manufacturer's firmware version, e.g. "1.38" — the third comma-separated component of
    /// the raw <c>Win32_Tpm.SpecVersion</c> value. Null if absent/unparsable; see
    /// <see cref="SpecVersionRaw"/>.
    /// </summary>
    [JsonPropertyName("manufacturer_version")]
    public string? ManufacturerVersion { get; init; }

    /// <summary>
    /// The unparsed <c>Win32_Tpm.SpecVersion</c> value exactly as reported (e.g. "2.0, 0, 1.38").
    /// Always kept when a TPM instance was read, even if parsing into
    /// <see cref="SpecVersion"/>/<see cref="ManufacturerVersion"/> failed — Collector never
    /// discards a raw value just because it derived a parsed one from it.
    /// </summary>
    [JsonPropertyName("spec_version_raw")]
    public string? SpecVersionRaw { get; init; }

    [JsonPropertyName("ready")]
    public bool? Ready { get; init; }
}
