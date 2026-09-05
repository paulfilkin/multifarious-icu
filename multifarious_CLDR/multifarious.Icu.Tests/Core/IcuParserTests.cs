using Icu.Core;
using Icu.Core.Tree;

namespace Icu.Core.Tests;

/// <summary>
/// The parser's structural behaviour: every node type of section 5.1, nesting, the
/// offset modifier, explicit values, and the '#' context rule.
/// </summary>
public class IcuParserTests
{
    [Fact]
    public void AnEmptyMessageParsesToNoNodes()
    {
        var message = IcuMessage.Parse("");

        Assert.Empty(message.Nodes);
        Assert.Equal("", message.Source);
    }

    [Fact]
    public void PlainTextIsOneTextNode()
    {
        var message = IcuMessage.Parse("Hello world!");

        var text = Assert.IsType<TextNode>(Assert.Single(message.Nodes));
        Assert.Equal("Hello world!", text.Value);
        Assert.Equal(new SourceSpan(0, 12), text.Span);
    }

    [Fact]
    public void ASimpleArgumentSplitsTheTextAroundIt()
    {
        var message = IcuMessage.Parse("Hello {name}!");

        Assert.Equal(3, message.Nodes.Count);
        Assert.Equal("Hello ", Assert.IsType<TextNode>(message.Nodes[0]).Value);
        var argument = Assert.IsType<ArgumentNode>(message.Nodes[1]);
        Assert.Equal("name", argument.Name);
        Assert.Equal("{name}", argument.Span.TextIn(message.Source));
        Assert.Equal("!", Assert.IsType<TextNode>(message.Nodes[2]).Value);
    }

    [Fact]
    public void WhitespaceInsideAnArgumentIsTolerated()
    {
        var message = IcuMessage.Parse("{ name }");

        var argument = Assert.IsType<ArgumentNode>(Assert.Single(message.Nodes));
        Assert.Equal("name", argument.Name);
    }

    [Fact]
    public void ATypedArgumentWithoutStyleKeepsItsTypeKeyword()
    {
        var message = IcuMessage.Parse("{n, number}");

        var argument = Assert.IsType<TypedArgumentNode>(Assert.Single(message.Nodes));
        Assert.Equal("n", argument.Name);
        Assert.Equal("number", argument.Type);
        Assert.Null(argument.Style);
    }

    [Theory]
    [InlineData("{amount, number, ::currency/EUR}", "amount", "number", "::currency/EUR")]
    [InlineData("{when, date, short}", "when", "date", "short")]
    [InlineData("{when, time, full }", "when", "time", "full")]
    public void ATypedArgumentPreservesItsStyleAsWritten(
        string source, string name, string type, string style)
    {
        var message = IcuMessage.Parse(source);

        var argument = Assert.IsType<TypedArgumentNode>(Assert.Single(message.Nodes));
        Assert.Equal(name, argument.Name);
        Assert.Equal(type, argument.Type);
        Assert.Equal(style, argument.Style);
    }

    /// <summary>
    /// Number patterns quote their own '#' with apostrophes and can contain braces.
    /// The style scan must not stop at either.
    /// </summary>
    [Theory]
    [InlineData("{n, number, '#'##}", "'#'##")]
    [InlineData("{n, number, a{b}c}", "a{b}c")]
    public void AStyleContainingQuotesOrBracesIsReadWhole(string source, string style)
    {
        var message = IcuMessage.Parse(source);

        var argument = Assert.IsType<TypedArgumentNode>(Assert.Single(message.Nodes));
        Assert.Equal(style, argument.Style);
    }

    [Fact]
    public void APluralParsesIntoKeywordBranches()
    {
        var message = IcuMessage.Parse("{count, plural, one {# message} other {# messages}}");

        var selector = Assert.IsType<SelectorNode>(Assert.Single(message.Nodes));
        Assert.Equal(SelectorType.Plural, selector.Type);
        Assert.Equal("count", selector.ArgumentName);
        Assert.False(selector.HasOffset);
        Assert.Equal(0m, selector.Offset);
        Assert.True(selector.IsPluralKind);

        Assert.Equal(2, selector.Branches.Count);
        Assert.Equal("one", selector.Branches[0].Key.Text);
        Assert.False(selector.Branches[0].Key.IsExplicit);

        Assert.IsType<PoundNode>(selector.Branches[0].Nodes[0]);
        Assert.Equal(" message", Assert.IsType<TextNode>(selector.Branches[0].Nodes[1]).Value);
    }

    [Fact]
    public void ASelectordinalParsesAsTheOrdinalKind()
    {
        var message = IcuMessage.Parse("{n, selectordinal, one {#st} other {#th}}");

        var selector = Assert.IsType<SelectorNode>(Assert.Single(message.Nodes));
        Assert.Equal(SelectorType.SelectOrdinal, selector.Type);
        Assert.True(selector.IsPluralKind);
        Assert.IsType<PoundNode>(selector.Branches[0].Nodes[0]);
    }

