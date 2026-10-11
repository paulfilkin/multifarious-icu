using Icu.Core;
using Icu.Core.Tests;
using multifarious.Icu.BatchTasks;
using multifarious.Icu.BatchTasks.Services;
using multifarious.Icu.Expansion;
using Sdl.Core.Globalization;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// The restore processor over units the expansion processor produced: pruning to the target
/// language's set, filling, escaping and parity, then the collapse to one plain segment per
/// side, with the native projection of the target paragraph parsed and rendered against the
/// source message for every count. The round trip through the expansion processor and back is
/// here too, since restore and expand are the two halves of one movement.
/// </summary>
public class IcuRestoreProcessorTests
{
    private const string UnreadCount =
        "Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!";

    private const string Guests =
        "{count, plural, offset:1 =0 {Nobody is coming} =1 {Only you are coming} one {You and # other guest are coming} other {You and # other guests are coming}}.";

    private static IcuExpandProcessor Expander(string language = "ru-RU", ExpansionOptions? options = null) =>
        new("en-GB", language, options ?? ExpansionOptions.Default, "1.0.0") { ItemFactory = ParagraphUnits.ItemFactory };

    private static IParagraphUnit Expanded(string value, string language = "ru-RU")
    {
        var unit = ParagraphUnits.Json(value, "key");
        Expander(language).ProcessParagraphUnit(unit);
        return unit;
    }

    private static IcuRestoreProcessor Restorer(string language, RestoreOptions? options = null) =>
        new(language, options ?? RestoreOptions.Default) { ItemFactory = ParagraphUnits.ItemFactory };

    private static List<ISegment> SegmentsOf(IParagraph paragraph) => ParagraphUnits.SegmentsOf(paragraph);

    private static string Projection(IParagraph paragraph) => RawValueReconstruction.Reconstruct(paragraph).RawValue;

    private static IContextInfo IcuContext(IParagraphUnit unit) => ResourceKey.IcuContextOf(unit)!;

    private static IText Text(string value) =>
        ParagraphUnits.ItemFactory.CreateText(ParagraphUnits.PropertiesFactory.CreateTextProperties(value));

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

    private static int LockedUnder(IAbstractMarkupDataContainer container)
    {
        var count = 0;
        for (var i = 0; i < container.Count; i++)
        {
            if (container[i] is ILockedContent) count++;
            else if (container[i] is IAbstractMarkupDataContainer nested) count += LockedUnder(nested);
        }
        return count;
    }

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

    /// <summary>One plain segment on each side: no tag, no locked span, and the unit says restored.</summary>
    private static void AssertRestoredShape(IParagraphUnit unit)
    {
        Assert.Single(SegmentsOf(unit.Source));
        Assert.Single(SegmentsOf(unit.Target));
        Assert.IsAssignableFrom<ISegment>(Assert.Single(ParagraphUnits.ItemsOf(unit.Source)));
        Assert.IsAssignableFrom<ISegment>(Assert.Single(ParagraphUnits.ItemsOf(unit.Target)));
        Assert.Equal(0, TagsUnder(unit.Source));
        Assert.Equal(0, TagsUnder(unit.Target));
        Assert.Equal(0, LockedUnder(unit.Source));
        Assert.Equal(0, LockedUnder(unit.Target));
        Assert.True(ResourceKey.IsRestored(unit));
        Assert.False(ResourceKey.IsExpanded(unit));
    }

    /// <summary>The Russian forms of the unread-count message typed into the expanded target segments, text and tags.</summary>
    private static void TranslateUnreadCount(IParagraphUnit unit)
    {
        var forms = new[]
        {
            new[] { "Привет, ", "{name}", ", у вас ", "#", " непрочитанное сообщение!" },
            new[] { "Привет, ", "{name}", ", у вас ", "#", " непрочитанных сообщения!" },
            new[] { "Привет, ", "{name}", ", у вас ", "#", " непрочитанных сообщений!" },
            new[] { "Привет, ", "{name}", ", у вас ", "#", " непрочитанного сообщения!" },
        };
        var targets = SegmentsOf(unit.Target);
        for (var i = 0; i < targets.Count; i++)
        {
            targets[i].Clear();
            foreach (var piece in forms[i])
            {
                targets[i].Add(piece == "{name}" || piece == "#" ? (IAbstractMarkupData)Tag(piece) : Text(piece));
            }
        }
    }

