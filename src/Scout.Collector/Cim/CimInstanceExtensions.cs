using Microsoft.Management.Infrastructure;

namespace Scout.Collector.Cim;

/// <summary>
/// Soft (non-throwing, non-logging) property readers for legitimately optional profile fields.
/// A missing/null CIM value here is treated as a real observation (e.g. "this hardware does not
/// report a SKU"), not a collection failure — so nothing is written to <c>collection_errors</c>.
/// Compare <see cref="RequiredFieldReader"/>, used for non-nullable schema fields.
/// </summary>
internal static class CimInstanceExtensions
{
    /// <summary>
    /// Reads a string (or single-character — some CIM properties, e.g. MSFT_Partition/
    /// MSFT_Volume's DriveLetter, are typed <c>char16</c> rather than <c>string</c>, coming back
    /// as a boxed <see cref="char"/>) property and passes it through
    /// <see cref="CimStringNormalizer"/>, so every text-like field in the profile is trimmed and
    /// stripped of common SMBIOS/OEM placeholder values the same way, regardless of which
    /// collector reads it. A blank/space character (e.g. an unassigned drive letter) normalizes
    /// to null the same way an empty string would.
    /// </summary>
    public static string? GetString(this CimInstance? instance, string propertyName) =>
        CimStringNormalizer.Normalize(GetRaw(instance, propertyName) switch
        {
            string s => s,
            char c => c.ToString(),
            _ => null
        });

    public static bool? GetBool(this CimInstance? instance, string propertyName) =>
        GetRaw(instance, propertyName) as bool?;

    public static uint? GetUInt32(this CimInstance? instance, string propertyName) =>
        GetRaw(instance, propertyName) switch
        {
            null => null,
            uint u => u,
            ushort us => us,
            byte b => b,
            int i and >= 0 => (uint)i,
            _ => null
        };

    public static ushort[]? GetUInt16Array(this CimInstance? instance, string propertyName) =>
        GetRaw(instance, propertyName) as ushort[];

    public static string[]? GetStringArray(this CimInstance? instance, string propertyName) =>
        GetRaw(instance, propertyName) as string[];

    /// <summary>Reads a 64-bit unsigned/signed numeric property (e.g. disk/memory sizes in bytes).</summary>
    public static long? GetInt64(this CimInstance? instance, string propertyName) =>
        GetRaw(instance, propertyName) switch
        {
            null => null,
            long l => l,
            ulong ul and <= long.MaxValue => (long)ul,
            uint u => u,
            int i => i,
            _ => null
        };

    public static DateOnly? GetDateOnly(this CimInstance? instance, string propertyName) =>
        GetRaw(instance, propertyName) switch
        {
            DateTime dt => DateOnly.FromDateTime(dt),
            _ => null
        };

    /// <summary>
    /// Reads a CIM datetime property as a <see cref="DateTimeOffset"/>. The MI client resolves
    /// CIM_DATETIME strings (which carry their own UTC offset) into a .NET <see cref="DateTime"/>
    /// — observed as Kind=Local in practice — and <see cref="DateTimeOffset(DateTime)"/> already
    /// handles Utc/Local/Unspecified correctly on its own, so no extra Kind handling is needed here.
    /// </summary>
    public static DateTimeOffset? GetDateTimeOffset(this CimInstance? instance, string propertyName) =>
        GetRaw(instance, propertyName) switch
        {
            DateTime dt => new DateTimeOffset(dt),
            _ => null
        };

    private static object? GetRaw(CimInstance? instance, string propertyName) =>
        instance?.CimInstanceProperties[propertyName]?.Value;
}
