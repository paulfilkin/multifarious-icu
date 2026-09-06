using System.Text;
using System.Xml;
using multifarious.Icu.BatchTasks.Settings;
using multifarious.Icu.BatchTasks.Settings.ViewModels;
using multifarious.Icu.Expansion;
using Sdl.Core.Settings;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// The settings groups through a real Studio settings bundle: the defaults a project that has
/// never seen the page gets, the round trip through the bundle's XML, which is how the project
/// file stores them, and the view models the pages bind to.
/// </summary>
public class SettingsTests
{
    private static ISettingsBundle Bundle() => SettingsUtil.CreateSettingsBundle(null);

    private static ISettingsBundle RoundTrip(ISettingsBundle bundle)
    {
        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, new XmlWriterSettings { OmitXmlDeclaration = true }))
        {
            SettingsUtil.SerializeSettingsBundle(writer, bundle);
        }

        using var reader = XmlReader.Create(new StringReader(builder.ToString()));
        return SettingsUtil.DeserializeSettingsBundle(reader, null);
    }

    [Fact]
    public void A_fresh_expand_group_yields_the_design_defaults()
    {
        var settings = Bundle().GetSettingsGroup<IcuExpandSettings>();

        Assert.Equal(ExpansionOptions.Default, settings.ToOptions());
    }

    [Fact]
    public void A_fresh_finalise_group_yields_the_design_defaults()
    {
        var settings = Bundle().GetSettingsGroup<IcuFinaliseSettings>();

        Assert.Equal(FinaliseOptions.Default, settings.ToOptions());
    }

    [Fact]
    public void Expand_values_survive_the_bundles_xml_round_trip()
    {
        var bundle = Bundle();
        var settings = bundle.GetSettingsGroup<IcuExpandSettings>();
        settings.ExpandCardinal = false;
        settings.ExpandOrdinal = false;
        settings.SeedStrategy = SourceSeedStrategy.AlwaysOther;
        settings.IncludeHints = false;
        settings.MaxUnitsPerMessage = 48;
        settings.OnParseError = ParseErrorBehaviour.FailTask;

        var restored = RoundTrip(bundle).GetSettingsGroup<IcuExpandSettings>();

        Assert.Equal(
            new ExpansionOptions
            {
                ExpandCardinal = false,
                ExpandOrdinal = false,
                SourceSeedStrategy = SourceSeedStrategy.AlwaysOther,
                IncludeHints = false,
                MaxUnitsPerMessage = 48,
                OnParseError = ParseErrorBehaviour.FailTask,
            },
            restored.ToOptions());
    }

    [Fact]
    public void Finalise_values_survive_the_bundles_xml_round_trip()
    {
        var bundle = Bundle();
        var settings = bundle.GetSettingsGroup<IcuFinaliseSettings>();
        settings.OnEmptyBranch = EmptyBranchBehaviour.FailTask;
        settings.OnPlaceholderMismatch = PlaceholderMismatchBehaviour.FailTask;

        var restored = RoundTrip(bundle).GetSettingsGroup<IcuFinaliseSettings>();

        Assert.Equal(EmptyBranchBehaviour.FailTask, restored.OnEmptyBranch);
        Assert.Equal(PlaceholderMismatchBehaviour.FailTask, restored.OnPlaceholderMismatch);
    }

    [Fact]
    public void Unreadable_stored_values_fall_back_to_the_defaults()
    {
        // A hand-edited project file, or a value from a later version this one does not know.
        var settings = Bundle().GetSettingsGroup<IcuExpandSettings>();
        settings.GetSetting<string>("SeedStrategy").Value = "bogus";
        settings.GetSetting<string>("OnParseError").Value = "";
        settings.MaxUnitsPerMessage = 100000;

        var options = settings.ToOptions();

        Assert.Equal(SourceSeedStrategy.MatchingElseOther, options.SourceSeedStrategy);
        Assert.Equal(ParseErrorBehaviour.PassThrough, options.OnParseError);
        Assert.Equal(IcuExpandSettings.MaximumBudget, options.MaxUnitsPerMessage);
    }

    [Fact]
    public void Reset_returns_a_group_to_the_defaults()
    {
        var settings = Bundle().GetSettingsGroup<IcuExpandSettings>();
        settings.ExpandCardinal = false;
        settings.MaxUnitsPerMessage = 36;

        settings.Reset();

        Assert.True(settings.ExpandCardinal);
        Assert.Equal(24, settings.MaxUnitsPerMessage);
    }

    [Fact]
    public void The_expand_view_model_loads_applies_and_resets()
    {
        var settings = Bundle().GetSettingsGroup<IcuExpandSettings>();
        var viewModel = new IcuExpandSettingsViewModel(settings);
        Assert.True(viewModel.ExpandCardinal);
        Assert.True(viewModel.SeedIsMatchingElseOther);
        Assert.Equal("24", viewModel.BudgetText);
        Assert.True(viewModel.ParseErrorPassesThrough);

        viewModel.ExpandOrdinal = false;
        viewModel.SeedIsAlwaysOther = true;
        viewModel.IncludeHints = false;
        viewModel.BudgetText = "36";
        viewModel.ParseErrorFailsTask = true;
        viewModel.Apply();

        Assert.False(settings.ExpandOrdinal);
        Assert.Equal(SourceSeedStrategy.AlwaysOther, settings.SeedStrategy);
        Assert.False(settings.IncludeHints);
        Assert.Equal(36, settings.MaxUnitsPerMessage);
        Assert.Equal(ParseErrorBehaviour.FailTask, settings.OnParseError);
        Assert.False(viewModel.SeedIsMatchingElseOther);

        viewModel.ResetToDefaults();

        Assert.True(viewModel.ExpandOrdinal);
        Assert.True(viewModel.SeedIsMatchingElseOther);
        Assert.Equal("24", viewModel.BudgetText);
        Assert.Equal(24, settings.MaxUnitsPerMessage);
    }

    [Theory]
    [InlineData("24", true)]
    [InlineData(" 1 ", true)]
    [InlineData("999", true)]
    [InlineData("0", false)]
    [InlineData("1000", false)]
    [InlineData("many", false)]
    [InlineData("", false)]
    public void The_budget_is_validated_as_typed_and_an_invalid_one_is_not_applied(string typed, bool valid)
    {
        var settings = Bundle().GetSettingsGroup<IcuExpandSettings>();
        var viewModel = new IcuExpandSettingsViewModel(settings) { BudgetText = typed };

        Assert.Equal(valid, viewModel.IsValid);

        viewModel.Apply();
        Assert.Equal(valid ? int.Parse(typed.Trim()) : 24, settings.MaxUnitsPerMessage);
    }

    [Fact]
    public void The_finalise_view_model_loads_applies_and_resets()
    {
        var settings = Bundle().GetSettingsGroup<IcuFinaliseSettings>();
        var viewModel = new IcuFinaliseSettingsViewModel(settings);
        Assert.True(viewModel.EmptyBranchUsesSource);
        Assert.True(viewModel.MismatchWarns);

        viewModel.EmptyBranchFailsTask = true;
        viewModel.MismatchFailsTask = true;
        viewModel.Apply();

        Assert.Equal(EmptyBranchBehaviour.FailTask, settings.OnEmptyBranch);
        Assert.Equal(PlaceholderMismatchBehaviour.FailTask, settings.OnPlaceholderMismatch);

        viewModel.ResetToDefaults();

        Assert.True(viewModel.EmptyBranchUsesSource);
        Assert.True(viewModel.MismatchWarns);
    }
}
