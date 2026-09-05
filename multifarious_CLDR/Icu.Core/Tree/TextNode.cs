namespace Icu.Core.Tree;

/// <summary>
/// A run of literal text containing no escape constructs: the value is exactly the
/// source slice. An apostrophe that neither doubles nor opens a quote is literal
/// text and lives here, so "don't" is one node.
/// </summary>
public sealed class TextNode : MessageNode
{
    public TextNode(string value, SourceSpan span)
        : base(span)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => $"text \"{Value}\"";
}
