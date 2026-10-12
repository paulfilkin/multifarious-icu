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
/// The expansion processor over real paragraph units built through the framework's factories:
/// the layout it writes, the metadata it attaches, and the native projection Studio's writer
/// would emit, which has to render exactly as the source message does for every count.
/// </summary>
public class IcuExpandProcessorTests
{
    private const string UnreadCount =
        "Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!";

    private static IcuExpandProcessor Processor(string target, ExpansionOptions? options = null) =>
        new("en-GB", target, options ?? ExpansionOptions.Default, "1.0.0")
        {
            ItemFactory = ParagraphUnits.ItemFactory,
        };

    private static List<ISegment> SegmentsOf(IAbstractMarkupDataContainer paragraph) =>
        ParagraphUnits.SegmentsOf(paragraph);

    /// <summary>The forms as the ICU Forms reader reads them back from the layout, in segment order.</summary>
    private static List<IcuFormRow> Rows(IParagraphUnit unit, string language = "ru-RU") =>
        new IcuFormsReader().Read(unit, language)!.Rows.ToList();

    [Fact]
    public void A_plural_expands_to_one_segment_per_target_category_in_both_paragraphs()
    {
        var unit = ParagraphUnits.Json(UnreadCount, "['inbox.unreadCount']");

        Processor("ru-RU").ProcessParagraphUnit(unit);

        var source = SegmentsOf(unit.Source);
        var target = SegmentsOf(unit.Target);
        Assert.Equal(4, source.Count);
        Assert.Equal(4, target.Count);
        Assert.Equal(["one", "few", "many", "other"], Rows(unit).Select(r => r.Category));
        Assert.Equal(["1", "2", "3", "4"], source.Select(s => s.Properties.Id.Id));
        Assert.Equal(source.Select(s => s.Properties.Id.Id), target.Select(s => s.Properties.Id.Id));
        // Count rather than Assert.Empty: enumerating a framework segment is not guaranteed.
        Assert.All(target, segment => Assert.True(segment.Count == 0, "target segment is not empty"));
    }

    private static ExpansionOptions WithLock => ExpansionOptions.Default with { LockPlaceholders = true };

    private static string LockedText(ILockedContent locked) =>
        RawValueReconstruction.Reconstruct(locked.Content).RawValue;

    /// <summary>
    /// The syntax sits between the segments as locked text, the one thing both of Studio's
    /// writers emit verbatim; arguments and '#' sit inside the segments as placeholder tags,
    /// which the translator can place with QuickPlace and the verifiers can check.
    /// </summary>
    [Fact]
    public void The_selector_syntax_sits_in_locked_content_between_the_segments_and_the_arguments_are_tags()
    {
        var unit = ParagraphUnits.Json(UnreadCount, "['inbox.unreadCount']");

        Processor("ru-RU").ProcessParagraphUnit(unit);

        var items = ParagraphUnits.ItemsOf(unit.Source);
        Assert.Empty(items.OfType<IPlaceholderTag>());
        Assert.Equal(
        [
            "{count, plural,",
            " one {", "}",
            " few {", "}",
            " many {", "}",
            " other {", "}",
            "}",
        ], items.OfType<ILockedContent>().Select(LockedText));

        var first = SegmentsOf(unit.Source)[0];
        var inner = ParagraphUnits.ContentOf(first);
        Assert.Empty(inner.OfType<ILockedContent>());
        var tags = inner.OfType<IPlaceholderTag>().ToList();
        Assert.Equal(["{name}", "#"], tags.Select(t => t.Properties.TagContent));
        Assert.Equal(["{name}", "#"], tags.Select(t => t.Properties.DisplayText));
        Assert.Equal("count:one", Rows(unit)[0].Path);
    }

