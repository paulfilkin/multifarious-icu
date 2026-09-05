namespace Icu.Core.Tree;

/// <summary>
/// One node of a parsed ICU message. A message is an ordered sequence of these; the
/// concrete types are literal text, a quoted literal, a simple argument, a typed
/// argument, the '#' placeholder and the three selector arguments.
/// </summary>
public abstract class MessageNode
{
    protected MessageNode(SourceSpan span)
    {
        Span = span;
    }

    /// <summary>Where in the original message text this node came from.</summary>
    public SourceSpan Span { get; }
}
