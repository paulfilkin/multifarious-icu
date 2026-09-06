using Icu.Core;
using Icu.Core.Tests;
using multifarious.Icu.BatchTasks.Services;
using multifarious.Icu.Expansion;
using Sdl.FileTypeSupport.Framework.BilingualApi;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// The finalise processor over units the expansion processor produced: pruning to the target
/// language's set, filling, escaping and parity, with the native projection of the target
/// paragraph parsed and rendered against the source message for every count.
/// </summary>
public class IcuFinaliseProcessorTests
{
    private const string UnreadCount =
        "Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!";

    private const string Guests =
        "{count, plural, offset:1 =0 {Nobody is coming} =1 {Only you are coming} one {You and # other guest are coming} other {You and # other guests are coming}}.";

    private static IParagraphUnit Expanded(string value, string language = "ru-RU")
    {
        var unit = ParagraphUnits.Json(value, "key");
        new IcuExpandProcessor("en-GB", language, ExpansionOptions.Default, "1.0.0")
        {
            ItemFactory = ParagraphUnits.ItemFactory,
        }.ProcessParagraphUnit(unit);
        return unit;
    }

    private static IcuFinaliseProcessor Finaliser(string language, FinaliseOptions? options = null) =>
        new(language, options ?? FinaliseOptions.Default) { ItemFactory = ParagraphUnits.ItemFactory };

    private static List<ISegment> SegmentsOf(IParagraph paragraph) =>
        ParagraphUnits.ItemsOf(paragraph).OfType<ISegment>().ToList();

    private static string Projection(IParagraph paragraph) => RawValueReconstruction.Reconstruct(paragraph).RawValue;

    private static void AssertRendersLikeSource(string source, string projection, string cldrLanguage,
        Dictionary<string, string> arguments, params string[] counts)
    {
        var categories = CldrCategories.For(cldrLanguage);
        foreach (var count in counts)
        {
            arguments["count"] = count;
            Assert.Equal(
                MessageRenderer.Render(IcuMessage.Parse(source).Nodes, arguments, categories),
                MessageRenderer.Render(IcuMessage.Parse(projection).Nodes, arguments, categories));
        }
    }

    [Fact]
    public void Finalising_for_the_expanded_language_prunes_nothing_and_fills_the_empty_targets()
    {
        var unit = Expanded(UnreadCount);
        var finaliser = Finaliser("ru-RU");

        finaliser.ProcessParagraphUnit(unit);

        Assert.Equal(1, finaliser.Units);
        Assert.Equal(0, finaliser.Pruned);
        Assert.Equal(4, finaliser.Filled);
        Assert.Equal(4, SegmentsOf(unit.Source).Count);
        Assert.Equal(4, SegmentsOf(unit.Target).Count);
        Assert.Equal(4, finaliser.Warnings.Count);
        Assert.All(finaliser.Warnings, w => Assert.Contains("untranslated", w.Reason));

        // The warning joins the expansion comment on the source segment; the target segment
        // carries no comment at all, because target comments are the translator's.
        Assert.All(SegmentsOf(unit.Source), s => Assert.Equal(2, ((ICommentMarker)s[0]).Comments.Count));
        Assert.All(SegmentsOf(unit.Target), s => Assert.DoesNotContain(ParagraphUnits.ItemsOf(s), i => i is ICommentMarker));

        AssertRendersLikeSource(UnreadCount, Projection(unit.Target), "ru",
            new Dictionary<string, string> { ["name"] = "Anna" }, "1", "2", "5", "21", "0.5");
    }

