using Icu.Cldr.Rules;

namespace Icu.Cldr;

/// <summary>
/// The ordered rules for one locale and one selector kind. This is the answer to
/// "which forms does this language need", and to "which form does this count take".
/// </summary>
/// <remarks>
/// Order is meaningful. The rules are evaluated in the order CLDR lists them and the
/// first match wins, so a value that satisfies two conditions belongs to the earlier
/// one. That is what the conformance suite means by a sample selecting "its own
/// category and no earlier one".
/// </remarks>
public sealed class PluralRuleSet
{
    public PluralRuleSet(string cldrKey, SelectorKind kind, IReadOnlyList<PluralRule> rules)
    {
        if (rules.Count == 0)
        {
            throw new CldrDataException($"'{cldrKey}' has no {kind} rules at all.");
        }

        if (rules[^1].Category != PluralCategory.Other)
        {
            throw new CldrDataException(
                $"'{cldrKey}' does not end its {kind} rules with 'other'. Selection relies on 'other' " +
                "being last and always matching.");
        }

        CldrKey = cldrKey;
        Kind = kind;
        Rules = rules;
        Categories = rules.Select(rule => rule.Category).ToList();
    }

    /// <summary>The CLDR key the rules were published under, such as <c>ru</c> or <c>pt-PT</c>.</summary>
    public string CldrKey { get; }

    public SelectorKind Kind { get; }

    public IReadOnlyList<PluralRule> Rules { get; }

    /// <summary>
    /// The categories this locale distinguishes, in CLDR's order. This is the exact set
    /// of keyword branches an expanded message must contain.
    /// </summary>
    public IReadOnlyList<PluralCategory> Categories { get; }

    public bool Contains(PluralCategory category) => Categories.Contains(category);

    /// <summary>Looks up the rule for one category, or null where the locale has none.</summary>
    public PluralRule? GetRule(PluralCategory category) =>
        Rules.FirstOrDefault(rule => rule.Category == category);

    /// <summary>
    /// Selects the category a number falls into. Never fails: <c>other</c> is always last
    /// and always matches.
    /// </summary>
    public PluralCategory Select(in PluralOperands operands)
    {
        foreach (var rule in Rules)
        {
            if (rule.Matches(operands))
            {
                return rule.Category;
            }
        }

        // Unreachable: the constructor rejects a rule set that does not end with 'other',
        // whose condition is empty and therefore always true.
        return PluralCategory.Other;
    }

    /// <summary>Selects the category for a number written as CLDR writes its samples.</summary>
    public PluralCategory Select(string number) => Select(PluralOperands.Parse(number));

    /// <summary>Selects the category for a plain integer count.</summary>
    public PluralCategory Select(long count) => Select(PluralOperands.FromInteger(count));

    public override string ToString() =>
        $"{CldrKey} {Kind}: {string.Join(", ", Categories.Select(c => c.ToKeyword()))}";
}
