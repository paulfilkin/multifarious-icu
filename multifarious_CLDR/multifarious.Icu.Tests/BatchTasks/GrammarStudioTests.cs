using System.Text;
using System.Xml;
using Icu.Core;
using Icu.Core.Tree;
using multifarious.Icu.BatchTasks;
using multifarious.Icu.BatchTasks.Editor.ViewModels;
using multifarious.Icu.BatchTasks.Services;
using multifarious.Icu.BatchTasks.Settings;
using multifarious.Icu.BatchTasks.Settings.ViewModels;
using multifarious.Icu.Expansion;
using Sdl.Core.Settings;
using Sdl.FileTypeSupport.Framework.BilingualApi;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// The grammar dictionary at the Studio boundary (Phase 3): the settings and the page's
/// view model, the unit context the writer records, the ICU Forms rows read back from it,
/// the notes in the outcomes and the report, and the restore of a grammar-expanded unit.
/// </summary>
public class GrammarStudioTests
{
    private const string ReadyMinimal = "{vehicleGender, select, other {Your {vehicle} is ready to board.}}";

    private const string ExampleFile = """
        {
          "version": 1,
          "arguments": {
            "vehicle": {
              "gender": "vehicleGender",
              "article": "vehicleArticle",
              "supplies": { "fr": ["article"], "it": ["article"] }
            }
          },
          "other": "the plural form"
        }
        """;

    private static readonly GrammarDictionary WithFile = GrammarDictionary.Default.WithDeveloperFile(DeveloperFile.Parse(ExampleFile));

    private static IcuExpandProcessor Expander(string language, GrammarDictionary? dictionary) =>
        new("en-GB", language, ExpansionOptions.Default, "1.0.0", dictionary) { ItemFactory = ParagraphUnits.ItemFactory };

    private static IParagraphUnit Expanded(string value, string language, GrammarDictionary? dictionary, out IcuExpandProcessor processor)
    {
        var unit = ParagraphUnits.Json(value, "['boarding.readyMinimal']");
        processor = Expander(language, dictionary);
        processor.ProcessParagraphUnit(unit);
        return unit;
    }

    private static string Metadata(IParagraphUnit unit, string key) =>
        ResourceKey.IcuContextOf(unit)!.GetMetaData(key) ?? string.Empty;

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

    // ---- settings and the page ---------------------------------------------------------------

    [Fact]
    public void A_fresh_group_has_no_file_and_yields_the_shipped_default()
    {
        var settings = Bundle().GetSettingsGroup<IcuExpandSettings>();

        Assert.False(settings.HasGrammarDictionaryFile);
        var dictionary = settings.ToDictionary(out var failure);
        Assert.Null(failure);
        Assert.Same(GrammarDictionary.Default, dictionary);
    }

    [Fact]
    public void The_imported_file_survives_the_bundles_xml_round_trip_as_text()
    {
        var bundle = Bundle();
        var settings = bundle.GetSettingsGroup<IcuExpandSettings>();
        settings.GrammarDictionaryText = ExampleFile;
        settings.GrammarDictionaryName = "vehicles.json";
        settings.GrammarDictionaryImportedAt = "2026-10-13T15:00:00.0000000Z";

        var restored = RoundTrip(bundle).GetSettingsGroup<IcuExpandSettings>();

        Assert.Equal(ExampleFile, restored.GrammarDictionaryText);
        Assert.Equal("vehicles.json", restored.GrammarDictionaryName);
        var dictionary = restored.ToDictionary(out var failure);
        Assert.Null(failure);
        Assert.NotNull(dictionary.DeveloperFile);
        Assert.True(dictionary.Supplies("vehicle", "fr-FR", "article"));
        Assert.Equal("the plural form", dictionary.Resolve("gender", "de")!.Hint("other"));
    }

