using Icu.Cldr;
using Icu.Cldr.Rules;

namespace Icu.Cldr.Tests;

/// <summary>
/// The embedded data itself: that it is there, that it is the pinned release, and that
/// every rule in it parses.
/// </summary>
public class CldrDataTests
{
    /// <summary>
    /// The pinned release. Changing the embedded files without changing this is the
    /// mistake it exists to catch, because the version travels into paragraph unit
    /// metadata and the preview footer and has to be true.
    /// </summary>
    [Fact]
    public void TheEmbeddedDataIsThePinnedRelease()
    {
        Assert.Equal("48", CldrPluralData.Embedded.CldrVersion);
        Assert.Equal("16.0.0", CldrPluralData.Embedded.UnicodeVersion);
        Assert.Equal("CLDR 48 (Unicode 16.0.0)", CldrPluralData.Embedded.VersionDescription);
    }

    /// <summary>
    /// Loading parses all 689 rule strings eagerly, so this passing is the statement that
    /// the parser handles every construct in the pinned data.
    /// </summary>
    [Fact]
    public void EveryRuleStringInBothTablesParses()
    {
        var rules = 0;
        foreach (var kind in new[] { SelectorKind.Cardinal, SelectorKind.Ordinal })
        {
            foreach (var ruleSet in CldrPluralData.Embedded.All(kind))
            {
                foreach (var rule in ruleSet.Rules)
                {
                    Assert.NotNull(rule.Condition);
                    Assert.NotEmpty(rule.Source);
                    rules++;
                }
            }
        }

        Assert.Equal(689, rules);
    }

    /// <summary>
    /// 332 of the 689 rules are a bare <c>other</c> with samples and no condition. An
    /// empty condition matching nothing rather than everything would break every one of
    /// those locales.
    /// </summary>
    [Fact]
    public void AnEmptyConditionIsAlwaysTrue()
    {
        var condition = PluralRuleParser.ParseCondition("   ");

        Assert.True(condition.IsAlwaysTrue);
        Assert.True(condition.Matches(PluralOperands.Parse("0")));
        Assert.True(condition.Matches(PluralOperands.Parse("1.5")));
    }

    [Fact]
    public void ConditionsRoundTripThroughTheirTextForm()
    {
        var condition = PluralRuleParser.ParseCondition("v = 0 and i % 10 = 2..4 and i % 100 != 12..14");

        Assert.Equal("v = 0 and i % 10 = 2..4 and i % 100 != 12..14", condition.ToString());
    }

    [Fact]
    public void OrBindsMoreLooselyThanAnd()
    {
        var condition = PluralRuleParser.ParseCondition("v = 0 and i = 1 or n = 5");

        Assert.Equal(2, condition.Clauses.Count);
        Assert.Equal(2, condition.Clauses[0].Count);
        Assert.Single(condition.Clauses[1]);
    }

    /// <summary>
    /// The deprecated forms appear nowhere in the pinned data. They are rejected rather
    /// than half-implemented, so that a CLDR release which reintroduced one would fail
    /// here instead of being read with the wrong semantics.
    /// </summary>
    [Theory]
    [InlineData("n is 1")]
    [InlineData("n in 1..3")]
    [InlineData("n within 1..3")]
    [InlineData("n mod 10 = 1")]
    [InlineData("n not in 1..3")]
    public void DeprecatedRuleSyntaxIsRejected(string condition)
    {
        Assert.Throws<CldrDataException>(() => PluralRuleParser.ParseCondition(condition));
    }

    [Theory]
    [InlineData("n =")]
    [InlineData("= 1")]
    [InlineData("n = 1 and")]
    [InlineData("n % 0 = 1")]
    [InlineData("n = 4..2")]
    [InlineData("q = 1")]
    [InlineData("n ~ 1")]
    public void MalformedConditionsAreRejected(string condition)
    {
        Assert.Throws<CldrDataException>(() => PluralRuleParser.ParseCondition(condition));
    }

    /// <summary>
    /// Ranges match integers only, which is the semantics the CLDR samples themselves
    /// select: the alternative reading, where a range covers every value between its
    /// bounds, contradicts 106 of the published samples.
    /// </summary>
    [Fact]
    public void RangesMatchIntegersOnly()
    {
        var condition = PluralRuleParser.ParseCondition("n = 0..1");

        Assert.True(condition.Matches(PluralOperands.Parse("0")));
        Assert.True(condition.Matches(PluralOperands.Parse("1")));
        Assert.False(condition.Matches(PluralOperands.Parse("0.5")));
    }

    /// <summary>
    /// A fractional value satisfies no range, so negating the relation makes it true.
    /// </summary>
    [Fact]
    public void ANegatedRelationIsTrueForAFractionalValue()
    {
        var condition = PluralRuleParser.ParseCondition("n != 0..1");

        Assert.False(condition.Matches(PluralOperands.Parse("0")));
        Assert.True(condition.Matches(PluralOperands.Parse("0.5")));
        Assert.True(condition.Matches(PluralOperands.Parse("2")));
    }

    [Fact]
    public void LookupIsByExactKeyWithNoFallback()
    {
        Assert.NotNull(CldrPluralData.Embedded.GetExact("ru", SelectorKind.Cardinal));
        Assert.Null(CldrPluralData.Embedded.GetExact("ru-RU", SelectorKind.Cardinal));
        Assert.Null(CldrPluralData.Embedded.GetExact("pt-PT", SelectorKind.Ordinal));
        Assert.NotNull(CldrPluralData.Embedded.GetExact("pt-PT", SelectorKind.Cardinal));
    }
}
