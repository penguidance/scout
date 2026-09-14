using Scout.Analyzer.Analysis;

namespace Scout.Reporter;

/// <summary>
/// Builds the short, English, machine-readable-ish summary Scout.Collector prints to stdout when
/// run with <c>--report</c> — see README.md's usage example:
/// <code>
/// Verdict: ready
/// 12 devices examined, 10 supported natively.
/// Report written to report.html
/// </code>
/// Deliberately separate from <see cref="ReportStrings"/>: that class holds the Turkish text of
/// the HTML report itself; this is a CLI/log line, in the same terse English style as the rest of
/// Scout.Collector's console output, not part of the localized report a reader might forward.
/// </summary>
public static class ReportSummaryFormatter
{
    public static IReadOnlyList<string> BuildLines(AnalysisResult analysis, string reportPath)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(reportPath);

        var counts = SupportCounts.From(analysis.Devices);

        return
        [
            $"Verdict: {VerdictToken(analysis.Verdict)}",
            $"{counts.Total} devices examined, {counts.Native} supported natively.",
            $"Report written to {reportPath}"
        ];
    }

    /// <summary>Lowercase snake_case token for <see cref="Verdict"/> — a stable CLI-output spelling, independent of the Turkish headline shown in the HTML report itself.</summary>
    private static string VerdictToken(Verdict verdict) => verdict switch
    {
        Verdict.Ready => "ready",
        Verdict.MinorIssues => "minor_issues",
        Verdict.NeedsAttention => "needs_attention",
        Verdict.Blocked => "blocked",
        Verdict.InsufficientData => "insufficient_data",
        _ => "unknown"
    };
}
