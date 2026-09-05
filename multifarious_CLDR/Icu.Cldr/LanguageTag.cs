namespace Icu.Cldr;

/// <summary>
/// The parts of a language tag this app needs: the language, the script and the region.
/// </summary>
/// <remarks>
/// This is not a general BCP 47 implementation and does not try to be. It exists to
/// drive the CLDR fallback chain, which needs the language alone and the language with
/// its script, and to tell a script apart from a region so that <c>pt-PT</c> is not
/// mistaken for a language and script pair. Anything beyond the region is kept only so
/// that the normalised tag can be compared for an exact match.
/// </remarks>
public readonly record struct LanguageTag
{
    private static readonly char[] Separators = ['-', '_'];

    private LanguageTag(string language, string? script, string? region, string normalised)
    {
        Language = language;
        Script = script;
        Region = region;
        Normalised = normalised;
    }

    /// <summary>The primary language subtag, lower case.</summary>
    public string Language { get; }

    /// <summary>The script subtag in title case, or null where the tag has none.</summary>
    public string? Script { get; }

    /// <summary>The region subtag in upper case, or null where the tag has none.</summary>
    public string? Region { get; }

    /// <summary>The whole tag with each subtag cased the way CLDR cases it.</summary>
    public string Normalised { get; }

    /// <summary>The language and script, or null where the tag has no script.</summary>
    public string? LanguageAndScript => Script is null ? null : $"{Language}-{Script}";

    /// <summary>
    /// Parses a language tag. Accepts the underscore separator as well as the hyphen,
    /// because Trados and Java-derived tooling both use it.
    /// </summary>
    public static bool TryParse(string? tag, out LanguageTag result)
    {
        result = default;
        if (tag is null || tag.Trim().Length == 0)
        {
            return false;
        }

        // The array has to be explicit. Split('-', '_', StringSplitOptions.RemoveEmptyEntries)
        // compiles, but binds to Split(char, int, StringSplitOptions) and silently takes
        // the underscore as a count of 95, leaving the underscore separator unhandled.
        var subtags = tag.Trim().Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (subtags.Length == 0 || !IsLanguageSubtag(subtags[0]))
        {
            return false;
        }

        var language = subtags[0].ToLowerInvariant();
        string? script = null;
        string? region = null;

        var index = 1;
        if (index < subtags.Length && IsScriptSubtag(subtags[index]))
        {
            script = char.ToUpperInvariant(subtags[index][0]) + subtags[index][1..].ToLowerInvariant();
            index++;
        }

        if (index < subtags.Length && IsRegionSubtag(subtags[index]))
        {
            region = subtags[index].ToUpperInvariant();
            index++;
        }

        var parts = new List<string> { language };
        if (script is not null)
        {
            parts.Add(script);
        }

        if (region is not null)
        {
            parts.Add(region);
        }

        // Whatever follows the region is a variant or an extension. It is preserved as
        // written so the normalised tag still round-trips, but it takes no part in the
        // fallback chain.
        for (; index < subtags.Length; index++)
        {
            parts.Add(subtags[index]);
        }

        result = new LanguageTag(language, script, region, string.Join("-", parts));
        return true;
    }

    public override string ToString() => Normalised;

    private static bool IsLanguageSubtag(string subtag) =>
        subtag.Length is >= 2 and <= 8 && subtag.All(Ascii.IsLetter);

    private static bool IsScriptSubtag(string subtag) =>
        subtag.Length == 4 && subtag.All(Ascii.IsLetter);

    private static bool IsRegionSubtag(string subtag) =>
        (subtag.Length == 2 && subtag.All(Ascii.IsLetter)) ||
        (subtag.Length == 3 && subtag.All(Ascii.IsDigit));
}