    [Fact]
    public void A_stored_file_that_no_longer_validates_falls_back_to_the_default_with_a_reason()
    {
        var settings = Bundle().GetSettingsGroup<IcuExpandSettings>();
        settings.GrammarDictionaryText = """{ "version": 2 }""";

        var dictionary = settings.ToDictionary(out var failure);

        Assert.Same(GrammarDictionary.Default, dictionary);
        Assert.Contains("version 2", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_imports_a_valid_file_refuses_an_invalid_one_and_removes()
    {
        var settings = Bundle().GetSettingsGroup<IcuExpandSettings>();
        var viewModel = new IcuExpandSettingsViewModel(settings);
        Assert.False(viewModel.HasDictionary);
        Assert.Contains("61", viewModel.DictionaryDefault, StringComparison.Ordinal);

        Assert.True(viewModel.TryImport("vehicles.json", ExampleFile));
        Assert.True(viewModel.HasDictionary);
        Assert.Null(viewModel.ImportError);
        Assert.Contains("vehicles.json", viewModel.DictionaryStatus, StringComparison.Ordinal);

        Assert.False(viewModel.TryImport("bad.json", """{ "version": 1, "keys": { "mood": { } } }"""));
        Assert.True(viewModel.HasImportError);
        Assert.Contains("no kind of that name", viewModel.ImportError, StringComparison.Ordinal);
        Assert.True(viewModel.HasDictionary);
        Assert.Equal(ExampleFile, viewModel.DictionaryText);

        var applied = viewModel.Apply();
        Assert.Equal(ExampleFile, applied.GrammarDictionaryText);
        Assert.Equal("vehicles.json", applied.GrammarDictionaryName);
        Assert.NotEqual("", applied.GrammarDictionaryImportedAt);

        viewModel.RemoveDictionary();
        Assert.False(viewModel.HasDictionary);
        Assert.Null(viewModel.ImportError);
        Assert.Equal("", viewModel.Apply().GrammarDictionaryText);
    }

    [Fact]
    public void The_template_is_a_valid_developers_file()
    {
        var text = IcuExpandSettingsViewModel.TemplateText();

        var dictionary = GrammarDictionary.Default.WithDeveloperFile(DeveloperFile.Parse(text));

        Assert.True(dictionary.Supplies("vehicle", "fr", "article"));
        Assert.Equal("vehicleArticle", dictionary.DependentArgumentName("vehicle", "article"));
        Assert.Equal(["MASKULINUM", "FEMININUM", "NEUTRUM", "other"], dictionary.Resolve("gender", "de", "line")!.Keys);
    }

    // ---- the unit context and the window -----------------------------------------------------

    [Fact]
    public void The_context_records_the_grammar_selects_and_the_forms()
    {
        var unit = Expanded(ReadyMinimal, "fr-FR", WithFile, out _);

        Assert.Equal(7, ParagraphUnits.SegmentsOf(unit.Source).Count);
        Assert.Equal(
            "vehicleGender,vehicleGender:MASCULINE/vehicleArticle,vehicleGender:FEMININE/vehicleArticle",
            Metadata(unit, "icu:expandedSelectors"));
        Assert.Equal(
            [
                "vehicleGender\tgender\tMASCULINE,FEMININE,other",
                "vehicleGender:MASCULINE/vehicleArticle\tarticle\tLE,L,other",
                "vehicleGender:FEMININE/vehicleArticle\tarticle\tLA,L,other",
            ],
            Metadata(unit, Constants.GrammarKey).Split('\n'));

        var forms = Metadata(unit, Constants.GrammarFormsKey).Split('\n');
        Assert.Equal(7, forms.Length);
        Assert.Equal("1\tarticle\tmasculine singular before a consonant\t\t", forms[0]);
        Assert.Equal("7\tgender\tthe plural form\t\t", forms[6]);
    }

    [Fact]
    public void The_window_shows_the_hint_where_a_plural_shows_its_counts()
    {
        var unit = Expanded(ReadyMinimal, "de-DE", GrammarDictionary.Default, out _);
        var model = new IcuFormsReader().Read(unit, "de-DE")!;

        Assert.Equal(["vehicleGender:MASCULINE", "vehicleGender:FEMININE", "vehicleGender:NEUTER", "vehicleGender:other"], model.Rows.Select(r => r.Path));
        var masculine = model.Rows[0];
        Assert.Equal("select", masculine.Selector);
        Assert.Equal("gender", masculine.GrammarKind);
        Assert.Equal("masculine", masculine.GrammarHint);
        Assert.Equal("der Zug", masculine.GrammarSample);
        Assert.Empty(masculine.Counts);
        Assert.Equal("other", masculine.SeededFrom);
        Assert.Equal("Grammar form: MASCULINE (masculine, e.g. der Zug)\nSource form: seeded from \"other\"", masculine.Comment);
        Assert.Equal("Grammar form: other (any other value)", model.Rows[3].Comment);

        var row = new IcuFormRowViewModel(masculine);
        Assert.Equal("masculine, e.g. der Zug", row.Counts);
        Assert.Equal("MASCULINE", row.Form);
    }

    [Fact]
    public void An_unsupplied_article_is_noted_on_the_forms_the_outcome_and_the_processor()
    {
        var unit = Expanded(ReadyMinimal, "fr-FR", GrammarDictionary.Default, out var processor);
        var model = new IcuFormsReader().Read(unit, "fr-FR")!;

        const string note = "French needs an article form for {vehicle}; the application does not supply vehicleArticle";
        Assert.Equal(3, model.Rows.Count);
        Assert.Equal(note, model.Rows[0].Needs);
        Assert.Equal(note, model.Rows[1].Needs);
        Assert.Equal("", model.Rows[2].Needs);
        Assert.EndsWith("\nNeeds: " + note, model.Rows[0].Comment, StringComparison.Ordinal);

        var recorded = Assert.Single(processor.Notes);
        Assert.Equal(note, recorded.Reason);
        Assert.Empty(processor.Warnings);
        var outcome = Assert.Single(processor.Outcomes);
        Assert.Equal(ExpandOutcome.Expanded, outcome.Outcome);
        Assert.Equal([note], outcome.Notes);
        Assert.Equal("", outcome.Detail);
    }

    [Fact]
    public void A_walked_select_has_no_grammar_record()
    {
        var unit = Expanded("{status, select, DELAYED {Late.} other {On time.}}", "fr-FR", WithFile, out var processor);
        var model = new IcuFormsReader().Read(unit, "fr-FR")!;

        Assert.Equal("", Metadata(unit, Constants.GrammarKey));
        Assert.Equal("", Metadata(unit, Constants.GrammarFormsKey));
        Assert.All(model.Rows, r => Assert.Equal("", r.GrammarKind));
        Assert.Equal("Select branch: DELAYED", model.Rows[0].Comment);
        Assert.Empty(processor.Notes);
    }

    [Fact]
    public void Without_a_dictionary_the_processor_walks_as_before()
    {
        var unit = Expanded(ReadyMinimal, "de-DE", null, out _);

        Assert.Single(ParagraphUnits.SegmentsOf(unit.Source));
        Assert.Equal("", Metadata(unit, "icu:expandedSelectors"));
        Assert.Equal("", Metadata(unit, Constants.GrammarKey));
    }

    // ---- restore ------------------------------------------------------------------------------

    [Fact]
    public void A_grammar_expanded_unit_restores_to_the_languages_message_and_the_source_as_written()
    {
        var unit = Expanded(ReadyMinimal, "fr-FR", WithFile, out _);
        ParagraphUnits.CopySourceToTargets(unit);

        new IcuRestoreProcessor("fr-FR", RestoreOptions.Default) { ItemFactory = ParagraphUnits.ItemFactory }.ProcessParagraphUnit(unit);

        Assert.Single(ParagraphUnits.SegmentsOf(unit.Source));
        Assert.Single(ParagraphUnits.SegmentsOf(unit.Target));
        Assert.True(ResourceKey.IsRestored(unit));
        Assert.Equal(ReadyMinimal, RawValueReconstruction.Reconstruct(unit.Source).RawValue);

        var target = IcuMessage.Parse(RawValueReconstruction.Reconstruct(unit.Target).RawValue);
        var gender = Assert.IsType<SelectorNode>(Assert.Single(target.Nodes));
        Assert.Equal("vehicleGender", gender.ArgumentName);
        Assert.Equal(["MASCULINE", "FEMININE", "other"], gender.Branches.Select(b => b.Key.Text));
        var article = Assert.IsType<SelectorNode>(Assert.Single(gender.Branches[0].Nodes));
        Assert.Equal("vehicleArticle", article.ArgumentName);
        Assert.Equal(["LE", "L", "other"], article.Branches.Select(b => b.Key.Text));
        Assert.Equal(["LA", "L", "other"], Assert.IsType<SelectorNode>(Assert.Single(gender.Branches[1].Nodes)).Branches.Select(b => b.Key.Text));
        Assert.Equal("Your {vehicle} is ready to board.", IcuSerialiser.Serialise(gender.Branches[2].Nodes));
    }

    // ---- the report ---------------------------------------------------------------------------

    [Fact]
    public void The_report_carries_the_dictionary_in_force_and_the_notes()
    {
        var context = new ReportContext
        {
            ProjectName = "Grammar",
            SourceLanguage = "English (United Kingdom)",
            TargetLanguage = "French (France)",
            RunAt = new DateTime(2026, 10, 13, 15, 0, 0),
            CldrVersion = "CLDR 48 (Unicode 16.0)",
            AppVersion = "2.1.0",
            GrammarDictionary = "vehicles.json",
        };
        var files = new List<ExpandReportFile>
        {
            new("grammar.json",
            [
                new ExpandUnitOutcome("u1", "boarding.readyMinimal", ExpandOutcome.Expanded, 3, null,
                    ["French needs an article form for {vehicle}; the application does not supply vehicleArticle"]),
            ]),
        };

        var xml = TaskReportWriter.Expand(context, ExpansionOptions.Default, files);

        Assert.Contains("vehicles.json", xml, StringComparison.Ordinal);
        Assert.Contains("notes=\"French needs an article form for {vehicle}; the application does not supply vehicleArticle\"", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("detail=", xml, StringComparison.Ordinal);
    }
}
