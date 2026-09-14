namespace Scout.Collector.Cim;

/// <summary>
/// Simplified result of invoking a static CIM method (e.g. StdRegProv.GetDWORDValue).
/// <para>
/// This deliberately does not expose the real <c>Microsoft.Management.Infrastructure.CimMethodResult</c>:
/// that type's constructor is internal to the MI client library, so tests in a different
/// assembly cannot build one to fake a method call. This DTO is trivial to construct from a
/// unit test instead.
/// </para>
/// </summary>
public sealed class CimStaticMethodResult
{
    /// <summary>The method's own uint32 return code (0 conventionally means success).</summary>
    public required uint ReturnCode { get; init; }

    /// <summary>The method's [out] parameters, by name (case-insensitive).</summary>
    public required IReadOnlyDictionary<string, object?> OutParameters { get; init; }

    public object? GetOutParameter(string name) =>
        OutParameters.TryGetValue(name, out var value) ? value : null;
}
