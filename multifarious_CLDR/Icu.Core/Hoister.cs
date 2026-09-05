using Icu.Core.Tree;

namespace Icu.Core;

/// <summary>
/// Restructures a message so that every leaf branch holds the complete sentence
/// (section 5.9): selectors move to the outside, and the text around each selector is
/// distributed into every one of its branches. The result is valid ICU, each leaf is
/// a whole sentence a translator or MT engine can work on, and word order and
/// agreement can change freely per form. The argument set is preserved exactly; the
/// shape is not, by design.
/// </summary>
/// <remarks>
/// The one delicate point is '#', which binds to the nearest enclosing plural or
/// selectordinal. Distributing content into a plural-kind selector's branches would
/// silently rebind any '#' it carries, so such a '#' is rewritten to an explicit
/// <c>{argument, number}</c> naming the selector it was bound to. That rewrite is
/// only faithful when the bound selector's offset is zero, because '#' renders the
/// number minus the offset and a plain argument renders the raw number; with a
/// non-zero offset the message is refused with <see cref="IcuHoistException"/> and
/// passes through unexpanded.
/// </remarks>
public static class Hoister
{
    /// <summary>
    /// Hoists a parsed message. The result is rebuilt from its own canonical
    /// serialisation, so its source text and spans are consistent. A message with no
    /// selectors comes back unchanged.
    /// </summary>
    public static IcuMessage Hoist(IcuMessage message)
    {
        if (message is null) throw new ArgumentNullException(nameof(message));

        var hoisted = Hoist(message.Nodes);
        return ReferenceEquals(hoisted, message.Nodes)
            ? message
            : IcuMessage.Parse(IcuSerialiser.Serialise(hoisted));
    }

    /// <summary>
    /// Hoists a node sequence. Nodes are reused where possible, so spans on the
    /// result may point into the tree's original source; serialise for clean text.
    /// </summary>
    public static IReadOnlyList<MessageNode> Hoist(IReadOnlyList<MessageNode> nodes)
    {
        if (nodes is null) throw new ArgumentNullException(nameof(nodes));
        return HoistSequence(nodes, enclosing: null);
    }

    /// <summary>The plural-kind selector whose branch a sequence sits inside, if any.</summary>
    private sealed record EnclosingPlural(string ArgumentName, decimal Offset);

    private static IReadOnlyList<MessageNode> HoistSequence(
        IReadOnlyList<MessageNode> nodes, EnclosingPlural? enclosing)
    {
        var index = IndexOfFirstSelector(nodes);
        if (index < 0)
        {
            return nodes;
        }

        var selector = (SelectorNode)nodes[index];
        var prefix = nodes.Take(index).ToList();
        var suffix = nodes.Skip(index + 1).ToList();

        // Only the plural kinds capture '#'; distributing into a select moves nothing
        // out of its binding scope.
        if (selector.IsPluralKind)
        {
            prefix = RebindPounds(prefix, enclosing);
            suffix = RebindPounds(suffix, enclosing);
        }

        var childEnclosing = selector.IsPluralKind
            ? new EnclosingPlural(selector.ArgumentName, selector.Offset)
            : enclosing;

        var branches = new List<Branch>(selector.Branches.Count);
        foreach (var branch in selector.Branches)
        {
            var content = new List<MessageNode>(prefix.Count + branch.Nodes.Count + suffix.Count);
            content.AddRange(prefix);
            content.AddRange(branch.Nodes);
            content.AddRange(suffix);
            branches.Add(new Branch(branch.Key, HoistSequence(content, childEnclosing), branch.Span));
        }

        return new MessageNode[]
        {
            new SelectorNode(
                selector.Type, selector.ArgumentName, selector.OffsetRaw, selector.Offset,
                branches, selector.Span)
        };
    }

    private static int IndexOfFirstSelector(IReadOnlyList<MessageNode> nodes)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is SelectorNode)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Rewrites each '#' in a sequence about to cross into another plural's scope.
    /// '#' binds straight through a select, so the rewrite follows it into select
    /// branches; a plural-kind selector re-binds '#' itself, so its content is
    /// shielded and it moves whole.
    /// </summary>
    private static List<MessageNode> RebindPounds(List<MessageNode> nodes, EnclosingPlural? enclosing)
    {
        if (!nodes.Any(ContainsBoundPound))
        {
            return nodes;
        }

        if (enclosing is null)
        {
            // Unreachable from a parse: the parser only produces '#' inside a plural.
            throw new IcuHoistException("A '#' outside any plural or selectordinal branch cannot be hoisted.");
        }

        if (enclosing.Offset != 0)
        {
            throw new IcuHoistException(
                $"Cannot hoist: a '#' bound to '{enclosing.ArgumentName}' with offset {enclosing.Offset} " +
                "would move inside another plural's scope, and ICU cannot write the offset number as an argument.");
        }

        return nodes.Select(node => RewritePounds(node, enclosing)).ToList();
    }

    /// <summary>Whether a node carries a '#' bound outside it: a '#' itself, or a select with one in a branch.</summary>
    private static bool ContainsBoundPound(MessageNode node) => node switch
    {
        PoundNode => true,
        SelectorNode { Type: SelectorType.Select } select =>
            select.Branches.Any(branch => branch.Nodes.Any(ContainsBoundPound)),
        _ => false
    };

    private static MessageNode RewritePounds(MessageNode node, EnclosingPlural enclosing) => node switch
    {
        PoundNode pound => new TypedArgumentNode(enclosing.ArgumentName, "number", null, pound.Span),
        SelectorNode { Type: SelectorType.Select } select => new SelectorNode(
            select.Type, select.ArgumentName, select.OffsetRaw, select.Offset,
            select.Branches
                .Select(branch => new Branch(
                    branch.Key,
                    branch.Nodes.Select(child => RewritePounds(child, enclosing)).ToList(),
                    branch.Span))
                .ToList(),
            select.Span),
        _ => node
    };
}
