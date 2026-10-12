using Icu.Core;
using multifarious.Icu.BatchTasks;
using multifarious.Icu.BatchTasks.Services;
using multifarious.Icu.Expansion;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// The filter's own tags through both tasks, as a configuration that tags embedded content
/// produces them (Project 56, 12 October 2026): tag pairs and placeholders inside a value.
/// Expand parses the value with a sentinel character for each tag and clones the tag into
/// every form its character reaches; Restore clones each form's tags into the restored
/// segment. The native projection never carries a sentinel, and a pair the layout would
/// split is refused.
/// </summary>
public class FilterTagTests
{
    private const string BoldCount =
        "You have <b>{count, plural, one {# unread message} other {# unread messages}}</b> today.";

    private static IcuExpandProcessor Expander(string language = "ru-RU", ExpansionOptions? options = null) =>
        new("en-GB", language, options ?? ExpansionOptions.Default, "2.0.0") { ItemFactory = ParagraphUnits.ItemFactory };

    private static IcuRestoreProcessor Restorer(string language = "ru-RU") =>
        new(language, RestoreOptions.Default) { ItemFactory = ParagraphUnits.ItemFactory };

    private static List<ISegment> SegmentsOf(IParagraph paragraph) => ParagraphUnits.SegmentsOf(paragraph);

    private static string Projection(IAbstractMarkupDataContainer container) =>
        RawValueReconstruction.Reconstruct(container).RawValue;

    private static IText Text(string value) => ParagraphUnits.Text(value);

    private static ITagPair Pair(string start, string end, string id, params IAbstractMarkupData[] content) =>
        ParagraphUnits.Pair(start, end, id, content);

    /// <summary>The unread-count message with the plural inside a bold pair, as the HTML embedded-content processor delivers it.</summary>
    private static IParagraphUnit BoldCountUnit() =>
        ParagraphUnits.JsonMarkup("['count.html']",
            Text("You have "),
            Pair("<b>", "</b>", "18", Text("{count, plural, one {# unread message} other {# unread messages}}")),
            Text(" today."));

    private static List<ITagPair> PairsOf(IAbstractMarkupDataContainer container) =>
        ParagraphUnits.ItemsOf(container).OfType<ITagPair>().ToList();

    private static void AssertNoSentinels(IAbstractMarkupDataContainer container)
    {
        Assert.DoesNotContain(Projection(container), c => c >= '' && c <= '');
    }

    // ---- the encoder ----------------------------------------------------------------------

    [Fact]
    public void The_encoder_puts_one_private_use_character_in_place_of_each_tag_and_decodes_it_back()
    {
        var unit = ParagraphUnits.JsonMarkup("['welcome.html']",
            Text("Hello "), Pair("<b>", "</b>", "6", Text("{name}")), Text(", you have "),
            Pair("<i>", "</i>", "9", Text("new")), Text(" messages"), ParagraphUnits.Placeholder("<br/>"), Text("."));

        var encoded = RawValueReconstruction.Encode(unit.Source);

        Assert.Equal(5, encoded.Sentinels.Count);
        Assert.Equal(new[] { SentinelKind.PairStart, SentinelKind.PairEnd, SentinelKind.PairStart, SentinelKind.PairEnd, SentinelKind.Placeholder },
            encoded.Sentinels.Select(s => s.Kind));
        Assert.Equal(new[] { 0, 0, 1, 1, -1 }, encoded.Sentinels.Select(s => s.PairIndex));
        Assert.Equal("Hello {name}, you have new messages.", encoded.RawValue);
        Assert.Equal("Hello <b>{name}</b>, you have <i>new</i> messages<br/>.", encoded.Decode(encoded.RawValue));
        Assert.Equal(encoded.Decode(encoded.RawValue), Projection(unit.Source));
        Assert.Same(encoded.Sentinels[0].Tag, encoded.Sentinels[1].Tag);
    }

    [Fact]
    public void The_encoder_skips_private_use_characters_the_text_already_uses()
    {
        var unit = ParagraphUnits.JsonMarkup("['k']", Text("AB"), Pair("<b>", "</b>", "1", Text("C")));

        var encoded = RawValueReconstruction.Encode(unit.Source);

        Assert.Equal("ABC", encoded.RawValue);
        Assert.Null(encoded.SentinelOf(''));
        Assert.Equal(SentinelKind.PairStart, encoded.SentinelOf('')!.Kind);
    }

    [Fact]
    public void A_value_without_tags_encodes_as_itself()
    {
        var unit = ParagraphUnits.Json("Hello {name}", "['k']");
        var encoded = RawValueReconstruction.Encode(unit.Source);
        Assert.False(encoded.HasTags);
        Assert.Equal("Hello {name}", encoded.RawValue);
    }

