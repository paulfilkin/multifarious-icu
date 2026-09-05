using Icu.Core;
using Icu.Core.Tree;

namespace Icu.Core.Tests;

/// <summary>
/// The apostrophe rules, enumerated: a doubled apostrophe is a literal apostrophe, an
/// apostrophe before a syntax character opens a quoted literal, and any other
/// apostrophe is literal text. These are the cases regex handling gets wrong and the
/// reason there is a parser at all.
/// </summary>
public class IcuParserApostropheTests
{
    [Fact]
    public void ADoubledApostropheIsALiteralApostrophe()
    {
        var message = IcuMessage.Parse("It''s here");

        Assert.Equal(3, message.Nodes.Count);
        Assert.Equal("It", Assert.IsType<TextNode>(message.Nodes[0]).Value);
        var quoted = Assert.IsType<QuotedTextNode>(message.Nodes[1]);
        Assert.Equal("'", quoted.Value);
        Assert.Equal("''", quoted.Span.TextIn(message.Source));
        Assert.Equal("s here", Assert.IsType<TextNode>(message.Nodes[2]).Value);
    }

    [Fact]
    public void AnApostropheBeforeABraceOpensAQuotedLiteral()
    {
        var message = IcuMessage.Parse("'{count}' first");

        var quoted = Assert.IsType<QuotedTextNode>(message.Nodes[0]);
        Assert.Equal("{count}", quoted.Value);
        Assert.Equal("'{count}'", quoted.Span.TextIn(message.Source));
        Assert.Equal(" first", Assert.IsType<TextNode>(message.Nodes[1]).Value);
    }

    [Fact]
    public void AnApostropheNotBeforeASyntaxCharacterIsLiteralText()
    {
        var message = IcuMessage.Parse("don't stop");

        var text = Assert.IsType<TextNode>(Assert.Single(message.Nodes));
        Assert.Equal("don't stop", text.Value);
    }

    [Fact]
    public void ATrailingApostropheIsLiteralText()
    {
        var message = IcuMessage.Parse("rock'");

        Assert.Equal("rock'", Assert.IsType<TextNode>(Assert.Single(message.Nodes)).Value);
    }

    [Fact]
    public void ADoubledApostropheInsideAQuoteDecodesToOne()
    {
        var message = IcuMessage.Parse("'{a''b}'");

        var quoted = Assert.IsType<QuotedTextNode>(Assert.Single(message.Nodes));
        Assert.Equal("{a'b}", quoted.Value);
    }

    /// <summary>ICU runs an unterminated quote to the end of the message, and so do we.</summary>
    [Fact]
    public void AnUnterminatedQuoteRunsToTheEnd()
    {
        var message = IcuMessage.Parse("text '{oops");

        var quoted = Assert.IsType<QuotedTextNode>(message.Nodes[1]);
        Assert.Equal("{oops", quoted.Value);
        Assert.Equal("'{oops", quoted.Span.TextIn(message.Source));
    }

    /// <summary>
    /// '#' is syntax only inside a plural branch, so quoting it is meaningful there
    /// and meaningless outside, where the apostrophe stays literal.
    /// </summary>
    [Fact]
    public void AQuotedPoundInsideAPluralIsLiteral()
    {
        var message = IcuMessage.Parse("{count, plural, other {'#' left}}");

        var selector = Assert.IsType<SelectorNode>(Assert.Single(message.Nodes));
        var quoted = Assert.IsType<QuotedTextNode>(selector.Branches[0].Nodes[0]);
        Assert.Equal("#", quoted.Value);
        Assert.Equal(" left", Assert.IsType<TextNode>(selector.Branches[0].Nodes[1]).Value);
    }

    [Fact]
    public void AnApostropheBeforePoundOutsideAPluralIsLiteralText()
    {
        var message = IcuMessage.Parse("'# not a quote");

        Assert.Equal("'# not a quote", Assert.IsType<TextNode>(Assert.Single(message.Nodes)).Value);
    }

    [Fact]
    public void AWholeQuotedSelectorIsOneLiteral()
    {
        var message = IcuMessage.Parse("'{count, plural, one {#} other {#}}'");

        var quoted = Assert.IsType<QuotedTextNode>(Assert.Single(message.Nodes));
        Assert.Equal("{count, plural, one {#} other {#}}", quoted.Value);
    }
}
