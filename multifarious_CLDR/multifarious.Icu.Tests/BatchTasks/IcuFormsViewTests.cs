using System.Drawing;
using System.Resources;
using System.Text.RegularExpressions;
using multifarious.Icu.BatchTasks;
using multifarious.Icu.BatchTasks.Editor;
using multifarious.Icu.BatchTasks.Editor.ViewModels;
using multifarious.Icu.BatchTasks.Editor.Views;
using multifarious.Icu.BatchTasks.Services;
using multifarious.Icu.Expansion;
using Sdl.Desktop.IntegrationApi.DefaultLocations;
using Sdl.Desktop.IntegrationApi.Extensions;
using Sdl.Desktop.IntegrationApi.Interfaces;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;
using Sdl.TranslationStudioAutomation.IntegrationApi;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// The ICU Forms window: the attributes Studio discovers its parts by, the view model driven
/// with real paragraph units and no editor, and the markup's bindings read as text.
/// </summary>
public class IcuFormsViewTests
{
    private const string UnreadCount =
        "Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!";

    private static IParagraphUnit Expanded(string value, ExpansionOptions? options = null)
    {
        var unit = ParagraphUnits.Json(value, "['inbox.unreadCount']");
        new IcuExpandProcessor("en-GB", "ru-RU", options ?? ExpansionOptions.Default, "1.0.0")
        {
            ItemFactory = ParagraphUnits.ItemFactory,
        }.ProcessParagraphUnit(unit);
        return unit;
    }

    private static ResourceManager PluginResources() =>
        new("multifarious.Icu.BatchTasks.PluginResources", typeof(Constants).Assembly);

    [Fact]
    public void The_view_part_carries_the_attributes_studio_discovers_it_by()
    {
        var part = typeof(IcuFormsViewPartController).GetCustomAttributes(typeof(ViewPartAttribute), false)
            .Cast<ViewPartAttribute>().Single();
        var layout = typeof(IcuFormsViewPartController).GetCustomAttributes(typeof(ViewPartLayoutAttribute), false)
            .Cast<ViewPartLayoutAttribute>().Single();

        Assert.Equal(Constants.FormsViewPartId, part.Id);
        Assert.Equal(DockType.Bottom, layout.Dock);
        Assert.Equal(typeof(EditorController), layout.LocationByType);

        var resources = PluginResources();
        Assert.False(string.IsNullOrEmpty(resources.GetString(part.Name)));
        Assert.False(string.IsNullOrEmpty(resources.GetString(part.Description)));
        Assert.IsType<Icon>(resources.GetObject(part.Icon));

        Assert.True(typeof(IUIControl).IsAssignableFrom(typeof(IcuFormsView)), "the control must declare IUIControl");
    }

    [Fact]
    public void The_ribbon_group_and_action_sit_on_the_editors_addins_tab()
    {
        var group = typeof(IcuRibbonGroup).GetCustomAttributes(typeof(RibbonGroupAttribute), false).Cast<RibbonGroupAttribute>().Single();
        var groupLayout = typeof(IcuRibbonGroup).GetCustomAttributes(typeof(RibbonGroupLayoutAttribute), false).Cast<RibbonGroupLayoutAttribute>().Single();
        var action = typeof(ShowIcuFormsAction).GetCustomAttributes(typeof(ActionAttribute), false).Cast<ActionAttribute>().Single();
        var actionLayout = typeof(ShowIcuFormsAction).GetCustomAttributes(typeof(ActionLayoutAttribute), false).Cast<ActionLayoutAttribute>().Single();

        Assert.Equal(Constants.RibbonGroupId, group.Id);
        Assert.Equal(typeof(EditorController), group.ContextByType);
        Assert.Equal(typeof(StudioDefaultRibbonTabs.AddinsRibbonTabLocation), groupLayout.LocationByType);
        Assert.Equal(Constants.ShowFormsActionId, action.Id);
        Assert.Equal(typeof(IcuRibbonGroup), actionLayout.LocationByType);

        var resources = PluginResources();
        Assert.False(string.IsNullOrEmpty(resources.GetString(group.Name)));
        Assert.False(string.IsNullOrEmpty(resources.GetString(action.Name)));
        Assert.False(string.IsNullOrEmpty(resources.GetString(action.Description)));
        Assert.IsType<Icon>(resources.GetObject(action.Icon));
    }

