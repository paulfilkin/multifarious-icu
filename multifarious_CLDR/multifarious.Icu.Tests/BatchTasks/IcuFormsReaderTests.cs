using multifarious.Icu.BatchTasks.Services;
using multifarious.Icu.Expansion;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// The ICU Forms model read back from real expanded paragraph units: one row per segment
/// with the form, its counts and rendered sentences, the active-count resolver, and the
/// target projection with its parse result, which is what the view shows while typing.
/// </summary>
public class IcuFormsReaderTests
{
    private const string UnreadCount =
        "Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!";

    private static IParagraphUnit Expanded(string value, string language = "ru-RU")
    {
        var unit = ParagraphUnits.Json(value, "['inbox.unreadCount']");
        new IcuExpandProcessor("en-GB", language, ExpansionOptions.Default, "1.0.0")
        {
            ItemFactory = ParagraphUnits.ItemFactory,
        }.ProcessParagraphUnit(unit);
        return unit;
    }

    [Fact]
    public void The_rows_and_their_tooltips_come_from_the_layout_cldr_and_the_unit_context()
    {
        var reader = new IcuFormsReader();
        var model = reader.Read(Expanded(UnreadCount), "ru-RU")!;

        Assert.Equal(["count:one", "count:few", "count:many", "count:other"], model.Rows.Select(r => r.Path));
        Assert.Equal(["one", "few", "many", "other"], model.Rows.Select(r => r.Category));
        Assert.All(model.Rows, r => Assert.Equal("plural", r.Selector));

        // The tooltip is composed: no comment is written anywhere, so the row says what a
        // comment used to say, from the layout, CLDR and the seeding record on the context.
        Assert.Contains("CLDR category: few", model.Rows[1].Comment);
        Assert.Contains("Used when the count is: 2, 3, 4", model.Rows[1].Comment);
        Assert.Contains("seeded from \"other\"", model.Rows[1].Comment);
        Assert.Contains("Grammar:", model.Rows[2].Comment);
        Assert.Equal("other", model.Rows[1].SeededFrom);
        Assert.Equal("", model.Rows[0].SeededFrom);

        // A nested select over plurals and a walked message place their segments the same way.
        const string gendered =
            "{gender, select, female {{count, plural, one {She has # item} other {She has # items}}} other {{count, plural, one {They have # item} other {They have # items}}}} in the basket.";
        var nested = reader.Read(Expanded(gendered), "ru-RU")!;
        Assert.Equal("gender:female/count:one", nested.Rows[0].Path);
        Assert.Equal("gender:other/count:other", nested.Rows[7].Path);
        Assert.Equal("plural", nested.Rows[0].Selector);
        Assert.Equal("one", nested.Rows[0].Category);
    }

    private static List<ISegment> TargetSegments(IParagraphUnit unit) =>
        ParagraphUnits.ItemsOf(unit.Target).OfType<ISegment>().ToList();

    private static void SetTarget(ISegment target, params object[] items)
    {
        target.Clear();
        foreach (var item in items)
        {
            if (item is string text)
            {
                target.Add(ParagraphUnits.ItemFactory.CreateText(ParagraphUnits.PropertiesFactory.CreateTextProperties(text)));
            }
            else
            {
                target.Add((IAbstractMarkupData)item);
            }
        }
    }

    private static ILockedContent Locked(string syntax)
    {
        var locked = ParagraphUnits.ItemFactory.CreateLockedContent(
            ParagraphUnits.PropertiesFactory.CreateLockedContentProperties(LockTypeFlags.Manual));
        locked.Content.Add(ParagraphUnits.ItemFactory.CreateText(ParagraphUnits.PropertiesFactory.CreateTextProperties(syntax)));
        return locked;
    }