    [Fact]
    public void OffsetAndExplicitValuesAreKeptDistinctFromCategories()
    {
        var message = IcuMessage.Parse(
            "{count, plural, offset:1 =0 {nobody} one {one other} other {# others}}");

        var selector = Assert.IsType<SelectorNode>(Assert.Single(message.Nodes));
        Assert.True(selector.HasOffset);
        Assert.Equal("1", selector.OffsetRaw);
        Assert.Equal(1m, selector.Offset);

        var explicitBranch = selector.Branches[0];
        Assert.True(explicitBranch.Key.IsExplicit);
        Assert.Equal("=0", explicitBranch.Key.Text);
        Assert.Equal(0m, explicitBranch.Key.ExplicitValue);
        Assert.Equal("nobody", Assert.IsType<TextNode>(Assert.Single(explicitBranch.Nodes)).Value);

        Assert.False(selector.Branches[1].Key.IsExplicit);
        Assert.Null(selector.Branches[1].Key.ExplicitValue);
    }

    [Fact]
    public void AnExplicitValueMayBeFractional()
    {
        var message = IcuMessage.Parse("{n, plural, =0.5 {half} other {#}}");

        var selector = Assert.IsType<SelectorNode>(Assert.Single(message.Nodes));
        Assert.Equal("=0.5", selector.Branches[0].Key.Text);
        Assert.Equal(0.5m, selector.Branches[0].Key.ExplicitValue);
    }

    [Fact]
    public void ASelectNestsAPluralAndBothKeepTheirShape()
    {
        var message = IcuMessage.Parse(
            "{gender, select, female {{count, plural, one {her item} other {her items}}} " +
            "other {{count, plural, one {their item} other {their items}}}}");

        var select = Assert.IsType<SelectorNode>(Assert.Single(message.Nodes));
        Assert.Equal(SelectorType.Select, select.Type);
        Assert.False(select.IsPluralKind);

        var nested = Assert.IsType<SelectorNode>(Assert.Single(select.Branches[0].Nodes));
        Assert.Equal(SelectorType.Plural, nested.Type);
        Assert.Equal("count", nested.ArgumentName);
        Assert.Equal("her item", Assert.IsType<TextNode>(Assert.Single(nested.Branches[0].Nodes)).Value);
    }

    [Fact]
    public void PoundOutsideAPluralIsPlainText()
    {
        var message = IcuMessage.Parse("# {count, plural, other {x}}");

        Assert.Equal("# ", Assert.IsType<TextNode>(message.Nodes[0]).Value);
    }

    [Fact]
    public void PoundInsideASelectAtTheTopLevelIsPlainText()
    {
        var message = IcuMessage.Parse("{gender, select, other {# items}}");

        var select = Assert.IsType<SelectorNode>(Assert.Single(message.Nodes));
        Assert.Equal("# items", Assert.IsType<TextNode>(Assert.Single(select.Branches[0].Nodes)).Value);
    }

    /// <summary>
    /// A select does not end the plural context: '#' inside a select branch that sits
    /// inside a plural branch still binds to the plural.
    /// </summary>
    [Fact]
    public void PoundInsideASelectNestedInAPluralStaysBound()
    {
        var message = IcuMessage.Parse("{count, plural, other {{gender, select, other {# items}}}}");

        var plural = Assert.IsType<SelectorNode>(Assert.Single(message.Nodes));
        var select = Assert.IsType<SelectorNode>(Assert.Single(plural.Branches[0].Nodes));
        Assert.IsType<PoundNode>(select.Branches[0].Nodes[0]);
    }

    [Fact]
    public void TwoIndependentPluralsParseAsTwoSelectors()
    {
        var message = IcuMessage.Parse(
            "{files, plural, one {# file} other {# files}} in " +
            "{folders, plural, one {# folder} other {# folders}}");

        var selectors = message.Nodes.OfType<SelectorNode>().ToList();
        Assert.Equal(2, selectors.Count);
        Assert.Equal("files", selectors[0].ArgumentName);
        Assert.Equal("folders", selectors[1].ArgumentName);
    }

    /// <summary>
    /// The lossless-offset promise: every node's span maps back to exactly the source
    /// text it was parsed from.
    /// </summary>
    [Fact]
    public void SpansMapBackToTheSourceText()
    {
        const string source =
            "Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!";
        var message = IcuMessage.Parse(source);

        Assert.Equal("Hello ", message.Nodes[0].Span.TextIn(source));
        Assert.Equal("{name}", message.Nodes[1].Span.TextIn(source));
        Assert.Equal(", you have ", message.Nodes[2].Span.TextIn(source));

        var selector = Assert.IsType<SelectorNode>(message.Nodes[3]);
        Assert.Equal(
            "{count, plural, one {# unread message} other {# unread messages}}",
            selector.Span.TextIn(source));
        Assert.Equal("one {# unread message}", selector.Branches[0].Span.TextIn(source));
        Assert.Equal("other {# unread messages}", selector.Branches[1].Span.TextIn(source));
        Assert.Equal("#", selector.Branches[0].Nodes[0].Span.TextIn(source));

        Assert.Equal("!", message.Nodes[4].Span.TextIn(source));
    }

    [Fact]
    public void AMessageWithoutSpacesParsesTheSameShape()
    {
        var message = IcuMessage.Parse("{count,plural,one{#}other{#s}}");

        var selector = Assert.IsType<SelectorNode>(Assert.Single(message.Nodes));
        Assert.Equal("count", selector.ArgumentName);
        Assert.Equal(2, selector.Branches.Count);
        Assert.IsType<PoundNode>(Assert.Single(selector.Branches[0].Nodes));
    }

    [Fact]
    public void AnEmptyBranchBodyIsValid()
    {
        var message = IcuMessage.Parse("{n, plural, one {} other {#}}");

        var selector = Assert.IsType<SelectorNode>(Assert.Single(message.Nodes));
        Assert.Empty(selector.Branches[0].Nodes);
    }
}
