using Icu.Core;

namespace Icu.Core.Tests;

/// <summary>
/// The renderer, directed, wired to real CLDR data: branch selection over categories,
/// explicit values and offsets, operand fidelity, select matching and '#'
/// substitution.
/// </summary>
public class MessageRendererTests
{
    private static string Render(string source, string locale, params (string Name, string Value)[] arguments) =>
        MessageRenderer.Render(
            IcuMessage.Parse(source),
            arguments.ToDictionary(pair => pair.Name, pair => pair.Value),
            CldrCategories.For(locale));

    [Theory]
    [InlineData("1", "Hello Anna, you have 1 unread message!")]
    [InlineData("5", "Hello Anna, you have 5 unread messages!")]
    public void TheWorkedExampleRenders(string count, string expected)
    {
        Assert.Equal(expected, Render(
            "Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!",
            "en", ("name", "Anna"), ("count", count)));
    }

    /// <summary>
    /// The classic offset example: explicit values match the raw number, the category
    /// evaluates on number minus offset, and '#' renders number minus offset.
    /// </summary>
    [Theory]
    [InlineData("0", "nobody")]
    [InlineData("1", "just you")]
    [InlineData("3", "you and 2 others")]
    public void OffsetAndExplicitValuesFollowTheIcuRules(string count, string expected)
    {
        Assert.Equal(expected, Render(
            "{count, plural, offset:1 =0 {nobody} =1 {just you} other {you and # others}}",
            "en", ("count", count)));
    }

    /// <summary>An explicit value wins over the category branch that also covers it.</summary>
    [Theory]
    [InlineData("1", "exactly one")]
    [InlineData("21", "a single one")]
    public void AnExplicitValueBeatsItsCategory(string count, string expected)
    {
        // Russian, where 21 is category 'one', so the keyword branch is reachable for
        // a count the explicit branch does not claim.
        Assert.Equal(expected, Render(
            "{n, plural, =1 {exactly one} one {a single one} other {many}}",
            "ru", ("n", count)));
    }

    /// <summary>
    /// Operands come from how the number is written: 1 and 1.0 are the same value and
    /// different categories in Russian. String counts preserve that.
    /// </summary>
    [Theory]
    [InlineData("1", "one")]
    [InlineData("1.0", "other")]
    public void OperandFidelitySurvivesRendering(string count, string expected)
    {
        Assert.Equal(expected, Render("{n, plural, one {one} other {other}}", "ru", ("n", count)));
    }

    [Theory]
    [InlineData("female", "her")]
    [InlineData("male", "his")]
    [InlineData("unknown", "their")]
    public void ASelectMatchesByValueAndFallsBackToOther(string gender, string expected)
    {
        Assert.Equal(expected, Render(
            "{gender, select, female {her} male {his} other {their}}",
            "en", ("gender", gender)));
    }

    /// <summary>Selectordinal reads the ordinal table, not the cardinal one.</summary>
    [Theory]
    [InlineData("1", "1st")]
    [InlineData("2", "2nd")]
    [InlineData("3", "3rd")]
    [InlineData("11", "11th")]
    [InlineData("21", "21st")]
    public void AnOrdinalSelectsFromTheOrdinalTable(string count, string expected)
    {
        Assert.Equal(expected, Render(
            "{n, selectordinal, one {#st} two {#nd} few {#rd} other {#th}}",
            "en", ("n", count)));
    }

    [Fact]
    public void QuotedTextRendersDecodedAndUnsubstituted()
    {
        Assert.Equal("It's #", Render(
            "It''s {n, plural, other {'#'}}", "en", ("n", "5")));
    }

    [Fact]
    public void AFractionalCountRendersThroughPound()
    {
        Assert.Equal("1.5", Render("{n, plural, other {#}}", "en", ("n", "1.5")));
    }

