using Scout.Analyzer;
using Scout.Analyzer.Analysis;
using Scout.Analyzer.Compatibility;
using Scout.Core.Serialization;
using Scout.Reporter;
using Xunit;

namespace Scout.Tests.Reporter;

/// <summary>End-to-end: real profile → real database → real report, exactly the path both Scout.Collector's --report and Scout.Reporter's standalone CLI take.</summary>
public class ReporterIntegrationTests
{
    private static string SamplePath(string fileName) => Path.Combine(AppContext.BaseDirectory, "samples", fileName);

    [Fact]
    public void Generate_AdminSample_ProducesAReadyReportWithNoAttentionSection()
    {
        var profile = ProfileJsonSerializer.Deserialize(File.ReadAllText(SamplePath("ryzen-5600x-msi-b450-admin.json")));
        var analyzer = new MachineAnalyzer(new CompatibilityMatcher(CompatibilityDatabase.LoadEmbedded()));
        var analysis = analyzer.Analyze(profile);

        var html = new ReportGenerator().Generate(profile, analysis);

        Assert.Contains(ReportStrings.VerdictHeadline(Verdict.Ready), html);
        Assert.Contains("AMD Radeon RX 5500 XT", html);
        Assert.DoesNotContain(ReportStrings.AttentionSectionTitle, html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    private static string GenerateReportFor(string sampleFileName)
    {
        var profile = ProfileJsonSerializer.Deserialize(File.ReadAllText(SamplePath(sampleFileName)));
        var analyzer = new MachineAnalyzer(new CompatibilityMatcher(CompatibilityDatabase.LoadEmbedded()));
        var analysis = analyzer.Analyze(profile);
        return new ReportGenerator().Generate(profile, analysis);
    }

    [Fact]
    public void Generate_OptimusLaptopSample_ShowsNvidiaInAttentionWithPlainLanguageNote()
    {
        var html = GenerateReportFor("synthetic-optimus-laptop.json");

        Assert.Contains(ReportStrings.VerdictHeadline(Verdict.NeedsAttention), html);
        Assert.Contains(ReportStrings.AttentionSectionTitle, html);
        Assert.Contains("NVIDIA GeForce GTX 1650", html);
        Assert.Contains("kendi sürücüsünü kurman gerekir", html); // notes text, not a driver module name
    }

    [Fact]
    public void Generate_OldRadeonSample_TechnicalDumpShowsRadeonDisplayNameNotRawAmdgpu()
    {
        var html = GenerateReportFor("synthetic-old-radeon.json");

        // No device is a problem (still MinorIssues, not Ready — see the system constraints
        // section assertions below), so the per-device attention block stays empty.
        Assert.Contains(ReportStrings.VerdictHeadline(Verdict.MinorIssues), html);
        Assert.DoesNotContain(ReportStrings.AttentionSectionTitle, html);
        Assert.Contains("Radeon (eski nesil sürücü)", html);
        Assert.DoesNotContain("<code>amdgpu</code>", html);
    }

    [Fact]
    public void Generate_OldRadeonSample_ShowsSystemConstraintsSectionForCpuAndBootMode()
    {
        var html = GenerateReportFor("synthetic-old-radeon.json");

        Assert.Contains(ReportStrings.SystemConstraintsSectionTitle, html);
        Assert.Contains("x86-64-v2", html);
        Assert.Contains("BIOS (Legacy)", html);
    }

    [Fact]
    public void Generate_BroadcomMacBookSample_ShowsAttentionSection()
    {
        var html = GenerateReportFor("synthetic-broadcom-macbook.json");

        Assert.Contains(ReportStrings.AttentionSectionTitle, html);
        Assert.Contains("Broadcom 802.11ac Network Adapter", html);
    }
}
