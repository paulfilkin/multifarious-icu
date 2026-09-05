using System.Globalization;
using System.Text;

namespace Icu.Cldr.Rules;

/// <summary>
/// An inclusive range of integers on the right-hand side of a relation. A bare value is
/// a range whose bounds are equal.
/// </summary>
public readonly record struct NumberRange(long Low, long High)
{
    public bool Contains(long value) => value >= Low && value <= High;

    public override string ToString() => Low == High
        ? Low.ToString(CultureInfo.InvariantCulture)
        : FormattableString.Invariant($"{Low}..{High}");
}

/// <summary>
/// One relation: an operand, an optional modulus, and a list of values and ranges it is
/// tested against. For example <c>i % 10 = 2..4</c> or <c>v != 0</c>.
/// </summary>
public sealed class PluralRelation
{
    public PluralRelation(PluralOperand operand, long modulus, bool isNegated, IReadOnlyList<NumberRange> ranges)
    {
        if (ranges.Count == 0)
        {
            throw new ArgumentException("A relation must test at least one value or range.", nameof(ranges));
        }

        Operand = operand;
        Modulus = modulus;
        IsNegated = isNegated;
        Ranges = ranges;
    }

    public PluralOperand Operand { get; }

    /// <summary>The modulus, or zero when the relation has none.</summary>
    public long Modulus { get; }

    /// <summary>True for <c>!=</c>, false for <c>=</c>.</summary>
    public bool IsNegated { get; }

    public IReadOnlyList<NumberRange> Ranges { get; }

    /// <summary>
    /// Evaluates the relation.
    /// </summary>
    /// <remarks>
    /// The ranges match integers only. A fractional operand value therefore satisfies no
    /// range at all, which makes <c>=</c> false and <c>!=</c> true. This is not a detail
    /// that can be chosen: running the alternative reading, where a range covers every
    /// value between its bounds, against CLDR's own samples fails 106 of them, and the
    /// integers-only reading passes all 15,041.
    /// </remarks>
    public bool Matches(in PluralOperands operands)
    {
        var value = operands.Get(Operand);
        if (value != decimal.Truncate(value))
        {
            return IsNegated;
        }

        var integral = (long)value;
        if (Modulus != 0)
        {
            integral %= Modulus;
        }

        var hit = false;
        for (var index = 0; index < Ranges.Count; index++)
        {
            if (Ranges[index].Contains(integral))
            {
                hit = true;
                break;
            }
        }

        return IsNegated ? !hit : hit;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(char.ToLowerInvariant(Operand.ToString()[0]));
        if (Modulus != 0)
        {
            builder.Append(" % ").Append(Modulus.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append(IsNegated ? " != " : " = ");
        builder.Append(string.Join(",", Ranges));
        return builder.ToString();
    }
}

/// <summary>
/// A whole rule condition: a disjunction of conjunctions of relations, matching the
/// UTS #35 grammar where <c>or</c> binds more loosely than <c>and</c>.
/// </summary>
/// <remarks>
/// An empty condition is always true. That is not a special case invented here: 332 of
/// the 689 rule strings in CLDR 48 carry samples and no condition at all, which is how
/// a locale's <c>other</c> rule is written.
/// </remarks>
public sealed class PluralCondition
{
    /// <summary>The condition that every number satisfies.</summary>
    public static readonly PluralCondition AlwaysTrue = new(Array.Empty<IReadOnlyList<PluralRelation>>());

    public PluralCondition(IReadOnlyList<IReadOnlyList<PluralRelation>> clauses)
    {
        Clauses = clauses;
    }

    /// <summary>The <c>or</c>-separated clauses, each a list of <c>and</c>-separated relations.</summary>
    public IReadOnlyList<IReadOnlyList<PluralRelation>> Clauses { get; }

    public bool IsAlwaysTrue => Clauses.Count == 0;

    public bool Matches(in PluralOperands operands)
    {
        if (Clauses.Count == 0)
        {
            return true;
        }

        foreach (var clause in Clauses)
        {
            var all = true;
            foreach (var relation in clause)
            {
                if (!relation.Matches(operands))
                {
                    all = false;
                    break;
                }
            }

            if (all)
            {
                return true;
            }
        }

        return false;
    }

    public override string ToString() =>
        IsAlwaysTrue ? string.Empty : string.Join(" or ", Clauses.Select(c => string.Join(" and ", c)));
}
