namespace Icu.Core.Tree;

/// <summary>
/// One quoted construct: either a doubled apostrophe, whose value is a single
/// apostrophe, or an apostrophe-quoted literal such as <c>'{'</c>, whose value is the
/// content with inner doubled apostrophes decoded. Kept distinct from
/// <see cref="TextNode"/> because the value and the source text differ, which is
/// exactly what the unescape transform removes and the escape transform restores.
/// </summary>
public sealed class QuotedTextNode : MessageNode
{
    public QuotedTextNode(string value, SourceSpan span)
        : base(span)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        Value = value;
    }

    /// <summary>The decoded content, without the quotes.</summary>
    public string Value { get; }

    public override string ToString() => $"quoted \"{Value}\"";
}
