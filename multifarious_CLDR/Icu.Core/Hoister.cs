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
        return HoistSequence(nodes, enclosing: null, bindings: Array.Empty<Binding>());
    }

    /// <summary>The plural-kind selector whose branch a sequence sits inside, if any.</summary>
    private sealed record EnclosingPlural(string ArgumentName, decimal Offset);

    /// <summary>
    /// A selector on the path above a sequence and the branch of it the sequence sits in:
    /// inside that branch the argument is known to match <see cref="Key"/>, or, under
    /// <c>other</c>, to match none of <see cref="SiblingKeys"/>.
    /// </summary>
    private sealed record Binding(
        SelectorType Type, string ArgumentName, decimal Offset, BranchKey Key, IReadOnlyList<BranchKey> SiblingKeys);

    private static IReadOnlyList<MessageNode> HoistSequence(
        IReadOnlyList<MessageNode> nodes, EnclosingPlural? enclosing, IReadOnlyList<Binding> bindings)
    {
        var index = IndexOfFirstSelector(nodes);
        if (index < 0)
        {
            return nodes;
        }

        var selector = (SelectorNode)nodes[index];
        var prefix = nodes.Take(index).ToList();
        var suffix = nodes.Skip(index + 1).ToList();

        // A second selector on an argument already bound above resolves to what the
        // binding allows, instead of nesting every one of its branches under every one
        // of the outer's: two article selects in one sentence are ten whole sentences,
        // not a hundred of which ninety could never be rendered (25 October 2026).
        var collapsed = Collapse(selector, enclosing, bindings);
        if (collapsed is not null)
        {
            var replaced = new List<MessageNode>(prefix.Count + collapsed.Count + suffix.Count);
            replaced.AddRange(prefix);
            replaced.AddRange(collapsed);
            replaced.AddRange(suffix);
            return HoistSequence(replaced, enclosing, bindings);
        }

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

        var siblingKeys = selector.Branches.Select(branch => branch.Key).ToList();
        var branches = new List<Branch>(selector.Branches.Count);
        foreach (var branch in selector.Branches)
        {
            var content = new List<MessageNode>(prefix.Count + branch.Nodes.Count + suffix.Count);
            content.AddRange(prefix);
            content.AddRange(branch.Nodes);
            content.AddRange(suffix);
            var childBindings = new List<Binding>(bindings.Count + 1);
            childBindings.AddRange(bindings);
            childBindings.Add(new Binding(selector.Type, selector.ArgumentName, selector.Offset, branch.Key, siblingKeys));
            branches.Add(new Branch(branch.Key, HoistSequence(content, childEnclosing, childBindings), branch.Span));
        }

        return new MessageNode[]
        {
            new SelectorNode(
                selector.Type, selector.ArgumentName, selector.OffsetRaw, selector.Offset,
                branches, selector.Span)
        };
    }

    /// <summary>
    /// What a selector reduces to under a binding of its own argument, or null where it
    /// cannot be reduced and nests as any other selector does. Only exact reductions are
    /// made, so rendering is unchanged for every value:
    /// a select under a select on the same argument, and a plural kind under the same
    /// kind, argument and offset when the bound key is a keyword and the inner has no
    /// explicit <c>=n</c> keys. Under a named key the inner's branch of that key stands,
    /// or its <c>other</c>; under <c>other</c> the value matches none of the outer's
    /// named keys, so those of the inner's branches are dropped and what is left stands,
    /// whole or as a smaller selector. An explicit key on either side is left alone:
    /// resolving a number to a category needs CLDR, which this layer has not got.
    /// </summary>
    private static IReadOnlyList<MessageNode>? Collapse(
        SelectorNode selector, EnclosingPlural? enclosing, IReadOnlyList<Binding> bindings)
    {
        Binding? binding = null;
        foreach (var candidate in bindings)
        {
            if (candidate.Type == selector.Type
                && string.Equals(candidate.ArgumentName, selector.ArgumentName, StringComparison.Ordinal)
                && (!selector.IsPluralKind || candidate.Offset == selector.Offset))
            {
                binding = candidate;
                break;
            }
        }

        if (binding is null)
        {
            return null;
        }

        if (selector.IsPluralKind
            && (binding.Key.IsExplicit || selector.Branches.Any(branch => branch.Key.IsExplicit)))
        {
            return null;
        }

        IReadOnlyList<MessageNode>? content;
        if (!IsOther(binding.Key))
        {
            var chosen = selector.Branches.FirstOrDefault(branch => branch.Key.Equals(binding.Key))
                ?? selector.Branches.FirstOrDefault(branch => IsOther(branch.Key));
            if (chosen is null)
            {
                return null;
            }

            content = chosen.Nodes;
        }
        else
        {
            var reachable = selector.Branches
                .Where(branch => IsOther(branch.Key) || !binding.SiblingKeys.Contains(branch.Key))
                .ToList();
            if (reachable.Count == selector.Branches.Count)
            {
                return null;
            }

            if (reachable.Count == 1)
            {
                content = reachable[0].Nodes;
            }
            else
            {
                return new MessageNode[]
                {
                    new SelectorNode(
                        selector.Type, selector.ArgumentName, selector.OffsetRaw, selector.Offset,
                        reachable, selector.Span)
                };
            }
        }

        // A '#' in a collapsed plural-kind branch was bound to that plural; in the sequence
        // it lands in it binds to the enclosing one, which is the same argument and offset
        // unless another plural sits between the two. Then it is rewritten, or, with an
        // offset ICU cannot write as an argument, the selector is left nested.
        if (selector.IsPluralKind && content.Any(ContainsBoundPound))
        {
            var sameScope = enclosing is not null
                && string.Equals(enclosing.ArgumentName, selector.ArgumentName, StringComparison.Ordinal)
                && enclosing.Offset == selector.Offset;
            if (!sameScope)
            {
                if (selector.Offset != 0)
                {
                    return null;
                }

                var own = new EnclosingPlural(selector.ArgumentName, selector.Offset);
                content = content.Select(node => RewritePounds(node, own)).ToList();
            }
        }

        return content;
    }

    private static bool IsOther(BranchKey key) => string.Equals(key.Text, "other", StringComparison.Ordinal);

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
