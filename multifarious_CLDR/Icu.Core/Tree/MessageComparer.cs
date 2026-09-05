using System.Text;

namespace Icu.Core.Tree;

/// <summary>
/// Structural equality over message trees: same content, same shape, spans ignored.
/// Adjacent literal and quoted text is compared as one decoded run, because the split
/// between the two node types records how the source was written, not what it says:
/// "don't" and "don''t" carry the same message. This is the equality the round-trip
/// property asserts, and later the hoisting properties.
/// </summary>
public static class MessageComparer
{
    public static bool StructurallyEqual(IcuMessage left, IcuMessage right)
    {
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
        return StructurallyEqual(left.Nodes, right.Nodes);
    }

    public static bool StructurallyEqual(IReadOnlyList<MessageNode> left, IReadOnlyList<MessageNode> right)
    {
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));

        var a = Normalise(left);
        var b = Normalise(right);
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!ItemsEqual(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Coalesces adjacent text and quoted-text nodes into their decoded string, drops
    /// empty runs, and leaves every other node as itself.
    /// </summary>
    private static List<object> Normalise(IReadOnlyList<MessageNode> nodes)
    {
        var items = new List<object>();
        var text = new StringBuilder();

        void Flush()
        {
            if (text.Length > 0)
            {
                items.Add(text.ToString());
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
                    items.Add(node);
                    break;
            }
        }

        Flush();
        return items;
    }

    private static bool ItemsEqual(object left, object right) => (left, right) switch
    {
        (string a, string b) => string.Equals(a, b, StringComparison.Ordinal),
        (PoundNode, PoundNode) => true,
        (ArgumentNode a, ArgumentNode b) => string.Equals(a.Name, b.Name, StringComparison.Ordinal),
        (TypedArgumentNode a, TypedArgumentNode b) =>
            string.Equals(a.Name, b.Name, StringComparison.Ordinal)
            && string.Equals(a.Type, b.Type, StringComparison.Ordinal)
            && string.Equals(a.Style, b.Style, StringComparison.Ordinal),
        (SelectorNode a, SelectorNode b) => SelectorsEqual(a, b),
        _ => false
    };

    private static bool SelectorsEqual(SelectorNode a, SelectorNode b)
    {
        // The offsets are compared by value, so an explicit offset:0 equals an absent
        // offset: they select identically and render identically.
        if (a.Type != b.Type
            || !string.Equals(a.ArgumentName, b.ArgumentName, StringComparison.Ordinal)
            || a.Offset != b.Offset
            || a.Branches.Count != b.Branches.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Branches.Count; i++)
        {
            if (!KeysEqual(a.Branches[i].Key, b.Branches[i].Key)
                || !StructurallyEqual(a.Branches[i].Nodes, b.Branches[i].Nodes))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Keywords compare by text; explicit values compare by number, because =0 and
    /// =0.0 match exactly the same counts.
    /// </summary>
    private static bool KeysEqual(BranchKey a, BranchKey b) =>
        a.IsExplicit == b.IsExplicit
        && (a.IsExplicit
            ? a.ExplicitValue == b.ExplicitValue
            : string.Equals(a.Text, b.Text, StringComparison.Ordinal));
}