    [Fact]
    public void A_russian_message_reads_as_four_rows_with_counts_and_rendered_sentences()
    {
        var unit = Expanded(UnreadCount);

        var model = new IcuFormsReader().Read(unit, "ru-RU")!;

        Assert.Equal("['inbox.unreadCount']", model.Key);
        Assert.Equal(UnreadCount, model.Pattern);
        Assert.Equal(["count"], model.ExpandedSelectors);
        Assert.Equal("count", model.CountArgument);
        Assert.Equal(["count:one", "count:few", "count:many", "count:other"], model.Rows.Select(r => r.Path));
        Assert.Equal(["one", "few", "many", "other"], model.Rows.Select(r => r.Category));
        Assert.All(model.Rows, r => Assert.Equal("plural", r.Selector));

        var one = model.Rows[0];
        Assert.Equal("1", one.SampleCount);
        Assert.Contains("21", one.Counts);
        Assert.Equal("Hello Anna, you have 1 unread message!", one.SourceRendered);
        Assert.True(one.TargetEmpty);
        Assert.Equal("", one.TargetRendered);
        Assert.Contains("CLDR category: one", one.Comment);

        // Russian 'other' is reachable only by fractional counts; the sample is one of them.
        var other = model.Rows[3];
        Assert.True(other.FractionalOnly);
        Assert.Contains(".", other.SampleCount);
        Assert.Equal("", other.SeededFrom);
        Assert.Equal("other", model.Rows[1].SeededFrom);
        Assert.True(model.Rows[1].SyntheticSource);
    }

    private static IPlaceholderTag Tag(string syntax)
    {
        var properties = ParagraphUnits.PropertiesFactory.CreatePlaceholderTagProperties(syntax);
        properties.DisplayText = syntax;
        return ParagraphUnits.ItemFactory.CreatePlaceholderTag(properties);
    }

    /// <summary>
    /// The expansion writes the arguments as placeholder tags, and a translator's target may
    /// hold tags, locked spans from an older file, or a locked tag. Every shape renders with the
    /// sample and counts as the same placeholder for parity.
    /// </summary>
    [Fact]
    public void Tags_locked_spans_and_locked_tags_render_and_match_alike()
    {
        var unit = Expanded(UnreadCount);
        var targets = TargetSegments(unit);
        var lockedTag = ParagraphUnits.ItemFactory.CreateLockedContent(
            ParagraphUnits.PropertiesFactory.CreateLockedContentProperties(LockTypeFlags.Manual));
        lockedTag.Content.Add(Tag("#"));
        SetTarget(targets[0], "Привет, ", Tag("{name}"), ", у вас ", Tag("#"), " сообщение!");
        SetTarget(targets[1], "Привет, ", Locked("{name}"), ", у вас ", lockedTag, " сообщения!");
        SetTarget(targets[2], "Привет, ", Tag("{name}"), ", у вас # сообщений!");

        var model = new IcuFormsReader().Read(unit, "ru-RU")!;

        Assert.Equal("Hello Anna, you have 1 unread message!", model.Rows[0].SourceRendered);
        Assert.Equal("Привет, Anna, у вас 1 сообщение!", model.Rows[0].TargetRendered);
        Assert.Null(model.Rows[0].PlaceholderWarning);
        Assert.Equal("Привет, Anna, у вас 2 сообщения!", model.Rows[1].TargetRendered);
        Assert.Null(model.Rows[1].PlaceholderWarning);
        Assert.Contains("missing #", model.Rows[2].PlaceholderWarning);
        Assert.True(model.Rows[2].TypedPound);
        Assert.True(model.TargetParses);
    }

    /// <summary>A placeholder pasted twice is an extra one, even though the set of names is the same (Project 56).</summary>
    [Fact]
    public void A_duplicated_placeholder_in_the_target_is_reported_as_extra()
    {
        var unit = Expanded(UnreadCount);
        var targets = TargetSegments(unit);
        SetTarget(targets[0], "Привет, ", Tag("{name}"), ", у вас ", Tag("#"), " ", Tag("#"), " сообщение!");
        SetTarget(targets[1], "Привет, ", Tag("{name}"), ", у вас сообщения!");

        var model = new IcuFormsReader().Read(unit, "ru-RU")!;

        Assert.Equal("extra #", model.Rows[0].PlaceholderWarning);
        Assert.Equal("missing #", model.Rows[1].PlaceholderWarning);
    }

