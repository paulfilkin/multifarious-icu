using Icu.Core;
using Icu.Core.Tests;
using multifarious.Icu.BatchTasks;
using multifarious.Icu.BatchTasks.Services;
using multifarious.Icu.Expansion;
using Sdl.FileTypeSupport.Framework.BilingualApi;

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
}
