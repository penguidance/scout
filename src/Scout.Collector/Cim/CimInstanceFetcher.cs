using Microsoft.Management.Infrastructure;

namespace Scout.Collector.Cim;

/// <summary>Outcome of fetching a CIM class that is expected to have at most one instance.</summary>
internal enum CimSingletonStatus
{
    /// <summary>The query succeeded and returned an instance.</summary>
    Present,

    /// <summary>The query succeeded but returned zero instances — a confirmed negative.</summary>
    Absent,

    /// <summary>The query itself failed (e.g. access denied, namespace unreachable) — unknown.</summary>
    Unknown
}

/// <summary>Fetches CIM instances, logging (never throwing) on failure.</summary>
internal static class CimInstanceFetcher
{
    /// <summary>
    /// Fetches the first instance of a class that every machine is expected to have exactly one
    /// of (Win32_ComputerSystem, Win32_BIOS, Win32_Processor, ...). An empty result is therefore
    /// treated the same as a query failure: both are logged and both yield a null instance.
    /// </summary>
    public static CimInstance? TryGetFirst(
        ICimQueryExecutor cim,
        string namespaceName,
        string className,
        CollectionErrorLog log)
    {
        try
        {
            var instances = cim.QueryInstances(namespaceName, className);
            if (instances.Count > 0) return instances[0];

            log.Add(className, "Sorgu sonuç döndürmedi (0 örnek).");
            return null;
        }
        catch (Exception ex)
        {
            log.Add(className, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Fetches a class that may legitimately have zero instances on some machines (e.g. Win32_Tpm
    /// on hardware with no TPM chip). Unlike <see cref="TryGetFirst"/>, an empty result is NOT an
    /// error — it is a confirmed negative (<see cref="CimSingletonStatus.Absent"/>) and is not
    /// logged. Only an actual query failure is logged and reported as
    /// <see cref="CimSingletonStatus.Unknown"/>, so callers never have to collapse "confirmed
    /// absent" and "could not tell" into the same boolean value.
    /// </summary>
    public static (CimSingletonStatus Status, CimInstance? Instance) TryGetOptionalSingleton(
        ICimQueryExecutor cim,
        string namespaceName,
        string className,
        CollectionErrorLog log)
    {
        try
        {
            var instances = cim.QueryInstances(namespaceName, className);
            return instances.Count > 0
                ? (CimSingletonStatus.Present, instances[0])
                : (CimSingletonStatus.Absent, null);
        }
        catch (Exception ex)
        {
            log.Add(className, ex.Message);
            return (CimSingletonStatus.Unknown, null);
        }
    }
}