    // ---- expand ---------------------------------------------------------------------------

    /// <summary>A pair around the whole plural is hoisted into every form with the text around it, and each form gets its own clone.</summary>
    [Fact]
    public void A_pair_around_the_plural_is_cloned_into_every_form()
    {
        var unit = BoldCountUnit();
        var expander = Expander();

        expander.ProcessParagraphUnit(unit);

        Assert.Equal(1, expander.Expanded);
        Assert.Empty(expander.Warnings);
        var sources = SegmentsOf(unit.Source);
        Assert.Equal(4, sources.Count);
        foreach (var segment in sources)
        {
            var pair = Assert.Single(PairsOf(segment));
            Assert.Equal("<b>", pair.StartTagProperties.TagContent);
            Assert.Equal("</b>", pair.EndTagProperties.TagContent);
            Assert.Equal("18", pair.StartTagProperties.TagId.Id);
            Assert.IsAssignableFrom<IPlaceholderTag>(pair[0]);
            Assert.Single(ParagraphUnits.ItemsOf(segment).OfType<IText>(), t => t.Properties.Text == "You have ");
        }
        Assert.Equal("You have <b># unread message</b> today.", Projection(sources[0]));
        Assert.Equal("You have <b># unread messages</b> today.", Projection(sources[3]));
        AssertNoSentinels(unit.Source);

        // The clones are distinct objects from each other and from the original.
        Assert.Equal(4, PairsOf(sources[0]).Concat(PairsOf(sources[1])).Concat(PairsOf(sources[2])).Concat(PairsOf(sources[3])).Distinct().Count());

        // The metadata is readable, tags as their native text.
        var context = ResourceKey.IcuContextOf(unit)!;
        Assert.Equal(BoldCount, context.GetMetaData("icu:pattern"));
        Assert.Equal("{count, plural, one {You have <b># unread message</b> today.} other {You have <b># unread messages</b> today.}}",
            context.GetMetaData("icu:hoisted"));

        // And the projection of the whole paragraph is the hoisted message with the markup in place.
        Assert.True(IcuParser.TryParse(Projection(unit.Source), out _, out _));
        Assert.Contains("one {You have <b># unread message</b> today.}", Projection(unit.Source));
    }

    [Fact]
    public void A_pair_inside_a_branch_stays_in_that_form_and_a_protected_message_keeps_its_pair()
    {
        var inBranch = ParagraphUnits.JsonMarkup("['files']",
            Text("{count, plural, one {"), Pair("<b>", "</b>", "3", Text("#")), Text(" file} other {"),
            Pair("<b>", "</b>", "4", Text("#")), Text(" files}}"));
        Expander().ProcessParagraphUnit(inBranch);
        var sources = SegmentsOf(inBranch.Source);
        Assert.Equal(4, sources.Count);
        Assert.Equal("<b>#</b> file", Projection(sources[0]));
        Assert.Equal("3", Assert.Single(PairsOf(sources[0])).StartTagProperties.TagId.Id);
        Assert.Equal("4", Assert.Single(PairsOf(sources[3])).StartTagProperties.TagId.Id);
        Assert.IsAssignableFrom<IPlaceholderTag>(Assert.Single(ParagraphUnits.ItemsOf(PairsOf(sources[1])[0])));

        var protected_ = ParagraphUnits.JsonMarkup("['welcome.html']",
            Text("Hello "), Pair("<b>", "</b>", "6", Text("{name}")), Text(", you have "),
            Pair("<i>", "</i>", "9", Text("new")), Text(" messages."));
        var expander = Expander();
        expander.ProcessParagraphUnit(protected_);
        Assert.Empty(expander.Warnings);
        var segment = Assert.Single(SegmentsOf(protected_.Source));
        Assert.Equal("Hello <b>{name}</b>, you have <i>new</i> messages.", Projection(segment));
        var pairs = PairsOf(segment);
        Assert.Equal(2, pairs.Count);
        Assert.IsAssignableFrom<IPlaceholderTag>(Assert.Single(ParagraphUnits.ItemsOf(pairs[0])));
        Assert.Equal("new", Projection(pairs[1]));
        AssertNoSentinels(protected_.Source);
    }

