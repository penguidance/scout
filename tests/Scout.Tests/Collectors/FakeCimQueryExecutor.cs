using Microsoft.Management.Infrastructure;
using Scout.Collector.Cim;

namespace Scout.Tests.Collectors;

/// <summary>
/// In-memory <see cref="ICimQueryExecutor"/> for tests. Backed by real
/// <see cref="CimInstance"/>/<see cref="CimProperty"/> objects (so property lookups exercise the
/// exact same code path as a real session would produce) but with no CIM session, WS-Man
/// connection, or real machine involved.
/// </summary>
internal sealed class FakeCimQueryExecutor : ICimQueryExecutor
{
    private readonly Dictionary<(string Namespace, string ClassName), IReadOnlyList<CimInstance>> _instances = new();
    private readonly Dictionary<(string Namespace, string ClassName), Exception> _instanceFailures = new();
    private readonly Dictionary<(string Namespace, string ClassName, string Method), Func<CimMethodParametersCollection, CimStaticMethodResult>> _methodHandlers = new();
    private readonly Dictionary<(string Namespace, string Method), Func<CimInstance, CimStaticMethodResult>> _instanceMethodHandlers = new();

    public void SetInstances(string namespaceName, string className, params CimInstance[] instances) =>
        _instances[(namespaceName, className)] = instances;

    public void SetInstanceQueryFailure(string namespaceName, string className, Exception exception) =>
        _instanceFailures[(namespaceName, className)] = exception;

    public void SetMethodHandler(
        string namespaceName,
        string className,
        string methodName,
        Func<CimMethodParametersCollection, CimStaticMethodResult> handler) =>
        _methodHandlers[(namespaceName, className, methodName)] = handler;

    public IReadOnlyList<CimInstance> QueryInstances(string namespaceName, string className)
    {
        if (_instanceFailures.TryGetValue((namespaceName, className), out var exception))
        {
            throw exception;
        }

        return _instances.TryGetValue((namespaceName, className), out var instances)
            ? instances
            : [];
    }

    public CimStaticMethodResult InvokeStaticMethod(
        string namespaceName,
        string className,
        string methodName,
        CimMethodParametersCollection parameters)
    {
        if (_methodHandlers.TryGetValue((namespaceName, className, methodName), out var handler))
        {
            return handler(parameters);
        }

        throw new InvalidOperationException(
            $"Test'te '{namespaceName}!{className}.{methodName}' için sahte bir işleyici tanımlanmadı.");
    }

    /// <param name="handler">Receives the specific instance the method was invoked on (e.g. so a test can branch on one of its properties).</param>
    public void SetInstanceMethodHandler(
        string namespaceName,
        string methodName,
        Func<CimInstance, CimStaticMethodResult> handler) =>
        _instanceMethodHandlers[(namespaceName, methodName)] = handler;

    public CimStaticMethodResult InvokeInstanceMethod(
        string namespaceName,
        CimInstance instance,
        string methodName,
        CimMethodParametersCollection parameters)
    {
        if (_instanceMethodHandlers.TryGetValue((namespaceName, methodName), out var handler))
        {
            return handler(instance);
        }

        throw new InvalidOperationException(
            $"Test'te '{namespaceName}!*.{methodName}' için sahte bir örnek-metodu işleyicisi tanımlanmadı.");
    }

    /// <summary>Builds a real <see cref="CimInstance"/> populated with the given fake properties.</summary>
    public static CimInstance Instance(string className, params (string Name, object? Value)[] properties)
    {
        var instance = new CimInstance(className);
        foreach (var (name, value) in properties)
        {
            instance.CimInstanceProperties.Add(
                value is null
                    ? CimProperty.Create(name, null, CimType.String, CimFlags.NullValue)
                    : CimProperty.Create(name, value, CimFlags.None));
        }

        return instance;
    }
}