    /// <summary>
    /// With the placeholders locked, each tag sits inside its own locked content, so it shows as
    /// a tag but cannot be moved or deleted. The syntax between segments is the same either way.
    /// </summary>
    [Fact]
    public void Locked_placeholders_wrap_each_tag_in_locked_content()
    {
        var unit = ParagraphUnits.Json(UnreadCount, "['inbox.unreadCount']");

        Processor("ru-RU", WithLock).ProcessParagraphUnit(unit);

        var inner = ParagraphUnits.ContentOf(SegmentsOf(unit.Source)[0]);
        Assert.Empty(inner.OfType<IPlaceholderTag>());
        var wrapped = inner.OfType<ILockedContent>().ToList();
        Assert.Equal(["{name}", "#"], wrapped.Select(LockedText));
        Assert.All(wrapped, locked => Assert.IsAssignableFrom<IPlaceholderTag>(locked.Content[0]));
        Assert.Equal(10, ParagraphUnits.ItemsOf(unit.Source).OfType<ILockedContent>().Count());
    }

    /// <summary>
    /// The expansion writes no comment anywhere: not in a segment, where Studio's
    /// pseudo-translation and Copy Source to Target copy it into the target (Project 46), not
    /// around a segment, which fails SDLXLIFF validation (Project 47), and not on the unit,
    /// which only repeats what the ICU Forms window shows. Only a warning goes on the unit.
    /// </summary>
    [Fact]
    public void The_expansion_writes_no_comment_at_all()
    {
        var unit = ParagraphUnits.Json(UnreadCount, "['inbox.unreadCount']");

        Processor("ru-RU").ProcessParagraphUnit(unit);

        Assert.True(ParagraphUnits.NoSegmentComments(unit.Source));
        Assert.True(ParagraphUnits.NoSegmentComments(unit.Target));
        Assert.All(SegmentsOf(unit.Source), s => Assert.IsAssignableFrom<IParagraph>(s.Parent));
        Assert.Empty(ParagraphUnits.UnitComments(unit));

        // The seeding facts the window's tooltip needs are on the unit context instead.
        var context = unit.Properties.Contexts.Contexts.First(c => c.ContextType == Constants.IcuContextType);
        Assert.Equal("2:other,3:other", context.GetMetaData("icu:seededFrom"));
        Assert.Equal("2,3", context.GetMetaData("icu:syntheticSource"));
        Assert.Equal(["", "other", "other", ""], Rows(unit).Select(r => r.SeededFrom));
        Assert.Contains("seeded from \"other\"", Rows(unit)[1].Comment);
    }

    [Fact]
    public void The_unit_gains_the_plugins_context_beside_the_filters_own()
    {
        var unit = ParagraphUnits.Json(UnreadCount, "['inbox.unreadCount']");

        Processor("ru-RU").ProcessParagraphUnit(unit);

        var contexts = unit.Properties.Contexts.Contexts;
        Assert.Equal(2, contexts.Count);
        Assert.Equal("sdl:paragraph", contexts[0].ContextType);

        var icu = contexts[1];
        Assert.Equal(Constants.IcuContextType, icu.ContextType);
        Assert.Equal("['inbox.unreadCount']", icu.Description);
        Assert.Equal(UnreadCount, icu.GetMetaData("icu:pattern"));
        Assert.Equal("['inbox.unreadCount']", icu.GetMetaData("icu:key"));
        Assert.Equal("4", icu.GetMetaData("icu:unitCount"));
        Assert.False(string.IsNullOrEmpty(icu.GetMetaData("icu:cldrVersion")));
        Assert.True(ResourceKey.IsExpanded(unit));
    }

