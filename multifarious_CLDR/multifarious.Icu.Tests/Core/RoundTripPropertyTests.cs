using FsCheck.Xunit;
using Icu.Core;
using Icu.Core.Tree;

namespace Icu.Core.Tests;

/// <summary>
/// The T1 round-trip properties of section 14.2, driven by FsCheck over the seeded
/// tree generator. A failure prints the seed, which reproduces the tree exactly.
/// </summary>
public class RoundTripPropertyTests
{
    private const int MaxDepth = 3;

    /// <summary>
    /// The serialiser's correctness half: whatever tree we build, its canonical text
    /// parses back to the same content. This is the property the expansion relies on,
    /// because hoisted trees are synthesised, not parsed.
    /// </summary>
    [Property(MaxTest = 500)]
    public void SerialiseThenParsePreservesContent(int seed)
    {
        var generated = MessageGenerator.GenerateNodes(new Random(seed), MaxDepth, inPlural: false);
        var text = IcuSerialiser.Serialise(generated);

        var parsed = IcuMessage.Parse(text);

        Assert.True(MessageComparer.StructurallyEqual(generated, parsed.Nodes),
            $"Content changed through serialise and parse for seed {seed}: \"{text}\"");
    }

    /// <summary>
    /// The parser's correctness half: parse, serialise, re-parse reaches a fixed point
    /// in one step, in both the tree and the text.
    /// </summary>
    [Property(MaxTest = 500)]
    public void ParseSerialiseParseIsAFixedPoint(int seed)
    {
        var generated = MessageGenerator.GenerateNodes(new Random(seed), MaxDepth, inPlural: false);

        var first = IcuMessage.Parse(IcuSerialiser.Serialise(generated));
        var canonical = IcuSerialiser.Serialise(first);
        var second = IcuMessage.Parse(canonical);

        Assert.True(MessageComparer.StructurallyEqual(first, second),
            $"Tree not stable through a second round trip for seed {seed}: \"{canonical}\"");
        Assert.Equal(canonical, IcuSerialiser.Serialise(second));
    }

    /// <summary>
    /// The lossless-offset promise, on every parsed tree: spans nest, run in document
    /// order and cover text of the right shape.
    /// </summary>
    [Property(MaxTest = 500)]
    public void OffsetsMapBackToTheSourceText(int seed)
    {
        var generated = MessageGenerator.GenerateNodes(new Random(seed), MaxDepth, inPlural: false);

        var parsed = IcuMessage.Parse(IcuSerialiser.Serialise(generated));

        SpanAssert.Valid(parsed);
    }
}
