using Scout.Analyzer.Compatibility;
using Scout.Core.Models;

namespace Scout.Analyzer.Analysis;

/// <summary>One relevant device paired with its compatibility lookup result.</summary>
public sealed class DeviceAssessment
{
    public required DeviceInfo Device { get; init; }
    public required CompatibilityMatch Match { get; init; }
}
