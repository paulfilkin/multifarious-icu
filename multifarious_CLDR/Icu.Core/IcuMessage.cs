using Icu.Core.Tree;

namespace Icu.Core;

/// <summary>
/// A parsed ICU MessageFormat message: the original text and the node sequence
/// produced from it. The original text is kept so that faithful reproduction and
/// offset-based reporting need nothing beyond this object.
/// </summary>
public sealed class IcuMessage
{
    public IcuMessage(string source, IReadOnlyList<MessageNode> nodes)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (nodes is null) throw new ArgumentNullException(nameof(nodes));
        Source = source;
        Nodes = nodes;
    }

    /// <summary>The message exactly as it was given to the parser.</summary>
    public string Source { get; }

    public IReadOnlyList<MessageNode> Nodes { get; }

    public static IcuMessage Parse(string source) => IcuParser.Parse(source);
}
