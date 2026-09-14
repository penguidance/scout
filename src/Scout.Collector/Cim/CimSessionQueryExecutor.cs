using Microsoft.Management.Infrastructure;

namespace Scout.Collector.Cim;

/// <summary>
/// Real <see cref="ICimQueryExecutor"/>, backed by a <see cref="CimSession"/> that the caller
/// creates and owns (e.g. <c>CimSession.Create(null)</c> for the local machine, or
/// <c>CimSession.Create("host.example.com")</c> for a WS-Man target). This class never creates
/// or closes the session itself, so the same collector code works unmodified against a local or
/// a remote machine.
/// </summary>
public sealed class CimSessionQueryExecutor : ICimQueryExecutor
{
    private readonly CimSession _session;

    public CimSessionQueryExecutor(CimSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public IReadOnlyList<CimInstance> QueryInstances(string namespaceName, string className) =>
        _session.QueryInstances(namespaceName, "WQL", $"SELECT * FROM {className}").ToList();

    public CimStaticMethodResult InvokeStaticMethod(
        string namespaceName,
        string className,
        string methodName,
        CimMethodParametersCollection parameters)
    {
        using var result = _session.InvokeMethod(namespaceName, className, methodName, parameters);
        return ToStaticMethodResult(result);
    }

    public CimStaticMethodResult InvokeInstanceMethod(
        string namespaceName,
        CimInstance instance,
        string methodName,
        CimMethodParametersCollection parameters)
    {
        using var result = _session.InvokeMethod(namespaceName, instance, methodName, parameters);
        return ToStaticMethodResult(result);
    }

    private static CimStaticMethodResult ToStaticMethodResult(CimMethodResult result)
    {
        var outParameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in result.OutParameters)
        {
            outParameters[parameter.Name] = parameter.Value;
        }

        return new CimStaticMethodResult
        {
            ReturnCode = Convert.ToUInt32(result.ReturnValue.Value),
            OutParameters = outParameters
        };
    }
}
