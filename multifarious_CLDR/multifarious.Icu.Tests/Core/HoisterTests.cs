using Icu.Core;
using Icu.Core.Tree;

namespace Icu.Core.Tests;

/// <summary>
/// Hoisting, directed: the design's worked example, distribution into each selector
/// type, nesting order, and the '#' rebinding rule with its refusal case.
/// </summary>
public class HoisterTests
{
    private static string Hoisted(string source) =>
        IcuSerialiser.Serialise(Hoister.Hoist(IcuMessage.Parse(source).Nodes));

    /// <summary>Section 4 of the design, exactly.</summary>
    [Fact]
    public void TheWorkedExampleHoistsToWholeSentences()
    {
        Assert.Equal(
            "{count, plural, " +
            "one {Hello {name}, you have # unread message!} " +
            "other {Hello {name}, you have # unread messages!}}",
            Hoisted("Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!"));
    }

    [Fact]
    public void AMessageWithoutSelectorsComesBackUnchanged()
    {
        var message = IcuMessage.Parse("Hello {name}!");

        Assert.Same(message, Hoister.Hoist(message));
    }

    [Fact]
    public void TextAroundASelectIsDistributedIntoItsBranches()
    {
        Assert.Equal(
            "{gender, select, female {You chose her.} other {You chose them.}}",
            Hoisted("You chose {gender, select, female {her} other {them}}."));
    }

    /// <summary>
    /// Two independent plurals multiply: the first becomes the outer selector and the
    /// second is hoisted within each of its branches, leaves carrying whole sentences.
    /// </summary>
    [Fact]
    public void TwoIndependentPluralsNestInDocumentOrder()
    {
        Assert.Equal(
            "{a, plural, " +
            "one {{b, plural, one {A and B} other {A and Bs}}} " +
            "other {{b, plural, one {As and B} other {As and Bs}}}}",
            Hoisted("{a, plural, one {A} other {As}} and {b, plural, one {B} other {Bs}}"));
    }

    [Fact]
    public void APluralInsideASelectBranchHoistsWithinThatBranch()
    {
        Assert.Equal(
            "{g, select, a {{c, plural, one {Hi. #} other {Hi. #s}}} other {Hi. none}}",
            Hoisted("Hi. {g, select, a {{c, plural, one {#} other {#s}}} other {none}}"));
    }

    /// <summary>
    /// The agreed rebinding rule: a '#' pushed inside another plural's scope is
    /// rewritten to an explicit argument naming the plural it was bound to, which is
    /// faithful because that plural's offset is zero.
    /// </summary>
    [Fact]
    public void APoundCrossingIntoAnotherPluralIsRewrittenToAnArgument()
    {
        Assert.Equal(
            "{count, plural, other {{folders, plural, " +
            "one {{count, number} items in # folder} " +
            "other {{count, number} items in # folders}}}}",
            Hoisted("{count, plural, other {# items in {folders, plural, one {# folder} other {# folders}}}}"));
    }

    /// <summary>
    /// The refusal half of the rule: with a non-zero offset there is no ICU spelling
    /// of what '#' renders, so the message is refused rather than silently changed.
    /// </summary>
    [Fact]
    public void APoundWithANonZeroOffsetCrossingScopeRefusesToHoist()
    {
        var exception = Assert.Throws<IcuHoistException>(() =>
            Hoister.Hoist(IcuMessage.Parse(
                "{count, plural, offset:1 other {# of {g, plural, one {a} other {b}}}}").Nodes));

        Assert.Contains("offset 1", exception.Message);
        Assert.Contains("count", exception.Message);
    }

    /// <summary>
    /// A '#' with a non-zero offset that stays inside its own plural's branch crosses
    /// nothing and hoists fine.
    /// </summary>
    [Fact]
    public void APoundWithANonZeroOffsetNotCrossingScopeHoistsFine()
    {
        Assert.Equal(
            "{count, plural, offset:1 one {You and # other.} other {You and # others.}}",
            Hoisted("You and {count, plural, offset:1 one {# other} other {# others}}."));
    }

    /// <summary>
    /// '#' binds straight through a select, so a '#' inside a select branch that
    /// hoisting pushes into another plural's scope is rewritten just as a bare one
    /// is. Found by the render-equality property before this test pinned it.
    /// </summary>
    [Fact]
    public void APoundInsideASelectCrossingIntoAPluralIsRewritten()
    {
        const string source =
            "{count, plural, other {{items, plural, one {# item} other {# items}} - {g, select, a {#} other {x}}}}";
        var message = IcuMessage.Parse(source);

        var hoisted = Hoister.Hoist(message.Nodes);

        Assert.Contains("{count, number}", IcuSerialiser.Serialise(hoisted));

        var arguments = new Dictionary<string, string>
        {
            ["count"] = "5",
            ["items"] = "1",
            ["g"] = "a"
        };
        var categories = CldrCategories.For("en");
        Assert.Equal("1 item - 5", MessageRenderer.Render(message.Nodes, arguments, categories));
        Assert.Equal("1 item - 5", MessageRenderer.Render(hoisted, arguments, categories));
    }

    [Fact]
    public void APoundInsideASelectCrossingScopeUnderANonZeroOffsetRefuses()
    {
        Assert.Throws<IcuHoistException>(() =>
            Hoister.Hoist(IcuMessage.Parse(
                "{count, plural, offset:1 other {{items, plural, one {x} other {y}} {g, select, a {#} other {b}}}}").Nodes));
    }

    /// <summary>
    /// The message-level overload rebuilds from its own canonical serialisation, so
    /// source text and spans are consistent on the result.
    /// </summary>
    [Fact]
    public void AHoistedMessageCarriesConsistentSourceAndSpans()
    {
        var hoisted = Hoister.Hoist(IcuMessage.Parse(
            "Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!"));

        Assert.Equal(IcuSerialiser.Serialise(hoisted), hoisted.Source);
        SpanAssert.Valid(hoisted);
    }

    [Fact]
    public void HoistingTwiceChangesNothing()
    {
        var once = Hoister.Hoist(IcuMessage.Parse(
            "Hi. {g, select, a {{c, plural, one {#} other {#s}}} other {none}}").Nodes);
        var twice = Hoister.Hoist(once);

        Assert.True(MessageComparer.StructurallyEqual(once, twice));
    }
}
