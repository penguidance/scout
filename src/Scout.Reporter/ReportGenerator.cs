using System.Text;
using Scout.Analyzer.Analysis;
using Scout.Analyzer.Compatibility;
using Scout.Analyzer.Recommendations;
using Scout.Core.Models;

namespace Scout.Reporter;

/// <summary>
/// Turns an <see cref="AnalysisResult"/> (plus the <see cref="MachineProfile"/> it came from, for
/// the system-summary block) into a single, self-contained HTML report.
/// </summary>
/// <remarks>
/// Design principle: the reader should get the whole judgment from one sentence at the top —
/// the technical breakdown is real but folded away by default, not the first thing they see.
/// The file has no external dependency of any kind (no CDN, font, script, image) so it opens
/// correctly as an email attachment or from a shared folder with no network access.
/// </remarks>
public sealed class ReportGenerator
{
    public string Generate(MachineProfile profile, AnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(analysis);

        var counts = SupportCounts.From(analysis.Devices);
        var machineLabel = BuildMachineLabel(profile);

        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"tr\">\n<head>\n<meta charset=\"utf-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append($"<title>{Html(ReportStrings.DocumentTitle(machineLabel))}</title>\n");
        sb.Append("<style>\n").Append(ReportCss.Content).Append("\n</style>\n");
        sb.Append("</head>\n<body>\n");

        AppendVerdictBanner(sb, analysis.Verdict, counts);

        sb.Append("<div class=\"container\">\n");
        AppendDistributionRecommendation(sb, analysis.DistributionRecommendation);
        AppendDailyLifeImpact(sb, profile, analysis);
        AppendAttentionSection(sb, analysis);
        AppendSoftwareProblemsSection(sb, analysis);
        AppendSystemConstraintsSection(sb, analysis);
        AppendSoftwareEquivalentsSection(sb, analysis);
        AppendTechnicalDetails(sb, analysis);
        AppendSoftwareTechnicalDetails(sb, profile, analysis);
        AppendSystemSummary(sb, profile);
        AppendNextSteps(sb, profile, analysis);
        sb.Append("</div>\n");

        sb.Append($"<footer>{Html(ReportStrings.GeneratedFooter(profile.CollectedAt))}</footer>\n");
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private static string BuildMachineLabel(MachineProfile profile)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(profile.System.Manufacturer)) parts.Add(profile.System.Manufacturer);
        if (!string.IsNullOrWhiteSpace(profile.System.Model)) parts.Add(profile.System.Model);

