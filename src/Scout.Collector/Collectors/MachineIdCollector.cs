using System.Security.Cryptography;
using System.Text;
using Microsoft.Management.Infrastructure;
using Scout.Collector.Cim;
using Scout.Core.Models;

namespace Scout.Collector.Collectors;

/// <summary>
/// Derives <c>machine_id</c>: the SHA-256 digest of the machine's
/// <c>HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid</c>, prefixed with <c>sha256:</c>. Read
/// via the StdRegProv WMI class (not the local Registry API), so it works the same way against
/// a remote target over WS-Man. The raw GUID is hashed immediately and never returned, logged,
/// or stored anywhere — only the digest leaves this method.
/// </summary>
public sealed class MachineIdCollector : ISectionCollector<string>
{
    private const uint HKeyLocalMachine = 0x80000002;
    private const string KeyPath = @"SOFTWARE\Microsoft\Cryptography";
    private const string ValueName = "MachineGuid";
    private const string Source = @"StdRegProv.GetStringValue(SOFTWARE\Microsoft\Cryptography\MachineGuid)";

    private readonly ICimQueryExecutor _cim;
    private readonly TimeProvider _clock;

    public MachineIdCollector(ICimQueryExecutor cim, TimeProvider? clock = null)
    {
        _cim = cim ?? throw new ArgumentNullException(nameof(cim));
        _clock = clock ?? TimeProvider.System;
    }

    public string ComponentName => "machine_id";

    /// <returns>e.g. "sha256:9f86d0..."; "" if the GUID could not be read.</returns>
    public string Collect(ICollection<CollectionError> errors)
    {
        var log = new CollectionErrorLog(errors, ComponentName, _clock);

        try
        {
            using var parameters = new CimMethodParametersCollection
            {
                CimMethodParameter.Create("hDefKey", HKeyLocalMachine, CimType.UInt32, CimFlags.In),
                CimMethodParameter.Create("sSubKeyName", KeyPath, CimType.String, CimFlags.In),
                CimMethodParameter.Create("sValueName", ValueName, CimType.String, CimFlags.In)
            };

            var result = _cim.InvokeStaticMethod("root/cimv2", "StdRegProv", "GetStringValue", parameters);
            if (result.ReturnCode != 0)
            {
                log.Add(Source, $"Registry sorgusu {result.ReturnCode} dönüş koduyla başarısız oldu.");
                return "";
            }

            var rawGuid = result.GetOutParameter("sValue") as string;
            if (string.IsNullOrWhiteSpace(rawGuid))
            {
                log.Add(Source, "MachineGuid değeri boş döndü.");
                return "";
            }

            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(rawGuid.Trim()));
            return $"sha256:{Convert.ToHexString(digest).ToLowerInvariant()}";
        }
        catch (Exception ex)
        {
            log.Add(Source, ex.Message);
            return "";
        }
    }
}
