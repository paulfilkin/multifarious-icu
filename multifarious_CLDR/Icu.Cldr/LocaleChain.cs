namespace Icu.Cldr;

/// <summary>
/// The CLDR keys a language tag is tried against, most specific first: the whole tag,
/// the language with its script, the language alone. This is the chain
/// <see cref="CldrPlurals"/> walks for the plural tables, lifted out so that any other
/// per-locale data, the grammar dictionary first, resolves a tag the same way.
/// </summary>
/// <remarks>
/// Added for Studio (13 October 2026); the reference project has no second per-locale
/// table. <see cref="CldrPlurals"/> keeps its own walk unchanged, so that file stays a
/// copy of the reference's.
/// </remarks>
public static class LocaleChain
{
    /// <summary>
    /// The candidate keys for a tag, most specific first, after the legacy code
    /// replacement. Empty where the tag does not parse.
    /// </summary>
    public static IReadOnlyList<string> Candidates(string? languageTag)
    {
        if (!LanguageTag.TryParse(languageTag, out var tag))
        {
            return Array.Empty<string>();
        }

        if (CldrPlurals.LegacyLanguageCodes.TryGetValue(tag.Language, out var current))
        {
            var replaced = current + tag.Normalised[tag.Language.Length..];
            if (!LanguageTag.TryParse(replaced, out tag))
            {
                return Array.Empty<string>();
            }
        }

        var candidates = new List<string>(3) { tag.Normalised };
        if (tag.LanguageAndScript is { } scripted && scripted != tag.Normalised)
        {
            candidates.Add(scripted);
        }

        if (tag.Language != tag.Normalised)
        {
            candidates.Add(tag.Language);
        }

        return candidates;
    }

    /// <summary>The first candidate the predicate accepts, or null where none is.</summary>
    public static string? FirstMatch(string? languageTag, Func<string, bool> hasKey)
    {
        if (hasKey is null) throw new ArgumentNullException(nameof(hasKey));

        foreach (var candidate in Candidates(languageTag))
        {
            if (hasKey(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
