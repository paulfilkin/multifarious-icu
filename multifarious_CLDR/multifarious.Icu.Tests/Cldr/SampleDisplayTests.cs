using Icu.Cldr.Rules;

namespace Icu.Cldr.Tests;

/// <summary>
/// The translator-facing example conversion: compact samples render at their
/// numeric value, duplicates collapse, plain samples pass through unchanged, and a
/// converted value that no longer selects its own category is dropped.
/// </summary>
public class SampleDisplayTests
{
    private static PluralRuleSet Cardinal(string tag) =>
        CldrPlurals.Default.GetRuleSet(tag, SelectorKind.Cardinal)!;

    [Fact]
    public void CompactIntegerSamplesRenderAtTheirNumericValue()
    {
        // Spanish 'many' lists 1000000, 1c6, 2c6, ... : 1c6 converts to 1000000 and
        // collapses into the plain sample ahead of it.
        Assert.Equal(
            ["1000000", "2000000", "3000000", "4000000", "5000000", "6000000"],
            SampleDisplay.IntegerExamples(Cardinal("es"), PluralCategory.Many, 6));
    }

    [Fact]
    public void ACompactDecimalSampleThatLeavesItsCategoryIsDropped()
    {
        // Every 'many' decimal sample is compact, and each numeric value (1100000,
        // 1000000.1, ...) selects 'other' instead: the exponent operand does not
        // survive conversion, so no displayable decimal example exists.
        Assert.Empty(SampleDisplay.DecimalExamples(Cardinal("es"), PluralCategory.Many, 6));
    }

    [Fact]
    public void PlainSamplesPassThroughUnchanged()
    {
        Assert.Equal(
            ["2", "3", "4", "22", "23", "24"],
            SampleDisplay.IntegerExamples(Cardinal("ru"), PluralCategory.Few, 6));
    }

    [Fact]
    public void AWrittenFractionKeepsItsTrailingZeros()
    {
        // Russian 'other' is reached only through visible fraction digits, and 10.0
        // must stay 10.0: rewritten as 10 it would select 'many'.
        var decimals = SampleDisplay.DecimalExamples(Cardinal("ru"), PluralCategory.Other, 6);

        Assert.NotEmpty(decimals);
        Assert.All(decimals, value => Assert.Contains(".", value));
    }

    [Fact]
    public void AMissingRuleSetYieldsNoExamples()
    {
        Assert.Empty(SampleDisplay.IntegerExamples(null, PluralCategory.Other, 6));
    }
}
