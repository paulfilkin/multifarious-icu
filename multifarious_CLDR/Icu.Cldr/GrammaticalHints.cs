using Newtonsoft.Json.Linq;

namespace Icu.Cldr;

/// <summary>
/// The curated grammatical notes shown on the <c>Grammar:</c> line of an expanded
/// segment's comment.
/// </summary>
/// <remarks>
/// <para>
/// This is the one part of the CLDR layer that is not derived from CLDR. A plural
/// category is a selector; it says which numbers take a form, not what the form has to
/// do grammatically. "Genitive plural agreement" is a linguist's statement about
/// Russian, not something that can be read out of a rule string.
/// </para>
/// <para>
/// The content is therefore editorial and the shipped file is a stub pending review.
/// Runtime generation was considered and rejected for v1: a note that varies between
/// runs is worse than no note, because the translator cannot tell which of the two they
/// are reading.
/// </para>
/// </remarks>
public sealed class GrammaticalHints
{
    private const string ResourceName = "Icu.Cldr.Data.grammatical-hints.json";

    private static readonly Lazy<GrammaticalHints> LazyEmbedded = new(LoadEmbedded, isThreadSafe: true);

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<PluralCategory, string>> _hints;

    private GrammaticalHints(IReadOnlyDictionary<string, IReadOnlyDictionary<PluralCategory, string>> hints)
    {
        _hints = hints;
    }

    /// <summary>The curated hints embedded in this assembly.</summary>
    public static GrammaticalHints Embedded => LazyEmbedded.Value;

    /// <summary>An empty set, for when the <c>includeHints</c> option is off.</summary>
    public static GrammaticalHints None { get; } =
        new(new Dictionary<string, IReadOnlyDictionary<PluralCategory, string>>(StringComparer.Ordinal));

    /// <summary>The locale keys that have at least one hint.</summary>
    public IReadOnlyCollection<string> Keys => _hints.Keys.ToList();

    /// <summary>
    /// Looks up the hint for a locale and category, trying the resolved CLDR key first
    /// and then the bare language, so an entry under <c>pt</c> covers <c>pt-PT</c> unless
    /// <c>pt-PT</c> has its own.
    /// </summary>
    public string? Find(string? cldrKey, PluralCategory category)
    {
        if (cldrKey is null || cldrKey.Length == 0)
        {
            return null;
        }

        if (_hints.TryGetValue(cldrKey, out var byCategory) && byCategory.TryGetValue(category, out var hint))
        {
            return hint;
        }

        var separator = cldrKey.IndexOf('-');
        if (separator > 0 &&
            _hints.TryGetValue(cldrKey[..separator], out var byLanguage) &&
            byLanguage.TryGetValue(category, out var languageHint))
        {
            return languageHint;
        }

        return null;
    }

    private static GrammaticalHints LoadEmbedded()
    {
        var document = CldrPluralData.ReadEmbeddedObject(typeof(GrammaticalHints), ResourceName);

        var hints = new Dictionary<string, IReadOnlyDictionary<PluralCategory, string>>(StringComparer.Ordinal);
        if (document["hints"] is not JObject root)
        {
            throw new CldrDataException($"'{ResourceName}' has no 'hints' object.");
        }

        foreach (var locale in root.Properties())
        {
            if (locale.Value is not JObject entries)
            {
                throw new CldrDataException($"'{ResourceName}' has a non-object entry for '{locale.Name}'.");
            }

            var byCategory = new Dictionary<PluralCategory, string>();
            foreach (var entry in entries.Properties())
            {
                if (!PluralCategories.TryParse(entry.Name, out var category))
                {
                    throw new CldrDataException(
                        $"'{ResourceName}' gives a hint for '{locale.Name}' under '{entry.Name}', " +
                        "which is not a CLDR plural category.");
                }

                var text = entry.Value is JValue { Type: JTokenType.String } value ? (string?)value.Value : null;
                if (text is null || text.Trim().Length == 0)
                {
                    throw new CldrDataException(
                        $"'{ResourceName}' has an empty hint for '{locale.Name}' / '{entry.Name}'.");
                }

                byCategory.Add(category, text!);
            }

            hints.Add(locale.Name, byCategory);
        }

        return new GrammaticalHints(hints);
    }
}