    /// <summary>
    /// The count result names the branch the count selected on the outermost selector. In a
    /// nested message the first matched row's own category is an inner branch: the Arabic
    /// sync message walked over budget, 5 marks the two "other / ..." rows and must say
    /// "selects other", not the first row's "one" (Paul, 21 Sep 2026, Studio screenshot).
    /// </summary>
    [Fact]
    public void The_count_result_names_the_outer_branch_in_a_nested_message()
    {
        const string sync =
            "{files, plural, one {# file} other {# files}} synchronised across " +
            "{devices, plural, one {# device} other {# devices}}.";
        var unit = ParagraphUnits.Json(sync, "['sync.status']");
        new IcuExpandProcessor("en-GB", "ar-SA", ExpansionOptions.Default, "1.0.0")
        {
            ItemFactory = ParagraphUnits.ItemFactory,
        }.ProcessParagraphUnit(unit);
        var segments = ParagraphUnits.SegmentsOf(unit.Source);
        var viewModel = new IcuFormsViewModel();
        viewModel.Show(unit, segments[0].Properties.Id.Id, "ar-SA", "Arabic (Saudi Arabia)");

        viewModel.CountText = "5";
        Assert.Equal(["other / one", "other / other"], viewModel.MatchedRows.Select(r => r.Form));
        Assert.Equal("selects other", viewModel.CountResult);

        viewModel.CountText = "1";
        Assert.Equal(["one / one", "one / other"], viewModel.MatchedRows.Select(r => r.Form));
        Assert.Equal("selects one", viewModel.CountResult);
    }

    [Fact]
    public void The_view_model_shows_the_message_marks_the_active_row_and_resolves_a_count()
    {
        var unit = Expanded(UnreadCount);
        var segments = ParagraphUnits.SegmentsOf(unit.Source);
        var viewModel = new IcuFormsViewModel();
        Assert.False(viewModel.HasMessage);
        Assert.Equal("No document open", viewModel.EmptyTitle);

        viewModel.Show(unit, segments[1].Properties.Id.Id, "ru-RU", "Russian (Russia)");

        Assert.True(viewModel.HasMessage);
        Assert.Equal("['inbox.unreadCount']", viewModel.Key);
        Assert.Contains("Russian (Russia)", viewModel.Summary);
        Assert.Contains("4", viewModel.Summary);
        Assert.Equal(["one", "few", "many", "other"], viewModel.Rows.Select(r => r.Form));
        Assert.Equal([false, true, false, false], viewModel.Rows.Select(r => r.IsActive));
        Assert.Equal("(not translated)", viewModel.Rows[0].Target);
        Assert.True(viewModel.Rows[0].TargetEmpty);
        Assert.Contains("fractional counts only", viewModel.Rows[3].Counts);
        Assert.True(viewModel.ShowsCountBox);
        Assert.True(viewModel.Parses);
        Assert.Equal("Valid ICU", viewModel.ParseStatus);
        Assert.StartsWith("{count, plural,", viewModel.Projection);

        viewModel.CountText = "22";
        Assert.Equal(["few"], viewModel.MatchedRows.Select(r => r.Form));
        Assert.Equal("selects few", viewModel.CountResult);

        viewModel.CountText = "x";
        Assert.Empty(viewModel.MatchedRows);
        Assert.Equal("not a count", viewModel.CountResult);

        viewModel.CountText = "";
        Assert.Equal("", viewModel.CountResult);

        viewModel.ShowNoDocument();
        Assert.False(viewModel.HasMessage);
        Assert.Empty(viewModel.Rows);
    }

    [Fact]
    public void Each_empty_state_has_a_title_the_purpose_and_a_note()
    {
        var viewModel = new IcuFormsViewModel();

        viewModel.Show(ParagraphUnits.Json("Message Centre", "['app.title']"), "1", "ru-RU", "Russian (Russia)");
        Assert.False(viewModel.HasMessage);
        Assert.Equal("Not an ICU message", viewModel.EmptyTitle);
        Assert.Contains("ordinary text", viewModel.EmptyNote);
        Assert.Contains("ICU MessageFormat", viewModel.EmptyBody);

        viewModel.ShowUnsupported("Word 2007-2019 v 1.0.0.0");
        Assert.Equal("Not available for this file", viewModel.EmptyTitle);
        Assert.Contains("\"Word 2007-2019 v 1.0.0.0\"", viewModel.EmptyNote);
        Assert.Contains("JSON and Java Resources", viewModel.EmptyNote);

        viewModel.ShowUnsupported("");
        Assert.DoesNotContain("\"\"", viewModel.EmptyNote);
        Assert.Contains("another type", viewModel.EmptyNote);

        viewModel.ShowNotExpanded();
        Assert.Equal("No ICU messages expanded in this file", viewModel.EmptyTitle);
        Assert.Contains("Expand ICU", viewModel.EmptyNote);

        viewModel.ShowNoDocument();
        Assert.Equal("No document open", viewModel.EmptyTitle);
        Assert.Empty(viewModel.Rows);
    }

