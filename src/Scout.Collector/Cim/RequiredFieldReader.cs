using Microsoft.Management.Infrastructure;

namespace Scout.Collector.Cim;

/// <summary>
/// Readers for profile fields the schema marks non-nullable in principle (every collector must
/// attempt them) but that must still become <c>null</c> — never a fabricated sentinel like
/// <c>0</c> or an empty string — when the source is genuinely unreadable. When the source
/// <see cref="CimInstance"/> itself could not be fetched, the class-level failure was already
/// logged by <see cref="CimInstanceFetcher"/>, so nothing further is logged here — only a
/// property that is genuinely missing on an otherwise-present instance is logged, since that is
/// an unusual, worth-flagging partial failure (e.g. a provider that dropped one field).
/// </summary>
internal static class RequiredFieldReader
{
    /// <summary>
    /// For the few fields that are truly required strings in the schema (never null in a
    /// well-formed document) and where an empty string is an acceptable, clearly-flagged
    /// fallback. Logs and falls back to "" when unreadable.
    /// </summary>
    public static string RequireString(
        this CimInstance? instance,
        string property,
        string source,
        CollectionErrorLog log,
        string fallback = "")
    {
        if (instance is null) return fallback;

        var value = instance.GetString(property);
        if (value is not null) return value;

        log.Add(source, $"'{property}' alanı boş veya okunamaz durumda.");
        return fallback;
    }

    /// <summary>Numeric field: null (never 0) when unreadable.</summary>
    public static int? RequireInt(this CimInstance? instance, string property, string source, CollectionErrorLog log)
    {
        if (instance is null) return null;

        var value = instance.GetUInt32(property);
        if (value is not null) return (int)value.Value;

        log.Add(source, $"'{property}' alanı boş veya okunamaz durumda.");
        return null;
    }

    /// <summary>64-bit numeric field: null (never 0) when unreadable.</summary>
    public static long? RequireInt64(this CimInstance? instance, string property, string source, CollectionErrorLog log)
    {
        if (instance is null) return null;

        var value = instance.GetInt64(property);
        if (value is not null) return value.Value;

        log.Add(source, $"'{property}' alanı boş veya okunamaz durumda.");
        return null;
    }
}
