using Scout.Collector.Cim;
using Xunit;

namespace Scout.Tests.Collectors;

/// <summary>
/// Regression tests for CimStringNormalizer's handling of '\0' — found via a real-machine run:
/// MSFT_Partition/MSFT_Volume report an unassigned DriveLetter as '\0', not a space, and
/// String.Trim() alone does not strip it, which the first implementation missed.
/// </summary>
public class CimStringNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\0")]
    [InlineData("  \0  ")]
    public void Normalize_BlankOrNulOnlyInput_ReturnsNull(string? raw)
    {
        Assert.Null(CimStringNormalizer.Normalize(raw));
    }

    [Fact]
    public void Normalize_TrimsSurroundingWhitespace()
    {
        Assert.Equal("Contoso", CimStringNormalizer.Normalize("  Contoso  "));
    }

    [Theory]
    [InlineData("To Be Filled By O.E.M.")]
    [InlineData("default string")]
    [InlineData("N/A")]
    [InlineData("Unknown")]
    public void Normalize_KnownOemPlaceholders_ReturnNull(string raw)
    {
        Assert.Null(CimStringNormalizer.Normalize(raw));
    }

    [Fact]
    public void Normalize_RealValue_IsPreservedAsIs()
    {
        Assert.Equal("C", CimStringNormalizer.Normalize("C"));
    }
}
