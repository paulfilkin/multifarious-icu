using Icu.Core;
using Icu.Core.Tree;

namespace Icu.Core.Tests;

/// <summary>
/// What structural equality does and does not see: content, not source spelling.
/// </summary>
public class MessageComparerTests
{
    private static bool Equal(string left, string right) =>
        MessageComparer.StructurallyEqual(IcuMessage.Parse(left), IcuMessage.Parse(right));

    /// <summary>
    /// "don't" parses to one text node and "don''t" to three; they say the same thing
    /// and compare equal, which is what lets the serialiser escape freely.
    /// </summary>
    [Fact]
    public void SplitAndWholeTextRunsCompareEqual()
    {
        Assert.True(Equal("don't", "don''t"));
    }

    [Fact]
    public void QuotedAndUnquotedSpellingsOfTheSameContentCompareEqual()
    {
        Assert.True(Equal("It''s", "It's"));
    }

    [Theory]
    [InlineData("{name}", "{title}")]
    [InlineData("{n, number}", "{n, date}")]
    [InlineData("{n, number, short}", "{n, number}")]
    [InlineData("Hello", "Hello!")]
    public void DifferentContentComparesUnequal(string left, string right)
    {
        Assert.False(Equal(left, right));
    }

    [Fact]
    public void PluralAndSelectordinalWithTheSameBranchesCompareUnequal()
    {
        Assert.False(Equal(
            "{n, plural, one {x} other {y}}",
            "{n, selectordinal, one {x} other {y}}"));
    }

    [Fact]
    public void DifferentBranchContentComparesUnequal()
    {
        Assert.False(Equal(
            "{n, plural, one {x} other {y}}",
            "{n, plural, one {x} other {z}}"));
    }

    /// <summary>Explicit values compare by number: =0 and =0.0 match the same counts.</summary>
    [Fact]
    public void ExplicitValueSpellingsCompareByNumber()
    {
        Assert.True(Equal(
            "{n, plural, =0 {x} other {y}}",
            "{n, plural, =0.0 {x} other {y}}"));
    }

    [Fact]
    public void AnExplicitValueAndAKeywordAreNeverEqual()
    {
        Assert.False(Equal(
            "{n, plural, =1 {x} other {y}}",
            "{n, plural, one {x} other {y}}"));
    }

    /// <summary>An offset of zero selects and renders exactly as no offset does.</summary>
    [Fact]
    public void OffsetZeroComparesEqualToAbsentOffset()
    {
        Assert.True(Equal(
            "{n, plural, offset:0 one {x} other {y}}",
            "{n, plural, one {x} other {y}}"));

        Assert.False(Equal(
            "{n, plural, offset:1 one {x} other {y}}",
            "{n, plural, one {x} other {y}}"));
    }

    [Fact]
    public void SpanDifferencesAreIgnored()
    {
        var left = new MessageNode[] { new ArgumentNode("name", new SourceSpan(0, 6)) };
        var right = new MessageNode[] { new ArgumentNode("name", new SourceSpan(40, 6)) };

        Assert.True(MessageComparer.StructurallyEqual(left, right));
    }

    [Fact]
    public void EmptyTextNodesAreInvisible()
    {
        var left = new MessageNode[] { new TextNode("", default), new PoundNode(default) };
        var right = new MessageNode[] { new PoundNode(default) };

        Assert.True(MessageComparer.StructurallyEqual(left, right));
    }
}
