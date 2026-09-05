using multifarious.Icu.Expansion;

namespace multifarious.Icu.Expansion.Tests;

/// <summary>
/// Classification: what expands, what is left alone, and what passes through with a
/// reason. No fixtures needed; the classifier works on reconstructed strings.
/// </summary>
public class MessageClassifierTests
{
    private static MessageClassification Classify(string raw, ExpansionOptions? options = null) =>
        MessageClassifier.Classify(raw, options ?? ExpansionOptions.Default);

    [Theory]
    [InlineData("Message Centre")]
    [InlineData("Hello {name}, welcome back!")]
    [InlineData("Your balance is {amount, number, ::currency/EUR} as of {when, date, short}.")]
    [InlineData("Type '{' to insert a placeholder, and don''t forget to close it.")]
    [InlineData("Use # to comment out a line.")]
    public void AValueWithNoSelectorIsNoIcu(string raw)
    {
        Assert.Equal(MessageKind.NoIcu, Classify(raw).Kind);
    }

    /// <summary>
    /// A select-only message is restructured with the select walked (design 5.6,
    /// extended by T4): the syntax must reach the translator as protected tags,
    /// not as editable text the platform segments mid-syntax.
    /// </summary>
    [Theory]
    [InlineData("{g, select, female {her} other {their}}")]
    [InlineData("{questionNumber, select, 1 {What is your name?} 2 {What is your quest?} other {No.}}")]
    public void ASelectOnlyValueClassifiesAsExpand(string raw)
    {
        var classification = Classify(raw);

        Assert.Equal(MessageKind.Expand, classification.Kind);
        Assert.NotNull(classification.Hoisted);
    }

    /// <summary>
    /// MessageFormat 2.0 (the .match syntax, an ICU tech preview) is not this app's
    /// format. An MF2 message fails the ICU parse and passes through with a report,
    /// per D10; it must never be half-expanded.
    /// </summary>
    [Fact]
    public void AMessageFormat2ValuePassesThrough()
    {
        var classification = Classify(".match {$count :number} one {{You have one item}} * {{You have many}}");

        Assert.Equal(MessageKind.PassThrough, classification.Kind);
        Assert.Contains("does not parse", classification.Reason);
    }

    [Theory]
    [InlineData("{count, plural, one {# message} other {# messages}}")]
    [InlineData("You are {n, selectordinal, one {#st} other {#th}} in the queue.")]
    [InlineData("{g, select, female {{count, plural, one {a} other {b}}} other {c}}")]
    public void AValueWithAPluralKindClassifiesAsExpand(string raw)
    {
        var classification = Classify(raw);

        Assert.Equal(MessageKind.Expand, classification.Kind);
        Assert.NotNull(classification.Hoisted);
    }

    [Fact]
    public void AParseFailurePassesThroughWithTheOffset()
    {
        var classification = Classify("oops }");

        Assert.Equal(MessageKind.PassThrough, classification.Kind);
        Assert.Equal(5, classification.ErrorOffset);
        Assert.Contains("does not parse", classification.Reason);
    }

    /// <summary>
    /// Our parser is stricter than ICU on purpose; a plural without 'other' cannot be
    /// seeded and is a parse failure, so it passes through.
    /// </summary>
    [Fact]
    public void APluralWithoutOtherPassesThrough()
    {
        var classification = Classify("{c, plural, one {x}}");

        Assert.Equal(MessageKind.PassThrough, classification.Kind);
    }

    [Fact]
    public void AHoistRefusalPassesThroughWithTheReason()
    {
        var classification = Classify(
            "{count, plural, offset:1 other {# of {g, plural, one {a} other {b}}}}");

        Assert.Equal(MessageKind.PassThrough, classification.Kind);
        Assert.Contains("offset 1", classification.Reason);
        Assert.Null(classification.ErrorOffset);
    }

    [Fact]
    public void ADisabledKindIsInvisible()
    {
        var noCardinal = new ExpansionOptions { ExpandCardinal = false };

        Assert.Equal(MessageKind.NoIcu,
            Classify("{count, plural, one {a} other {b}}", noCardinal).Kind);

        // An enabled ordinal in the same message still expands.
        Assert.Equal(MessageKind.Expand,
            Classify("{count, plural, one {a} other {b}} and {p, selectordinal, one {x} other {y}}", noCardinal).Kind);
    }
}
