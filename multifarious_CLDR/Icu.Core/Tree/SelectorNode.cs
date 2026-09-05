namespace Icu.Core.Tree;

/// <summary>
/// A <c>plural</c>, <c>selectordinal</c> or <c>select</c> argument: the argument name,
/// the offset where one was written, and the ordered branches. One node type for all
/// three, because expansion, hoisting and serialisation treat them almost identically
/// and the differences key cleanly off <see cref="Type"/>.
/// </summary>
public sealed class SelectorNode : MessageNode
{
    public SelectorNode(
        SelectorType type,
        string argumentName,
        string? offsetRaw,
        decimal offset,
        IReadOnlyList<Branch> branches,
        SourceSpan span)
        : base(span)
    {
        if (argumentName is null) throw new ArgumentNullException(nameof(argumentName));
        if (branches is null) throw new ArgumentNullException(nameof(branches));
        Type = type;
        ArgumentName = argumentName;
        OffsetRaw = offsetRaw;
        Offset = offset;
        Branches = branches;
    }

    public SelectorType Type { get; }

    public string ArgumentName { get; }

    /// <summary>The offset exactly as written after <c>offset:</c>, or null where absent.</summary>
    public string? OffsetRaw { get; }

    /// <summary>
    /// The parsed offset, 0 where none was written. Keyword branches evaluate on the
    /// number minus this; explicit value branches match the raw number regardless.
    /// </summary>
    public decimal Offset { get; }

    public bool HasOffset => OffsetRaw is not null;

    public IReadOnlyList<Branch> Branches { get; }

    /// <summary>True for plural and selectordinal, the two kinds '#' binds to and expansion applies to.</summary>
    public bool IsPluralKind => Type != SelectorType.Select;

    public override string ToString() =>
        $"{{{ArgumentName}, {Type.ToKeyword()}: {string.Join(" ", Branches.Select(b => b.Key.Text))}}}";
}
