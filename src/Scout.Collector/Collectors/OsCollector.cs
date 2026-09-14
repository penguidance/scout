using Microsoft.Management.Infrastructure;
using Scout.Collector.Cim;
using Scout.Core.Models;

namespace Scout.Collector.Collectors;

/// <summary>
/// Collects <see cref="OsInfo"/>, primarily from Win32_OperatingSystem — see
/// docs/schema/profile-v0.1.md — "Kaynak eşlemesi".
/// </summary>
/// <remarks>
/// <c>display_version</c> (e.g. "24H2") has no WMI equivalent on any Windows release — it only
/// ever existed in the registry — so, like <see cref="FirmwareCollector"/> does for SecureBoot,
/// this collector also reads one registry value via StdRegProv (same CIM/WS-Man connection, so
/// this still works against a remote target) to fill it in.
/// </remarks>
public sealed class OsCollector : ISectionCollector<OsInfo>
{
    private const string Source = "Win32_OperatingSystem";
    private const uint HKeyLocalMachine = 0x80000002;
    private const string DisplayVersionKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const string DisplayVersionValueName = "DisplayVersion";
    private const string DisplayVersionSource = @"StdRegProv.GetStringValue(...\CurrentVersion\DisplayVersion)";

    private readonly ICimQueryExecutor _cim;
    private readonly TimeProvider _clock;

    public OsCollector(ICimQueryExecutor cim, TimeProvider? clock = null)
    {
        _cim = cim ?? throw new ArgumentNullException(nameof(cim));
        _clock = clock ?? TimeProvider.System;
    }

    public string ComponentName => "os";

    public OsInfo Collect(ICollection<CollectionError> errors)
    {
        var log = new CollectionErrorLog(errors, ComponentName, _clock);
        var os = CimInstanceFetcher.TryGetFirst(_cim, "root/cimv2", "Win32_OperatingSystem", log);

        return new OsInfo
        {
            Edition = os.RequireString("Caption", Source, log),
            Version = os.RequireString("Version", Source, log),
            Build = os.RequireString("BuildNumber", Source, log),
            DisplayVersion = ReadDisplayVersion(log),
            InstallDate = os.GetDateTimeOffset("InstallDate"),
            Architecture = MapArchitecture(os, log),
            Language = FirstLanguage(os)
        };
    }

    /// <summary>
    /// <c>OSArchitecture</c> is a free-text string (observed values: "64 bit"/"32 bit" on x86_64
    /// Windows, "ARM 64-bit Processor" on Windows on Arm) — not a coded enum — so this matches on
    /// substrings rather than exact values.
    /// </summary>
    private static CpuArchitecture? MapArchitecture(CimInstance? os, CollectionErrorLog log)
    {
        if (os is null) return null;

        var raw = os.GetString("OSArchitecture");
        if (raw is null)
        {
            log.Add($"{Source}.OSArchitecture", "Alan okunamadı.");
            return null;
        }

        if (raw.Contains("ARM", StringComparison.OrdinalIgnoreCase)) return CpuArchitecture.arm64;
        if (raw.Contains("64")) return CpuArchitecture.x86_64;
        if (raw.Contains("32")) return CpuArchitecture.x86;

        log.Add($"{Source}.OSArchitecture", $"Tanınmayan değer: '{raw}'.");
        return CpuArchitecture.Unknown;
    }

    private static string? FirstLanguage(CimInstance? os) =>
        os.GetStringArray("MUILanguages") is { Length: > 0 } languages ? languages[0] : null;

    private string? ReadDisplayVersion(CollectionErrorLog log)
    {
        try
        {
            using var parameters = new CimMethodParametersCollection
            {
                CimMethodParameter.Create("hDefKey", HKeyLocalMachine, CimType.UInt32, CimFlags.In),
                CimMethodParameter.Create("sSubKeyName", DisplayVersionKeyPath, CimType.String, CimFlags.In),
                CimMethodParameter.Create("sValueName", DisplayVersionValueName, CimType.String, CimFlags.In)
            };

            var result = _cim.InvokeStaticMethod("root/cimv2", "StdRegProv", "GetStringValue", parameters);
            if (result.ReturnCode != 0)
            {
                // Older Windows releases (pre-1909) genuinely lack this value — not logged as an
                // error there would be noisy on every such machine; but we cannot tell that case
                // apart from a real failure here, so we log it and let the reader judge from
                // collector_version/os.build alongside it.
                log.Add(DisplayVersionSource, $"Registry sorgusu {result.ReturnCode} dönüş koduyla başarısız oldu.");
                return null;
            }

            return result.GetOutParameter("sValue") as string;
        }
        catch (Exception ex)
        {
            log.Add(DisplayVersionSource, ex.Message);
            return null;
        }
    }
}
