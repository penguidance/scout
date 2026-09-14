namespace Scout.Collector.Cim;

/// <summary>
/// Normalizes every raw CIM string value before it reaches a profile field: trims surrounding
/// whitespace, and maps well-known "no real value" SMBIOS/OEM placeholder strings to null
/// (case-insensitive, whitespace-insensitive) instead of passing them through as if they were
/// real hardware data. Generic/whitebox motherboards and many OEM BIOS images report these
/// literal strings when a field was never actually set by the manufacturer.
/// </summary>
internal static class CimStringNormalizer
{
    private static readonly HashSet<string> PlaceholderValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "to be filled by o.e.m.",
        "to be filled by o.e.m",
        "default string",
        "system manufacturer",
        "system product name",
        "system version",
        "system serial number",
        "system sku number",
        "chassis manufacturer",
        "chassis version",
        "chassis serial number",
        "chassis asset tag",
        "not specified",
        "not applicable",
        "none",
        "n/a",
        "na",
        "unknown",
        "invalid",
        "0123456789",
        "123456789",
    };

    /// <summary>Trims the value (including embedded/padding NUL characters), then returns null if it is empty or a known placeholder.</summary>
    public static string? Normalize(string? raw)
    {
        if (raw is null) return null;

        // Some fixed-width CIM char/char16 fields (e.g. an unassigned MSFT_Partition/MSFT_Volume
        // DriveLetter) use '\0' as their "no value" sentinel rather than a space; .NET's
        // Trim() does not treat '\0' as whitespace, so it is stripped explicitly here too.
        var trimmed = raw.Trim().Trim('\0');
        if (trimmed.Length == 0) return null;

        return PlaceholderValues.Contains(trimmed) ? null : trimmed;
    }
}
