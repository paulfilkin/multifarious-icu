using Icu.Core;
using Icu.Core.Tree;

namespace Icu.Core.Tests;

/// <summary>
/// The canonical serialiser: exact output for known inputs, so a change in spacing,
/// quoting or apostrophe policy is caught here rather than surfacing as a diff in a
/// generated file.
/// </summary>
public class IcuSerialiserTests
{
    [Theory]
    [InlineData("Hello {name}!")]
    [InlineData("{count, plural, one {# message} other {# messages}}")]
    [InlineData("{count, plural, offset:1 =0 {nobody} one {#} other {#}}")]
    [InlineData("{amount, number, ::currency/EUR}")]
    [InlineData("{n, number}")]
    [InlineData("{n, selectordinal, one {#st} other {#th}}")]
    [InlineData("{count, plural, other {'#' left}}")]
    [InlineData("It''s here")]
    [InlineData("# {count, plural, other {x}}")]
    [InlineData("{gender, select, female {{count, plural, one {her item} other {her items}}} other {them}}")]
    public void ACanonicalMessageSerialisesToItself(string source)
    {
        Assert.Equal(source, IcuSerialiser.Serialise(IcuMessage.Parse(source)));
    }

    [Fact]
    public void CompactInputIsSerialisedWithCanonicalSpacing()
    {
        var message = IcuMessage.Parse("{count,plural,one{#}other{#s}}");

        Assert.Equal("{count, plural, one {#} other {#s}}", IcuSerialiser.Serialise(message));
    }

    /// <summary>
    /// A lone literal apostrophe is doubled on output, per the finalise escaping rule
    /// of section 5.8, and the doubled form parses back to the same content.
    /// </summary>
    [Fact]
    public void ALiteralApostropheIsDoubled()
    {
        var message = IcuMessage.Parse("don't stop");

        var serialised = IcuSerialiser.Serialise(message);

        Assert.Equal("don''t stop", serialised);
        Assert.True(MessageComparer.StructurallyEqual(message, IcuMessage.Parse(serialised)));
    }

    [Fact]
    public void AnUnterminatedQuoteIsTerminatedOnOutput()
    {
        var message = IcuMessage.Parse("text '{oops");

        // The quote closes straight after the brace, the only syntax character, so
        // the trailing letters come out unquoted; the content is unchanged.
        Assert.Equal("text '{'oops", IcuSerialiser.Serialise(message));
    }

    /// <summary>
    /// Synthetic trees are the point of the serialiser: hoisting builds nodes no parse
    /// produced, and text carrying syntax characters must come out quoted.
    /// </summary>
    [Fact]
    public void SyntheticTextContainingBracesIsQuoted()
    {
        var nodes = new MessageNode[] { new TextNode("a{b}c", default) };

        var serialised = IcuSerialiser.Serialise(nodes);

        Assert.Equal("a'{b}'c", serialised);
        Assert.True(MessageComparer.StructurallyEqual(nodes, IcuMessage.Parse(serialised).Nodes));
    }

    [Fact]
    public void SyntheticTextContainingPoundIsQuotedOnlyInPluralContext()
    {
        var nodes = new MessageNode[] { new TextNode("50#", default) };

        Assert.Equal("50'#'", IcuSerialiser.Serialise(nodes, inPluralContext: true));
        Assert.Equal("50#", IcuSerialiser.Serialise(nodes, inPluralContext: false));
    }

    [Fact]
    public void SyntheticTextMixingApostrophesAndBracesSurvives()
    {
        var nodes = new MessageNode[] { new TextNode("it's {here}", default) };

        var serialised = IcuSerialiser.Serialise(nodes);

        Assert.Equal("it''s '{here}'", serialised);
        Assert.True(MessageComparer.StructurallyEqual(nodes, IcuMessage.Parse(serialised).Nodes));
    }
}
