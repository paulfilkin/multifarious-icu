using Icu.Core;

namespace Icu.Core.Tests;

/// <summary>
/// What the parser rejects and how it reports it. The task contract passes an
/// unparseable message through untouched and reports the offset, so the exception's
/// position matters as much as the rejection itself.
/// </summary>
public class IcuParserErrorTests
{
    [Fact]
    public void AnUnmatchedClosingBraceIsRejectedAtItsOffset()
    {
        var exception = Assert.Throws<IcuParseException>(() => IcuMessage.Parse("oops }"));

        Assert.Equal(5, exception.Position);
        Assert.Contains("Unmatched '}'", exception.Message);
    }

    [Theory]
    [InlineData("{name")]
    [InlineData("{n, number")]
    [InlineData("{c, plural, other {x}")]
    [InlineData("{c, plural, other {x")]
    public void AnUnclosedConstructIsRejected(string source)
    {
        Assert.Throws<IcuParseException>(() => IcuMessage.Parse(source));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{, plural, other {x}}")]
    public void AnEmptyArgumentNameIsRejected(string source)
    {
        var exception = Assert.Throws<IcuParseException>(() => IcuMessage.Parse(source));

        Assert.Contains("argument name", exception.Message);
    }

    /// <summary>
    /// ICU only enforces 'other' at format time. This parser enforces it at parse
    /// time, because seeding falls back to the 'other' branch and a message without
    /// one cannot be expanded; better reported now than half-processed later.
    /// </summary>
    [Theory]
    [InlineData("{c, plural, one {x}}")]
    [InlineData("{n, selectordinal, one {x}}")]
    [InlineData("{g, select, female {x}}")]
    [InlineData("{c, plural, =0 {x} =1 {y}}")]
    public void ASelectorWithoutAnOtherBranchIsRejected(string source)
    {
        var exception = Assert.Throws<IcuParseException>(() => IcuMessage.Parse(source));

        Assert.Contains("'other'", exception.Message);
    }

    /// <summary>
    /// ICU tolerates duplicate branch keys; this parser rejects them, because two
    /// branches with one key would share a branch path and expansion keys on the path.
    /// </summary>
    [Fact]
    public void ADuplicateBranchKeyIsRejected()
    {
        var exception = Assert.Throws<IcuParseException>(
            () => IcuMessage.Parse("{c, plural, one {x} one {y} other {z}}"));

        Assert.Contains("Duplicate branch 'one'", exception.Message);
    }

    [Fact]
    public void AnExplicitValueInASelectIsRejected()
    {
        var exception = Assert.Throws<IcuParseException>(
            () => IcuMessage.Parse("{g, select, =1 {x} other {y}}"));

        Assert.Contains("'select'", exception.Message);
    }

    [Fact]
    public void AnOffsetInASelectIsRejected()
    {
        Assert.Throws<IcuParseException>(
            () => IcuMessage.Parse("{g, select, offset:1 other {y}}"));
    }

    [Fact]
    public void AnOffsetWithoutANumberIsRejected()
    {
        var exception = Assert.Throws<IcuParseException>(
            () => IcuMessage.Parse("{c, plural, offset: other {x}}"));

        Assert.Contains("offset", exception.Message);
    }

    [Fact]
    public void ABranchWithoutABodyIsRejected()
    {
        var exception = Assert.Throws<IcuParseException>(
            () => IcuMessage.Parse("{c, plural, one other {x}}"));

        Assert.Contains("'{' opening the branch", exception.Message);
    }

    [Fact]
    public void AnEmptyStyleIsRejected()
    {
        var exception = Assert.Throws<IcuParseException>(() => IcuMessage.Parse("{n, number, }"));

        Assert.Contains("argument style", exception.Message);
    }

    [Fact]
    public void AMissingSelectorBodyIsRejected()
    {
        var exception = Assert.Throws<IcuParseException>(() => IcuMessage.Parse("{c, plural}"));

        Assert.Contains("',' after 'plural'", exception.Message);
    }

    [Fact]
    public void TheReportedPositionPointsAtTheFailure()
    {
        // The branch keyword 'one' is fine; the failure is the missing '{' at the 'o'
        // of 'other', offset 16.
        var exception = Assert.Throws<IcuParseException>(
            () => IcuMessage.Parse("{c, plural, one other {x}}"));

        Assert.Equal(16, exception.Position);
        Assert.Contains("(offset 16)", exception.Message);
    }

    [Fact]
    public void TryParseReportsFailureWithoutThrowing()
    {
        var parsed = IcuParser.TryParse("oops }", out var message, out var error);

        Assert.False(parsed);
        Assert.Null(message);
        Assert.NotNull(error);
        Assert.Equal(5, error.Position);
    }

    [Fact]
    public void TryParseReturnsTheMessageOnSuccess()
    {
        var parsed = IcuParser.TryParse("Hello {name}", out var message, out var error);

        Assert.True(parsed);
        Assert.NotNull(message);
        Assert.Null(error);
        Assert.Equal(2, message.Nodes.Count);
    }
}
