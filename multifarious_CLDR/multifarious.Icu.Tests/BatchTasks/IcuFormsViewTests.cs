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

    private static IParagraphUnit Expanded(string value)
    {
        var unit = ParagraphUnits.Json(value, "['inbox.unreadCount']");
        new IcuExpandProcessor("en-GB", "ru-RU", ExpansionOptions.Default, "1.0.0")
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
        Assert.Contains("ICU Expand Plural Forms", viewModel.EmptyNote);

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
}