    [Fact]
    public void A_translated_row_renders_the_target_with_the_same_samples_and_checks_parity()
    {
        var unit = Expanded(UnreadCount);
        var targets = TargetSegments(unit);
        SetTarget(targets[0], "Привет, ", Locked("{name}"), ", у вас ", Locked("#"), " сообщение!");
        SetTarget(targets[1], "Привет, у вас ", Locked("#"), " сообщения!");

        var model = new IcuFormsReader().Read(unit, "ru-RU")!;

        Assert.Equal("Привет, Anna, у вас 1 сообщение!", model.Rows[0].TargetRendered);
        Assert.False(model.Rows[0].TargetEmpty);
        Assert.Null(model.Rows[0].PlaceholderWarning);

        Assert.Equal("missing {name}", model.Rows[1].PlaceholderWarning);
        Assert.True(model.Rows[2].TargetEmpty);
    }

    [Fact]
    public void The_target_projection_is_escaped_as_finalise_writes_it_and_parsed()
    {
        var unit = Expanded(UnreadCount);
        var targets = TargetSegments(unit);
        SetTarget(targets[0], "It's ", Locked("{name}"), ", ", Locked("#"), " {literal}");

        var model = new IcuFormsReader().Read(unit, "ru-RU")!;

        Assert.True(model.TargetParses);
        Assert.Null(model.ParseError);
        Assert.Contains("It''s {name}, # '{literal}'", model.TargetProjection);
        Assert.StartsWith("{count, plural,", model.TargetProjection);

        // The rendered row shows the translator's own text, not the escaped form.
        Assert.Equal("It's Anna, 1 {literal}", model.Rows[0].TargetRendered);
    }

    [Fact]
    public void A_target_that_will_not_parse_reports_the_problem()
    {
        var unit = Expanded(UnreadCount);
        var targets = TargetSegments(unit);
        // A lost close brace on a locked span is what a damaged layout looks like.
        SetTarget(targets[0], "Text ", Locked("{count, plural,"));

        var model = new IcuFormsReader().Read(unit, "ru-RU")!;

        Assert.False(model.TargetParses);
        Assert.NotNull(model.ParseError);
    }

    [Fact]
    public void The_editors_live_copy_of_the_active_segment_replaces_the_units_copy()
    {
        // The editor's paragraph unit is what was last committed; the segment pair's target is
        // what the translator is typing into. A detached copy with the same id stands in.
        var unit = Expanded(UnreadCount);
        var live = ParagraphUnits.ItemFactory.CreateSegment(TargetSegments(unit)[1].Properties);
        SetTarget(live, "Привет, ", Locked("{name}"), ", у вас ", Locked("#"), " сообщения!");

        var model = new IcuFormsReader().Read(unit, "ru-RU", live)!;

        Assert.Equal("Привет, Anna, у вас 2 сообщения!", model.Rows[1].TargetRendered);
        Assert.True(model.Rows[0].TargetEmpty);
        Assert.Contains("few {Привет, {name}, у вас # сообщения!}", model.TargetProjection);
        Assert.True(model.TargetParses);
    }

    [Fact]
    public void A_typed_count_marks_the_rows_it_selects()
    {
        var reader = new IcuFormsReader();
        var model = reader.Read(Expanded(UnreadCount), "ru-RU")!;

        Assert.Equal(["count:one"], reader.RowsFor(model, "21").Select(r => r.Path));
        Assert.Equal(["count:few"], reader.RowsFor(model, " 3 ").Select(r => r.Path));
        Assert.Equal(["count:many"], reader.RowsFor(model, "0").Select(r => r.Path));
        Assert.Equal(["count:other"], reader.RowsFor(model, "1.5").Select(r => r.Path));
        Assert.Empty(reader.RowsFor(model, "abc"));
        Assert.Empty(reader.RowsFor(model, ""));
    }

