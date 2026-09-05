namespace Icu.Cldr;

/// <summary>
/// A CLDR plural category, and the keyword an ICU branch uses to name it.
/// </summary>
/// <remarks>
/// The declared order is the order CLDR itself lists rules in, which is also the order
/// they are evaluated in: the first rule whose condition matches wins, and
/// <see cref="Other"/> is always last and always matches. Every locale in both tables
/// was checked to be in this order, so the order is a property of the data and not an
/// assumption imposed on it.
/// </remarks>
public enum PluralCategory
{
    Zero,
    One,
    Two,
    Few,
    Many,
    Other
}

/// <summary>
/// Conversions between <see cref="PluralCategory"/> and the CLDR keyword.
/// </summary>
public static class PluralCategories
{
    /// <summary>Every category, in CLDR's own evaluation order.</summary>
    public static readonly IReadOnlyList<PluralCategory> All = new[]
    {
        PluralCategory.Zero,
        PluralCategory.One,
        PluralCategory.Two,
        PluralCategory.Few,
        PluralCategory.Many,
        PluralCategory.Other
    };

    /// <summary>
    /// The CLDR keyword for a category, which is also the keyword an ICU branch uses.
    /// </summary>
    public static string ToKeyword(this PluralCategory category) => category switch
    {
        PluralCategory.Zero => "zero",
        PluralCategory.One => "one",
        PluralCategory.Two => "two",
        PluralCategory.Few => "few",
        PluralCategory.Many => "many",
        PluralCategory.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Not a CLDR plural category.")
    };

    /// <summary>
    /// Parses a CLDR keyword. Case-sensitive, because CLDR keywords are lower case and
    /// an ICU branch keyword that differs in case is a different branch.
    /// </summary>
    public static bool TryParse(string keyword, out PluralCategory category)
    {
        switch (keyword)
        {
            case "zero": category = PluralCategory.Zero; return true;
            case "one": category = PluralCategory.One; return true;
            case "two": category = PluralCategory.Two; return true;
            case "few": category = PluralCategory.Few; return true;
            case "many": category = PluralCategory.Many; return true;
            case "other": category = PluralCategory.Other; return true;
            default: category = default; return false;
        }
    }
}
