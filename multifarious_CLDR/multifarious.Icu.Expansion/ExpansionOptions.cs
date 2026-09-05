namespace multifarious.Icu.Expansion;

/// <summary>How a target category with no matching source branch is seeded (design 5.4).</summary>
public enum SourceSeedStrategy
{
    /// <summary>The source branch with the same keyword where present, else the source 'other'.</summary>
    MatchingElseOther,

    /// <summary>Always the source 'other' branch, even where a matching keyword exists.</summary>
    AlwaysOther
}

/// <summary>What a parse failure does to the task (design 9.5).</summary>
public enum ParseErrorBehaviour
{
    PassThrough,
    FailTask
}

/// <summary>
/// The BCM construct carrying non-translatable ICU syntax (D11). T4 showed the
/// JSON filter's writer drops tags of every kind while both writers emit locked
/// text verbatim, so locked content is the default and placeholder tags remain
/// as the variant.
/// </summary>
public enum TagConstruct
{
    /// <summary>Locked text spans; the default (D11 as finally revised by T4).</summary>
    LockedContent,

    /// <summary>Placeholder tags with skeleton definitions; works for the properties writer, dropped by the JSON writer.</summary>
    PlaceholderTags
}

/// <summary>
/// The workflow template options of the expand extension (design section 9), with the
/// descriptor's defaults. One record, passed everywhere, so a task's configuration is
/// applied in exactly one shape.
/// </summary>
public sealed record ExpansionOptions
{
    public static ExpansionOptions Default { get; } = new();

    public bool ExpandCardinal { get; init; } = true;

    public bool ExpandOrdinal { get; init; } = true;

    public SourceSeedStrategy SourceSeedStrategy { get; init; } = SourceSeedStrategy.MatchingElseOther;

    public bool IncludeHints { get; init; } = true;

    /// <summary>
    /// The branch budget: the maximum number of segments one message may expand to.
    /// Over budget, the message passes through unexpanded and is flagged (design 5.7).
    /// </summary>
    public int MaxUnitsPerMessage { get; init; } = 24;

    public ParseErrorBehaviour OnParseError { get; init; } = ParseErrorBehaviour.PassThrough;

    public TagConstruct TagConstruct { get; init; } = TagConstruct.LockedContent;
}