    /// <summary>
    /// The SDLXLIFF reader gives every paragraph unit in a group the same context properties
    /// object, and the Java Resources filter groups a value with the whitespace units around
    /// it. Adding to that shared object marked the neighbours as ICU units and produced context
    /// references without definitions in the written file, which the next task read back as a
    /// null context and crashed on. Found in the first Studio run of the expansion.
    /// </summary>
    [Fact]
    public void The_context_is_added_to_the_unit_alone_not_to_a_shared_properties_object()
    {
        var value = ParagraphUnits.Properties(UnreadCount, "inbox.unreadCount");
        var whitespace = ParagraphUnits.WithSegments("\n");
        whitespace.Properties.Contexts = value.Properties.Contexts;

        Processor("ru-RU").ProcessParagraphUnit(value);

        Assert.Equal(2, value.Properties.Contexts.Contexts.Count);
        Assert.Single(whitespace.Properties.Contexts.Contexts);
        Assert.False(ResourceKey.IsExpanded(whitespace));
        Assert.NotSame(whitespace.Properties.Contexts, value.Properties.Contexts);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("5")]
    [InlineData("21")]
    [InlineData("0.5")]
    public void The_native_projection_renders_exactly_as_the_source_for_every_count(string count)
    {
        var unit = ParagraphUnits.Json(UnreadCount, "['inbox.unreadCount']");

        Processor("ru-RU").ProcessParagraphUnit(unit);

        var projection = RawValueReconstruction.Reconstruct(unit.Source).RawValue;
        var arguments = new Dictionary<string, string> { ["name"] = "Anna", ["count"] = count };
        var categories = CldrCategories.For("ru");

        Assert.Equal(
            MessageRenderer.Render(IcuMessage.Parse(UnreadCount).Nodes, arguments, categories),
            MessageRenderer.Render(IcuMessage.Parse(projection).Nodes, arguments, categories));
    }

    [Fact]
    public void Japanese_gets_a_single_segment()
    {
        var unit = ParagraphUnits.Json(UnreadCount, "['inbox.unreadCount']");

        Processor("ja-JP").ProcessParagraphUnit(unit);

        Assert.Single(SegmentsOf(unit.Source));
        Assert.Equal("other", Rows(unit, "ja-JP")[0].Category);
    }

    [Fact]
    public void Explicit_values_and_the_offset_are_kept_verbatim()
    {
        const string guests =
            "{count, plural, offset:1 =0 {Nobody is coming} =1 {Only you are coming} one {You and # other guest are coming} other {You and # other guests are coming}}.";
        var unit = ParagraphUnits.Properties(guests, "party.guests");

        Processor("ru-RU").ProcessParagraphUnit(unit);

        Assert.Equal(6, SegmentsOf(unit.Source).Count);
        var syntax = ParagraphUnits.ItemsOf(unit.Source).OfType<ILockedContent>().Select(LockedText).ToList();
        Assert.Equal("{count, plural, offset:1", syntax[0]);
        Assert.Equal(" =0 {", syntax[1]);
        Assert.Equal("count:=0", Rows(unit)[0].Path);

        var projection = RawValueReconstruction.Reconstruct(unit.Source).RawValue;
        var categories = CldrCategories.For("ru");
        foreach (var count in new[] { "0", "1", "2", "3", "6", "22" })
        {
            var arguments = new Dictionary<string, string> { ["count"] = count };
            Assert.Equal(
                MessageRenderer.Render(IcuMessage.Parse(guests).Nodes, arguments, categories),
                MessageRenderer.Render(IcuMessage.Parse(projection).Nodes, arguments, categories));
        }
    }

    [Fact]
    public void Selectordinal_resolves_against_the_ordinal_table()
    {
        const string position = "You are {position, selectordinal, one {#st} two {#nd} few {#rd} other {#th}} in the queue.";

        var russian = ParagraphUnits.Json(position, "queue.position");
        Processor("ru-RU").ProcessParagraphUnit(russian);
        Assert.Single(SegmentsOf(russian.Source));

        var english = ParagraphUnits.Json(position, "queue.position");
        Processor("en-US").ProcessParagraphUnit(english);
        Assert.Equal(4, SegmentsOf(english.Source).Count);
    }

