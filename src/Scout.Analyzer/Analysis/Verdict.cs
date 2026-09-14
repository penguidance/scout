namespace Scout.Analyzer.Analysis;

/// <summary>Overall Windows→Linux compatibility call for a machine, from <see cref="VerdictEngine"/>.</summary>
public enum Verdict
{
    /// <summary>Every relevant device is natively supported (or unknowns are a small minority).</summary>
    Ready,

    /// <summary>At least one device needs a separate firmware package, but nothing worse.</summary>
    MinorIssues,

    /// <summary>At least one device needs a proprietary driver or only has partial support.</summary>
    NeedsAttention,

    /// <summary>At least one relevant device is flatly unsupported.</summary>
    Blocked,

    /// <summary>Too little is known about this machine's devices to call it any of the above with confidence.</summary>
    InsufficientData
}
