using Icu.Cldr;
using Icu.Cldr.Rules;

namespace Icu.Cldr.Tests;

/// <summary>
/// The sample parser, which feeds both the conformance suite and the example counts
/// shown to the translator.
/// </summary>
public class SampleParsingTests
{
    [Fact]
    public void IntegerRangesExpandOneByOne()
    {
        var samples = PluralRuleParser.ParseSamples("@integer 2~4, 22~24, 62, …");

        Assert.Equal(new[] { "2", "3", "4", "22", "23", "24", "62" }, samples.Integers.Expand());
        Assert.True(samples.Integers.IsOpenEnded);
        Assert.True(samples.Decimals.IsEmpty);
    }

    /// <summary>
    /// The step comes from how the endpoints are written, so <c>0.0~1.5</c> is sixteen
    /// values and not two.
    /// </summary>
    [Fact]
    public void DecimalRangesStepByTheirWrittenPrecision()
    {
        var samples = PluralRuleParser.ParseSamples("@decimal 0.0~1.5, 10.0, …");

        Assert.Equal(
            new[] { "0.0", "0.1", "0.2", "0.3", "0.4", "0.5", "0.6", "0.7", "0.8", "0.9",
                    "1.0", "1.1", "1.2", "1.3", "1.4", "1.5", "10.0" },
            samples.Decimals.Expand());
    }

    [Fact]
    public void BothSampleListsAreReadWhenBothArePresent()
    {
        var samples = PluralRuleParser.ParseSamples("@integer 0, 5~7, … @decimal 0.5, 1.5, …");

        Assert.Equal(new[] { "0", "5", "6", "7" }, samples.Integers.Expand());
        Assert.Equal(new[] { "0.5", "1.5" }, samples.Decimals.Expand());
        Assert.False(samples.IsFractionalOnly);
    }

    /// <summary>
    /// Russian <c>other</c> is the case that matters: no integer reaches it, so the
    /// comment has to say "fractional counts only" rather than show an empty list.
    /// </summary>
    [Fact]
    public void RussianOtherIsReportedAsFractionalOnly()
    {
        var ruleSet = CldrPluralData.Embedded.GetExact("ru", SelectorKind.Cardinal);
        Assert.NotNull(ruleSet);

        var other = ruleSet.GetRule(PluralCategory.Other);
        Assert.NotNull(other);

        Assert.True(other.Samples.IsFractionalOnly);
        Assert.Empty(other.Samples.TakeIntegerExamples(6));
        Assert.NotEmpty(other.Samples.TakeDecimalExamples(6));
    }

    [Fact]
    public void ExampleCountsAreTakenInDocumentOrderAndCapped()
    {
        var ruleSet = CldrPluralData.Embedded.GetExact("ru", SelectorKind.Cardinal);
        Assert.NotNull(ruleSet);

        var few = ruleSet.GetRule(PluralCategory.Few);
        Assert.NotNull(few);

        Assert.Equal(new[] { "2", "3", "4", "22", "23", "24" }, few.Samples.TakeIntegerExamples(6));
    }

    [Fact]
    public void ARuleWithNoSamplesParsesToAnEmptySet()
    {
        var samples = PluralRuleParser.ParseSamples(string.Empty);

        Assert.True(samples.Integers.IsEmpty);
        Assert.True(samples.Decimals.IsEmpty);
        Assert.False(samples.IsFractionalOnly);
        Assert.Empty(samples.Expand());
    }

    /// <summary>
    /// No range in CLDR 48 has endpoints of differing precision, so there is no agreed
    /// step for one and guessing would silently produce the wrong sample set.
    /// </summary>
    [Fact]
    public void ARangeWhoseEndpointsDisagreeOnPrecisionIsRejected()
    {
        var exception = Assert.Throws<CldrDataException>(() => new PluralSampleRange("0.0", "1.50"));
        Assert.Contains("decimal places", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EverySampleInTheEmbeddedDataParsesAsANumber()
    {
        foreach (var kind in new[] { SelectorKind.Cardinal, SelectorKind.Ordinal })
        {
            foreach (var ruleSet in CldrPluralData.Embedded.All(kind))
            {
                foreach (var rule in ruleSet.Rules)
                {
                    foreach (var sample in rule.Samples.Expand())
                    {
                        Assert.True(
                            PluralOperands.TryParse(sample, out _),
                            $"{ruleSet.CldrKey} {kind} '{rule.Category.ToKeyword()}' sample '{sample}' does not parse.");
                    }
                }
            }
        }
    }
}
