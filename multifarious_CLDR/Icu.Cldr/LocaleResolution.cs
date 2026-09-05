namespace Icu.Cldr;

/// <summary>
/// How far down the fallback chain a language tag had to go to reach CLDR data.
/// </summary>
public enum LocaleMatch
{
    /// <summary>The whole tag is a CLDR key. <c>pt-PT</c> for cardinal, <c>ru</c> for a bare tag.</summary>
    Exact,

    /// <summary>The language and script matched, the region was dropped.</summary>
    LanguageAndScript,

    /// <summary>The language alone matched. <c>ru-RU</c> to <c>ru</c>, <c>zh-Hans-CN</c> to <c>zh</c>.</summary>
    Language,

    /// <summary>Nothing matched. The caller gets <c>other</c> only, and a warning.</summary>
    None
}

/// <summary>
/// The result of resolving one Trados language tag against one CLDR table.
/// </summary>
/// <remarks>
/// Resolution runs per selector kind, and the two kinds can land in different places
/// for the same tag: <c>pt-PT</c> is a cardinal key in its own right, and falls back to
/// <c>pt</c> for ordinals because the ordinal table does not list it. A resolution that
/// carried one answer for both would be wrong for one of them.
/// </remarks>
public sealed record LocaleResolution
{
    public LocaleResolution(
        string requestedTag,
        SelectorKind kind,
        string? resolvedKey,
        LocaleMatch match,
        PluralRuleSet? ruleSet,
        IReadOnlyList<PluralCategory> categories)
    {
        RequestedTag = requestedTag;
        Kind = kind;
        ResolvedKey = resolvedKey;
        Match = match;
        RuleSet = ruleSet;
        Categories = categories;
    }

    /// <summary>The tag as the platform supplied it.</summary>
    public string RequestedTag { get; }

    public SelectorKind Kind { get; }

    /// <summary>The CLDR key the tag resolved to, or null where nothing matched.</summary>
    public string? ResolvedKey { get; }

    public LocaleMatch Match { get; }

    /// <summary>The rules, or null where nothing matched.</summary>
    public PluralRuleSet? RuleSet { get; }

    /// <summary>
    /// The categories a message must be expanded to for this language. Where nothing
    /// matched this is <c>other</c> alone, which leaves the message with the single
    /// branch it already has rather than inventing forms for a language we know nothing
    /// about.
    /// </summary>
    public IReadOnlyList<PluralCategory> Categories { get; }

    /// <summary>
    /// True where no CLDR data was found and the categories are a safe default rather
    /// than an answer. The task reports this as a warning on its outcome.
    /// </summary>
    public bool IsFallback => Match == LocaleMatch.None;

    /// <summary>The warning text for the task outcome, or null where there is nothing to warn about.</summary>
    public string? Warning => IsFallback
        ? $"No CLDR {Kind.ToString().ToLowerInvariant()} plural data for '{RequestedTag}'. " +
          "Expanded to 'other' only."
        : null;

    public override string ToString() => IsFallback
        ? $"{RequestedTag} {Kind}: no data, other only"
        : $"{RequestedTag} {Kind}: {ResolvedKey} ({Match}) {string.Join(", ", Categories.Select(c => c.ToKeyword()))}";
}
