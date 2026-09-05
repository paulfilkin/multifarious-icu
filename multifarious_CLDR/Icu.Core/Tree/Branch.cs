namespace Icu.Core.Tree;

/// <summary>One branch of a selector: its key and the sub-message it holds.</summary>
public sealed class Branch
{
    public Branch(BranchKey key, IReadOnlyList<MessageNode> nodes, SourceSpan span)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (nodes is null) throw new ArgumentNullException(nameof(nodes));
        Key = key;
        Nodes = nodes;
        Span = span;
    }

    public BranchKey Key { get; }

    public IReadOnlyList<MessageNode> Nodes { get; }

    /// <summary>From the first character of the key to the brace closing the branch.</summary>
    public SourceSpan Span { get; }

    public override string ToString() => $"{Key} {{…}}";
}