    // ---- the checks, as before ---------------------------------------------------------------

    [Fact]
    public void Restoring_for_the_expanded_language_prunes_nothing_and_fills_the_empty_targets()
    {
        var unit = Expanded(UnreadCount);
        var restorer = Restorer("ru-RU");

        restorer.ProcessParagraphUnit(unit);

        Assert.Equal(1, restorer.Units);
        Assert.Equal(0, restorer.Pruned);
        Assert.Equal(4, restorer.Filled);
        Assert.Equal(1, restorer.Restored);
        Assert.Equal(4, restorer.Warnings.Count);
        Assert.All(restorer.Warnings, w => Assert.Contains("untranslated", w.Reason));
        AssertRestoredShape(unit);

        // Each filled form carries its finding as a medium comment by the plugin, around that
        // form's part of the restored text; the source and the unit carry nothing.
        var markers = ParagraphUnits.ItemsOf(SegmentsOf(unit.Target)[0]).OfType<ICommentMarker>().ToList();
        Assert.Equal(4, markers.Count);
        foreach (var marker in markers)
        {
            var comment = marker.Comments.GetItem(0);
            Assert.Contains("untranslated", comment.Text);
            Assert.Equal(Constants.CommentAuthor, comment.Author);
            Assert.Equal(Severity.Medium, comment.Severity);
            Assert.Contains("unread message", RawValueReconstruction.Reconstruct(marker).RawValue);
        }
        Assert.True(ParagraphUnits.NoSegmentComments(unit.Source));
        Assert.Empty(ParagraphUnits.UnitComments(unit));

        AssertRendersLikeSource(UnreadCount, Projection(unit.Target), "ru",
            new Dictionary<string, string> { ["name"] = "Anna" }, "1", "2", "5", "21", "0.5");
    }

    [Fact]
    public void Restoring_for_a_language_with_fewer_forms_prunes_both_paragraphs_to_its_set()
    {
        // Expanded for Russian, restored as Japanese: three category triples go, from both sides.
        var unit = Expanded(UnreadCount);
        var restorer = Restorer("ja-JP");

        restorer.ProcessParagraphUnit(unit);

        Assert.Equal(3, restorer.Pruned);
        AssertRestoredShape(unit);
        Assert.Equal(Projection(unit.Source), Projection(unit.Target));
        AssertRendersLikeSource(UnreadCount, Projection(unit.Target), "ja",
            new Dictionary<string, string> { ["name"] = "Anna" }, "0", "1", "2", "5", "1.5");
    }

    [Fact]
    public void Explicit_values_and_the_offset_survive_pruning()
    {
        var unit = Expanded(Guests);
        var restorer = Restorer("ja-JP");

        restorer.ProcessParagraphUnit(unit);

        Assert.Equal(3, restorer.Pruned);
        var projection = Projection(unit.Target);
        Assert.StartsWith("{count, plural, offset:1 =0 {", projection);
        AssertRendersLikeSource(Guests, projection, "ja", new Dictionary<string, string>(), "0", "1", "2", "3", "22");
    }

