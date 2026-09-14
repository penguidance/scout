namespace Scout.Analyzer.Compatibility;

/// <summary>Which kind of rule produced a <see cref="SoftwareMatch"/> — see <see cref="SoftwareMatcher"/>.</summary>
public enum SoftwareMatchLevel
{
    /// <summary>The program's full name matched a database entry's pattern exactly.</summary>
    Exact,

    /// <summary>The program's name started with a database entry's pattern.</summary>
    Prefix,

    /// <summary>A database entry's pattern appeared somewhere in the program's name.</summary>
    Contains,

    /// <summary>No entry matched — never guessed at.</summary>
    None
}
