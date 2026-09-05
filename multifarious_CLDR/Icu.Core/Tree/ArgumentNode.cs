namespace Icu.Core.Tree;

/// <summary>A simple argument, <c>{name}</c>.</summary>
public sealed class ArgumentNode : MessageNode
{
    public ArgumentNode(string name, SourceSpan span)
        : base(span)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        Name = name;
    }

    public string Name { get; }

    public override string ToString() => $"{{{Name}}}";
}