    [Fact]
    public void Typed_apostrophes_and_braces_in_the_target_are_escaped_and_the_syntax_is_not()
    {
        // Restored once (filled from the source), expanded again, then a translator types an
        // apostrophe and a literal brace into the first target form.
        var unit = Expanded(UnreadCount);
        Restorer("ru-RU").ProcessParagraphUnit(unit);
        Expander().ProcessParagraphUnit(unit);
        var text = ParagraphUnits.ContentOf(SegmentsOf(unit.Target)[0]).OfType<IText>().First();
        text.Properties.Text = "It's {literal} ";

        Restorer("ru-RU").ProcessParagraphUnit(unit);

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
    public void Restore_then_expand_then_restore_gives_the_same_message_with_the_escaping_applied_once()
    {
        var unit = Expanded(UnreadCount);
        Restorer("ru-RU").ProcessParagraphUnit(unit);
        Expander().ProcessParagraphUnit(unit);
        var text = ParagraphUnits.ContentOf(SegmentsOf(unit.Target)[0]).OfType<IText>().First();
        text.Properties.Text = "It's ";

        Restorer("ru-RU").ProcessParagraphUnit(unit);
        var once = Projection(unit.Target);
        Expander().ProcessParagraphUnit(unit);
        Restorer("ru-RU").ProcessParagraphUnit(unit);
        var twice = Projection(unit.Target);

        Assert.Equal(once, twice);
        Assert.Contains("It''s ", once);
        Assert.DoesNotContain("''''", once);
    }

    [Fact]
    public void A_restored_unit_is_left_alone_by_restore()
    {
        var unit = Expanded(UnreadCount);
        Restorer("ru-RU").ProcessParagraphUnit(unit);
        var before = Projection(unit.Target);

        var again = Restorer("ru-RU");
        again.ProcessParagraphUnit(unit);

        Assert.Equal(0, again.Units);
        Assert.Equal(before, Projection(unit.Target));
    }

    // ---- the collapse ------------------------------------------------------------------------

    /// <summary>
    /// The expansion writes placeholder tags inside the segments and locked syntax between
    /// them. Restore turns the lot into the text of one segment per side, in the hoisted shape
    /// the forms were laid out in, and the projection is unchanged.
    /// </summary>
    [Fact]
    public void The_tags_and_the_locked_syntax_become_the_text_of_one_segment_per_side()
    {
        var unit = Expanded(UnreadCount);
        TranslateUnreadCount(unit);
        Assert.True(TagsUnder(unit.Source) > 0);
        var expandedProjection = Projection(unit.Target);

        var restorer = Restorer("ru-RU");
        restorer.ProcessParagraphUnit(unit);

        Assert.Empty(restorer.Warnings);
        AssertRestoredShape(unit);
        Assert.Equal(expandedProjection, Projection(unit.Target));
        Assert.Equal("{count, plural, one {Hello {name}, you have # unread message!} few {Hello {name}, you have # unread messages!} "
            + "many {Hello {name}, you have # unread messages!} other {Hello {name}, you have # unread messages!}}",
            Projection(unit.Source));
        Assert.Contains("one {Привет, {name}, у вас # непрочитанное сообщение!}", Projection(unit.Target));
        Assert.Equal("Привет, Anna, у вас 1 непрочитанное сообщение!",
            MessageRenderer.Render(IcuMessage.Parse(Projection(unit.Target)).Nodes,
                new Dictionary<string, string> { ["name"] = "Anna", ["count"] = "1" }, CldrCategories.For("ru")));

        // The text is one run on the source side, and the pair's id is 1.
        var source = SegmentsOf(unit.Source)[0];
        Assert.Single(ParagraphUnits.ItemsOf(source));
        Assert.IsAssignableFrom<IText>(source[0]);
        Assert.Equal("1", source.Properties.Id.Id);
    }

    [Fact]
    public void Locked_placeholder_tags_collapse_the_same_way()
    {
        var unit = ParagraphUnits.Json(UnreadCount, "key");
        Expander(options: ExpansionOptions.Default with { LockPlaceholders = true }).ProcessParagraphUnit(unit);
        Assert.True(TagsUnder(unit.Source) > 0);
        Assert.NotEmpty(ParagraphUnits.ContentOf(SegmentsOf(unit.Source)[0]).OfType<ILockedContent>());

        Restorer("ru-RU").ProcessParagraphUnit(unit);

        AssertRestoredShape(unit);
        AssertRendersLikeSource(UnreadCount, Projection(unit.Target), "ru", new Dictionary<string, string> { ["name"] = "Anna" }, "1", "2", "5");
    }

    /// <summary>A tag in the target matches a locked span in the source and the other way round: the syntax is the key.</summary>
    [Fact]
    public void Parity_is_by_syntax_whichever_construct_carries_it()
    {
        var unit = ParagraphUnits.Json(UnreadCount, "key");
        Expander(options: ExpansionOptions.Default with { LockPlaceholders = true }).ProcessParagraphUnit(unit);
        TranslateUnreadCount(unit);
        Assert.All(SegmentsOf(unit.Source), s => Assert.NotEmpty(ParagraphUnits.ItemsOf(s).OfType<ILockedContent>()));
        Assert.All(SegmentsOf(unit.Target), t => Assert.NotEmpty(ParagraphUnits.ItemsOf(t).OfType<IPlaceholderTag>()));

        var restorer = Restorer("ru-RU");
        restorer.ProcessParagraphUnit(unit);

        Assert.Empty(restorer.Warnings);
        AssertRestoredShape(unit);
    }

    [Fact]
    public void The_pattern_as_the_file_carried_it_stays_on_the_context_and_the_state_says_restored()
    {
        var unit = Expanded(UnreadCount);
        Restorer("ru-RU").ProcessParagraphUnit(unit);

        var context = IcuContext(unit);
        Assert.Equal(Constants.StateRestored, context.GetMetaData(Constants.StateKey));
        Assert.Equal(UnreadCount, context.GetMetaData("icu:pattern"));
        Assert.Equal("count", context.GetMetaData("icu:expandedSelectors"));
        Assert.Equal(2, unit.Properties.Contexts.Contexts.Count);

        var states = SegmentStates.Parse(context.GetMetaData(Constants.SegmentStatesKey));
        Assert.Equal(new[] { "count:few", "count:many", "count:one", "count:other" }, states.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void The_restored_pair_takes_the_forms_common_confirmation_level_or_draft()
    {
        var unit = Expanded(UnreadCount);
        TranslateUnreadCount(unit);
        foreach (var segment in SegmentsOf(unit.Target)) segment.Properties.ConfirmationLevel = ConfirmationLevel.Translated;
        Restorer("ru-RU").ProcessParagraphUnit(unit);
        Assert.Equal(ConfirmationLevel.Translated, SegmentsOf(unit.Target)[0].Properties.ConfirmationLevel);
        Assert.False(SegmentsOf(unit.Target)[0].Properties.IsLocked);

        var mixed = Expanded(UnreadCount);
        TranslateUnreadCount(mixed);
        var segments = SegmentsOf(mixed.Target);
        segments[0].Properties.ConfirmationLevel = ConfirmationLevel.Translated;
        segments[1].Properties.ConfirmationLevel = ConfirmationLevel.ApprovedTranslation;
        foreach (var segment in segments) segment.Properties.IsLocked = true;
        Restorer("ru-RU").ProcessParagraphUnit(mixed);
        Assert.Equal(ConfirmationLevel.Draft, SegmentsOf(mixed.Target)[0].Properties.ConfirmationLevel);
        Assert.True(SegmentsOf(mixed.Target)[0].Properties.IsLocked);
    }

    [Fact]
    public void Restore_numbers_the_segments_on_through_the_file_and_expand_does_so_again()
    {
        var expander = Expander();
        var plain = ParagraphUnits.WithSegments("Message Centre");
        var first = ParagraphUnits.Json(UnreadCount, "a");
        var second = ParagraphUnits.Json("Hello {name}, welcome back!", "b");
        var third = ParagraphUnits.Json(UnreadCount, "c");
        foreach (var unit in new[] { plain, first, second, third }) expander.ProcessParagraphUnit(unit);
        Assert.Equal(["2", "3", "4", "5"], SegmentsOf(first.Source).Select(s => s.Properties.Id.Id));
        Assert.Equal(["7", "8", "9", "10"], SegmentsOf(third.Source).Select(s => s.Properties.Id.Id));

        var restorer = Restorer("ru-RU");
        foreach (var unit in new[] { plain, first, second, third }) restorer.ProcessParagraphUnit(unit);
        Assert.Equal("1", SegmentsOf(plain.Source)[0].Properties.Id.Id);
        Assert.Equal("2", SegmentsOf(first.Source)[0].Properties.Id.Id);
        Assert.Equal("2", SegmentsOf(first.Target)[0].Properties.Id.Id);
        Assert.Equal("3", SegmentsOf(second.Target)[0].Properties.Id.Id);
        Assert.Equal("4", SegmentsOf(third.Target)[0].Properties.Id.Id);

        var again = Expander();
        foreach (var unit in new[] { plain, first, second, third }) again.ProcessParagraphUnit(unit);
        Assert.Equal(["2", "3", "4", "5"], SegmentsOf(first.Target).Select(s => s.Properties.Id.Id));
        Assert.Equal(["6"], SegmentsOf(second.Target).Select(s => s.Properties.Id.Id));
        Assert.Equal(["7", "8", "9", "10"], SegmentsOf(third.Target).Select(s => s.Properties.Id.Id));
    }

    // ---- the round trip ----------------------------------------------------------------------

    [Fact]
    public void Expanding_a_restored_unit_hands_each_form_its_translation_and_its_state_back()
    {
        var unit = Expanded(UnreadCount);
        TranslateUnreadCount(unit);
        var targets = SegmentsOf(unit.Target);
        targets[0].Properties.ConfirmationLevel = ConfirmationLevel.Translated;
        targets[1].Properties.ConfirmationLevel = ConfirmationLevel.Draft;
        var origin = ParagraphUnits.ItemFactory.CreateTranslationOrigin();
        origin.OriginType = "tm";
        origin.OriginSystem = "Main TM";
        origin.MatchPercent = 85;
        targets[1].Properties.TranslationOrigin = origin;
        targets[2].Properties.ConfirmationLevel = ConfirmationLevel.ApprovedTranslation;
        targets[2].Properties.IsLocked = true;
        var expandedTarget = Projection(unit.Target);

        Restorer("ru-RU").ProcessParagraphUnit(unit);
        var restoredTarget = Projection(unit.Target);
        var expander = Expander();
        expander.ProcessParagraphUnit(unit);

        Assert.Equal(1, expander.Expanded);
        Assert.Equal(1, expander.Reexpanded);
        Assert.Equal(0, expander.AlreadyExpanded);
        Assert.Empty(expander.Warnings);
        Assert.True(ResourceKey.IsExpanded(unit));
        Assert.Equal(2, unit.Properties.Contexts.Contexts.Count);
        Assert.Equal(UnreadCount, IcuContext(unit).GetMetaData("icu:pattern"));
        Assert.Equal(Constants.StateExpanded, IcuContext(unit).GetMetaData(Constants.StateKey));

        // The four forms are back, with the translation each had, as text and tags.
        targets = SegmentsOf(unit.Target);
        Assert.Equal(4, targets.Count);
        Assert.Equal(expandedTarget, Projection(unit.Target));
        Assert.Equal(8, TagsUnder(unit.Target));
        Assert.Equal("Привет, {name}, у вас # непрочитанное сообщение!", RawValueReconstruction.Reconstruct(targets[0]).RawValue);

        // And the state each had.
        Assert.Equal(ConfirmationLevel.Translated, targets[0].Properties.ConfirmationLevel);
        Assert.Null(targets[0].Properties.TranslationOrigin);
        Assert.Equal(ConfirmationLevel.Draft, targets[1].Properties.ConfirmationLevel);
        Assert.Equal("tm", targets[1].Properties.TranslationOrigin.OriginType);
        Assert.Equal("Main TM", targets[1].Properties.TranslationOrigin.OriginSystem);
        Assert.Equal(85, targets[1].Properties.TranslationOrigin.MatchPercent);
        Assert.Equal(ConfirmationLevel.ApprovedTranslation, targets[2].Properties.ConfirmationLevel);
        Assert.True(targets[2].Properties.IsLocked);
        Assert.False(targets[3].Properties.IsLocked);

        // Restored again, the message is the same one.
        Restorer("ru-RU").ProcessParagraphUnit(unit);
        Assert.Equal(restoredTarget, Projection(unit.Target));
    }

    [Fact]
    public void A_form_the_restored_target_has_no_translation_for_starts_empty_and_is_reported()
    {
        // Restored as Japanese, the target has one form; expanded again as Russian, three of
        // the four forms have nothing to come back to.
        var unit = Expanded(UnreadCount);
        TranslateUnreadCount(unit);
        Restorer("ja-JP").ProcessParagraphUnit(unit);

        var expander = Expander("ru-RU");
        expander.ProcessParagraphUnit(unit);

        var targets = SegmentsOf(unit.Target);
        Assert.Equal(4, targets.Count);
        Assert.Equal(1, targets.Count(t => t.Count > 0));
        var warning = Assert.Single(expander.Warnings);
        Assert.Contains("no translation for 3 of 4 forms", warning.Reason);
        Assert.Contains("count:one", warning.Reason);
        Assert.Equal(warning.Reason, Assert.Single(expander.Outcomes).Detail);
        Assert.Empty(ParagraphUnits.UnitComments(unit));
    }

    // ---- comments ----------------------------------------------------------------------------

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
    /// with the plugin's comment marker in it. Restore takes the plugin's comments out of the
    /// targets and leaves the translator's own, whether in a marker of their own or sharing one,
    /// each rebuilt around its text in the restored segment.
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
        note.Add(Text(" NB"));
        copied.Add(note);

        // Segment 3: the translator added a comment to the copied marker itself.
        ((ICommentMarker)targets[2][0]).Comments.Add(
            ParagraphUnits.PropertiesFactory.CreateComment("query for the client", "Anna", Severity.Low));

        var restorer = Restorer("ru-RU");
        restorer.ProcessParagraphUnit(unit);

        Assert.Equal(4, restorer.CommentsStripped);
        Assert.Empty(restorer.Warnings);
        AssertRestoredShape(unit);

        var markers = ParagraphUnits.ItemsOf(SegmentsOf(unit.Target)[0]).OfType<ICommentMarker>().ToList();
        Assert.Equal(2, markers.Count);
        Assert.Equal("check the case", markers[0].Comments.GetItem(0).Text);
        Assert.Equal(" NB", RawValueReconstruction.Reconstruct(markers[0]).RawValue);
        Assert.Equal(1, markers[1].Comments.Count);
        Assert.Equal("query for the client", markers[1].Comments.GetItem(0).Text);
        Assert.Equal("Hello {name}, you have # unread messages!", RawValueReconstruction.Reconstruct(markers[1]).RawValue);

        // The projection still parses with the note's text in place.
        Assert.Contains("unread messages! NB}", Projection(unit.Target));
        Assert.True(IcuParser.TryParse(Projection(unit.Target), out _, out _));
    }

    /// <summary>A finding written on a target does not come back through the round trip, so a corrected file comes out clean and a stale warning never lingers.</summary>
    [Fact]
    public void A_finding_on_a_target_is_cleared_by_the_next_round()
    {
        var unit = Expanded(UnreadCount);
        Restorer("ru-RU").ProcessParagraphUnit(unit);
        Assert.NotEmpty(ParagraphUnits.ItemsOf(SegmentsOf(unit.Target)[0]).OfType<ICommentMarker>());
        Expander().ProcessParagraphUnit(unit);

        var again = Restorer("ru-RU");
        again.ProcessParagraphUnit(unit);

        // Filled from the source on the first run, so translated now: no finding, no comment.
        Assert.Empty(again.Warnings);
        Assert.Equal(0, again.Filled);
        Assert.True(ParagraphUnits.NoSegmentComments(unit.Target));
        Assert.True(ParagraphUnits.NoSegmentComments(unit.Source));
        Assert.Empty(ParagraphUnits.UnitComments(unit));
    }

    // ---- mismatches --------------------------------------------------------------------------

    /// <summary>
    /// A message with a placeholder mismatch is warned about and left laid out and editable: its
    /// spans are tags on both sides, so the missing one can be placed with QuickPlace (Project
    /// 52). Once corrected, the next run restores the message as usual.
    /// </summary>
    [Fact]
    public void A_missing_protected_span_in_the_target_is_warned_about_and_the_message_stays_expanded()
    {
        var unit = Expanded(UnreadCount);
        Restorer("ru-RU").ProcessParagraphUnit(unit);
        Expander().ProcessParagraphUnit(unit);
        var target = SegmentsOf(unit.Target)[0];
        ParagraphUnits.ContentOf(target).OfType<IPlaceholderTag>().First().RemoveFromParent();

        var restorer = Restorer("ru-RU");
        restorer.ProcessParagraphUnit(unit);

        Assert.Equal(0, restorer.Restored);
        Assert.Contains(restorer.Warnings, w => w.Reason.Contains("missing from the target"));
        Assert.Contains(restorer.Warnings, w => w.Reason.Contains("run ICU Restore again"));
        Assert.True(ResourceKey.IsExpanded(unit));

        // The finding sits on the target segment as a high-severity comment; the others are clear.
        var targets = SegmentsOf(unit.Target);
        Assert.Equal(4, targets.Count);
        var marker = Assert.IsAssignableFrom<ICommentMarker>(targets[0][0]);
        Assert.StartsWith("Placeholder mismatch", marker.Comments.GetItem(0).Text);
        Assert.Contains("run ICU Restore again", marker.Comments.GetItem(0).Text);
        Assert.Equal(Severity.High, marker.Comments.GetItem(0).Severity);
        Assert.All(targets.Skip(1), t => Assert.Null(ParagraphUnits.CommentOf(t)));
        Assert.Empty(ParagraphUnits.UnitComments(unit));

        // Editable shape on both sides: tags, no locked span inside any segment.
        Assert.Equal(8, TagsUnder(unit.Source));
        Assert.Equal(7, TagsUnder(unit.Target));
        Assert.All(SegmentsOf(unit.Source), s => Assert.Empty(ParagraphUnits.ItemsOf(s).OfType<ILockedContent>()));
        Assert.All(SegmentsOf(unit.Target), s => Assert.Empty(ParagraphUnits.ContentOf(s).OfType<ILockedContent>()));

        // The translator places the tag; the next run clears the comment and restores the message.
        marker.Insert(1, Tag("{name}"));
        var again = Restorer("ru-RU");
        again.ProcessParagraphUnit(unit);

        Assert.DoesNotContain(again.Warnings, w => w.Reason.Contains("Placeholder mismatch"));
        Assert.Equal(1, again.Restored);
        AssertRestoredShape(unit);
        Assert.True(ParagraphUnits.NoSegmentComments(unit.Target));
        AssertRendersLikeSource(UnreadCount, Projection(unit.Target), "ru", new Dictionary<string, string> { ["name"] = "Anna" }, "1", "2", "5");
    }

    [Fact]
    public void A_mismatch_on_a_freshly_expanded_message_leaves_its_tags_in_place()
    {
        var unit = Expanded(UnreadCount);
        var targets = SegmentsOf(unit.Target);
        targets[0].Add(Text("Привет, у вас # сообщение!"));

        var restorer = Restorer("ru-RU");
        restorer.ProcessParagraphUnit(unit);

        Assert.Contains(restorer.Warnings, w => w.Reason.Contains("Placeholder mismatch"));
        Assert.Equal(8, TagsUnder(unit.Source));
        Assert.Equal(6, TagsUnder(unit.Target));
        Assert.All(SegmentsOf(unit.Source), s => Assert.Empty(ParagraphUnits.ItemsOf(s).OfType<ILockedContent>()));
    }

    [Fact]
    public void A_missing_protected_span_fails_the_run_when_configured_to()
    {
        var unit = Expanded(UnreadCount);
        Restorer("ru-RU").ProcessParagraphUnit(unit);
        Expander().ProcessParagraphUnit(unit);
        var target = SegmentsOf(unit.Target)[0];
        ParagraphUnits.ContentOf(target).OfType<IPlaceholderTag>().First().RemoveFromParent();

        var restorer = Restorer("ru-RU",
            RestoreOptions.Default with { OnPlaceholderMismatch = PlaceholderMismatchBehaviour.FailTask });

        Assert.Throws<RestoreFailedException>(() => restorer.ProcessParagraphUnit(unit));
    }

    [Fact]
    public void An_empty_target_fails_the_run_when_configured_to()
    {
        var unit = Expanded(UnreadCount);
        var restorer = Restorer("ru-RU", RestoreOptions.Default with { OnEmptyBranch = EmptyBranchBehaviour.FailTask });

        Assert.Throws<RestoreFailedException>(() => restorer.ProcessParagraphUnit(unit));
    }

    // ---- other message kinds -----------------------------------------------------------------

    [Fact]
    public void A_protected_argument_only_message_is_filled_and_escaped_without_plural_rules()
    {
        var unit = Expanded("Hello {name}, welcome back!");
        var restorer = Restorer("ru-RU");

        restorer.ProcessParagraphUnit(unit);

        Assert.Equal(0, restorer.Pruned);
        Assert.Equal(1, restorer.Filled);
        AssertRestoredShape(unit);
        Assert.Equal("Hello {name}, welcome back!", Projection(unit.Target));
        Assert.Equal("", SegmentStates.Parse(IcuContext(unit).GetMetaData(Constants.SegmentStatesKey)).Keys.Single());

        // '#' is plain text outside a plural and must not be quoted.
        Expander().ProcessParagraphUnit(unit);
        Assert.Single(SegmentsOf(unit.Target));
        Assert.Equal("Hello {name}, welcome back!", Projection(unit.Target));
        var text = ParagraphUnits.ContentOf(SegmentsOf(unit.Target)[0]).OfType<IText>().First();
        text.Properties.Text = "Item #1 for ";
        Restorer("ru-RU").ProcessParagraphUnit(unit);
        Assert.StartsWith("Item #1 for {name}", Projection(unit.Target));
    }

    [Fact]
    public void A_unit_the_expansion_never_touched_is_left_alone()
    {
        var unit = ParagraphUnits.Json("Message Centre", "key");
        var restorer = Restorer("ru-RU");

        restorer.ProcessParagraphUnit(unit);

        Assert.Equal(0, restorer.Units);
        Assert.Equal("Message Centre", Projection(unit.Source));
    }

    [Fact]
    public void A_walked_plural_kind_is_kept_whole_and_escaped_whatever_the_language()
    {
        // Over the branch budget for Arabic, so laid out walked: the source's one and other
        // branches are the developer's, and restore keeps them even for a language with fewer
        // forms, because nothing is recorded as expanded.
        const string sync =
            "{files, plural, one {# file} other {# files}} synchronised across " +
            "{devices, plural, one {# device} other {# devices}}.";
        var unit = Expanded(sync, "ar-SA");
        Assert.Equal(4, SegmentsOf(unit.Source).Count);

        var restorer = Restorer("ja-JP");
        restorer.ProcessParagraphUnit(unit);

        Assert.Equal(0, restorer.Pruned);
        Assert.Equal(4, restorer.Filled);
        AssertRestoredShape(unit);
        Assert.Equal(Projection(unit.Source), Projection(unit.Target));
        Assert.Equal("1 file synchronised across 2 devices.",
            MessageRenderer.Render(IcuMessage.Parse(Projection(unit.Target)).Nodes,
                new Dictionary<string, string> { ["files"] = "1", ["devices"] = "2" }, CldrCategories.For("en")));

        // Expanded again as Arabic it is walked again, four forms with their text; a typed
        // apostrophe in a walked branch is escaped like any other.
        Expander("ar-SA").ProcessParagraphUnit(unit);
        Assert.Equal(4, SegmentsOf(unit.Target).Count);
        Assert.All(SegmentsOf(unit.Target), t => Assert.True(t.Count > 0));
        var text = ParagraphUnits.ContentOf(SegmentsOf(unit.Target)[0]).OfType<IText>().First();
        text.Properties.Text = "It's ";
        Restorer("ja-JP").ProcessParagraphUnit(unit);
        Assert.Contains("It''s ", Projection(unit.Target));
    }

    [Fact]
    public void The_processor_records_an_outcome_per_unit_for_the_report()
    {
        // Expanded for Russian, restored as Japanese: three forms pruned, one segment filled.
        var unit = Expanded(UnreadCount);
        var restorer = Restorer("ja-JP");

        restorer.ProcessParagraphUnit(unit);

        var outcome = Assert.Single(restorer.Outcomes);
        Assert.Equal("key", outcome.Key);
        Assert.Equal(1, outcome.Segments);
        Assert.Equal(3, outcome.Pruned);
        Assert.Equal(1, outcome.Filled);
        var warning = Assert.Single(outcome.Warnings);
        Assert.StartsWith("Segment ", warning);
        Assert.Contains("untranslated", warning);
    }
}