    [Fact]
    public void A_value_without_braces_is_left_exactly_as_it_was()
    {
        var unit = ParagraphUnits.Json("Message Centre", "['app.title']");
        var processor = Processor("ru-RU");

        processor.ProcessParagraphUnit(unit);

        Assert.Equal(1, processor.Units);
        Assert.Equal(0, processor.Expanded);
        Assert.Single(SegmentsOf(unit.Source));
        Assert.Equal("Message Centre", RawValueReconstruction.Reconstruct(unit.Source).RawValue);
        Assert.Single(unit.Properties.Contexts.Contexts);
        Assert.False(ResourceKey.IsExpanded(unit));
    }

    /// <summary>
    /// A message with arguments and no selector is laid out as one segment with the arguments
    /// protected. Decided on 5 September 2026 after pseudo-translation garbled every such value.
    /// </summary>
    [Theory]
    [InlineData("Hello {name}, welcome back!", "{name}")]
    [InlineData("Your balance is {amount, number, ::currency/EUR} as of {when, date, short}.", "{amount, number, ::currency/EUR}")]
    public void An_argument_only_message_is_protected_in_a_single_segment(string value, string firstArgument)
    {
        var unit = ParagraphUnits.Json(value, "['key']");
        var processor = Processor("ru-RU");

        processor.ProcessParagraphUnit(unit);

        Assert.Equal(1, processor.Expanded);
        var segments = SegmentsOf(unit.Source);
        Assert.Single(segments);
        Assert.Empty(ParagraphUnits.ItemsOf(unit.Source).OfType<ILockedContent>());

        var spans = ParagraphUnits.ContentOf(segments[0]).OfType<IPlaceholderTag>().Select(t => t.Properties.TagContent).ToList();
        Assert.Equal(firstArgument, spans[0]);

        Assert.Equal(value, RawValueReconstruction.Reconstruct(unit.Source).RawValue);
        Assert.True(ResourceKey.IsExpanded(unit));
        Assert.Equal("1", unit.Properties.Contexts.Contexts[1].GetMetaData("icu:unitCount"));
        Assert.Equal("", unit.Properties.Contexts.Contexts[1].GetMetaData("icu:expandedSelectors"));
    }

    [Fact]
    public void The_unit_context_records_which_selectors_were_expanded()
    {
        const string nested =
            "{gender, select, female {{count, plural, one {She has # item} other {She has # items}}} other {{count, plural, one {They have # item} other {They have # items}}}}";
        var unit = ParagraphUnits.Json(nested, "['key']");

        Processor("ru-RU").ProcessParagraphUnit(unit);

        Assert.Equal("gender:female/count,gender:other/count",
            unit.Properties.Contexts.Contexts[1].GetMetaData("icu:expandedSelectors"));
    }

    [Fact]
    public void A_second_run_leaves_an_expanded_unit_alone()
    {
        var unit = ParagraphUnits.Json(UnreadCount, "['inbox.unreadCount']");
        Processor("ru-RU").ProcessParagraphUnit(unit);
        var before = RawValueReconstruction.Reconstruct(unit.Source).RawValue;

        var second = Processor("ru-RU");
        second.ProcessParagraphUnit(unit);

        Assert.Equal(1, second.AlreadyExpanded);
        Assert.Equal(0, second.Expanded);
        Assert.Equal(before, RawValueReconstruction.Reconstruct(unit.Source).RawValue);
        Assert.Equal(2, unit.Properties.Contexts.Contexts.Count);
    }

    [Fact]
    public void An_unparseable_message_passes_through_with_a_warning_on_the_unit()
    {
        const string broken = "{count, plural, one {# unread message} other {# unread messages}";
        var unit = ParagraphUnits.Json(broken, "['broken']");
        var processor = Processor("ru-RU");

        processor.ProcessParagraphUnit(unit);

        Assert.Equal(0, processor.Expanded);
        Assert.Single(processor.Warnings);
        Assert.Equal(broken, RawValueReconstruction.Reconstruct(unit.Source).RawValue);
        Assert.Equal(1, unit.Properties.Comments.Count);
        Assert.Contains("passed through", unit.Properties.Comments.GetItem(0).Text);
    }