        var label = parts.Count > 0 ? string.Join(" ", parts) : "Bilinmeyen Makine";
        return profile.System.Hostname is not null ? $"{label} ({profile.System.Hostname})" : label;
    }

    // ---- Block 1: the verdict ----------------------------------------------------------------

    private static void AppendVerdictBanner(StringBuilder sb, Verdict verdict, SupportCounts counts)
    {
        var cssClass = VerdictCssClass(verdict);
        sb.Append($"<header class=\"verdict verdict-{cssClass}\">\n");
        sb.Append($"  <p class=\"verdict-headline\">{Html(ReportStrings.VerdictHeadline(verdict))}</p>\n");
        sb.Append($"  <p class=\"verdict-reason\">{Html(ReportStrings.VerdictReason(verdict, counts))}</p>\n");
        sb.Append("</header>\n");
    }

    private static string VerdictCssClass(Verdict verdict) => verdict switch
    {
        Verdict.Ready => "good",
        Verdict.MinorIssues => "warn",
        Verdict.NeedsAttention => "attention",
        Verdict.Blocked => "bad",
        Verdict.InsufficientData => "unknown",
        _ => "unknown"
    };

    private static string SupportCssClass(SupportLevel level) => level switch
    {
        SupportLevel.Native => "good",
        SupportLevel.FirmwareRequired => "warn",
        SupportLevel.Proprietary or SupportLevel.Partial => "attention",
        SupportLevel.Unsupported => "bad",
        SupportLevel.Unknown => "unknown",
        _ => "unknown"
    };

    // ---- Block 2: distribution recommendation — right under the verdict, one pick, one reason -

    private static void AppendDistributionRecommendation(StringBuilder sb, DistributionRecommendation recommendation)
    {
        var primary = recommendation.Primary;

        sb.Append("<section class=\"card\">\n");
        sb.Append($"  <h2>{Html(ReportStrings.DistributionSectionTitle)}</h2>\n");
        sb.Append($"  <p class=\"distro-name\">{Html(primary.Name)} <span class=\"muted\">({Html(primary.DesktopEnvironment)})</span></p>\n");
        sb.Append($"  <p>{Html(ReportStrings.DistributionReason(recommendation.PrimaryReason, primary.Name))}</p>\n");
        sb.Append(
            $"  <p><a href=\"{Html(primary.DownloadUrl)}\">{Html(ReportStrings.DistributionDownloadLabel(primary.Name))}</a></p>\n");

        if (recommendation.Secondary is { } secondary)
        {
            sb.Append(
                $"  <p class=\"muted\">{Html(ReportStrings.DistributionAlternative(secondary.Name, secondary.DesktopEnvironment))}</p>\n");
        }

        sb.Append("</section>\n");
    }

    // ---- Block 3: daily-life impact — plain language, only the items that actually apply ------

    private static readonly string[] GamePlatformNameKeywords =
        ["Steam", "Epic Games", "Battle.net", "Origin", "EA app", "Ubisoft Connect", "Uplay", "GOG Galaxy"];

    private static readonly string[] OfficeNameKeywords = ["Microsoft Office", "Microsoft 365 Apps"];

    private static void AppendDailyLifeImpact(StringBuilder sb, MachineProfile profile, AnalysisResult analysis)
    {
        var items = new List<string>();

        var printer = analysis.Devices.FirstOrDefault(d => string.Equals(d.Device.Class, "PrintQueue", StringComparison.OrdinalIgnoreCase));
        if (printer is not null)
        {
            items.Add(ReportStrings.DailyLifePrinter(printer.Match.Support));
        }

        var gamingAssessments = analysis.SoftwareAssessments
            .Where(a => GamePlatformNameKeywords.Any(k => a.Software.Name.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (gamingAssessments.Any(a => a.Match.Status == SoftwareCompatibilityStatus.Native))
        {
            items.Add(ReportStrings.DailyLifeGamingNative);
        }
        if (gamingAssessments.Any(a => a.Match.Status is SoftwareCompatibilityStatus.Wine or SoftwareCompatibilityStatus.Equivalent))
        {
            items.Add(ReportStrings.DailyLifeGamingWine);
        }

        if (analysis.SoftwareAssessments.Any(a => OfficeNameKeywords.Any(k => a.Software.Name.Contains(k, StringComparison.OrdinalIgnoreCase))))
        {
            items.Add(ReportStrings.DailyLifeOffice);
        }

        if (analysis.Devices.Any(d => string.Equals(d.Device.Class, "Net", StringComparison.OrdinalIgnoreCase) && d.Match.Support == SupportLevel.FirmwareRequired))
        {
            items.Add(ReportStrings.DailyLifeWifiFirmware);
        }

        if (profile.GpuTopology.Layout == GpuLayout.Hybrid)
        {
            items.Add(ReportStrings.DailyLifeHybridGpu);
        }

        // Same "say nothing when there is nothing to say" rule as every other section here.
        if (items.Count == 0) return;

        sb.Append("<section class=\"card\">\n");
        sb.Append($"  <h2>{Html(ReportStrings.DailyLifeSectionTitle)}</h2>\n");
        sb.Append("  <ul>\n");
        foreach (var item in items)
        {
            sb.Append($"    <li>{Html(item)}</li>\n");
        }
        sb.Append("  </ul>\n</section>\n");
    }

    // ---- Block 4: devices needing attention --------------------------------------------------

    private static readonly HashSet<SupportLevel> AttentionWorthyLevels =
    [
        SupportLevel.FirmwareRequired,
        SupportLevel.Proprietary,
        SupportLevel.Partial,
        SupportLevel.Unsupported
    ];

    private static void AppendAttentionSection(StringBuilder sb, AnalysisResult analysis)
    {
        // Deliberately not "everything that isn't Native": an Unknown device (no database entry
        // at all — e.g. a generic USB root hub with no vendor_id to look up) is not a known
        // problem, it is a gap in what we could check. Surfacing it here as "go research this
        // yourself" is noise that undercuts the one-sentence-first design goal; it still appears,
        // honestly labeled "Bilinmiyor", in the folded technical breakdown below.
        var attentionDevices = analysis.Devices
            .Where(d => AttentionWorthyLevels.Contains(d.Match.Support))
            .OrderBy(d => SeverityRank(d.Match.Support))
            .ThenBy(d => d.Device.FriendlyName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // The whole design point of this block: if there is nothing to flag, it does not appear
        // at all — a clean "Ready" report has no empty "Attention" card sitting there for show.
        if (attentionDevices.Count == 0) return;

        sb.Append("<section class=\"card\">\n");
        sb.Append($"  <h2>{Html(ReportStrings.AttentionSectionTitle)}</h2>\n");
        sb.Append("  <table>\n    <thead><tr>");
        sb.Append($"<th>{Html(ReportStrings.AttentionColumnDevice)}</th>");
        sb.Append($"<th>{Html(ReportStrings.AttentionColumnWhatHappens)}</th>");
        sb.Append($"<th>{Html(ReportStrings.AttentionColumnWhatToDo)}</th>");
        sb.Append("</tr></thead>\n    <tbody>\n");

        foreach (var assessment in attentionDevices)
        {
            // The "what to do" text is the database's own notes field, verbatim — never a
            // string composed in code — so it can be corrected/improved by editing the JSON.
            var whatToDo = assessment.Match.Entry?.Notes ?? ReportStrings.NoNotesAvailable;

            sb.Append("      <tr>");
            sb.Append($"<td>{Html(assessment.Device.FriendlyName)}</td>");
            sb.Append(
                $"<td><span class=\"badge badge-{SupportCssClass(assessment.Match.Support)}\">" +
                $"{Html(ReportStrings.SupportLevelLabel(assessment.Match.Support))}</span></td>");
            sb.Append($"<td>{Html(whatToDo)}</td>");
            sb.Append("</tr>\n");
        }

        sb.Append("    </tbody>\n  </table>\n</section>\n");
    }

    private static int SeverityRank(SupportLevel level) => level switch
    {
        SupportLevel.Unsupported => 0,
        SupportLevel.Proprietary => 1,
        SupportLevel.Partial => 2,
        SupportLevel.FirmwareRequired => 3,
        SupportLevel.Unknown => 4,
        _ => 5
    };

    // ---- Block 5: blocked/Wine-only software — the software equivalent of Block 4 -------------

    private static readonly HashSet<SoftwareCompatibilityStatus> SoftwareProblemStatuses =
    [
        SoftwareCompatibilityStatus.Blocked,
        SoftwareCompatibilityStatus.Wine
    ];

    private static void AppendSoftwareProblemsSection(StringBuilder sb, AnalysisResult analysis)
    {
        var problems = analysis.SoftwareAssessments
            .Where(a => SoftwareProblemStatuses.Contains(a.Match.Status))
            .OrderBy(a => a.Match.Status == SoftwareCompatibilityStatus.Blocked ? 0 : 1)
            .ThenBy(a => a.Software.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (problems.Count == 0) return;

        sb.Append("<section class=\"card\">\n");
        sb.Append($"  <h2>{Html(ReportStrings.SoftwareProblemsSectionTitle)}</h2>\n");
        sb.Append("  <table>\n    <thead><tr>");
        sb.Append($"<th>{Html(ReportStrings.SoftwareProblemsColumnProgram)}</th>");
        sb.Append($"<th>{Html(ReportStrings.SoftwareProblemsColumnWhatHappens)}</th>");
        sb.Append($"<th>{Html(ReportStrings.SoftwareProblemsColumnWhatToDo)}</th>");
        sb.Append($"<th>{Html(ReportStrings.SoftwareProblemsColumnAlternative)}</th>");
        sb.Append("</tr></thead>\n    <tbody>\n");

        foreach (var assessment in problems)
        {
            sb.Append("      <tr>");
            sb.Append($"<td>{Html(assessment.Software.Name)}</td>");
            sb.Append(
                $"<td><span class=\"badge badge-{SoftwareStatusCssClass(assessment.Match.Status)}\">" +
                $"{Html(ReportStrings.SoftwareStatusLabel(assessment.Match.Status))}</span></td>");
            sb.Append($"<td>{Html(assessment.Match.Entry?.Notes ?? ReportStrings.Unknown)}</td>");
            sb.Append($"<td>{Html(ReportStrings.SoftwareAlternativesSummary(assessment.Match.Entry))}</td>");
            sb.Append("</tr>\n");
        }

        sb.Append("    </tbody>\n  </table>\n</section>\n");
    }

    private static string SoftwareStatusCssClass(SoftwareCompatibilityStatus status) => status switch
    {
        SoftwareCompatibilityStatus.Native or SoftwareCompatibilityStatus.Web => "good",
        SoftwareCompatibilityStatus.Equivalent => "warn",
        SoftwareCompatibilityStatus.Wine => "attention",
        SoftwareCompatibilityStatus.Blocked => "bad",
        SoftwareCompatibilityStatus.Unknown => "unknown",
        _ => "unknown"
    };

    // ---- Block 6: system-wide constraints, kept separate from per-device findings ------------

    private static string SystemConstraintCssClass(SystemConstraintSeverity severity) => severity switch
    {
        SystemConstraintSeverity.Critical => "attention",
        SystemConstraintSeverity.Soft => "warn",
        _ => "unknown"
    };

    private static void AppendSystemConstraintsSection(StringBuilder sb, AnalysisResult analysis)
    {
        // Same "say nothing when there is nothing to say" rule as the device attention section.
        if (analysis.SystemConstraints.Count == 0) return;

        var constraints = analysis.SystemConstraints
            .OrderByDescending(c => c.Severity == SystemConstraintSeverity.Critical);

        sb.Append("<section class=\"card\">\n");
        sb.Append($"  <h2>{Html(ReportStrings.SystemConstraintsSectionTitle)}</h2>\n");
        sb.Append("  <table>\n    <thead><tr>");
        sb.Append($"<th>{Html(ReportStrings.SystemConstraintsColumnTopic)}</th>");
        sb.Append($"<th>{Html(ReportStrings.SystemConstraintsColumnSeverity)}</th>");
        sb.Append($"<th>{Html(ReportStrings.SystemConstraintsColumnDetail)}</th>");
        sb.Append("</tr></thead>\n    <tbody>\n");

        foreach (var constraint in constraints)
        {
            sb.Append("      <tr>");
            sb.Append($"<td>{Html(ReportStrings.SystemConstraintTopic(constraint.Kind))}</td>");
            sb.Append(
                $"<td><span class=\"badge badge-{SystemConstraintCssClass(constraint.Severity)}\">" +
                $"{Html(ReportStrings.SystemConstraintSeverityLabel(constraint.Severity))}</span></td>");
            sb.Append($"<td>{Html(ReportStrings.SystemConstraintDetail(constraint))}</td>");
            sb.Append("</tr>\n");
        }

        sb.Append("    </tbody>\n  </table>\n</section>\n");
    }

    // ---- Block 7: software with a substitute, plus a one-line summary of the rest -------------

    private static readonly HashSet<SoftwareCompatibilityStatus> SoftwareEquivalentStatuses =
    [
        SoftwareCompatibilityStatus.Equivalent,
        SoftwareCompatibilityStatus.Web
    ];

    private static void AppendSoftwareEquivalentsSection(StringBuilder sb, AnalysisResult analysis)
    {
        var equivalents = analysis.SoftwareAssessments
            .Where(a => SoftwareEquivalentStatuses.Contains(a.Match.Status) && a.Match.Entry is not null)
            .OrderBy(a => a.Software.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Unknown status is deliberately excluded from this count, same reasoning as an Unknown
        // device: not looked up at all is not the same claim as "confirmed to work".
        var native = analysis.SoftwareAssessments
            .Where(a => a.Match.Status == SoftwareCompatibilityStatus.Native)
            .OrderBy(a => a.Software.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Same "say nothing when there is nothing to say" rule as the other sections — if neither
        // half has anything, the whole card (including an empty summary sentence) is skipped.
        if (equivalents.Count == 0 && native.Count == 0) return;

        sb.Append("<section class=\"card\">\n");
        sb.Append($"  <h2>{Html(ReportStrings.SoftwareEquivalentsSectionTitle)}</h2>\n");

        if (equivalents.Count > 0)
        {
            sb.Append("  <table>\n    <thead><tr>");
            sb.Append($"<th>{Html(ReportStrings.SoftwareEquivalentsColumnProgram)}</th>");
            sb.Append($"<th>{Html(ReportStrings.SoftwareEquivalentsColumnAlternative)}</th>");
            sb.Append("</tr></thead>\n    <tbody>\n");

            foreach (var assessment in equivalents)
            {
                sb.Append("      <tr>");
                sb.Append($"<td>{Html(assessment.Software.Name)}</td>");
                sb.Append($"<td>{Html(ReportStrings.SoftwareEquivalentCell(assessment.Match.Entry!))}</td>");
                sb.Append("</tr>\n");
            }

            sb.Append("    </tbody>\n  </table>\n");
        }

        if (native.Count > 0)
        {
            const int exampleCount = 3;
            var examples = native.Take(exampleCount).Select(a => a.Software.Name).ToList();
            var remaining = native.Count - examples.Count;

            sb.Append($"  <p class=\"muted\">{Html(ReportStrings.NativeSoftwareSummary(examples, remaining))}</p>\n");
        }

        sb.Append("</section>\n");
    }

    // ---- Block 8: full technical breakdown, folded away by default ------------------------

    private static void AppendTechnicalDetails(StringBuilder sb, AnalysisResult analysis)
    {
        sb.Append("<section class=\"card\">\n  <details>\n");
        sb.Append($"    <summary>{Html(ReportStrings.TechnicalDetailsSummary)} ({analysis.Devices.Count})</summary>\n");
        sb.Append(
            $"    <p class=\"muted\">{Html(ReportStrings.NoiseRemovedNote(analysis.DevicesRemovedForRelevance, analysis.DevicesRemovedForDuplication))}</p>\n");

        sb.Append("    <table>\n      <thead><tr>");
        sb.Append($"<th>{Html(ReportStrings.TechnicalColumnDevice)}</th>");
        sb.Append($"<th>{Html(ReportStrings.TechnicalColumnVendorDevice)}</th>");
        sb.Append($"<th>{Html(ReportStrings.TechnicalColumnClass)}</th>");
        sb.Append($"<th>{Html(ReportStrings.TechnicalColumnDriver)}</th>");
        sb.Append($"<th>{Html(ReportStrings.TechnicalColumnMatch)}</th>");
        sb.Append($"<th>{Html(ReportStrings.TechnicalColumnSupport)}</th>");
        sb.Append("</tr></thead>\n      <tbody>\n");

        var rows = analysis.Devices
            .OrderBy(d => d.Device.Class, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Device.FriendlyName, StringComparer.OrdinalIgnoreCase);

        foreach (var assessment in rows)
        {
            var vendorDevice = assessment.Device.VendorId is null
                ? "—"
                : $"{assessment.Device.VendorId}:{assessment.Device.DeviceId ?? "—"}";

            sb.Append("        <tr>");
            sb.Append($"<td>{Html(assessment.Device.FriendlyName)}</td>");
            sb.Append($"<td><code>{Html(vendorDevice)}</code></td>");
            sb.Append($"<td>{Html(assessment.Device.Class)}</td>");
            sb.Append($"<td>{DriverCell(assessment.Match.Entry)}</td>");
            sb.Append($"<td>{Html(ReportStrings.MatchLevelLabel(assessment.Match.Level))}</td>");
            sb.Append(
                $"<td><span class=\"badge badge-{SupportCssClass(assessment.Match.Support)}\">" +
                $"{Html(ReportStrings.SupportLevelLabel(assessment.Match.Support))}</span></td>");
            sb.Append("</tr>\n");
        }

        sb.Append("      </tbody>\n    </table>\n  </details>\n</section>\n");
    }

    // ---- Block 9: full software inventory, folded away by default -----------------------------
    //
    // Unlike the device technical dump, this includes every profile.Software entry — runtime/
    // driver/system categories included — since "tam liste" means the complete collected
    // inventory, not just the subset that was actually looked up against the database.

    private static void AppendSoftwareTechnicalDetails(StringBuilder sb, MachineProfile profile, AnalysisResult analysis)
    {
        if (profile.Software.Count == 0) return;

        var matchBySoftware = analysis.SoftwareAssessments.ToDictionary(a => a.Software, a => a.Match);

        sb.Append("<section class=\"card\">\n  <details>\n");
        sb.Append($"    <summary>{Html(ReportStrings.SoftwareTechnicalDetailsSummary)} ({profile.Software.Count})</summary>\n");
        sb.Append(
            $"    <p class=\"muted\">{Html(ReportStrings.SoftwareNoiseRemovedNote(profile.SoftwareFilteredCount ?? 0, analysis.SoftwareSkippedForCategory))}</p>\n");

        sb.Append("    <table>\n      <thead><tr>");
        sb.Append($"<th>{Html(ReportStrings.SoftwareTechnicalColumnProgram)}</th>");
        sb.Append($"<th>{Html(ReportStrings.SoftwareTechnicalColumnPublisher)}</th>");
        sb.Append($"<th>{Html(ReportStrings.SoftwareTechnicalColumnCategory)}</th>");
        sb.Append($"<th>{Html(ReportStrings.SoftwareTechnicalColumnMatch)}</th>");
        sb.Append($"<th>{Html(ReportStrings.SoftwareTechnicalColumnSignal)}</th>");
        sb.Append($"<th>{Html(ReportStrings.SoftwareTechnicalColumnStatus)}</th>");
        sb.Append("</tr></thead>\n      <tbody>\n");

        // Known-status and never-assessed (runtime/driver/system) rows first — both say something
        // real; Unknown ("no database entry at all") is the only genuinely low-value bucket, so it
        // sinks to the bottom with its own one-line explanation instead of hiding among the rest
        // in plain alphabetical order.
        var rows = profile.Software
            .Select(s => (Software: s, Match: matchBySoftware.GetValueOrDefault(s)))
            .OrderBy(r => r.Match?.Status == SoftwareCompatibilityStatus.Unknown ? 1 : 0)
            .ThenBy(r => r.Software.Name, StringComparer.OrdinalIgnoreCase);

        var unknownIntroShown = false;

        foreach (var (software, match) in rows)
        {
            if (!unknownIntroShown && match?.Status == SoftwareCompatibilityStatus.Unknown)
            {
                sb.Append($"        <tr><td colspan=\"6\" class=\"muted\">{Html(ReportStrings.SoftwareUnknownGroupIntro)}</td></tr>\n");
                unknownIntroShown = true;
            }

            sb.Append("        <tr>");
            sb.Append($"<td>{Html(software.Name)}</td>");
            sb.Append($"<td>{Html(software.Publisher ?? "—")}</td>");
            sb.Append($"<td>{Html(ReportStrings.SoftwareCategoryLabel(software.Category))}</td>");

            if (match is not null)
            {
                sb.Append($"<td>{Html(ReportStrings.SoftwareMatchLevelLabel(match.Level))}</td>");
                // Which evidence identified the program — lets a reader spot, at a glance, a
                // match that was only made on a loose name pattern versus one confirmed by the
                // far more reliable registry key, without needing to open the database file.
                sb.Append($"<td>{Html(ReportStrings.SoftwareMatchSignalsSummary(match.Signals))}</td>");
                sb.Append(
                    $"<td><span class=\"badge badge-{SoftwareStatusCssClass(match.Status)}\">" +
                    $"{Html(ReportStrings.SoftwareStatusLabel(match.Status))}</span></td>");
            }
            else
            {
                sb.Append($"<td>—</td><td>—</td><td>{Html(ReportStrings.SoftwareNotAssessed)}</td>");
            }

            sb.Append("</tr>\n");
        }

        sb.Append("      </tbody>\n    </table>\n  </details>\n</section>\n");
    }

    // ---- Block 10: system summary ------------------------------------------------------------

    private static void AppendSystemSummary(StringBuilder sb, MachineProfile profile)
    {
        sb.Append("<section class=\"card\">\n");
        sb.Append($"  <h2>{Html(ReportStrings.SystemSummaryTitle)}</h2>\n");
        sb.Append("  <table class=\"kv\">\n    <tbody>\n");

        var cpuLine = $"{profile.Cpu.Vendor} {profile.Cpu.Model}".Trim();
        var cpuValue =
            $"{Html(cpuLine)}<br><span class=\"muted\">" +
            $"{Html(ReportStrings.CoresAndThreads(profile.Cpu.PhysicalCores, profile.Cpu.LogicalProcessors))}</span>";
        AppendKeyValueRow(sb, ReportStrings.LabelCpu, cpuValue, isPreEscaped: true);

        AppendKeyValueRow(
            sb, ReportStrings.LabelArchitecture,
            ReportStrings.ArchitectureAndFeatureLevel(profile.Cpu.Architecture, profile.Cpu.X86_64FeatureLevel));

        AppendKeyValueRow(sb, ReportStrings.LabelMemory, ReportStrings.MemorySize(profile.Memory.TotalBytes));

        if (profile.Storage.Disks.Count == 0)
        {
            AppendKeyValueRow(sb, ReportStrings.LabelDisks, ReportStrings.Unknown);
        }
        else
        {
            foreach (var disk in profile.Storage.Disks)
            {
                AppendKeyValueRow(sb, ReportStrings.LabelDisks, ReportStrings.DiskSummary(disk.Model, disk.SizeBytes, disk.MediaType));
            }
        }

        AppendKeyValueRow(sb, ReportStrings.LabelBootMode, ReportStrings.BootModeLabel(profile.Firmware.BootMode));
        AppendKeyValueRow(
            sb, ReportStrings.LabelSecureBoot,
            profile.Firmware.SecureBootEnabled switch { true => ReportStrings.Yes, false => ReportStrings.No, null => ReportStrings.Unknown });
        AppendKeyValueRow(sb, ReportStrings.LabelTpm, ReportStrings.TpmSummary(profile.Firmware.Tpm.Present, profile.Firmware.Tpm.SpecVersion));

        sb.Append("    </tbody>\n  </table>\n");

        // Only shown when something is actually missing for this reason — a report collected
        // elevated (where these fields are always populated) has nothing to explain here.
        if (profile.Firmware.Tpm.Present is null || profile.Storage.BitlockerAvailable is null)
        {
            sb.Append($"  <p class=\"muted\">{Html(ReportStrings.ElevationNote)}</p>\n");
        }

        sb.Append("</section>\n");
    }

    // ---- Block 11: next step — only when there is a next step worth taking --------------------

    private static void AppendNextSteps(StringBuilder sb, MachineProfile profile, AnalysisResult analysis)
    {
        // A Blocked machine has nothing to "try" yet — leading with "here's how to try it" would
        // contradict the verdict just shown. Every other verdict (including InsufficientData) can
        // still genuinely be tried from a USB drive.
        if (analysis.Verdict == Verdict.Blocked) return;

        sb.Append("<section class=\"card\">\n");
        sb.Append($"  <h2>{Html(ReportStrings.NextStepsSectionTitle)}</h2>\n");
        sb.Append($"  <p>{Html(ReportStrings.NextStepsIntro)}</p>\n");

        sb.Append("  <ol>\n");
        foreach (var step in ReportStrings.NextStepsList)
        {
            sb.Append($"    <li>{Html(step)}</li>\n");
        }
        sb.Append("  </ol>\n");

        if (analysis.SystemConstraints.Any(c => c.Kind == SystemConstraintKind.LowDiskSpace))
        {
            sb.Append($"  <p class=\"muted\">{Html(ReportStrings.NextStepsLowDiskSpaceWarning)}</p>\n");
        }

        if (HasBitLockerEnabledPartition(profile))
        {
            sb.Append($"  <p class=\"muted\">{Html(ReportStrings.NextStepsBitLockerWarning)}</p>\n");
        }

        sb.Append("</section>\n");
    }

    private static bool HasBitLockerEnabledPartition(MachineProfile profile) =>
        profile.Storage.Disks
            .SelectMany(d => d.Partitions)
            .Any(p => p.BitlockerStatus is BitLockerStatus.FullyEncrypted or BitLockerStatus.EncryptionInProgress);

    private static void AppendKeyValueRow(StringBuilder sb, string key, string value, bool isPreEscaped = false)
    {
        sb.Append("      <tr>");
        sb.Append($"<th>{Html(key)}</th>");
        sb.Append(isPreEscaped ? $"<td>{value}</td>" : $"<td>{Html(value)}</td>");
        sb.Append("</tr>\n");
    }

    /// <summary>
    /// Renders the "Linux Sürücüsü" cell: the entry's <c>display_name</c> when it has one (a
    /// plain-language label, shown as normal text — not styled as code, since it is prose, not a
    /// literal technical identifier), otherwise the raw <c>kernel_driver</c> module name in
    /// <c>&lt;code&gt;</c>.
    /// </summary>
    private static string DriverCell(CompatibilityEntry? entry)
    {
        if (entry?.DisplayName is not null)
        {
            return Html(entry.DisplayName);
        }

        return $"<code>{Html(entry?.KernelDriver ?? "—")}</code>";
    }

    /// <summary>
    /// Escapes the characters that are actually unsafe in this document: every attribute value
    /// here is double-quoted, so a bare apostrophe in text content (common in Turkish
    /// possessives, e.g. "Linux'a") needs no escaping and is left as-is for readability.
    /// Deliberately not <see cref="System.Net.WebUtility.HtmlEncode"/>: that method also turns
    /// every Latin-1-range letter (ç, ö, ü, ...) into a numeric character reference, which is
    /// still valid HTML but makes the page source needlessly unreadable — the document already
    /// declares UTF-8, so non-ASCII text (Turkish included) is safe to emit as literal bytes.
    /// </summary>
    private static string Html(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }
}
