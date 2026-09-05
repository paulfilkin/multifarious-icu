namespace Icu.Core.Tree;

/// <summary>
/// A typed argument, <c>{name, number}</c> or <c>{when, date, short}</c>. The type
/// keyword and the style are preserved as written and never interpreted: this layer's
/// job is to carry them through expansion intact, not to format anything with them.
/// </summary>
public sealed class TypedArgumentNode : MessageNode
{
    public TypedArgumentNode(string name, string type, string? style, SourceSpan span)
        : base(span)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        if (type is null) throw new ArgumentNullException(nameof(type));
        Name = name;
        Type = type;
        Style = style;
    }

    public string Name { get; }

    /// <summary>The type keyword as written, such as <c>number</c> or <c>date</c>.</summary>
    public string Type { get; }

    /// <summary>The style as written, such as <c>::currency/EUR</c>, or null where absent.</summary>
    public string? Style { get; }

    public override string ToString() =>
        Style is null ? $"{{{Name}, {Type}}}" : $"{{{Name}, {Type}, {Style}}}";
}
