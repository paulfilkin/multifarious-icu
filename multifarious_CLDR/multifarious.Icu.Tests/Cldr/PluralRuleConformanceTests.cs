using Icu.Cldr;
using Icu.Cldr.Rules;

namespace Icu.Cldr.Tests;

/// <summary>
/// The conformance suite, generated from the CLDR data rather than written by hand.
/// </summary>
/// <remarks>
/// <para>
/// Every rule string carries the sample values that belong to it. Those samples are a
/// specification of the rule's behaviour written by the people who wrote the rule, and
/// they cover the edge cases a hand-written test would not think of: the values either
/// side of a modulus boundary, the fractional values that reach a category no integer
/// reaches, the compact-exponent forms. Asserting that each sample selects the category
/// it is listed under, across every locale in both tables, tests the operand
/// computation, the rule parser, the evaluator and the ordering all at once, against an
/// oracle nobody here wrote.
/// </para>
/// <para>
/// One test case per locale and kind, rather than one per sample: 332 cases is a
/// readable run, and a failure names its locale and lists every sample that missed.
/// </para>
/// </remarks>
public class PluralRuleConformanceTests
{
    /// <summary>
    /// The number of sample values in CLDR 48 with every range expanded. Asserted so that
    /// a change to the pinned data, or a sample parser that quietly started dropping
    /// values, shows up as a failure rather than as a suite that still passes while
    /// testing less.
    /// </summary>
    private const int ExpectedSampleCount = 15041;

    public static TheoryData<SelectorKind, string> Locales { get; } = BuildLocales();

    [Theory]
    [MemberData(nameof(Locales))]
    public void EverySampleSelectsTheCategoryItIsListedUnder(SelectorKind kind, string cldrKey)
    {
        var ruleSet = CldrPluralData.Embedded.GetExact(cldrKey, kind);
        Assert.NotNull(ruleSet);

        var failures = new List<string>();

        foreach (var rule in ruleSet.Rules)
        {
            foreach (var sample in rule.Samples.Expand())
            {
                if (!PluralOperands.TryParse(sample, out var operands))
                {
                    failures.Add($"sample '{sample}' under '{rule.Category.ToKeyword()}' does not parse");
                    continue;
                }

                var selected = ruleSet.Select(operands);
                if (selected != rule.Category)
                {
                    failures.Add(
                        $"sample '{sample}' is listed under '{rule.Category.ToKeyword()}' " +
                        $"but selects '{selected.ToKeyword()}'");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            $"{cldrKey} {kind} has {failures.Count} sample(s) that select the wrong category:" +
            Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", failures));
    }

    [Fact]
    public void TheSuiteCoversTheExpectedNumberOfSamples()
    {
        var count = 0;
        foreach (var kind in new[] { SelectorKind.Cardinal, SelectorKind.Ordinal })
        {
            foreach (var ruleSet in CldrPluralData.Embedded.All(kind))
            {
                foreach (var rule in ruleSet.Rules)
                {
                    count += rule.Samples.Expand().Count();
                }
            }
        }

        Assert.True(
            count == ExpectedSampleCount,
            $"The conformance suite ran {count} samples, not the {ExpectedSampleCount} pinned for CLDR " +
            $"{CldrPluralData.Embedded.CldrVersion}. If the embedded data was updated, confirm the new " +
            "count is right and change the constant; if it was not, the sample parser has started " +
            "dropping or inventing values.");
    }

    /// <summary>
    /// Every locale must distinguish at least one category and end with <c>other</c>.
    /// The rule set constructor enforces the second; this proves it holds for all the
    /// real data rather than only where a test happens to look.
    /// </summary>
    [Theory]
    [MemberData(nameof(Locales))]
    public void EveryLocaleEndsWithOther(SelectorKind kind, string cldrKey)
    {
        var ruleSet = CldrPluralData.Embedded.GetExact(cldrKey, kind);

        Assert.NotNull(ruleSet);
        Assert.NotEmpty(ruleSet.Categories);
        Assert.Equal(PluralCategory.Other, ruleSet.Categories[^1]);
        Assert.Equal(ruleSet.Categories.Count, ruleSet.Categories.Distinct().Count());
    }

    /// <summary>
    /// Rules are listed, and therefore evaluated, in the canonical category order. The
    /// evaluator relies on it, so it is asserted rather than assumed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Locales))]
    public void RulesAreInCanonicalCategoryOrder(SelectorKind kind, string cldrKey)
    {
        var ruleSet = CldrPluralData.Embedded.GetExact(cldrKey, kind);
        Assert.NotNull(ruleSet);

        var canonical = PluralCategories.All.Where(ruleSet.Categories.Contains).ToList();
        Assert.Equal(canonical, ruleSet.Categories);
    }

    private static TheoryData<SelectorKind, string> BuildLocales()
    {
        var data = new TheoryData<SelectorKind, string>();
        foreach (var kind in new[] { SelectorKind.Cardinal, SelectorKind.Ordinal })
        {
            foreach (var key in CldrPluralData.Embedded.Keys(kind))
            {
                data.Add(kind, key);
            }
        }

        return data;
    }
}