    [Fact]
    public void An_unparseable_message_fails_the_run_when_configured_to()
    {
        var unit = ParagraphUnits.Json("{count, plural, one {x}", "['broken']");
        var processor = Processor("ru-RU", ExpansionOptions.Default with { OnParseError = ParseErrorBehaviour.FailTask });

        Assert.Throws<ExpansionFailedException>(() => processor.ProcessParagraphUnit(unit));
    }

    [Fact]
    public void Locked_and_unlocked_placeholders_produce_the_same_native_projection()
    {
        var unlocked = ParagraphUnits.Json(UnreadCount, "['inbox.unreadCount']");
        Processor("ru-RU").ProcessParagraphUnit(unlocked);

        var locked = ParagraphUnits.Json(UnreadCount, "['inbox.unreadCount']");
        Processor("ru-RU", WithLock).ProcessParagraphUnit(locked);

        Assert.Equal(
            RawValueReconstruction.Reconstruct(unlocked.Source).RawValue,
            RawValueReconstruction.Reconstruct(locked.Source).RawValue);
    }

    [Fact]
    public void An_over_budget_message_is_laid_out_walked_with_its_syntax_locked_and_a_warning_on_the_unit()
    {
        // Two independent plurals are 36 segments for Arabic, over the budget of 24.
        const string sync =
            "{files, plural, one {# file} other {# files}} synchronised across " +
            "{devices, plural, one {# device} other {# devices}}.";
        var unit = ParagraphUnits.Json(sync, "['sync.status']");
        var processor = Processor("ar-SA");

        processor.ProcessParagraphUnit(unit);

        Assert.Equal(1, processor.Expanded);
        var warning = Assert.Single(processor.Warnings);
        Assert.Contains("36 segments needed, 24 allowed", warning.Reason);
        Assert.Equal(1, unit.Properties.Comments.Count);
        Assert.StartsWith("ICU message laid out without category expansion", unit.Properties.Comments.GetItem(0).Text);

        // The source's own four branch paths, every piece of syntax locked, nothing recorded as
        // expanded, and a projection that renders exactly as the source.
        Assert.Equal(4, SegmentsOf(unit.Source).Count);
        Assert.Equal(4, SegmentsOf(unit.Target).Count);
        Assert.NotEmpty(ParagraphUnits.ItemsOf(unit.Source).OfType<ILockedContent>());

        var context = unit.Properties.Contexts.Contexts.First(c => c.ContextType == Constants.IcuContextType);
        Assert.Equal("", context.GetMetaData("icu:expandedSelectors"));

        var projection = RawValueReconstruction.Reconstruct(unit.Source).RawValue;
        var arguments = new Dictionary<string, string> { ["files"] = "3", ["devices"] = "1" };
        Assert.Equal(
            MessageRenderer.Render(IcuMessage.Parse(sync).Nodes, arguments, CldrCategories.For("en")),
            MessageRenderer.Render(IcuMessage.Parse(projection).Nodes, arguments, CldrCategories.For("en")));
    }

    [Fact]
    public void The_processor_records_an_outcome_per_unit_for_the_report()
    {
        var processor = Processor("ru-RU");
        var expanded = ParagraphUnits.Json(UnreadCount, "['inbox.unreadCount']");
        var protectedUnit = ParagraphUnits.Json("Hello {name}!", "['app.greeting']");
        var broken = ParagraphUnits.Json("{count, plural, one {x}", "['broken']");
        var plain = ParagraphUnits.Json("Message Centre", "['app.title']");

        processor.ProcessParagraphUnit(expanded);
        processor.ProcessParagraphUnit(protectedUnit);
        processor.ProcessParagraphUnit(broken);
        processor.ProcessParagraphUnit(plain);
        processor.ProcessParagraphUnit(expanded);

        // A value without ICU is not reported; a second pass over an expanded unit is.
        Assert.Equal(
            new[] { ExpandOutcome.Expanded, ExpandOutcome.Protected, ExpandOutcome.PassedThrough, ExpandOutcome.Skipped },
            processor.Outcomes.Select(o => o.Outcome));
        Assert.Equal(new[] { 4, 1, 0, 4 }, processor.Outcomes.Select(o => o.Segments));
        Assert.Equal("['inbox.unreadCount']", processor.Outcomes[0].Key);
        Assert.Contains("does not parse", processor.Outcomes[2].Detail);
        Assert.Equal("", processor.Outcomes[0].Detail);
    }