    /// <summary>
    /// The rebinding rule's other half: a hoisted message with '#' rewritten to an
    /// explicit number argument renders exactly as the original does.
    /// </summary>
    [Fact]
    public void ARewrittenPoundRendersAsThePoundDid()
    {
        const string source =
            "{count, plural, other {# items in {folders, plural, one {# folder} other {# folders}}}}";
        var message = IcuMessage.Parse(source);
        var hoisted = Hoister.Hoist(message.Nodes);
        var arguments = new Dictionary<string, string> { ["count"] = "3", ["folders"] = "1" };

        var original = MessageRenderer.Render(message.Nodes, arguments, CldrCategories.For("en"));
        var afterHoist = MessageRenderer.Render(hoisted, arguments, CldrCategories.For("en"));

        Assert.Equal("3 items in 1 folder", original);
        Assert.Equal(original, afterHoist);
    }

    /// <summary>
    /// CLDR's compact notation: 1c6 is 1000000 written compactly, and the exponent is
    /// itself a plural operand. Spanish reaches 'many' through it, so the count must
    /// go to the category decision as written, not reformatted through decimal.
    /// </summary>
    [Theory]
    [InlineData("1c6", "many")]
    [InlineData("2c6", "many")]
    [InlineData("1000000", "many")]
    [InlineData("5", "other")]
    public void ACompactCountSelectsOnItsWrittenForm(string count, string expected)
    {
        Assert.Equal(expected, Render(
            "{n, plural, one {one} many {many} other {other}}", "es", ("n", count)));
    }

    /// <summary>
    /// '#' renders the numeric value: the exponent consumes fraction digits into the
    /// integer part, so 1.2c6 is 1200000 with nothing visible after the point.
    /// </summary>
    [Theory]
    [InlineData("1c6", "1000000")]
    [InlineData("1.2c6", "1200000")]
    [InlineData("1.0000001c6", "1000000.1")]
    public void APoundRendersACompactCountAtItsNumericValue(string count, string expected)
    {
        Assert.Equal(expected, Render("{n, plural, other {#}}", "en", ("n", count)));
    }

    [Fact]
    public void AnExplicitValueMatchesACompactCountNumerically()
    {
        Assert.Equal("a round million", Render(
            "{n, plural, =1000000 {a round million} other {#}}", "en", ("n", "1c6")));
    }

    [Fact]
    public void AnOffsetForcesACompactCountNumeric()
    {
        // The compact operand does not survive the offset subtraction: 1c6 minus 1
        // is 999999, which is Spanish 'other', not 'many'.
        Assert.Equal("999999", Render(
            "{n, plural, offset:1 many {millions} other {#}}", "es", ("n", "1c6")));
    }

    /// <summary>
    /// The hoisting property holds for compact counts too: '#' rewritten to a number
    /// argument renders the same numeric value the '#' did.
    /// </summary>
    [Fact]
    public void ARewrittenPoundRendersACompactCountAsThePoundDid()
    {
        var message = IcuMessage.Parse("{n, plural, many {# things} other {# thing}}");
        var hoisted = Hoister.Hoist(message.Nodes);
        var arguments = new Dictionary<string, string> { ["n"] = "1.2c6" };

        var original = MessageRenderer.Render(message.Nodes, arguments, CldrCategories.For("es"));
        var afterHoist = MessageRenderer.Render(hoisted, arguments, CldrCategories.For("es"));

        Assert.Equal("1200000 things", original);
        Assert.Equal(original, afterHoist);
    }

    [Theory]
    [InlineData("1c")]
    [InlineData("c6")]
    [InlineData("1c2c3")]
    [InlineData("1c-2")]
    public void AMalformedCompactCountIsRejected(string count)
    {
        Assert.Throws<InvalidOperationException>(() =>
            Render("{n, plural, other {#}}", "en", ("n", count)));
    }

    [Fact]
    public void AMissingArgumentIsReportedByName()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Render("Hello {name}!", "en"));

        Assert.Contains("'name'", exception.Message);
    }

    [Fact]
    public void ANonNumericCountIsReportedByArgument()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Render("{n, plural, other {#}}", "en", ("n", "many")));

        Assert.Contains("'n'", exception.Message);
    }
}
