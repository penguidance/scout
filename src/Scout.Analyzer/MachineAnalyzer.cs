using Scout.Analyzer.Analysis;
using Scout.Analyzer.Compatibility;
using Scout.Analyzer.Filtering;
using Scout.Analyzer.Recommendations;
using Scout.Core.Models;

namespace Scout.Analyzer;

/// <summary>
/// Entry point for turning a collected <see cref="MachineProfile"/> into an
/// <see cref="AnalysisResult"/>: filter out irrelevant/duplicate devices, look each remaining one
/// up in the compatibility database, then decide an overall verdict. Report formatting is not
/// part of this — that is a separate concern layered on top of <see cref="AnalysisResult"/>.
/// </summary>
public sealed class MachineAnalyzer
{
    private readonly CompatibilityMatcher _matcher;
    private readonly DeviceRelevanceFilter _filter;
    private readonly VerdictEngine _verdictEngine;
    private readonly SoftwareMatcher _softwareMatcher;
    private readonly DistributionRecommender _distributionRecommender;

    public MachineAnalyzer(
        CompatibilityMatcher matcher,
        DeviceRelevanceFilter? filter = null,
        VerdictEngine? verdictEngine = null,
        SoftwareMatcher? softwareMatcher = null,
        DistributionRecommender? distributionRecommender = null)
    {
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        _filter = filter ?? new DeviceRelevanceFilter();
        _verdictEngine = verdictEngine ?? new VerdictEngine();
        _softwareMatcher = softwareMatcher ?? new SoftwareMatcher(SoftwareCompatibilityDatabase.LoadEmbedded());
        _distributionRecommender = distributionRecommender ?? new DistributionRecommender(DistributionDatabase.LoadEmbedded());
    }

    public AnalysisResult Analyze(MachineProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var filterResult = _filter.Filter(profile.Devices);

        var assessments = filterResult.RelevantDevices
            .Select(device => new DeviceAssessment { Device = device, Match = _matcher.Match(device) })
            .ToList();

        var (softwareAssessments, softwareSkipped) = AssessSoftware(profile.Software, profile.Os.Language);

        var systemConstraints = _verdictEngine.EvaluateSystemConstraints(profile);
        var verdict = _verdictEngine.Decide(
            assessments.Select(a => a.Match).ToList(), systemConstraints, softwareAssessments);

        var distributionRecommendation = _distributionRecommender.Recommend(profile, assessments);

        return new AnalysisResult
        {
            Devices = assessments,
            Verdict = verdict,
            SystemConstraints = systemConstraints,
            DevicesRemovedForRelevance = filterResult.RemovedForRelevance,
            DevicesRemovedForDuplication = filterResult.RemovedForDuplication,
            SoftwareAssessments = softwareAssessments,
            SoftwareSkippedForCategory = softwareSkipped,
            DistributionRecommendation = distributionRecommendation
        };
    }

    /// <summary>
    /// Only <see cref="SoftwareCategory.application"/> entries are meaningful to look up — a
    /// bundled VC++ Redistributable or a display driver package has no standalone Linux-program
    /// identity, so those categories are never even attempted (not attempted-and-Unknown).
    /// </summary>
    /// <param name="profileLanguage">
    /// <c>profile.os.language</c>, forwarded to <see cref="SoftwareMatcher"/> purely as a
    /// tie-break preference between equally-good name aliases — never required, and every alias
    /// is still tried even when this is null (an unknown/missing language must never cause a real
    /// match to be skipped).
    /// </param>
    private (List<SoftwareAssessment> Assessments, int Skipped) AssessSoftware(
        IReadOnlyList<SoftwareEntry> software, string? profileLanguage)
    {
        var assessments = new List<SoftwareAssessment>();
        var skipped = 0;

        foreach (var entry in software)
        {
            if (entry.Category != SoftwareCategory.application)
            {
                skipped++;
                continue;
            }

            assessments.Add(new SoftwareAssessment { Software = entry, Match = _softwareMatcher.Match(entry, profileLanguage) });
        }

        return (assessments, skipped);
    }
}
