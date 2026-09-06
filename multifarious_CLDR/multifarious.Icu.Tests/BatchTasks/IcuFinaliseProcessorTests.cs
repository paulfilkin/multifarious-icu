using Icu.Core;
using Icu.Core.Tests;
using multifarious.Icu.BatchTasks;
using multifarious.Icu.BatchTasks.Services;
using multifarious.Icu.Expansion;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

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

    private static List<ISegment> SegmentsOf(IParagraph paragraph) => ParagraphUnits.SegmentsOf(paragraph);

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

        // Each filled target carries the finding as a medium comment by the plugin; the source
        // and the unit carry nothing.
        foreach (var target in SegmentsOf(unit.Target))
        {
            var marker = Assert.IsAssignableFrom<ICommentMarker>(target[0]);
            var comment = marker.Comments.GetItem(0);
            Assert.Contains("untranslated", comment.Text);
            Assert.Equal(Constants.CommentAuthor, comment.Author);
            Assert.Equal(Severity.Medium, comment.Severity);
        }
        Assert.True(ParagraphUnits.NoSegmentComments(unit.Source));
        Assert.Empty(ParagraphUnits.UnitComments(unit));

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
        var text = ParagraphUnits.ContentOf(target).OfType<IText>().First();
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
        var text = ParagraphUnits.ContentOf(target).OfType<IText>().First();
        text.Properties.Text = "It's ";

        Finaliser("ru-RU").ProcessParagraphUnit(unit);
        var once = Projection(unit.Target);
        Finaliser("ru-RU").ProcessParagraphUnit(unit);
        var twice = Projection(unit.Target);

        Assert.Equal(once, twice);
        Assert.Contains("It''s ", once);
        Assert.DoesNotContain("''''", once);
    }

    private static IParagraphUnit ExpandedLocked(string value)
    {
        var unit = ParagraphUnits.Json(value, "key");
        new IcuExpandProcessor("en-GB", "ru-RU", ExpansionOptions.Default with { LockPlaceholders = true }, "1.0.0")
        {
            ItemFactory = ParagraphUnits.ItemFactory,
        }.ProcessParagraphUnit(unit);
        return unit;
    }

    private static IPlaceholderTag Tag(string syntax)
    {
        var properties = ParagraphUnits.PropertiesFactory.CreatePlaceholderTagProperties(syntax);
        properties.DisplayText = syntax;
        return ParagraphUnits.ItemFactory.CreatePlaceholderTag(properties);
    }

    private static int TagsUnder(IAbstractMarkupDataContainer container)
    {
        var count = 0;
        for (var i = 0; i < container.Count; i++)
        {
            if (container[i] is IPlaceholderTag) count++;
            var locked = container[i] as ILockedContent;
            if (locked != null) count += TagsUnder(locked.Content);
            else if (container[i] is IAbstractMarkupDataContainer nested) count += TagsUnder(nested);
        }
        return count;
    }

    /// <summary>
    /// The expansion writes placeholder tags inside the segments; Studio's JSON writer emits
    /// none of them. Finalise turns every tag, bare in the target or locked in the source, into
    /// locked text on both sides, the projection is unchanged, and a second run finds nothing
    /// left to do.
    /// </summary>
    [Fact]
    public void Placeholder_tags_become_locked_text_on_both_sides_and_a_second_run_changes_nothing()
    {
        var unit = Expanded(UnreadCount);
        var targets = SegmentsOf(unit.Target);
        targets[0].Add(ParagraphUnits.ItemFactory.CreateText(ParagraphUnits.PropertiesFactory.CreateTextProperties("Привет, ")));
        targets[0].Add(Tag("{name}"));
        targets[0].Add(ParagraphUnits.ItemFactory.CreateText(ParagraphUnits.PropertiesFactory.CreateTextProperties(", у вас ")));
        targets[0].Add(Tag("#"));
        targets[0].Add(ParagraphUnits.ItemFactory.CreateText(ParagraphUnits.PropertiesFactory.CreateTextProperties(" сообщение!")));
        Assert.True(TagsUnder(unit.Source) > 0);

        var finaliser = Finaliser("ru-RU");
        finaliser.ProcessParagraphUnit(unit);
        var once = Projection(unit.Target);

        Assert.DoesNotContain(finaliser.Warnings, w => w.Reason.Contains("Placeholder mismatch"));
        Assert.Equal(0, TagsUnder(unit.Source));
        Assert.Equal(0, TagsUnder(unit.Target));
        Assert.Equal(["{name}", "#"],
            ParagraphUnits.ContentOf(SegmentsOf(unit.Target)[0]).OfType<ILockedContent>()
                .Select(l => RawValueReconstruction.Reconstruct(l.Content).RawValue));
        Assert.Contains("one {Привет, {name}, у вас # сообщение!}", once);
        Assert.Equal("Привет, Anna, у вас 1 сообщение!",
            MessageRenderer.Render(IcuMessage.Parse(once).Nodes,
                new Dictionary<string, string> { ["name"] = "Anna", ["count"] = "1" }, CldrCategories.For("ru")));

        Finaliser("ru-RU").ProcessParagraphUnit(unit);
        Assert.Equal(once, Projection(unit.Target));
    }

    [Fact]
    public void Locked_placeholder_tags_flatten_the_same_way()
    {
        var unit = ExpandedLocked(UnreadCount);
        Assert.True(TagsUnder(unit.Source) > 0);
        Assert.NotEmpty(ParagraphUnits.ContentOf(SegmentsOf(unit.Source)[0]).OfType<ILockedContent>());

        Finaliser("ru-RU").ProcessParagraphUnit(unit);

        Assert.Equal(0, TagsUnder(unit.Source));
        Assert.Equal(0, TagsUnder(unit.Target));
        AssertRendersLikeSource(UnreadCount, Projection(unit.Target), "ru", new Dictionary<string, string> { ["name"] = "Anna" }, "1", "2", "5");
    }

    /// <summary>A tag in the target matches a locked span in the source and the other way round: the syntax is the key.</summary>
    [Fact]
    public void Parity_is_by_syntax_whichever_construct_carries_it()
    {
        var unit = Expanded(UnreadCount);
        Finaliser("ru-RU").ProcessParagraphUnit(unit);
        var target = SegmentsOf(unit.Target)[0];
        IAbstractMarkupDataContainer container = ParagraphUnits.CommentOf(target) ?? (IAbstractMarkupDataContainer)target;
        var span = ParagraphUnits.ContentOf(target).OfType<ILockedContent>().First();
        var index = container.IndexOf(span);
        container.RemoveAt(index);
        container.Insert(index, Tag("{name}"));

        var finaliser = Finaliser("ru-RU");
        finaliser.ProcessParagraphUnit(unit);

        Assert.Empty(finaliser.Warnings);
        Assert.Equal(0, TagsUnder(unit.Target));
    }

    private static void CopySourceToTarget(ISegment source, ISegment target)
    {
        target.Clear();
        foreach (var item in ParagraphUnits.ItemsOf(source))
        {
            target.Add((IAbstractMarkupData)item.Clone());
        }
    }

    private static void WrapInPluginComment(ISegment segment)
    {
        var comment = ParagraphUnits.PropertiesFactory.CreateComment("CLDR category: one", Constants.CommentAuthor, Severity.Low);
        var properties = ParagraphUnits.PropertiesFactory.CreateCommentProperties();
        properties.Add(comment);
        var marker = ParagraphUnits.ItemFactory.CreateCommentMarker(properties);
        var content = ParagraphUnits.ItemsOf(segment);
        segment.Clear();
        foreach (var item in content) marker.Add(item);
        segment.Add(marker);
    }

    private static ICommentMarker TranslatorComment(string text)
    {
        var comment = ParagraphUnits.PropertiesFactory.CreateComment(text, "Anna", Severity.Low);
        var properties = ParagraphUnits.PropertiesFactory.CreateCommentProperties();
        properties.Add(comment);
        return ParagraphUnits.ItemFactory.CreateCommentMarker(properties);
    }

    /// <summary>
    /// Studio's pseudo-translation and Copy Source to Target copy the source segment's content
    /// with the plugin's comment marker in it. Finalise takes the plugin's comments out of the
    /// targets and leaves the translator's own, whether in a marker of their own or sharing one.
    /// </summary>
    [Fact]
    public void The_plugins_comments_copied_into_targets_are_removed_and_the_translators_stay()
    {
        // A file expanded before the notes moved to the unit comment: the source segments carry
        // the plugin's marker, and Studio's copy put it into the targets.
        var unit = Expanded(UnreadCount);
        var sources = SegmentsOf(unit.Source);
        var targets = SegmentsOf(unit.Target);
        foreach (var source in sources) WrapInPluginComment(source);
        for (var i = 0; i < targets.Count; i++) CopySourceToTarget(sources[i], targets[i]);
        Assert.All(targets, t => Assert.IsAssignableFrom<ICommentMarker>(t[0]));

        // Segment 2: the translator's own marker around a word, inside the copied marker.
        var copied = (ICommentMarker)targets[1][0];
        var note = TranslatorComment("check the case");
        note.Add(ParagraphUnits.ItemFactory.CreateText(ParagraphUnits.PropertiesFactory.CreateTextProperties(" NB")));
        copied.Add(note);

        // Segment 3: the translator added a comment to the copied marker itself.
        ((ICommentMarker)targets[2][0]).Comments.Add(
            ParagraphUnits.PropertiesFactory.CreateComment("query for the client", "Anna", Severity.Low));

        var finaliser = Finaliser("ru-RU");
        finaliser.ProcessParagraphUnit(unit);

        Assert.Equal(4, finaliser.CommentsStripped);
        Assert.Empty(finaliser.Warnings);
        targets = SegmentsOf(unit.Target);

        Assert.DoesNotContain(ParagraphUnits.ContentOf(targets[0]), i => i is ICommentMarker);
        Assert.Equal(["{name}", "#"],
            ParagraphUnits.ContentOf(targets[0]).OfType<ILockedContent>().Select(l => RawValueReconstruction.Reconstruct(l.Content).RawValue));

        var kept = Assert.Single(ParagraphUnits.ContentOf(targets[1]).OfType<ICommentMarker>());
        Assert.Equal("check the case", kept.Comments.GetItem(0).Text);

        var shared = Assert.IsAssignableFrom<ICommentMarker>(targets[2][0]);
        Assert.Equal(1, shared.Comments.Count);
        Assert.Equal("query for the client", shared.Comments.GetItem(0).Text);

        // The source keeps its comments, and the projection still parses with the note's text in place.
        Assert.All(SegmentsOf(unit.Source), s => Assert.NotNull(ParagraphUnits.CommentOf(s)));
        Assert.Contains("unread messages! NB}", Projection(unit.Target));
        Assert.True(IcuParser.TryParse(Projection(unit.Target), out _, out _));

        Finaliser("ru-RU").ProcessParagraphUnit(unit);
        Assert.Equal(1, Assert.IsAssignableFrom<ICommentMarker>(SegmentsOf(unit.Target)[2][0]).Comments.Count);
    }

    /// <summary>A finding written on a target is removed by the next run, so a corrected file comes out clean and a stale warning never lingers.</summary>
    [Fact]
    public void A_finding_on_a_target_is_cleared_by_the_next_run()
    {
        var unit = Expanded(UnreadCount);
        Finaliser("ru-RU").ProcessParagraphUnit(unit);
        Assert.All(SegmentsOf(unit.Target), t => Assert.NotNull(ParagraphUnits.CommentOf(t)));

        var again = Finaliser("ru-RU");
        again.ProcessParagraphUnit(unit);

        // Filled from the source on the first run, so translated now: no finding, no comment.
        Assert.Empty(again.Warnings);
        Assert.True(ParagraphUnits.NoSegmentComments(unit.Target));
        Assert.True(ParagraphUnits.NoSegmentComments(unit.Source));
        Assert.Empty(ParagraphUnits.UnitComments(unit));
    }

    /// <summary>
    /// A message with a placeholder mismatch is warned about and left editable: its spans are
    /// tags on both sides, and locked spans from the earlier run are turned back into tags, so
    /// the missing one can be placed with QuickPlace (Project 52). Once corrected, the next run
    /// locks the message as usual.
    /// </summary>
    [Fact]
    public void A_missing_protected_span_in_the_target_is_warned_about_and_the_message_stays_editable()
    {
        var unit = Expanded(UnreadCount);
        Finaliser("ru-RU").ProcessParagraphUnit(unit);
        Assert.Equal(0, TagsUnder(unit.Target));
        var target = SegmentsOf(unit.Target)[0];
        var span = ParagraphUnits.ContentOf(target).OfType<ILockedContent>().First();
        span.RemoveFromParent();

        var finaliser = Finaliser("ru-RU");
        finaliser.ProcessParagraphUnit(unit);

        Assert.Contains(finaliser.Warnings, w => w.Reason.Contains("missing from the target"));
        Assert.Contains(finaliser.Warnings, w => w.Reason.Contains("run ICU Finalise Messages again"));

        // The finding sits on the target segment as a high-severity comment; the others are clear.
        var targets = SegmentsOf(unit.Target);
        var marker = Assert.IsAssignableFrom<ICommentMarker>(targets[0][0]);
        Assert.StartsWith("Placeholder mismatch", marker.Comments.GetItem(0).Text);
        Assert.Contains("run ICU Finalise Messages again", marker.Comments.GetItem(0).Text);
        Assert.Equal(Severity.High, marker.Comments.GetItem(0).Severity);
        Assert.All(targets.Skip(1), t => Assert.Null(ParagraphUnits.CommentOf(t)));
        Assert.Empty(ParagraphUnits.UnitComments(unit));

        // Editable shape on both sides: tags, no locked span inside any segment.
        Assert.Equal(8, TagsUnder(unit.Source));
        Assert.Equal(7, TagsUnder(unit.Target));
        Assert.All(SegmentsOf(unit.Source), s => Assert.Empty(ParagraphUnits.ItemsOf(s).OfType<ILockedContent>()));
        Assert.All(SegmentsOf(unit.Target), s => Assert.Empty(ParagraphUnits.ContentOf(s).OfType<ILockedContent>()));
        Assert.Equal("{name}", ParagraphUnits.ItemsOf(SegmentsOf(unit.Source)[0]).OfType<IPlaceholderTag>().First().Properties.TagContent);

        // The translator places the tag; the next run clears the comment and locks the message.
        marker.Insert(1, Tag("{name}"));
        var again = Finaliser("ru-RU");
        again.ProcessParagraphUnit(unit);

        Assert.DoesNotContain(again.Warnings, w => w.Reason.Contains("Placeholder mismatch"));
        Assert.True(ParagraphUnits.NoSegmentComments(unit.Target));
        Assert.Equal(0, TagsUnder(unit.Source));
        Assert.Equal(0, TagsUnder(unit.Target));
        AssertRendersLikeSource(UnreadCount, Projection(unit.Target), "ru", new Dictionary<string, string> { ["name"] = "Anna" }, "1", "2", "5");
    }

    [Fact]
    public void A_mismatch_on_a_freshly_expanded_message_leaves_its_tags_in_place()
    {
        var unit = Expanded(UnreadCount);
        var targets = SegmentsOf(unit.Target);
        targets[0].Add(ParagraphUnits.ItemFactory.CreateText(ParagraphUnits.PropertiesFactory.CreateTextProperties("Привет, у вас # сообщение!")));

        var finaliser = Finaliser("ru-RU");
        finaliser.ProcessParagraphUnit(unit);

        Assert.Contains(finaliser.Warnings, w => w.Reason.Contains("Placeholder mismatch"));
        Assert.Equal(8, TagsUnder(unit.Source));
        Assert.Equal(6, TagsUnder(unit.Target));
        Assert.All(SegmentsOf(unit.Source), s => Assert.Empty(ParagraphUnits.ItemsOf(s).OfType<ILockedContent>()));
    }

    [Fact]
    public void A_missing_protected_span_fails_the_run_when_configured_to()
    {
        var unit = Expanded(UnreadCount);
        Finaliser("ru-RU").ProcessParagraphUnit(unit);
        var target = SegmentsOf(unit.Target)[0];
        ParagraphUnits.ContentOf(target).OfType<ILockedContent>().First().RemoveFromParent();

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
        var text = ParagraphUnits.ContentOf(SegmentsOf(unit.Target)[0]).OfType<IText>().First();
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
        var text = ParagraphUnits.ContentOf(SegmentsOf(unit.Target)[0]).OfType<IText>().First();
        text.Properties.Text = "It's ";
        Finaliser("ja-JP").ProcessParagraphUnit(unit);
        Assert.Contains("It''s ", Projection(unit.Target));
    }

    [Fact]
    public void The_processor_records_an_outcome_per_unit_for_the_report()
    {
        // Expanded for Russian, finalised as Japanese: three forms pruned, one segment filled.
        var unit = Expanded(UnreadCount);
        var finaliser = Finaliser("ja-JP");

        finaliser.ProcessParagraphUnit(unit);

        var outcome = Assert.Single(finaliser.Outcomes);
        Assert.Equal("key", outcome.Key);
        Assert.Equal(1, outcome.Segments);
        Assert.Equal(3, outcome.Pruned);
        Assert.Equal(1, outcome.Filled);
        var warning = Assert.Single(outcome.Warnings);
        Assert.StartsWith("Segment ", warning);
        Assert.Contains("untranslated", warning);
    }
}
