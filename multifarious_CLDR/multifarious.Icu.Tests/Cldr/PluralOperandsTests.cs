using Icu.Cldr.Rules;

namespace Icu.Cldr.Tests;

/// <summary>
/// The operand computation, against the worked examples in UTS #35 part 5.
/// </summary>
/// <remarks>
/// The conformance suite already exercises this heavily, but only through the rules.
/// These cases pin the operands directly, so a failure says which operand is wrong
/// rather than which locale noticed.
/// </remarks>
public class PluralOperandsTests
{
    [Theory]
    // source            n            i          v  w  f    t   c
    [InlineData("1", "1", 1L, 0, 0, 0L, 0L, 0)]
    [InlineData("1.0", "1", 1L, 1, 0, 0L, 0L, 0)]
    [InlineData("1.00", "1", 1L, 2, 0, 0L, 0L, 0)]
    [InlineData("1.3", "1.3", 1L, 1, 1, 3L, 3L, 0)]
    [InlineData("1.03", "1.03", 1L, 2, 2, 3L, 3L, 0)]
    [InlineData("1.230", "1.23", 1L, 3, 2, 230L, 23L, 0)]
    [InlineData("1200000", "1200000", 1200000L, 0, 0, 0L, 0L, 0)]
    [InlineData("1.2c6", "1200000", 1200000L, 0, 0, 0L, 0L, 6)]
    [InlineData("123c6", "123000000", 123000000L, 0, 0, 0L, 0L, 6)]
    [InlineData("123c5", "12300000", 12300000L, 0, 0, 0L, 0L, 5)]
    [InlineData("1200.50", "1200.5", 1200L, 2, 1, 50L, 5L, 0)]
    [InlineData("1.20050c3", "1200.5", 1200L, 2, 1, 50L, 5L, 3)]
    [InlineData("0", "0", 0L, 0, 0, 0L, 0L, 0)]
    [InlineData("0.0", "0", 0L, 1, 0, 0L, 0L, 0)]
    public void OperandsMatchTheSpecification(
        string source, string n, long i, int v, int w, long f, long t, int c)
    {
        var operands = PluralOperands.Parse(source);

        Assert.Equal(decimal.Parse(n, System.Globalization.CultureInfo.InvariantCulture), operands.N);
        Assert.Equal(i, operands.I);
        Assert.Equal(v, operands.V);
        Assert.Equal(w, operands.W);
        Assert.Equal(f, operands.F);
        Assert.Equal(t, operands.T);
        Assert.Equal(c, operands.C);
    }

    /// <summary>
    /// <c>e</c> is a synonym for <c>c</c>. CLDR 48 writes <c>c</c> in its samples and
    /// <c>e</c> in its conditions, so both have to be read the same way.
    /// </summary>
    [Fact]
    public void TheExponentMayBeSpelledWithEitherLetter()
    {
        Assert.Equal(PluralOperands.Parse("1.2c6"), PluralOperands.Parse("1.2e6"));
    }

    /// <summary>
    /// The written form is what distinguishes these, not the value. This is the whole
    /// reason the operands are parsed from a string.
    /// </summary>
    [Fact]
    public void TrailingZerosAreVisibleToTheRules()
    {
        var bare = PluralOperands.Parse("1");
        var withFraction = PluralOperands.Parse("1.0");

        Assert.Equal(bare.N, withFraction.N);
        Assert.Equal(0, bare.V);
        Assert.Equal(1, withFraction.V);
        Assert.NotEqual(bare, withFraction);
    }

    [Fact]
    public void TheSignIsDroppedBecauseNIsAnAbsoluteValue()
    {
        Assert.Equal(PluralOperands.Parse("3"), PluralOperands.Parse("-3"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.")]
    [InlineData(".5")]
    [InlineData("1.2c")]
    [InlineData("1c")]
    [InlineData("c6")]
    [InlineData("1,5")]
    [InlineData("1 000")]
    [InlineData("99999999999999999999999")]
    public void MalformedNumbersAreRejectedRatherThanGuessedAt(string source)
    {
        Assert.False(PluralOperands.TryParse(source, out _));
        Assert.Throws<FormatException>(() => PluralOperands.Parse(source));
    }

    [Fact]
    public void AnIntegerCountHasNoVisibleFractionDigits()
    {
        var operands = PluralOperands.FromInteger(27);

        Assert.Equal(27m, operands.N);
        Assert.Equal(27L, operands.I);
        Assert.Equal(0, operands.V);
        Assert.Equal(0, operands.C);
    }
}
