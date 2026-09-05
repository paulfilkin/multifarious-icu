using Icu.Cldr.Rules;

namespace Icu.Cldr;

/// <summary>
/// The entry point to the CLDR layer: give it a Trados language tag and a selector
/// kind, and it answers which plural forms that language needs.
/// </summary>
public sealed class CldrPlurals
{
    /// <summary>
    /// Language codes that were renamed and whose old form is still in circulation.
    /// Applied before lookup, because CLDR publishes only the current code.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> LegacyLanguageCodes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["iw"] = "he",
            ["in"] = "id",
            ["ji"] = "yi"
        };

    /// <summary>
    /// What a locale with no CLDR data expands to. One category, so a message keeps the
    /// single branch it has rather than gaining forms invented for it.
    /// </summary>
    private static readonly IReadOnlyList<PluralCategory> OtherOnly = new[] { PluralCategory.Other };

    private static readonly Lazy<CldrPlurals> LazyDefault =
        new(() => new CldrPlurals(CldrPluralData.Embedded), isThreadSafe: true);

    private readonly CldrPluralData _data;

    public CldrPlurals(CldrPluralData data)
    {
        _data = data;
    }

    /// <summary>The instance over the pinned, embedded data.</summary>
    public static CldrPlurals Default => LazyDefault.Value;

    /// <summary>The pinned version, for metadata, the documentation endpoint and the preview footer.</summary>
    public string VersionDescription => _data.VersionDescription;

    public string CldrVersion => _data.CldrVersion;

    public string UnicodeVersion => _data.UnicodeVersion;

    /// <summary>
    /// Resolves one language tag against one CLDR table and reports both the categories
    /// and how they were arrived at.
    /// </summary>
    public LocaleResolution Resolve(string languageTag, SelectorKind kind)
    {
        if (languageTag is null) throw new ArgumentNullException(nameof(languageTag));

        var (ruleSet, key, match) = Lookup(languageTag, kind);

        return ruleSet is null
            ? new LocaleResolution(languageTag, kind, null, LocaleMatch.None, null, OtherOnly)
            : new LocaleResolution(languageTag, kind, key, match, ruleSet, ruleSet.Categories);
    }

    /// <summary>
    /// The categories one language needs for one selector kind. Shorthand for
    /// <see cref="Resolve"/> where the resolution detail is not wanted.
    /// </summary>
    public IReadOnlyList<PluralCategory> Categories(string languageTag, SelectorKind kind) =>
        Resolve(languageTag, kind).Categories;

    /// <summary>
    /// The union of the categories several languages need, in CLDR's order.
    /// </summary>
    /// <remarks>
    /// This is what an expansion at <c>bcmSource</c> targets. That task runs once per
    /// source file, before the project fans out into its target languages, so it cannot
    /// expand to one language's set. It expands to the union and the finalise task, which
    /// does run per language, prunes back to the exact set. A project with one target
    /// language therefore expands exactly and wastes nothing, which is the recommended
    /// shape; a mixed project over-expands and the extra branches are dropped later.
    /// </remarks>
    public IReadOnlyList<PluralCategory> UnionOfCategories(IEnumerable<string> languageTags, SelectorKind kind)
    {
        if (languageTags is null) throw new ArgumentNullException(nameof(languageTags));

        var union = new HashSet<PluralCategory>();
        foreach (var tag in languageTags)
        {
            foreach (var category in Categories(tag, kind))
            {
                union.Add(category);
            }
        }

        if (union.Count == 0)
        {
            return OtherOnly;
        }

        // 'other' is always present, because every locale has it and the fallback is it.
        return PluralCategories.All.Where(union.Contains).ToList();
    }

    /// <summary>
    /// The rules for a language, after fallback, or null where there are none.
    /// </summary>
    public PluralRuleSet? GetRuleSet(string languageTag, SelectorKind kind) => Lookup(languageTag, kind).RuleSet;

    /// <summary>
    /// Selects the category a count falls into for one language, which is what the
    /// preview's count box reports.
    /// </summary>
    public PluralCategory Select(string languageTag, SelectorKind kind, in PluralOperands operands) =>
        GetRuleSet(languageTag, kind)?.Select(operands) ?? PluralCategory.Other;

    /// <summary>Selects the category for a count written as a number.</summary>
    public PluralCategory Select(string languageTag, SelectorKind kind, string count) =>
        Select(languageTag, kind, PluralOperands.Parse(count));

    private (PluralRuleSet? RuleSet, string? Key, LocaleMatch Match) Lookup(string languageTag, SelectorKind kind)
    {
        if (!LanguageTag.TryParse(languageTag, out var tag))
        {
            return (null, null, LocaleMatch.None);
        }

        // The legacy code is replaced before any lookup, so the whole chain runs against
        // the current code. Rebuilding the tag rather than only the first step matters
        // for something like iw-IL, which has to reach he and not stop at iw-IL.
        if (LegacyLanguageCodes.TryGetValue(tag.Language, out var current))
        {
            var replaced = current + tag.Normalised[tag.Language.Length..];
            if (!LanguageTag.TryParse(replaced, out tag))
            {
                return (null, null, LocaleMatch.None);
            }
        }

        var exact = _data.GetExact(tag.Normalised, kind);
        if (exact is not null)
        {
            return (exact, tag.Normalised, LocaleMatch.Exact);
        }

        var languageAndScript = tag.LanguageAndScript;
        if (languageAndScript is not null)
        {
            var scripted = _data.GetExact(languageAndScript, kind);
            if (scripted is not null)
            {
                return (scripted, languageAndScript, LocaleMatch.LanguageAndScript);
            }
        }

        var language = _data.GetExact(tag.Language, kind);
        return language is not null
            ? (language, tag.Language, LocaleMatch.Language)
            : (null, null, LocaleMatch.None);
    }
}
