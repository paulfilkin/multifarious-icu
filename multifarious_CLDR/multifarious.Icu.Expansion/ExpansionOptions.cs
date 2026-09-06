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

    /// <summary>
    /// The branch budget: the maximum number of segments one message may expand to.
    /// Over budget, the message passes through unexpanded and is flagged (design 5.7).
    /// </summary>
    public int MaxUnitsPerMessage { get; init; } = 24;

    public ParseErrorBehaviour OnParseError { get; init; } = ParseErrorBehaviour.PassThrough;

    /// <summary>
    /// Whether each placeholder tag inside a segment (an argument or '#') is wrapped in locked
    /// content so it cannot be moved or deleted. Off by default: a bare tag is what Studio's
    /// QuickPlace, tag verification and translation memory placeables work with, and the
    /// verifiers catch a missing one. The selector syntax between segments is always locked.
    /// Studio's JSON writer emits no placeholder tag of any kind, so the finalise task turns
    /// the tags back into locked text before the target file is generated (Paul, 6 September
    /// 2026).
    /// </summary>
    public bool LockPlaceholders { get; init; } = false;
}