    [Fact]
    public void Explicit_values_and_the_offset_read_back_and_resolve_first()
    {
        const string guests =
            "{count, plural, offset:1 =0 {Nobody is coming} =1 {Only you are coming} one {You and # other guest are coming} other {You and # other guests are coming}}.";
        var reader = new IcuFormsReader();
        var model = reader.Read(Expanded(guests), "ru-RU")!;

        Assert.Equal(["count:=0", "count:=1", "count:one", "count:few", "count:many", "count:other"], model.Rows.Select(r => r.Path));
        Assert.Equal("Nobody is coming.", model.Rows[0].SourceRendered);
        Assert.Equal("0", model.Rows[0].SampleCount);

        // The comment's examples are what '#' shows, the count after the offset (design 5.5).
        var one = model.Rows[2];
        Assert.Equal("1", one.SampleCount);
        Assert.Equal("You and 1 other guest are coming.", one.SourceRendered);

        Assert.Equal(["count:=1"], reader.RowsFor(model, "1").Select(r => r.Path));
        Assert.Equal(["count:one"], reader.RowsFor(model, "2").Select(r => r.Path));
    }

    /// <summary>
    /// Hoisting rewrites an outer plural's '#' to "{argument, number}". Each row renders that
    /// span with the outer form's own sample count, so "few / one" reads as 2 helpings for
    /// 1 diner and "many / few" as 0 helpings for 2 diners, not a fixed 2 in every row.
    /// </summary>
    [Fact]
    public void An_outer_plurals_count_marker_renders_with_that_rows_outer_count()
    {
        const string order =
            "{spam, plural, =0 {Egg and bacon, no spam} one {Egg, bacon and spam} other {Egg, bacon and # helpings of spam}} " +
            "for {diners, plural, one {# diner} other {# diners}}.";
        var model = new IcuFormsReader().Read(Expanded(order), "ru-RU")!;

        Assert.Equal(20, model.Rows.Count);
        var rendered = model.Rows.ToDictionary(r => r.Path, r => r.SourceRendered);
        Assert.Equal("Egg and bacon, no spam for 1 diner.", rendered["spam:=0/diners:one"]);
        Assert.Equal("Egg, bacon and 2 helpings of spam for 1 diner.", rendered["spam:few/diners:one"]);
        Assert.Equal("Egg, bacon and 0 helpings of spam for 2 diners.", rendered["spam:many/diners:few"]);
        Assert.Equal("Egg, bacon and 0.0 helpings of spam for 0.0 diners.", rendered["spam:other/diners:other"]);
    }

    [Fact]
    public void A_select_over_a_plural_reads_every_leaf_and_the_count_marks_all_rows_of_a_form()
    {
        const string gendered =
            "{gender, select, female {{count, plural, one {She has # item} other {She has # items}}} other {{count, plural, one {They have # item} other {They have # items}}}} in the basket.";
        var reader = new IcuFormsReader();
        var model = reader.Read(Expanded(gendered), "ru-RU")!;

        Assert.Equal(8, model.Rows.Count);
        Assert.Equal("gender:female/count:one", model.Rows[0].Path);
        Assert.Equal("She has 1 item in the basket.", model.Rows[0].SourceRendered);
        Assert.Null(model.CountArgument);
        Assert.Empty(reader.RowsFor(model, "3"));
    }

    [Fact]
    public void A_protected_message_reads_as_one_row_and_a_plain_unit_reads_as_nothing()
    {
        var reader = new IcuFormsReader();

        var protectedModel = reader.Read(Expanded("Hello {name}, welcome back!"), "ru-RU")!;
        var row = Assert.Single(protectedModel.Rows);
        Assert.Equal("", row.Path);
        Assert.Equal("none", row.Selector);
        Assert.Equal("Hello Anna, welcome back!", row.SourceRendered);
        Assert.Null(protectedModel.CountArgument);
        Assert.True(protectedModel.TargetParses);

        Assert.Null(reader.Read(ParagraphUnits.Json("Message Centre", "['app.title']"), "ru-RU"));
    }
}
