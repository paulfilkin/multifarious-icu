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
/// The construct carrying non-translatable ICU syntax in the bilingual document. The
/// cloud design settled on locked content (its D11) because the JSON writer dropped
/// tags of every kind while both writers emit locked text verbatim. Studio evidence
/// (5 September 2026, Project 39) repeated the finding exactly: the target paragraph
/// carried every placeholder tag and the generated JSON carried none. The filters are
/// the same code in both products. Locked content is therefore the default here as
/// well, and placeholder tags remain the variant.
/// </summary>
public enum TagConstruct
{
    /// <summary>Locked text spans; the default. They carry no metadata, so the segment's comment is the machine-readable carrier.</summary>
    LockedContent,

    /// <summary>Placeholder tags carrying the branch path and role as metadata; dropped by the JSON writer.</summary>
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
    /// Whether each source segment gets a comment naming its form and the counts that select
    /// it. Off, the segments carry no comment marker at all; the layout and the unit context
    /// are the same, and the ICU Forms window reads those rather than the comment. Added in
    /// Studio (Paul, 6 September 2026): with the window open the comments can be noise.
    /// </summary>
    public bool WriteSegmentComments { get; init; } = true;

    /// <summary>
    /// The branch budget: the maximum number of segments one message may expand to.
    /// Over budget, the message passes through unexpanded and is flagged (design 5.7).
    /// </summary>
    public int MaxUnitsPerMessage { get; init; } = 24;

    public ParseErrorBehaviour OnParseError { get; init; } = ParseErrorBehaviour.PassThrough;

    public TagConstruct TagConstruct { get; init; } = TagConstruct.LockedContent;
}