    // ---- pre-translated messages and the keep rule ------------------------------------------

    private const string RussianUnreadCount =
        "Привет, {name}, у вас {count, plural, one {# непрочитанное сообщение} few {# непрочитанных сообщения} many {# непрочитанных сообщений} other {# непрочитанного сообщения}}!";

    /// <summary>A unit as pre-translation leaves it: the whole target message in one segment, with the match it came with.</summary>
    private static IParagraphUnit PreTranslated(string target, string? originType = "tm", byte percent = 100,
        TextContextMatchLevel context = TextContextMatchLevel.None, ConfirmationLevel level = ConfirmationLevel.Translated)
    {
        var unit = ParagraphUnits.Json(UnreadCount, "['inbox.unreadCount']");
        var segment = SegmentsOf(unit.Target)[0];
        segment.Add(ParagraphUnits.ItemFactory.CreateText(ParagraphUnits.PropertiesFactory.CreateTextProperties(target)));
        segment.Properties.ConfirmationLevel = level;
        if (originType != null)
        {
            var origin = ParagraphUnits.ItemFactory.CreateTranslationOrigin();
            origin.OriginType = originType;
            origin.OriginSystem = "Main TM";
            origin.MatchPercent = percent;
            origin.TextContextMatchLevel = context;
            segment.Properties.TranslationOrigin = origin;
        }
        return unit;
    }

    private static ExpansionOptions Keep(int minimum = 100, bool lockKept = true) =>
        ExpansionOptions.Default with { KeepTranslated = true, KeepMinimumMatch = minimum, LockKept = lockKept };

    /// <summary>
    /// A whole message pre-translated in the developer's shape is hoisted and split into its
    /// forms like a restored one, each form starting with the message's own match and level.
    /// </summary>
    [Fact]
    public void A_pre_translated_whole_message_is_split_into_its_forms_with_its_match()
    {
        var unit = PreTranslated(RussianUnreadCount, "tm", 85, TextContextMatchLevel.None, ConfirmationLevel.Draft);
        var processor = Processor("ru-RU");

        processor.ProcessParagraphUnit(unit);

        Assert.Equal(1, processor.Expanded);
        Assert.Equal(0, processor.Kept);
        Assert.Empty(processor.Warnings);
        var targets = SegmentsOf(unit.Target);
        Assert.Equal(4, targets.Count);
        Assert.Equal("Привет, {name}, у вас # непрочитанное сообщение!", RawValueReconstruction.Reconstruct(targets[0]).RawValue);
        Assert.Equal("Привет, {name}, у вас # непрочитанных сообщений!", RawValueReconstruction.Reconstruct(targets[2]).RawValue);
        Assert.All(targets, t =>
        {
            Assert.Equal(ConfirmationLevel.Draft, t.Properties.ConfirmationLevel);
            Assert.Equal("tm", t.Properties.TranslationOrigin.OriginType);
            Assert.Equal(85, t.Properties.TranslationOrigin.MatchPercent);
            Assert.False(t.Properties.IsLocked);
        });
        Assert.Equal(UnreadCount, ResourceKey.IcuContextOf(unit)!.GetMetaData("icu:pattern"));
    }