    [Fact]
    public void Finalising_for_a_language_with_fewer_forms_prunes_both_paragraphs_to_its_set()
    {
        // Expanded for Russian, finalised as Japanese: three category triples go, from both sides.
        var unit = Expanded(UnreadCount);
        var finaliser = Finaliser("ja-JP");

        finaliser.ProcessParagraphUnit(unit);

        Assert.Equal(3, finaliser.Pruned);
        Assert.Single(SegmentsOf(unit.Source));
        Assert.Single(SegmentsOf(unit.Target));
        Assert.Equal(Projection(unit.Source), Projection(unit.Target));
        AssertRendersLikeSource(UnreadCount, Projection(unit.Target), "ja",
            new Dictionary<string, string> { ["name"] = "Anna" }, "0", "1", "2", "5", "1.5");
    }

    [Fact]
    public void Explicit_values_and_the_offset_survive_pruning()
    {
        var unit = Expanded(Guests);
        var finaliser = Finaliser("ja-JP");

        finaliser.ProcessParagraphUnit(unit);

        Assert.Equal(3, finaliser.Pruned);
        Assert.Equal(3, SegmentsOf(unit.Target).Count);
        var projection = Projection(unit.Target);
        Assert.StartsWith("{count, plural, offset:1 =0 {", projection);
        AssertRendersLikeSource(Guests, projection, "ja", new Dictionary<string, string>(), "0", "1", "2", "3", "22");
    }

    [Fact]
    public void Typed_apostrophes_and_braces_in_the_target_are_escaped_and_the_locked_syntax_is_not()
    {
        var unit = Expanded(UnreadCount);
        Finaliser("ru-RU").ProcessParagraphUnit(unit);

        // A translator types an apostrophe and a literal brace into the first target form.
        var target = SegmentsOf(unit.Target)[0];
        var text = ParagraphUnits.ItemsOf(target).OfType<IText>().First();
        text.Properties.Text = "It's {literal} ";

        Finaliser("ru-RU").ProcessParagraphUnit(unit);

        // The escaper doubles the apostrophe and quotes the stretch from the first syntax
        // character to the last as one literal.
        var projection = Projection(unit.Target);
        Assert.Contains("It''s '{literal}' ", projection);
        Assert.Contains("{count, plural,", projection);
        var parsed = IcuMessage.Parse(projection);
        Assert.Equal("It's {literal} Anna, you have 1 unread message!",
            MessageRenderer.Render(parsed.Nodes, new Dictionary<string, string> { ["name"] = "Anna", ["count"] = "1" },
                CldrCategories.For("ru")));
    }

    [Fact]
    public void Finalise_is_idempotent()
    {
        var unit = Expanded(UnreadCount);
        Finaliser("ru-RU").ProcessParagraphUnit(unit);
        var target = SegmentsOf(unit.Target)[0];
        var text = ParagraphUnits.ItemsOf(target).OfType<IText>().First();
        text.Properties.Text = "It's ";

        Finaliser("ru-RU").ProcessParagraphUnit(unit);
        var once = Projection(unit.Target);
        Finaliser("ru-RU").ProcessParagraphUnit(unit);
        var twice = Projection(unit.Target);

        Assert.Equal(once, twice);
        Assert.Contains("It''s ", once);
        Assert.DoesNotContain("''''", once);
    }

    [Fact]
    public void A_missing_protected_span_in_the_target_is_warned_about()
    {
        var unit = Expanded(UnreadCount);
        Finaliser("ru-RU").ProcessParagraphUnit(unit);
        var target = SegmentsOf(unit.Target)[0];
        var span = ParagraphUnits.ItemsOf(target).OfType<ILockedContent>().First();
        span.RemoveFromParent();

        var finaliser = Finaliser("ru-RU");
        finaliser.ProcessParagraphUnit(unit);

        Assert.Contains(finaliser.Warnings, w => w.Reason.Contains("missing from the target"));
        var sourceComments = ((ICommentMarker)SegmentsOf(unit.Source)[0][0]).Comments;
        Assert.Contains("Placeholder mismatch", sourceComments.GetItem(sourceComments.Count - 1).Text);
        Assert.DoesNotContain(ParagraphUnits.ItemsOf(target), i => i is ICommentMarker);
    }

