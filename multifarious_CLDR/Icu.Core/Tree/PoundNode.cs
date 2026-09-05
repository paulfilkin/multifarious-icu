namespace Icu.Core.Tree;

/// <summary>
/// The <c>#</c> placeholder. It renders as the selector's number minus its offset, and
/// it is only ever produced inside a plural or selectordinal branch, because that is
/// the only place ICU treats the character as syntax; elsewhere '#' is plain text.
/// </summary>
public sealed class PoundNode : MessageNode
{
    public PoundNode(SourceSpan span)
        : base(span)
    {
    }

    public override string ToString() => "#";
}
