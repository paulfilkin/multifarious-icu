namespace Icu.Cldr;

/// <summary>
/// Which of the two CLDR plural tables applies.
/// </summary>
/// <remarks>
/// The lookup keys on selector kind and locale, not locale alone, and the two tables
/// disagree in both directions: English has two cardinal categories and four ordinal
/// ones, Russian has four cardinal and one ordinal. They also cover different locale
/// sets, so resolution runs independently for each.
/// </remarks>
public enum SelectorKind
{
    /// <summary>The ICU <c>plural</c> selector. CLDR <c>plurals-type-cardinal</c>.</summary>
    Cardinal,

    /// <summary>The ICU <c>selectordinal</c> selector. CLDR <c>plurals-type-ordinal</c>.</summary>
    Ordinal
}