    [Fact]
    public void A_filter_placeholder_is_cloned_into_every_form_and_is_not_an_argument()
    {
        var unit = ParagraphUnits.JsonMarkup("['k']",
            Text("Line"), ParagraphUnits.Placeholder("<br/>"),
            Text("{count, plural, one {# file} other {# files}}"));

        Expander().ProcessParagraphUnit(unit);

        var sources = SegmentsOf(unit.Source);
        Assert.Equal(4, sources.Count);
        foreach (var segment in sources)
        {
            var tags = ParagraphUnits.ItemsOf(segment).OfType<IPlaceholderTag>().ToList();
            Assert.Equal(2, tags.Count);
            Assert.Equal("<br/>", tags[0].Properties.TagContent);
            Assert.False(IcuRestoreProcessor.IsArgumentTag(tags[0]));
            Assert.Equal("#", tags[1].Properties.TagContent);
            Assert.True(IcuRestoreProcessor.IsArgumentTag(tags[1]));
        }
        Assert.Equal("Line<br/># file", Projection(sources[0]));
    }

    /// <summary>A pair that opens in one branch and closes in another cannot be laid out: Studio's writers drop a split pair, so the unit is left alone.</summary>
    [Fact]
    public void A_pair_crossing_the_branch_structure_passes_the_unit_through_with_a_warning()
    {
        var unit = ParagraphUnits.JsonMarkup("['k']",
            Text("{count, plural, one {"), Pair("<b>", "</b>", "3", Text("# file} other {# files")), Text("}}"));
        var before = Projection(unit.Source);
        var expander = Expander();

        expander.ProcessParagraphUnit(unit);

        Assert.Equal(0, expander.Expanded);
        var warning = Assert.Single(expander.Warnings);
        Assert.Contains("tag pair crosses", warning.Reason);
        Assert.Equal(ExpandOutcome.PassedThrough, Assert.Single(expander.Outcomes).Outcome);
        Assert.Equal(before, Projection(unit.Source));
        Assert.Single(SegmentsOf(unit.Source));
        Assert.Single(PairsOf(SegmentsOf(unit.Source)[0]));
        Assert.Null(ResourceKey.IcuContextOf(unit));
        Assert.NotEmpty(ParagraphUnits.UnitComments(unit));
    }

    /// <summary>Studio models a pair that spans segments as a pair holding the segments; the encoder walks into it like any container.</summary>
    [Fact]
    public void A_pair_holding_the_segments_themselves_is_read_and_cloned()
    {
        var unit = ParagraphUnits.Json("{count, plural, one {# file.", "['k']");
        var paragraph = unit.Source;
        var first = (ISegment)paragraph[0];
        paragraph.Clear();
        var pair = Pair("<b>", "</b>", "5");
        pair.Add(first);
        var secondProperties = ParagraphUnits.ItemFactory.CreateSegmentPairProperties();
        secondProperties.Id = new SegmentId("2");
        var second = ParagraphUnits.ItemFactory.CreateSegment(secondProperties);
        second.Add(Text("} other {# files.}}"));
        pair.Add(second);
        paragraph.Add(pair);
        unit.Target.Clear();

        var expander = Expander();
        expander.ProcessParagraphUnit(unit);

        Assert.Empty(expander.Warnings);
        var sources = SegmentsOf(unit.Source);
        Assert.Equal(4, sources.Count);
        Assert.Equal("<b># file.</b>", Projection(sources[0]));
        Assert.Single(PairsOf(sources[0]));
    }

    // ---- restore and the round trip -------------------------------------------------------

    [Fact]
    public void Restore_clones_each_forms_tags_into_the_restored_segment_on_both_sides()
    {
        var unit = BoldCountUnit();
        Expander().ProcessParagraphUnit(unit);
        ParagraphUnits.CopySourceToTargets(unit);
        var restorer = Restorer();

        restorer.ProcessParagraphUnit(unit);

        Assert.Equal(1, restorer.Restored);
        Assert.Equal(0, restorer.Filled);
        Assert.True(restorer.Warnings.Count == 0, string.Join(" | ", restorer.Warnings.Select(w => w.Reason)));
        var source = Assert.Single(SegmentsOf(unit.Source));
        var target = Assert.Single(SegmentsOf(unit.Target));
        const string expected =
            "{count, plural, one {You have <b># unread message</b> today.} few {You have <b># unread messages</b> today.} many {You have <b># unread messages</b> today.} other {You have <b># unread messages</b> today.}}";
        Assert.Equal(expected, Projection(source));
        Assert.Equal(expected, Projection(target));
        Assert.Equal(4, PairsOf(source).Count);
        Assert.Equal(4, PairsOf(target).Count);
        Assert.All(PairsOf(target), p => Assert.Equal("18", p.StartTagProperties.TagId.Id));
        Assert.All(PairsOf(target), p => Assert.Equal("# unread message", Projection(p).Substring(0, 16)));
        Assert.Empty(ParagraphUnits.ItemsOf(target).OfType<IPlaceholderTag>());
        Assert.Empty(ParagraphUnits.ItemsOf(target).OfType<ILockedContent>());
        AssertNoSentinels(unit.Source);
        AssertNoSentinels(unit.Target);
        Assert.True(IcuParser.TryParse(Projection(target), out _, out _));
        Assert.True(ResourceKey.IsRestored(unit));
    }

