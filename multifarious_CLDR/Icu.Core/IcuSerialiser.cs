using System.Text;
using Icu.Core.Tree;

namespace Icu.Core;

/// <summary>
/// Produces canonical ICU text from a tree. The output is not the original source: it
/// carries canonical spacing, every apostrophe in text doubled and every syntax
/// character quoted, matching the escaping the design applies at finalise. Faithful
/// reproduction of a parsed message needs no serialiser at all, because
/// <see cref="IcuMessage.Source"/> is kept.
/// </summary>
/// <remarks>
/// Serialisation is context-correct, not context-free: '#' in text is quoted only
/// inside a plural or selectordinal branch, because outside one it is not syntax and
/// quoting it would just put a literal apostrophe in the output. The context flows
/// down the tree the same way it does in the parser.
/// </remarks>
public static class IcuSerialiser
{
    public static string Serialise(IcuMessage message)
    {
        if (message is null) throw new ArgumentNullException(nameof(message));
        return Serialise(message.Nodes, inPluralContext: false);
    }

    /// <summary>
    /// Serialises a node sequence. <paramref name="inPluralContext"/> says whether the
    /// sequence sits inside a plural or selectordinal branch, where '#' is syntax.
    /// </summary>
    public static string Serialise(IReadOnlyList<MessageNode> nodes, bool inPluralContext = false)
    {
        if (nodes is null) throw new ArgumentNullException(nameof(nodes));

        var builder = new StringBuilder();
        AppendNodes(builder, nodes, inPluralContext);
        return builder.ToString();
    }

    private static void AppendNodes(StringBuilder builder, IReadOnlyList<MessageNode> nodes, bool inPlural)
    {
        // Adjacent text and quoted-text nodes are escaped as one run. Escaped one node
        // at a time, a node ending in a closing quote followed by one opening a quote
        // would fuse into '', which parses as a literal apostrophe inside a quote that
        // never closed, silently changing the content.
        var text = new StringBuilder();

        void Flush()
        {
            if (text.Length > 0)
            {
                IcuEscaping.AppendEscaped(builder, text.ToString(), inPlural);
                text.Clear();
            }
        }

        foreach (var node in nodes)
        {
            switch (node)
            {
                case TextNode literal:
                    text.Append(literal.Value);
                    break;
                case QuotedTextNode quoted:
                    text.Append(quoted.Value);
                    break;
                default:
                    Flush();
                    AppendNode(builder, node, inPlural);
                    break;
            }
        }

        Flush();
    }

    private static void AppendNode(StringBuilder builder, MessageNode node, bool inPlural)
    {
        switch (node)
        {
            case PoundNode:
                builder.Append('#');
                break;
            case ArgumentNode argument:
                builder.Append('{').Append(argument.Name).Append('}');
                break;
            case TypedArgumentNode typed:
                builder.Append('{').Append(typed.Name).Append(", ").Append(typed.Type);
                if (typed.Style is not null)
                {
                    builder.Append(", ").Append(typed.Style);
                }

                builder.Append('}');
                break;
            case SelectorNode selector:
                AppendSelector(builder, selector, inPlural);
                break;
            default:
                throw new InvalidOperationException($"Unknown node type '{node.GetType().Name}'.");
        }
    }

    private static void AppendSelector(StringBuilder builder, SelectorNode selector, bool inPlural)
    {
        builder.Append('{').Append(selector.ArgumentName)
            .Append(", ").Append(selector.Type.ToKeyword()).Append(',');

        if (selector.HasOffset)
        {
            builder.Append(" offset:").Append(selector.OffsetRaw);
        }

        var childContext = selector.Type != SelectorType.Select || inPlural;

        foreach (var branch in selector.Branches)
        {
            builder.Append(' ').Append(branch.Key.Text).Append(" {");
            AppendNodes(builder, branch.Nodes, childContext);
            builder.Append('}');
        }

        builder.Append('}');
    }

}
