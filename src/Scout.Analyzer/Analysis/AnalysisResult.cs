using Scout.Analyzer.Recommendations;

namespace Scout.Analyzer.Analysis;

/// <summary>Full result of analyzing one machine profile — the engine's output, before any report formatting.</summary>
public sealed class AnalysisResult
{
    /// <summary>Per-device assessments, for the devices that survived <see cref="Filtering.DeviceRelevanceFilter"/>.</summary>
    public required IReadOnlyList<DeviceAssessment> Devices { get; init; }

    public required Verdict Verdict { get; init; }

    /// <summary>
    /// System-wide (not per-device) findings from <see cref="VerdictEngine.EvaluateSystemConstraints"/>
    /// — CPU baseline, boot mode, memory, disk space. Deliberately a separate list from
    /// <see cref="Devices"/> so a report never mixes "this piece of hardware needs work" with
    /// "moving this machine at all comes with limitations".
    /// </summary>
    public required IReadOnlyList<SystemConstraint> SystemConstraints { get; init; }

    public required int DevicesRemovedForRelevance { get; init; }

    public required int DevicesRemovedForDuplication { get; init; }

    /// <summary>
    /// Per-software assessments — inventory only, no Linux-equivalent mapping. Excludes
    /// <see cref="Core.Models.SoftwareCategory.runtime"/>/<see cref="Core.Models.SoftwareCategory.driver"/>/
    /// <see cref="Core.Models.SoftwareCategory.system"/> entries entirely (see
    /// <see cref="SoftwareSkippedForCategory"/>): those categories are meaningless as standalone
    /// Linux programs, so they were never looked up at all, not looked up and found unknown.
    /// Deliberately a separate list from <see cref="Devices"/> — a hardware finding and "you'd
    /// need a different program for this" are different kinds of problems.
    /// </summary>
    public required IReadOnlyList<SoftwareAssessment> SoftwareAssessments { get; init; }

    /// <summary>How many <see cref="Core.Models.MachineProfile.Software"/> entries were skipped because their category is not <see cref="Core.Models.SoftwareCategory.application"/> — see <see cref="SoftwareAssessments"/>.</summary>
    public required int SoftwareSkippedForCategory { get; init; }

    /// <summary>The single suggested distribution (plus one alternative) for this machine — see <see cref="Recommendations.DistributionRecommender"/>.</summary>
    public required DistributionRecommendation DistributionRecommendation { get; init; }
}