    private static string Xaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "views", "IcuFormsView.xaml");
        Assert.True(File.Exists(path), "the view's XAML must be copied to the test output");
        return Regex.Replace(File.ReadAllText(path), "<!--.*?-->", string.Empty, RegexOptions.Singleline);
    }

    /// <summary>
    /// A TwoWay binding onto a getter-only property throws when the control loads, and inside
    /// Studio that takes the editor down. Everything the window shows is read-only except the
    /// count box, so every other binding says OneWay.
    /// </summary>
    [Fact]
    public void Every_binding_except_the_count_box_is_one_way()
    {
        var offenders = new List<string>();
        foreach (Match match in Regex.Matches(Xaml(), @"\{Binding\s+([^}]*)\}"))
        {
            var expression = match.Groups[1].Value;
            var path = expression.Split(',')[0].Trim();
            if (path.StartsWith("Path=", StringComparison.Ordinal)) path = path.Substring(5).Trim();
            if (path == "CountText") continue;
            if (!expression.Contains("Mode=OneWay")) offenders.Add(expression);
        }

        Assert.True(offenders.Count == 0, "bindings without Mode=OneWay:\n  " + string.Join("\n  ", offenders));

        // The count box writes back on every keystroke, so the rows re-mark as the number is typed.
        Assert.Matches(@"\{Binding\s+CountText[^}]*UpdateSourceTrigger=PropertyChanged", Xaml());
    }

    /// <summary>
    /// The zoom steps by a tenth between 60% and 250%, comes back to 100% on reset, and is
    /// remembered through the store file so a restart keeps it. A store holding rubbish or a
    /// value outside the range reads as 100%.
    /// </summary>
    [Fact]
    public void The_zoom_steps_within_its_range_and_is_remembered()
    {
        var store = Path.Combine(Path.GetTempPath(), "multifarious-icu-tests", Guid.NewGuid().ToString("N"), "zoom.txt");
        try
        {
            var zoom = new ZoomState(store);
            Assert.Equal(1.0, zoom.Factor);
            Assert.Equal(1.1, zoom.ZoomIn(), 5);
            Assert.Equal(1.0, zoom.ZoomOut(), 5);
            for (var i = 0; i < 30; i++) zoom.ZoomIn();
            Assert.Equal(ZoomState.Maximum, zoom.Factor, 5);
            Assert.False(zoom.CanZoomIn);
            for (var i = 0; i < 30; i++) zoom.ZoomOut();
            Assert.Equal(ZoomState.Minimum, zoom.Factor, 5);
            Assert.False(zoom.CanZoomOut);
            Assert.Equal(1.0, zoom.Reset());

            zoom.Set(1.5);
            Assert.Equal(1.5, new ZoomState(store).Factor, 5);

            File.WriteAllText(store, "nonsense");
            Assert.Equal(1.0, new ZoomState(store).Factor);
            File.WriteAllText(store, "9");
            Assert.Equal(1.0, new ZoomState(store).Factor);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(store), true);
        }
    }

    [Fact]
    public void The_view_model_raises_the_zoom_and_shows_it_as_a_percentage()
    {
        var store = Path.Combine(Path.GetTempPath(), "multifarious-icu-tests", Guid.NewGuid().ToString("N"), "zoom.txt");
        try
        {
            var model = new IcuFormsViewModel(zoom: new ZoomState(store));
            var raised = new List<string>();
            model.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            model.ZoomInCommand.Execute(null);
            Assert.Equal(1.1, model.Zoom, 5);
            Assert.Equal("110%", model.ZoomPercent);
            Assert.Contains(nameof(IcuFormsViewModel.Zoom), raised);
            Assert.Contains(nameof(IcuFormsViewModel.ZoomPercent), raised);

            model.ZoomResetCommand.Execute(null);
            Assert.Equal("100%", model.ZoomPercent);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(store), true);
        }

        // The scale is applied as a layout transform, so the rows wrap to the zoomed width.
        Assert.Matches(@"<ScaleTransform ScaleX=""\{Binding Zoom, Mode=OneWay\}"" ScaleY=""\{Binding Zoom, Mode=OneWay\}""", Xaml());
        Assert.Contains("PreviewMouseWheel=\"OnPreviewMouseWheel\"", Xaml());
    }

    /// <summary>
    /// A click on a row asks the editor to go to that segment (Paul, 14 October 2026: the window
    /// followed the editor but not the other way). The view model raises the request with the
    /// unit and segment ids; the controller, which alone holds the document, moves the editor.
    /// </summary>
    [Fact]
    public void A_clicked_row_asks_for_its_segment_by_unit_and_segment_id()
    {
        var unit = Expanded(UnreadCount);
        var segments = ParagraphUnits.SegmentsOf(unit.Source);
        var viewModel = new IcuFormsViewModel();
        viewModel.Show(unit, segments[0].Properties.Id.Id, "ru-RU", "Russian (Russia)");

        RowActivatedEventArgs? raised = null;
        viewModel.RowActivated += (_, e) => raised = e;

        viewModel.ActivateRow(viewModel.Rows[2]);

        Assert.NotNull(raised);
        Assert.Equal(unit.Properties.ParagraphUnitId.Id, raised!.ParagraphUnitId);
        Assert.Equal(segments[2].Properties.Id.Id, raised.SegmentId);
        Assert.Contains("MouseLeftButtonUp=\"OnRowClick\"", Xaml());
    }

    /// <summary>
    /// A message the expand task passed through carries no context of the plugin's, and Studio
    /// segments its raw value, so the window used to call it ordinary text (handover 12, the
    /// test file's DEVICE_NEEDS_TIME). It now reads the task's own warning off the unit and
    /// points at the setting that answers it.
    /// </summary>
    [Fact]
    public void A_passed_through_message_shows_the_units_warning_and_the_setting_to_change()
    {
        var overBudget = Expanded(UnreadCount, ExpansionOptions.Default with { MaxUnitsPerMessage = 1 });
        Assert.Single(ParagraphUnits.UnitComments(overBudget));
        var viewModel = new IcuFormsViewModel();

        viewModel.Show(overBudget, "1", "ru-RU", "Russian (Russia)");

        Assert.False(viewModel.HasMessage);
        Assert.Empty(viewModel.Rows);
        Assert.Equal("ICU message passed through unexpanded", viewModel.EmptyTitle);
        Assert.Contains("the message ['inbox.unreadCount'] as it was", viewModel.EmptyNote);
        Assert.Contains("Expansion is over the branch budget (4 segments needed, 1 allowed)", viewModel.EmptyNote);
        Assert.Contains("raise the branch budget on the Expand ICU page", viewModel.EmptyNote);
        Assert.DoesNotContain("ICU message passed through unexpanded: Expansion", viewModel.EmptyNote);

        var broken = Expanded("{count, plural, one {# unread message} other {# unread messages}");
        viewModel.Show(broken, "1", "ru-RU", "Russian (Russia)");

        Assert.Equal("ICU message passed through unexpanded", viewModel.EmptyTitle);
        Assert.Contains("The value does not parse as ICU", viewModel.EmptyNote);
        Assert.Contains("Correct the value in the native file", viewModel.EmptyNote);
        Assert.DoesNotContain("branch budget", viewModel.EmptyNote);
    }

    [Fact]
    public void Only_the_tasks_own_comment_makes_a_unit_passed_through()
    {
        // A translator's comment that happens to open with the same words is theirs, not the task's.
        var unit = ParagraphUnits.Json("Message Centre", "['app.title']");
        unit.Properties.Comments = ParagraphUnits.PropertiesFactory.CreateCommentProperties();
        unit.Properties.Comments.Add(ParagraphUnits.PropertiesFactory.CreateComment(
            Constants.PassedThroughCommentPrefix + "a note of my own", "Anna", Severity.Low));
        var viewModel = new IcuFormsViewModel();

        viewModel.Show(unit, "1", "ru-RU", "Russian (Russia)");

        Assert.Equal("Not an ICU message", viewModel.EmptyTitle);
    }

    /// <summary>
    /// An ordinary segment's note names the unit's key where the filter put one on the unit, so
    /// a comment-like value such as _comment is seen for what it is (Paul, 14 October 2026).
    /// </summary>
    [Fact]
    public void An_ordinary_segment_is_named_by_its_key_where_the_unit_has_one()
    {
        var viewModel = new IcuFormsViewModel();

        viewModel.Show(ParagraphUnits.Json("Strings for the device screens.", "['_comment']"), "1", "fr-FR", "French (France)");
        Assert.Equal("Not an ICU message", viewModel.EmptyTitle);
        Assert.Contains("ordinary text with nothing to expand: ['_comment'].", viewModel.EmptyNote);

        viewModel.Show(ParagraphUnits.Properties("Message Centre", "app.title"), "1", "fr-FR", "French (France)");
        Assert.Contains("nothing to expand: app.title.", viewModel.EmptyNote);

        // A sub-content unit under an embedded content processor carries no key of its own.
        viewModel.Show(ParagraphUnits.WithSegments("Message Centre"), "1", "fr-FR", "French (France)");
        Assert.Contains("ordinary text with nothing to expand. Move", viewModel.EmptyNote);
        Assert.DoesNotContain(":", viewModel.EmptyNote.Substring(0, viewModel.EmptyNote.IndexOf("Move", StringComparison.Ordinal)));
    }

    /// <summary>
    /// A large count is explained in the summary line where it comes from nested selectors,
    /// "g 2 × count 6"; a single selector, or a nesting whose product does not add up (a
    /// select branch with no plural under it is one row), keeps the plain line.
    /// </summary>
    [Fact]
    public void The_summary_says_where_nested_forms_come_from()
    {
        var nested = ParagraphUnits.Json(
            "{g, select, a {You have {count, plural, one {# file} other {# files}}} other {They have {count, plural, one {# file} other {# files}}}}",
            "['files']");
        var uneven = ParagraphUnits.Json(
            "{g, select, a {You have {count, plural, one {# file} other {# files}}} other {They have files}}",
            "['files.uneven']");
        foreach (var unit in new[] { nested, uneven })
        {
            new IcuExpandProcessor("en-GB", "ar-SA", ExpansionOptions.Default, "1.0.0") { ItemFactory = ParagraphUnits.ItemFactory }
                .ProcessParagraphUnit(unit);
        }
        var viewModel = new IcuFormsViewModel();

        viewModel.Show(nested, "1", "ar-SA", "Arabic (Saudi Arabia)");
        Assert.Equal(12, viewModel.Rows.Count);
        Assert.Equal("Arabic (Saudi Arabia), 12 form(s): g 2 × count 6", viewModel.Summary);

        viewModel.Show(uneven, "1", "ar-SA", "Arabic (Saudi Arabia)");
        Assert.Equal(7, viewModel.Rows.Count);
        Assert.Equal("Arabic (Saudi Arabia), 7 form(s)", viewModel.Summary);

        viewModel.Show(Expanded(UnreadCount), "1", "ru-RU", "Russian (Russia)");
        Assert.Equal("Russian (Russia), 4 form(s)", viewModel.Summary);
    }

    /// <summary>
    /// A message over the branch budget is laid out walked, the source's own branches with
    /// nothing category expanded, and the summary says so (Paul, 25 October 2026: a French
    /// message showing =0, =1 and other under every gender, with no word on why).
    /// </summary>
    [Fact]
    public void The_summary_says_when_a_message_was_laid_out_as_written()
    {
        // Russian needs four categories; the walked layout is the source's two branches.
        var walked = Expanded(UnreadCount, ExpansionOptions.Default with { MaxUnitsPerMessage = 2 });
        var nested = ParagraphUnits.Json(
            "{g, select, a {You have {count, plural, one {# file} other {# files}}} other {They have {count, plural, one {# file} other {# files}}}}",
            "['files']");
        new IcuExpandProcessor("en-GB", "ar-SA", ExpansionOptions.Default with { MaxUnitsPerMessage = 4 }, "1.0.0") { ItemFactory = ParagraphUnits.ItemFactory }
            .ProcessParagraphUnit(nested);
        var viewModel = new IcuFormsViewModel();

        viewModel.Show(walked, "1", "ru-RU", "Russian (Russia)");
        Assert.Equal(["one", "other"], viewModel.Rows.Select(r => r.Form));
        Assert.Equal("Russian (Russia), 2 form(s) laid out as written", viewModel.Summary);

        viewModel.Show(nested, "1", "ar-SA", "Arabic (Saudi Arabia)");
        Assert.Equal(4, viewModel.Rows.Count);
        Assert.Equal("Arabic (Saudi Arabia), 4 form(s) laid out as written: g 2 × count 2", viewModel.Summary);
    }

    [Fact]
    public void The_view_keeps_the_active_row_in_view_through_named_parts()
    {
        // The scroll logic lives in the code-behind and cannot run here; the markup names the
        // parts it reaches for, so a rename fails this test rather than a null reference in Studio.
        Assert.Contains("x:Name=\"RowsScroller\"", Xaml());
        Assert.Contains("x:Name=\"RowsControl\"", Xaml());
    }
}
