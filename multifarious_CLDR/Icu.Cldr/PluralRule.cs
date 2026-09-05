using Icu.Cldr.Rules;

namespace Icu.Cldr;

/// <summary>
/// One CLDR rule: the category it selects, the condition that selects it, and the
/// samples published with it.
/// </summary>
public sealed class PluralRule
{
    public PluralRule(PluralCategory category, PluralCondition condition, PluralSamples samples, string source)
    {
        Category = category;
        Condition = condition;
        Samples = samples;
        Source = source;
    }

    public PluralCategory Category { get; }

    public PluralCondition Condition { get; }

    public PluralSamples Samples { get; }

    /// <summary>The rule string as CLDR published it, kept for diagnostics.</summary>
    public string Source { get; }

    public bool Matches(in PluralOperands operands) => Condition.Matches(operands);

    public override string ToString() => $"{Category.ToKeyword()}: {Condition}";
}
