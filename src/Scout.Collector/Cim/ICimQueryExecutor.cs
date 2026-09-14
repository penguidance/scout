using Microsoft.Management.Infrastructure;

namespace Scout.Collector.Cim;

/// <summary>
/// Abstraction over CIM instance/method access. Collector classes depend on this interface
/// instead of <see cref="CimSession"/> directly, so:
/// <list type="bullet">
/// <item>they carry no hard-coded assumption about the local machine — the caller decides which
/// machine a <see cref="CimSession"/> (local or remote, over WS-Man) points at and hands the
/// executor in;</item>
/// <item>they can be unit-tested against fake CIM data (see Scout.Tests) without a real CIM
/// session or a real machine.</item>
/// </list>
/// </summary>
public interface ICimQueryExecutor
{
    /// <summary>Runs "SELECT * FROM &lt;className&gt;" against the given CIM namespace.</summary>
    IReadOnlyList<CimInstance> QueryInstances(string namespaceName, string className);

    /// <summary>Invokes a static (class-level) CIM method, e.g. StdRegProv.GetDWORDValue.</summary>
    CimStaticMethodResult InvokeStaticMethod(
        string namespaceName,
        string className,
        string methodName,
        CimMethodParametersCollection parameters);

    /// <summary>
    /// Invokes an instance method on a specific CIM instance, e.g.
    /// Win32_EncryptableVolume.GetConversionStatus() on one particular volume.
    /// </summary>
    CimStaticMethodResult InvokeInstanceMethod(
        string namespaceName,
        CimInstance instance,
        string methodName,
        CimMethodParametersCollection parameters);
}
