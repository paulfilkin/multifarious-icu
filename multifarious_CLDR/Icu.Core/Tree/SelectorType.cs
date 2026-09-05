namespace Icu.Core.Tree;

/// <summary>
/// Which of the three ICU selector arguments a <see cref="SelectorNode"/> is. Plural
/// and selectordinal expand against CLDR category sets; select is walked but never
/// expanded, because its branches are the developer's, not the language's.
/// </summary>
public enum SelectorType
{
    Plural,
    SelectOrdinal,
    Select
}

public static class SelectorTypes
{
    /// <summary>The keyword ICU syntax uses for this selector type.</summary>
    public static string ToKeyword(this SelectorType type) => type switch
    {
        SelectorType.Plural => "plural",
        SelectorType.SelectOrdinal => "selectordinal",
        SelectorType.Select => "select",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
}
