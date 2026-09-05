using Icu.Core.Tree;

namespace multifarious.Icu.Expansion;

/// <summary>
/// The layout of one expanded message, decided in full before any BCM is touched.
/// The tree mirrors the hoisted message: selectors outside, one
/// <see cref="PlannedSegment"/> at every leaf. The writer executes this plan
/// mechanically; every decision - categories, seeding, comments, metadata - is
/// already in it.
/// </summary>
public sealed class ExpansionPlan
{
    public ExpansionPlan(
        string rawValue,
        string hoistedText,
        PlannedNode root,
        IReadOnlyList<PlannedSegment> segments,
        int maxUnitsPerMessage)
    {
        RawValue = rawValue;
        HoistedText = hoistedText;
        Root = root;
        Segments = segments;
        MaxUnitsPerMessage = maxUnitsPerMessage;
    }

    /// <summary>The original source value, for the paragraph unit's `icu:pattern`.</summary>
    public string RawValue { get; }

    /// <summary>The hoisted form, canonically serialised, for `icu:hoisted`.</summary>
    public string HoistedText { get; }

    public PlannedNode Root { get; }

    /// <summary>Every planned segment, in document order.</summary>
    public IReadOnlyList<PlannedSegment> Segments { get; }

    public int MaxUnitsPerMessage { get; }

    /// <summary>
    /// True where the plan is over the branch budget. The caller passes the message
    /// through unexpanded and flags it (design 5.7); the plan is still returned so the
    /// flag can say how large it would have been.
    /// </summary>
    public bool ExceedsBudget => Segments.Count > MaxUnitsPerMessage;
}

public abstract class PlannedNode
{
}

/// <summary>A selector in the output message, expanded or merely walked.</summary>
public sealed class PlannedSelector : PlannedNode
{
    public PlannedSelector(
        SelectorType type,
        string argumentName,
        string? offsetRaw,
        bool isExpanded,
        string path,
        IReadOnlyList<PlannedBranch> branches)
    {
        Type = type;
        ArgumentName = argumentName;
        OffsetRaw = offsetRaw;
        IsExpanded = isExpanded;
        Path = path;
        Branches = branches;
    }

    /// <summary>The argument's position in the path tree, such as <c>count</c> or <c>gender:female/count</c>; carried on the selector's open and close tags.</summary>
    public string Path { get; }

    public SelectorType Type { get; }

    public string ArgumentName { get; }

    /// <summary>The offset exactly as the source wrote it, or null where absent.</summary>
    public string? OffsetRaw { get; }

    /// <summary>
    /// True for an expanded plural kind; false for a select, or a plural kind whose
    /// expansion is disabled, whose branches are the source's, walked untouched.
    /// </summary>
    public bool IsExpanded { get; }

    public IReadOnlyList<PlannedBranch> Branches { get; }
}

public sealed class PlannedBranch
{
    public PlannedBranch(
        string keyText, bool isExplicit, string? seededFrom, bool syntheticSource, string path, PlannedNode content)
    {
        KeyText = keyText;
        IsExplicit = isExplicit;
        SeededFrom = seededFrom;
        SyntheticSource = syntheticSource;
        Path = path;
        Content = content;
    }

    /// <summary>The branch path, such as <c>count:few</c>; carried on the branch's open and close tags.</summary>
    public string Path { get; }

    /// <summary>The branch key as it will be written: a category keyword, a select key, or an explicit value such as <c>=0</c>.</summary>
    public string KeyText { get; }

    public bool IsExplicit { get; }

    /// <summary>The source branch keyword this branch's content was taken from, or null where the branch is its own source.</summary>
    public string? SeededFrom { get; }

    /// <summary>True where the source message had no branch with this keyword at all.</summary>
    public bool SyntheticSource { get; }

    public PlannedNode Content { get; }
}

/// <summary>
/// One translatable segment: a whole sentence for one branch path, with its comment
/// and machine metadata already composed (design sections 5.2 and 6).
/// </summary>
public sealed class PlannedSegment : PlannedNode
{
    public PlannedSegment(
        string path,
        IReadOnlyList<MessageNode> nodes,
        string comment,
        IReadOnlyDictionary<string, string> metadata)
    {
        Path = path;
        Nodes = nodes;
        Comment = comment;
        Metadata = metadata;
    }

    /// <summary>The branch path, such as <c>count:few</c> or <c>gender:female/count:many</c>.</summary>
    public string Path { get; }

    /// <summary>
    /// The sentence as Icu.Core nodes. Text and quoted-text nodes carry decoded
    /// values, which is the unescaping of design 5.8 already applied; arguments and
    /// '#' become placeholder tags in the layout.
    /// </summary>
    public IReadOnlyList<MessageNode> Nodes { get; }

    /// <summary>The rendered translator-visible comment (design section 6).</summary>
    public string Comment { get; }

    /// <summary>The machine-readable equivalent, keyed `icu:*` (design section 6).</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