    [Fact]
    public void A_pre_translated_message_that_does_not_parse_leaves_the_forms_empty_and_is_reported()
    {
        var unit = PreTranslated("Привет, {name}, у вас {count, plural, one {# сообщение}", "tm", 70);
        var processor = Processor("ru-RU");

        processor.ProcessParagraphUnit(unit);

        Assert.Equal(1, processor.Expanded);
        Assert.All(SegmentsOf(unit.Target), t => Assert.Empty(ParagraphUnits.ItemsOf(t)));
        var warning = Assert.Single(processor.Warnings);
        Assert.Contains("could not be split into forms", warning.Reason);
        Assert.Equal(warning.Reason, Assert.Single(processor.Outcomes).Detail);
        Assert.Empty(ParagraphUnits.UnitComments(unit));
    }

    [Fact]
    public void A_pre_translated_message_at_the_keep_threshold_is_kept_and_locked()
    {
        var unit = PreTranslated(RussianUnreadCount, "tm", 100);
        var processor = Processor("ru-RU", Keep());

        processor.ProcessParagraphUnit(unit);

        Assert.Equal(1, processor.Kept);
        Assert.Equal(0, processor.Expanded);
        Assert.Null(ResourceKey.IcuContextOf(unit));
        var target = Assert.Single(SegmentsOf(unit.Target));
        Assert.Equal(RussianUnreadCount, RawValueReconstruction.Reconstruct(target).RawValue);
        Assert.True(target.Properties.IsLocked);
        var outcome = Assert.Single(processor.Outcomes);
        Assert.Equal(ExpandOutcome.Kept, outcome.Outcome);
        Assert.Equal(1, outcome.Segments);
    }

    [Theory]
    [InlineData("tm", 99, TextContextMatchLevel.None, 100, false)]
    [InlineData("tm", 99, TextContextMatchLevel.None, 95, true)]
    [InlineData("tm", 100, TextContextMatchLevel.SourceAndTarget, 100, true)]
    [InlineData("tm", 0, TextContextMatchLevel.SourceAndTarget, 100, true)]
    [InlineData("document-match", 0, TextContextMatchLevel.None, 100, true)]
    [InlineData("mt", 0, TextContextMatchLevel.None, 0, true)]
    [InlineData(null, 0, TextContextMatchLevel.None, 0, false)]
    public void The_keep_rule_counts_perfect_and_context_matches_as_full_and_an_absent_origin_as_none(
        string? originType, byte percent, TextContextMatchLevel context, int minimum, bool kept)
    {
        var unit = PreTranslated(RussianUnreadCount, originType, percent, context);
        var processor = Processor("ru-RU", Keep(minimum));

        processor.ProcessParagraphUnit(unit);

        Assert.Equal(kept ? 1 : 0, processor.Kept);
        Assert.Equal(kept ? 1 : 4, SegmentsOf(unit.Target).Count);
    }

    [Fact]
    public void Kept_segments_stay_unlocked_when_the_lock_setting_is_off_and_nothing_is_kept_when_keeping_is_off()
    {
        var unlocked = PreTranslated(RussianUnreadCount, "tm", 100);
        Processor("ru-RU", Keep(lockKept: false)).ProcessParagraphUnit(unlocked);
        Assert.False(Assert.Single(SegmentsOf(unlocked.Target)).Properties.IsLocked);

        var expanded = PreTranslated(RussianUnreadCount, "tm", 100);
        var processor = Processor("ru-RU");
        processor.ProcessParagraphUnit(expanded);
        Assert.Equal(0, processor.Kept);
        Assert.Equal(4, SegmentsOf(expanded.Target).Count);
        Assert.All(SegmentsOf(expanded.Target), t => Assert.Equal(100, t.Properties.TranslationOrigin.MatchPercent));
    }