    [Fact]
    public void A_missing_protected_span_fails_the_run_when_configured_to()
    {
        var unit = Expanded(UnreadCount);
        Finaliser("ru-RU").ProcessParagraphUnit(unit);
        var target = SegmentsOf(unit.Target)[0];
        ParagraphUnits.ItemsOf(target).OfType<ILockedContent>().First().RemoveFromParent();

        var finaliser = Finaliser("ru-RU",
            FinaliseOptions.Default with { OnPlaceholderMismatch = PlaceholderMismatchBehaviour.FailTask });

        Assert.Throws<FinaliseFailedException>(() => finaliser.ProcessParagraphUnit(unit));
    }

    [Fact]
    public void An_empty_target_fails_the_run_when_configured_to()
    {
        var unit = Expanded(UnreadCount);
        var finaliser = Finaliser("ru-RU", FinaliseOptions.Default with { OnEmptyBranch = EmptyBranchBehaviour.FailTask });

        Assert.Throws<FinaliseFailedException>(() => finaliser.ProcessParagraphUnit(unit));
    }

    [Fact]
    public void A_protected_argument_only_message_is_filled_and_escaped_without_plural_rules()
    {
        var unit = Expanded("Hello {name}, welcome back!");
        var finaliser = Finaliser("ru-RU");

        finaliser.ProcessParagraphUnit(unit);

        Assert.Equal(0, finaliser.Pruned);
        Assert.Equal(1, finaliser.Filled);
        Assert.Equal("Hello {name}, welcome back!", Projection(unit.Target));

        // '#' is plain text outside a plural and must not be quoted.
        var text = ParagraphUnits.ItemsOf(SegmentsOf(unit.Target)[0]).OfType<IText>().First();
        text.Properties.Text = "Item #1 for ";
        Finaliser("ru-RU").ProcessParagraphUnit(unit);
        Assert.StartsWith("Item #1 for {name}", Projection(unit.Target));
    }

    [Fact]
    public void A_unit_the_expansion_never_touched_is_left_alone()
    {
        var unit = ParagraphUnits.Json("Message Centre", "key");
        var finaliser = Finaliser("ru-RU");

        finaliser.ProcessParagraphUnit(unit);

        Assert.Equal(0, finaliser.Units);
        Assert.Equal("Message Centre", Projection(unit.Source));
    }

    [Fact]
    public void A_walked_plural_kind_is_kept_whole_and_escaped_whatever_the_language()
    {
        // Over the branch budget for Arabic, so laid out walked: the source's one and other
        // branches are the developer's, and finalise keeps them even for a language with fewer
        // forms, because nothing is recorded as expanded.
        const string sync =
            "{files, plural, one {# file} other {# files}} synchronised across " +
            "{devices, plural, one {# device} other {# devices}}.";
        var unit = Expanded(sync, "ar-SA");
        Assert.Equal(4, SegmentsOf(unit.Source).Count);

        var finaliser = Finaliser("ja-JP");
        finaliser.ProcessParagraphUnit(unit);

        Assert.Equal(0, finaliser.Pruned);
        Assert.Equal(4, finaliser.Filled);
        Assert.Equal(4, SegmentsOf(unit.Target).Count);
        Assert.Equal(Projection(unit.Source), Projection(unit.Target));
        Assert.Equal("1 file synchronised across 2 devices.",
            MessageRenderer.Render(IcuMessage.Parse(Projection(unit.Target)).Nodes,
                new Dictionary<string, string> { ["files"] = "1", ["devices"] = "2" }, CldrCategories.For("en")));

        // A typed apostrophe in a walked branch is escaped like any other.
        var text = ParagraphUnits.ItemsOf(SegmentsOf(unit.Target)[0]).OfType<IText>().First();
        text.Properties.Text = "It's ";
        Finaliser("ja-JP").ProcessParagraphUnit(unit);
        Assert.Contains("It''s ", Projection(unit.Target));
    }
}
