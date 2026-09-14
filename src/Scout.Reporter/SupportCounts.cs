using Scout.Analyzer.Analysis;
using Scout.Analyzer.Compatibility;

namespace Scout.Reporter;

/// <summary>Tally of relevant devices by outcome — computed once, used both for the verdict reason text and elsewhere.</summary>
public readonly record struct SupportCounts(
    int Total,
    int Native,
    int FirmwareRequired,
    int ProprietaryOrPartial,
    int Unsupported,
    int Unknown)
{
    /// <summary>
    /// The single place this tally is computed from an <see cref="AnalysisResult"/> — used by
    /// both <see cref="ReportGenerator"/> (the HTML report) and <see cref="ReportSummaryFormatter"/>
    /// (the CLI's plain-text summary), so the two can never disagree on what "N supported
    /// natively" means.
    /// </summary>
    public static SupportCounts From(IReadOnlyList<DeviceAssessment> devices)
    {
        int native = 0, firmware = 0, proprietaryOrPartial = 0, unsupported = 0, unknown = 0;
        foreach (var assessment in devices)
        {
            switch (assessment.Match.Support)
            {
                case SupportLevel.Native: native++; break;
                case SupportLevel.FirmwareRequired: firmware++; break;
                case SupportLevel.Proprietary or SupportLevel.Partial: proprietaryOrPartial++; break;
                case SupportLevel.Unsupported: unsupported++; break;
                case SupportLevel.Unknown: unknown++; break;
            }
        }

        return new SupportCounts(devices.Count, native, firmware, proprietaryOrPartial, unsupported, unknown);
    }
}