    /// <summary>
    /// Studio shows the segment id as the segment number, so the numbers must run on through
    /// the file, through units the task lays out and units it leaves alone, as Studio's own
    /// filters number them (Project 52, 11 October 2026).
    /// </summary>
    [Fact]
    public void Segment_numbers_run_on_through_the_file_across_touched_and_untouched_units()
    {
        var processor = Processor("ru-RU");
        var plain = ParagraphUnits.WithSegments("Message Centre", "Welcome.");
        var expanded = ParagraphUnits.Json(UnreadCount, "['inbox.unreadCount']");
        var protectedUnit = ParagraphUnits.Json("Hello {name}!", "['app.greeting']");
        var kept = PreTranslated(RussianUnreadCount, "tm", 100);

        processor.ProcessParagraphUnit(plain);
        processor.ProcessParagraphUnit(expanded);
        processor.ProcessParagraphUnit(protectedUnit);
        var keeping = Processor("ru-RU", Keep());

        Assert.Equal(["1", "2"], SegmentsOf(plain.Source).Select(s => s.Properties.Id.Id));
        Assert.Equal(["3", "4", "5", "6"], SegmentsOf(expanded.Source).Select(s => s.Properties.Id.Id));
        Assert.Equal(["3", "4", "5", "6"], SegmentsOf(expanded.Target).Select(s => s.Properties.Id.Id));
        Assert.Equal(["7"], SegmentsOf(protectedUnit.Source).Select(s => s.Properties.Id.Id));

        // The seeding record on the context uses the file-wide numbers, which is what the
        // ICU Forms reader matches rows on.
        Assert.Equal("4:other,5:other", ResourceKey.IcuContextOf(expanded)!.GetMetaData("icu:seededFrom"));
        var rows = Rows(expanded);
        Assert.Equal("other", rows[1].SeededFrom);
        Assert.Equal("", rows[0].SeededFrom);

        // A kept unit is numbered too.
        keeping.ProcessParagraphUnit(plain);
        keeping.ProcessParagraphUnit(kept);
        Assert.Equal(["3"], SegmentsOf(kept.Source).Select(s => s.Properties.Id.Id));
    }

    [Fact]
    public void An_empty_target_is_never_kept()
    {
        var unit = ParagraphUnits.Json(UnreadCount, "['inbox.unreadCount']");
        var processor = Processor("ru-RU", Keep(0));

        processor.ProcessParagraphUnit(unit);

        Assert.Equal(0, processor.Kept);
        Assert.Equal(4, SegmentsOf(unit.Target).Count);
    }

    /// <summary>
    /// A comment on text the developer wrote once, outside the plural, reaches every form the
    /// hoisting copies that text into; a comment inside one branch reaches that form alone.
    /// </summary>
    [Fact]
    public void A_comment_on_a_pre_translated_messages_shared_text_comes_back_on_every_form()
    {
        var unit = PreTranslated(RussianUnreadCount, "tm", 100);
        var segment = SegmentsOf(unit.Target)[0];
        ParagraphUnits.WrapPhrase(segment, "Привет", "greeting");
        ParagraphUnits.WrapPhrase(segment, "сообщений", "genitive plural");
        var processor = Processor("ru-RU");

        processor.ProcessParagraphUnit(unit);

        Assert.Empty(processor.Warnings);
        var targets = SegmentsOf(unit.Target);
        Assert.Equal(4, targets.Count);
        Assert.All(targets, t =>
        {
            var greeting = ParagraphUnits.MarkersOf(t)[0];
            Assert.Equal("greeting", ParagraphUnits.CommentText(greeting));
            Assert.Equal("Привет", RawValueReconstruction.Reconstruct(greeting).RawValue);
        });
        Assert.Equal(new[] { 1, 1, 2, 1 }, targets.Select(t => ParagraphUnits.MarkersOf(t).Count));
        var many = ParagraphUnits.MarkersOf(targets[2])[1];
        Assert.Equal("genitive plural", ParagraphUnits.CommentText(many));
        Assert.Equal("сообщений", RawValueReconstruction.Reconstruct(many).RawValue);
        Assert.Equal("Привет, {name}, у вас # непрочитанных сообщений!", RawValueReconstruction.Reconstruct(targets[2]).RawValue);
    }
}
