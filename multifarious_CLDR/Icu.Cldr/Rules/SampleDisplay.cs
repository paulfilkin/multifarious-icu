using System.Globalization;

namespace Icu.Cldr.Rules;

/// <summary>
/// The example counts a translator sees, converted from CLDR's written samples.
/// </summary>
/// <remarks>
/// CLDR writes compact samples such as <c>1c6</c>, meaning 1000000 written
/// compactly, and several languages reach a category through them. Shown raw the
/// notation means nothing to a translator, so examples are rendered at their
/// numeric value. The compact exponent is itself a plural operand, so a converted
/// value does not always select the category its sample did: Spanish <c>1.1c6</c>
/// is <c>many</c> where 1100000 is not. Such a sample is dropped rather than
/// shown, because an example that fails its own category misleads. Duplicates the
/// conversion creates (<c>1000000</c> and <c>1c6</c> are the same value) collapse
/// to one, and the samples keep CLDR's document order, small values first.
/// </remarks>
public static class SampleDisplay
{
    /// <summary>The displayable <c>@integer</c> examples for one category.</summary>
    public static IReadOnlyList<string> IntegerExamples(PluralRuleSet? ruleSet, PluralCategory category, int count) =>
        Examples(ruleSet, category, count, samples => samples.Integers);

    /// <summary>The displayable <c>@decimal</c> examples for one category.</summary>
    public static IReadOnlyList<string> DecimalExamples(PluralRuleSet? ruleSet, PluralCategory category, int count) =>
        Examples(ruleSet, category, count, samples => samples.Decimals);

    private static IReadOnlyList<string> Examples(
        PluralRuleSet? ruleSet,
        PluralCategory category,
        int count,
        Func<PluralSamples, PluralSampleList> side)
    {
        var samples = ruleSet?.GetRule(category)?.Samples;
        if (samples is null)
        {
            return [];
        }

        return side(samples).Expand()
            .Select(sample => PluralOperands.Parse(sample).N.ToString(CultureInfo.InvariantCulture))
            .Distinct(StringComparer.Ordinal)
            .Where(value => ruleSet!.Select(value) == category)
            .Take(count)
            .ToList();
    }
}
