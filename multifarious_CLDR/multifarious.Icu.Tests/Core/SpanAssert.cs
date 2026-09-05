using Icu.Core;
using Icu.Core.Tree;

namespace Icu.Core.Tests;

/// <summary>
/// The lossless-offset checks, applied to a whole parsed tree: every span lies inside
/// its parent, siblings do not overlap and run in document order, and each node's
/// span covers text of the shape that node claims to be.
/// </summary>
internal static class SpanAssert
{
    public static void Valid(IcuMessage message)
    {
        ValidateSequence(message.Nodes, message.Source, 0, message.Source.Length);
    }

    private static void ValidateSequence(
        IReadOnlyList<MessageNode> nodes, string source, int lowerBound, int upperBound)
    {
        var previousEnd = lowerBound;
        foreach (var node in nodes)
        {
            Assert.True(node.Span.Start >= previousEnd,
                $"Node {node} starts at {node.Span.Start}, before the previous node ended at {previousEnd}.");
            Assert.True(node.Span.End <= upperBound,
                $"Node {node} ends at {node.Span.End}, past its parent's end at {upperBound}.");

            ValidateNode(node, source);
            previousEnd = node.Span.End;
        }
    }

    private static void ValidateNode(MessageNode node, string source)
    {
        var text = node.Span.TextIn(source);
        switch (node)
        {
            case TextNode literal:
                Assert.Equal(literal.Value, text);
                break;
            case QuotedTextNode:
                Assert.StartsWith("'", text);
                break;
            case PoundNode:
                Assert.Equal("#", text);
                break;
            case ArgumentNode:
            case TypedArgumentNode:
                Assert.StartsWith("{", text);
                Assert.EndsWith("}", text);
                break;
            case SelectorNode selector:
                Assert.StartsWith("{", text);
                Assert.EndsWith("}", text);
                ValidateBranches(selector, source);
                break;
        }
    }

    private static void ValidateBranches(SelectorNode selector, string source)
    {
        var previousEnd = selector.Span.Start;
        foreach (var branch in selector.Branches)
        {
            Assert.True(branch.Span.Start >= previousEnd,
                $"Branch {branch.Key} starts before the previous branch ended.");
            Assert.True(branch.Span.End <= selector.Span.End,
                $"Branch {branch.Key} ends past its selector.");
            Assert.StartsWith(branch.Key.Text, branch.Span.TextIn(source));
            Assert.EndsWith("}", branch.Span.TextIn(source));

            ValidateSequence(branch.Nodes, source, branch.Span.Start, branch.Span.End);
            previousEnd = branch.Span.End;
        }
    }
}
