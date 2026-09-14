using Scout.Analyzer.Compatibility;
using Scout.Core.Models;

namespace Scout.Analyzer.Analysis;

/// <summary>One assessed installed-software entry paired with its compatibility lookup result.</summary>
public sealed class SoftwareAssessment
{
    public required SoftwareEntry Software { get; init; }
    public required SoftwareMatch Match { get; init; }
}