    [Fact]
    public void A_restored_message_with_tags_expands_again_and_restores_to_the_same_message()
    {
        var unit = BoldCountUnit();
        Expander().ProcessParagraphUnit(unit);
        ParagraphUnits.CopySourceToTargets(unit);
        Restorer().ProcessParagraphUnit(unit);
        var restored = Projection(unit.Target);

        var expander = Expander();
        expander.ProcessParagraphUnit(unit);

        Assert.Equal(1, expander.Reexpanded);
        Assert.Empty(expander.Warnings);
        var targets = SegmentsOf(unit.Target);
        Assert.Equal(4, targets.Count);
        Assert.All(targets, t => Assert.Single(PairsOf(t)));
        Assert.Equal("You have <b># unread messages</b> today.", Projection(targets[1]));
        Assert.IsAssignableFrom<IPlaceholderTag>(PairsOf(targets[1])[0][0]);
        AssertNoSentinels(unit.Target);
        Assert.Equal(BoldCount, ResourceKey.IcuContextOf(unit)!.GetMetaData("icu:pattern"));

        Restorer().ProcessParagraphUnit(unit);
        Assert.Equal(restored, Projection(unit.Target));
        Assert.Equal(restored, Projection(unit.Source));
    }

    [Fact]
    public void A_comment_inside_a_pair_comes_back_inside_the_pair_and_one_around_it_around_it()
    {
        var unit = BoldCountUnit();
        Expander().ProcessParagraphUnit(unit);
        ParagraphUnits.CopySourceToTargets(unit);
        Restorer().ProcessParagraphUnit(unit);
        var target = SegmentsOf(unit.Target)[0];
        var pairs = PairsOf(target);

        // Inside the one form's pair: a word; around the few form's pair: the whole pair.
        ParagraphUnits.WrapPhrase(pairs[0], "unread message", "inside");
        var around = ParagraphUnits.TranslatorComment("around");
        var items = ParagraphUnits.ItemsOf(target);
        var index = items.IndexOf(pairs[1]);
        target.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            if (i == index)
            {
                around.Add(items[i]);
                target.Add(around);
            }
            else
            {
                target.Add(items[i]);
            }
        }

        var expander = Expander();
        expander.ProcessParagraphUnit(unit);

        Assert.Empty(expander.Warnings);
        var targets = SegmentsOf(unit.Target);
        var one = Assert.Single(PairsOf(targets[0]));
        var inside = Assert.Single(ParagraphUnits.MarkersOf(one));
        Assert.Equal("inside", ParagraphUnits.CommentText(inside));
        Assert.Equal("unread message", Projection(inside));
        Assert.Empty(ParagraphUnits.MarkersOf(targets[0]));

        var fewMarker = Assert.Single(ParagraphUnits.MarkersOf(targets[1]));
        Assert.Equal("around", ParagraphUnits.CommentText(fewMarker));
        var fewPair = Assert.Single(ParagraphUnits.ItemsOf(fewMarker).OfType<ITagPair>());
        Assert.Equal("<b># unread messages</b>", Projection(fewMarker));
        Assert.Empty(ParagraphUnits.MarkersOf(fewPair));
        Assert.Empty(ParagraphUnits.MarkersOf(targets[2]));
    }

    [Fact]
    public void An_untranslated_form_filled_from_the_source_gets_the_sources_tags()
    {
        var unit = ParagraphUnits.JsonMarkup("['k']",
            Text("Line"), ParagraphUnits.Placeholder("<br/>"),
            Pair("<b>", "</b>", "2", Text("{count, plural, one {# file} other {# files}}")));
        Expander().ProcessParagraphUnit(unit);
        var restorer = Restorer("ja-JP");

        restorer.ProcessParagraphUnit(unit);

        Assert.Equal(1, restorer.Filled);
        Assert.Equal(3, restorer.Pruned);
        var target = Assert.Single(SegmentsOf(unit.Target));
        Assert.Equal("{count, plural, other {Line<br/><b># files</b>}}", Projection(target));

        // The filled form carries the plugin's finding as a marker, rebuilt around the clones.
        var marker = Assert.Single(ParagraphUnits.MarkersOf(target));
        Assert.Equal(Constants.CommentAuthor, marker.Comments.GetItem(0).Author);
        var placeholder = Assert.Single(ParagraphUnits.ItemsOf(marker).OfType<IPlaceholderTag>());
        Assert.Equal("<br/>", placeholder.Properties.TagContent);
        Assert.Single(PairsOf(marker));
    }
}
