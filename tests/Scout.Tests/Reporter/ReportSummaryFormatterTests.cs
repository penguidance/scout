using Scout.Analyzer.Analysis;
using Scout.Analyzer.Compatibility;
using Scout.Core.Models;
using Scout.Reporter;
using Xunit;

namespace Scout.Tests.Reporter;

/// <summary>
/// Locks the exact stdout summary Scout.Collector prints in <c>--report</c> mode to the shape
/// README.md documents:
/// <code>
/// Verdict: ready
/// 12 devices examined, 10 supported natively.
/// Report written to report.html
/// </code>
/// </summary>
public class ReportSummaryFormatterTests
{
    private static DeviceAssessment Device(SupportLevel support) => new()
    {
        Device = new DeviceInfo
        {
            HardwareId = @"PCI\VEN_1002&DEV_0000",
            HardwareIdRaw = @"PCI\VEN_1002&DEV_0000",
            BusType = DeviceBusType.PCI,
            CompatibleIds = [],
            FriendlyName = "Test Device",
            Class = "Display",
            Status = DeviceStatus.OK
        },
        Match = new CompatibilityMatch
        {
            Level = support == SupportLevel.Unknown ? MatchLevel.None : MatchLevel.Exact,
            Entry = null,
            Support = support
        }
    };

    private static AnalysisResult Result(Verdict verdict, params SupportLevel[] deviceSupport) => new()
    {
        Devices = deviceSupport.Select(Device).ToList(),
        Verdict = verdict,
        SystemConstraints = [],
        DevicesRemovedForRelevance = 0,
        DevicesRemovedForDuplication = 0,
        SoftwareAssessments = [],
        SoftwareSkippedForCategory = 0,
        DistributionRecommendation = TestDistributionRecommendation.Default()
    };

    [Fact]
    public void BuildLines_MatchesTheReadmeExampleShape()
    {
        var analysis = Result(
            Verdict.Ready,
            SupportLevel.Native, SupportLevel.Native, SupportLevel.Native, SupportLevel.Native,
            SupportLevel.Native, SupportLevel.Native, SupportLevel.Native, SupportLevel.Native,
            SupportLevel.Native, SupportLevel.Native, SupportLevel.FirmwareRequired, SupportLevel.Unknown);

        var lines = ReportSummaryFormatter.BuildLines(analysis, "report.html");

        Assert.Equal(
        [
            "Verdict: ready",
            "12 devices examined, 10 supported natively.",
            "Report written to report.html"
        ], lines);
    }

    [Theory]
    [InlineData(Verdict.Ready, "ready")]
    [InlineData(Verdict.MinorIssues, "minor_issues")]
    [InlineData(Verdict.NeedsAttention, "needs_attention")]
    [InlineData(Verdict.Blocked, "blocked")]
    [InlineData(Verdict.InsufficientData, "insufficient_data")]
    public void BuildLines_UsesAStableLowercaseTokenPerVerdict(Verdict verdict, string expectedToken)
    {
        var lines = ReportSummaryFormatter.BuildLines(Result(verdict), "report.html");

        Assert.Equal($"Verdict: {expectedToken}", lines[0]);
    }

    [Fact]
    public void BuildLines_ReportsTheGivenPathVerbatim()
    {
        var lines = ReportSummaryFormatter.BuildLines(Result(Verdict.Ready), @"C:\out\my-report.html");

        Assert.Equal(@"Report written to C:\out\my-report.html", lines[2]);
    }

    [Fact]
    public void BuildLines_ThrowsOnNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => ReportSummaryFormatter.BuildLines(null!, "report.html"));
        Assert.Throws<ArgumentNullException>(() => ReportSummaryFormatter.BuildLines(Result(Verdict.Ready), null!));
    }
}
