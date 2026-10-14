using multifarious.Icu.BatchTasks.Settings;
using multifarious.Icu.BatchTasks.Settings.ViewModels;
using multifarious.Icu.Expansion;
using Newtonsoft.Json.Linq;
using Sdl.Core.Settings;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// The Grammar dictionary tab's view model: the rows it shows for the shipped default and for
/// a project's file, the edits it writes into the developer's file, what it refuses, and the
/// round trip through import, export and the Expand page's Apply.
/// </summary>
public class GrammarPageTests
{
    private const string ExampleFile = """
        {
          "_note": ["kept as imported"],
          "version": 1,
          "arguments": {
            "vehicle": {
              "gender": "vehicleGender",
              "article": "vehicleArticle",
              "supplies": { "fr": ["article"], "it": ["article"] }
            },
            "line": {
              "gender": "lineGenus",
              "keys": { "gender": { "MASCULINE": "MASKULINUM", "FEMININE": "FEMININUM", "NEUTER": "NEUTRUM" } }
            }
          },
          "keys": {
            "gender": { "languages": { "cs": ["MASCULINE", "FEMININE", "NEUTER", "other"] } }
          },
          "other": "the plural form"
        }
        """;

    private static GrammarLanguageRow Row(GrammarDictionaryViewModel viewModel, string kind, string locale) =>
        viewModel.Languages.Single(row => row.Kind == kind && row.LocaleKey == locale);

    private static GrammarDictionary Effective(GrammarDictionaryViewModel viewModel) =>
        GrammarDictionary.Default.WithDeveloperFile(DeveloperFile.Parse(viewModel.DictionaryText));

    // ---- what the tables show ----------------------------------------------------------------

    [Fact]
    public void A_fresh_page_shows_the_shipped_default_and_nothing_else()
    {
        var viewModel = new GrammarDictionaryViewModel();

        Assert.Equal(["gender", "article"], viewModel.KindNames);
        Assert.Equal(["gender", "article", "count"], viewModel.RoleNames);
        Assert.Equal(63, viewModel.Languages.Count);
        Assert.Empty(viewModel.Arguments);
        Assert.False(viewModel.HasDictionary);
        Assert.True(viewModel.IsValid);
        Assert.Equal("", viewModel.OtherText);
        Assert.False(viewModel.HasRenames);

        var german = Row(viewModel, "gender", "de");
        Assert.Equal("MASCULINE, FEMININE, NEUTER, other", german.KeysText);
        Assert.False(german.IsProject);
        Assert.False(german.IsAddedByProject);
        Assert.Contains("der Zug", german.Detail, StringComparison.Ordinal);
        Assert.Contains("(de)", german.Language, StringComparison.Ordinal);

        var french = Row(viewModel, "gender", "fr");
        Assert.Contains("article", french.Detail, StringComparison.Ordinal);
        Assert.Contains("MASCULINE, FEMININE", french.Detail, StringComparison.Ordinal);

        // Kinds in the default's order, languages sorted within each.
        Assert.Equal("gender", viewModel.Languages.First().Kind);
        Assert.Equal("article", viewModel.Languages.Last().Kind);
        Assert.Equal(["fr", "it"], viewModel.Languages.Where(row => row.Kind == "article").Select(row => row.LocaleKey));
    }

    [Fact]
    public void An_imported_file_marks_its_rows_and_fills_the_argument_table()
    {
        var viewModel = new GrammarDictionaryViewModel();
        Assert.True(viewModel.TryImport("vehicles.json", ExampleFile));

        var czech = Row(viewModel, "gender", "cs");
        Assert.Equal("MASCULINE, FEMININE, NEUTER, other", czech.KeysText);
        Assert.True(czech.IsProject);
        Assert.False(czech.IsAddedByProject);
        Assert.False(Row(viewModel, "gender", "de").IsProject);

        Assert.Equal(["vehicle", "line"], viewModel.Arguments.Select(row => row.BaseName));
        var vehicle = viewModel.Arguments[0];
        Assert.Equal(["vehicleGender", "vehicleArticle", ""], vehicle.Roles.Select(role => role.ArgumentName));
        Assert.Equal("fr: article; it: article", vehicle.SuppliesText);
        Assert.False(vehicle.HasRespelling);
        var line = viewModel.Arguments[1];
        Assert.Contains("MASCULINE", line.Respelling, StringComparison.Ordinal);
        Assert.Contains("MASKULINUM", line.Respelling, StringComparison.Ordinal);

        Assert.Equal("the plural form", viewModel.OtherText);
        Assert.Equal(ExampleFile, viewModel.DictionaryText);
        Assert.Contains("vehicles.json", viewModel.DictionaryStatus, StringComparison.Ordinal);
        Assert.DoesNotContain("edited", viewModel.DictionaryStatus, StringComparison.Ordinal);
    }

    // ---- edits -------------------------------------------------------------------------------

    [Fact]
    public void Editing_a_key_set_writes_a_language_override_and_the_default_takes_it_back()
    {
        var viewModel = new GrammarDictionaryViewModel();
        var czech = Row(viewModel, "gender", "cs");
        Assert.Equal("ANIMATE, INANIMATE, FEMININE, NEUTER, other", czech.KeysText);

        czech.KeysText = "MASCULINE, FEMININE, NEUTER, other";

        Assert.True(viewModel.IsValid);
        Assert.True(viewModel.HasDictionary);
        Assert.True(czech.IsProject);
        Assert.Equal(["MASCULINE", "FEMININE", "NEUTER", "other"], Effective(viewModel).Resolve("gender", "cs-CZ")!.Keys);
        Assert.Contains("made on this page", viewModel.DictionaryStatus, StringComparison.Ordinal);
        Assert.Equal("", viewModel.DictionaryName);

        // Typing the default back removes the override rather than storing a copy of it.
        czech.KeysText = "ANIMATE, INANIMATE, FEMININE, NEUTER, other";
        Assert.False(czech.IsProject);
        Assert.Null(JObject.Parse(viewModel.DictionaryText)["keys"]);
    }

    [Fact]
    public void An_edit_that_does_not_validate_shows_its_reason_and_blocks_saving_until_fixed()
    {
        var viewModel = new GrammarDictionaryViewModel();
        var expand = new IcuExpandSettingsViewModel(SettingsUtil.CreateSettingsBundle(null).GetSettingsGroup<IcuExpandSettings>());
        var german = Row(expand.Grammar, "gender", "de");

        german.KeysText = "MASCULINE, FEMININE";

        Assert.False(expand.Grammar.IsValid);
        Assert.Contains("must end in 'other'", expand.Grammar.Error, StringComparison.Ordinal);
        Assert.False(expand.IsValid);

        german.KeysText = "MASCULINE, FEMININE, other";
        Assert.True(expand.Grammar.IsValid);
        Assert.Null(expand.Grammar.Error);
        Assert.True(expand.IsValid);
        Assert.True(viewModel.IsValid);
    }

    [Fact]
    public void Reset_restores_the_default_keys_and_removes_a_language_the_project_added()
    {
        var viewModel = new GrammarDictionaryViewModel();
        var german = Row(viewModel, "gender", "de");
        german.KeysText = "MASCULINE, FEMININE, other";
        Assert.True(german.IsProject);

        german.Reset();

        Assert.False(german.IsProject);
        Assert.Equal("MASCULINE, FEMININE, NEUTER, other", german.KeysText);

        viewModel.AddLanguageKind = "gender";
        viewModel.AddLanguageLocale = "eu";
        var basque = viewModel.AddLanguage();
        Assert.NotNull(basque);
        Assert.True(basque!.IsAddedByProject);
        Assert.Equal("other", basque.KeysText);
        Assert.Equal(64, viewModel.Languages.Count);
        Assert.Equal("eu", viewModel.Languages[viewModel.Languages.IndexOf(Row(viewModel, "gender", "es")) + 1].LocaleKey);

        basque.Reset();
        Assert.Equal(63, viewModel.Languages.Count);
        Assert.DoesNotContain(viewModel.Languages, row => row.LocaleKey == "eu");
    }

    [Fact]
    public void Adding_a_language_normalises_the_tag_and_refuses_what_is_not_one()
    {
        var viewModel = new GrammarDictionaryViewModel();
        viewModel.AddLanguageKind = "article";
        viewModel.AddLanguageLocale = "PT-br";

        var row = viewModel.AddLanguage();

        Assert.NotNull(row);
        Assert.Equal("pt-BR", row!.LocaleKey);
        Assert.Equal("", viewModel.AddLanguageLocale);
        Assert.Equal(["fr", "it", "pt-BR"], viewModel.Languages.Where(r => r.Kind == "article").Select(r => r.LocaleKey));

        viewModel.AddLanguageLocale = "not a tag";
        Assert.Null(viewModel.AddLanguage());
        Assert.False(viewModel.IsValid);
        Assert.Contains("not a language tag", viewModel.Error, StringComparison.Ordinal);
        Assert.Equal(64, viewModel.Languages.Count);

        // The next good edit clears the message.
        viewModel.AddLanguageLocale = "ca";
        Assert.NotNull(viewModel.AddLanguage());
        Assert.True(viewModel.IsValid);
    }

    [Fact]
    public void The_argument_table_writes_names_supplies_and_the_other_wording()
    {
        var viewModel = new GrammarDictionaryViewModel();

        var row = viewModel.AddArgument();
        Assert.Equal("argument", row.BaseName);
        row.BaseName = "vehicle";
        row.Roles.Single(role => role.Role == "gender").ArgumentName = "vehicleGender";
        row.Roles.Single(role => role.Role == "article").ArgumentName = "vehicleArticle";
        row.Roles.Single(role => role.Role == "count").ArgumentName = "vehicleCount";
        row.SuppliesText = "fr: article; it: article";
        viewModel.OtherText = "the plural form";

        Assert.True(viewModel.IsValid);
        var dictionary = Effective(viewModel);
        Assert.True(dictionary.Supplies("vehicle", "fr-FR", "article"));
        Assert.False(dictionary.Supplies("vehicle", "de", "article"));
        Assert.Equal("vehicleArticle", dictionary.DependentArgumentName("vehicle", "article"));
        Assert.Equal("vehicleGender", dictionary.Recognise("vehicleGender")!.ArgumentName);
        Assert.True(dictionary.Recognise("vehicleGender")!.Declared);
        Assert.Equal("the plural form", dictionary.Resolve("gender", "de")!.Hint("other"));

        var document = JObject.Parse(viewModel.DictionaryText);
        Assert.Equal("vehicleCount", (string)document["arguments"]!["vehicle"]!["count"]!);
        Assert.Equal(["article"], document["arguments"]!["vehicle"]!["supplies"]!["it"]!.Select(item => (string)item!));

        // Clearing a role and the supplies takes the entries out of the file.
        row.Roles.Single(role => role.Role == "count").ArgumentName = "";
        row.SuppliesText = "";
        document = JObject.Parse(viewModel.DictionaryText);
        Assert.Null(document["arguments"]!["vehicle"]!["count"]);
        Assert.Null(document["arguments"]!["vehicle"]!["supplies"]);

        row.Remove();
        Assert.Empty(viewModel.Arguments);
        Assert.Null(JObject.Parse(viewModel.DictionaryText)["arguments"]!["vehicle"]);
    }

    [Fact]
    public void An_argument_supplied_where_no_kind_needs_it_is_refused_in_place()
    {
        var viewModel = new GrammarDictionaryViewModel();
        var row = viewModel.AddArgument();
        row.BaseName = "vehicle";
        row.Roles.Single(role => role.Role == "gender").ArgumentName = "vehicleGender";
        row.Roles.Single(role => role.Role == "article").ArgumentName = "vehicleArticle";

        row.SuppliesText = "de: article";

        Assert.False(viewModel.IsValid);
        Assert.Contains("no kind of the argument needs it", viewModel.Error, StringComparison.Ordinal);

        row.SuppliesText = "fr: article";
        Assert.True(viewModel.IsValid);
    }

    [Fact]
    public void Renaming_an_argument_keeps_its_place_and_refuses_an_empty_or_repeated_name()
    {
        var viewModel = new GrammarDictionaryViewModel();
        Assert.True(viewModel.TryImport("vehicles.json", ExampleFile));
        var vehicle = viewModel.Arguments[0];

        vehicle.BaseName = "transport";

        Assert.True(viewModel.IsValid);
        Assert.Equal(["transport", "line"], JObject.Parse(viewModel.DictionaryText)["arguments"]!.Children<JProperty>().Select(p => p.Name));
        Assert.Equal("transport", vehicle.StoredName);
        Assert.True(Effective(viewModel).Supplies("transport", "fr", "article"));

        vehicle.BaseName = "line";
        Assert.False(viewModel.IsValid);
        Assert.Contains("'line' is already declared", viewModel.Error, StringComparison.Ordinal);
        Assert.Equal("transport", vehicle.StoredName);

        vehicle.BaseName = "";
        Assert.False(viewModel.IsValid);
        Assert.Contains("base name", viewModel.Error, StringComparison.Ordinal);

        vehicle.BaseName = "vehicle";
        Assert.True(viewModel.IsValid);
        Assert.Equal("vehicle", vehicle.StoredName);
    }

    // ---- the file around the edits -----------------------------------------------------------

    [Fact]
    public void Edits_keep_the_imported_note_and_entries_the_tables_do_not_touch()
    {
        var viewModel = new GrammarDictionaryViewModel();
        Assert.True(viewModel.TryImport("vehicles.json", ExampleFile));

        Row(viewModel, "gender", "de").KeysText = "MASCULINE, FEMININE, other";

        var document = JObject.Parse(viewModel.DictionaryText);
        Assert.Equal("kept as imported", (string)document["_note"]![0]!);
        Assert.Equal("MASKULINUM", (string)document["arguments"]!["line"]!["keys"]!["gender"]!["MASCULINE"]!);
        Assert.Equal(["MASCULINE", "FEMININE", "NEUTER", "other"], document["keys"]!["gender"]!["languages"]!["cs"]!.Select(item => (string)item!));
        Assert.Equal(["MASCULINE", "FEMININE", "other"], document["keys"]!["gender"]!["languages"]!["de"]!.Select(item => (string)item!));
        Assert.Contains("edited on this page", viewModel.DictionaryStatus, StringComparison.Ordinal);
        Assert.Equal("vehicles.json", viewModel.DictionaryName);
    }

    [Fact]
    public void The_edited_file_is_what_the_page_applies_and_the_task_reads_back()
    {
        var bundle = SettingsUtil.CreateSettingsBundle(null);
        var settings = bundle.GetSettingsGroup<IcuExpandSettings>();
        var viewModel = new IcuExpandSettingsViewModel(settings);
        Row(viewModel.Grammar, "gender", "cs").KeysText = "MASCULINE, FEMININE, NEUTER, other";
        viewModel.Grammar.OtherText = "the plural form";

        viewModel.Apply();

        Assert.True(settings.HasGrammarDictionaryFile);
        Assert.Equal("", settings.GrammarDictionaryName);
        var dictionary = settings.ToDictionary(out var failure);
        Assert.Null(failure);
        Assert.Equal(["MASCULINE", "FEMININE", "NEUTER", "other"], dictionary.Resolve("gender", "cs")!.Keys);
        Assert.Equal("the plural form", dictionary.Resolve("gender", "fr")!.Hint("other"));

        // A new view model over the same settings shows the edits as the project's.
        var reopened = new IcuExpandSettingsViewModel(settings);
        Assert.True(Row(reopened.Grammar, "gender", "cs").IsProject);
        Assert.Equal("the plural form", reopened.Grammar.OtherText);
        Assert.Contains("made on this page", reopened.Grammar.DictionaryStatus, StringComparison.Ordinal);

        reopened.ResetToDefaults();
        Assert.False(reopened.Grammar.HasDictionary);
        Assert.False(Row(reopened.Grammar, "gender", "cs").IsProject);
    }

    [Fact]
    public void A_stored_file_that_does_not_validate_is_shown_as_an_error_and_the_default_rows_stand()
    {
        var viewModel = new GrammarDictionaryViewModel();

        viewModel.Load("""{ "version": 1, "keys": { "mood": { } } }""", "mood.json", "");

        Assert.True(viewModel.HasDictionary);
        Assert.False(viewModel.IsValid);
        Assert.Contains("no kind of that name", viewModel.Error, StringComparison.Ordinal);
        Assert.Equal(63, viewModel.Languages.Count);
        Assert.Equal("MASCULINE, FEMININE, NEUTER, other", Row(viewModel, "gender", "de").KeysText);

        viewModel.RemoveDictionary();
        Assert.True(viewModel.IsValid);
        Assert.False(viewModel.HasDictionary);
    }

    [Fact]
    public void Keys_and_supplies_are_parsed_leniently()
    {
        Assert.Equal(["A", "B", "other"], GrammarDictionaryViewModel.ParseKeys(" A,B ; other "));
        Assert.Empty(GrammarDictionaryViewModel.ParseKeys(""));

        var supplies = GrammarDictionaryViewModel.ParseSupplies(" fr : article ; it: article, gender; broken; : article");
        Assert.Equal(["fr", "it"], supplies.Select(pair => pair.Key));
        Assert.Equal(["article", "gender"], supplies[1].Value);
    }

    // ---- where a refusal shows ---------------------------------------------------------------

    /// <summary>
    /// A refused edit is shown under the table it came from, on the row that caused it (Paul,
    /// test plan S4a and C17 to C20, 14 October 2026: a message at the top of a long page went
    /// unseen).
    /// </summary>
    [Fact]
    public void A_refused_key_set_marks_its_row_and_the_languages_block_until_fixed()
    {
        var viewModel = new GrammarDictionaryViewModel();
        var german = Row(viewModel, "gender", "de");
        var french = Row(viewModel, "gender", "fr");

        german.KeysText = "MASCULINE, FEMININE";

        Assert.True(viewModel.ErrorInLanguages);
        Assert.False(viewModel.ErrorInFile);
        Assert.False(viewModel.ErrorInArguments);
        Assert.True(german.HasError);
        Assert.False(french.HasError);

        german.KeysText = "MASCULINE, FEMININE, other";
        Assert.False(viewModel.HasError);
        Assert.False(viewModel.ErrorInLanguages);
        Assert.False(german.HasError);

        // A good edit on another row leaves the mark where the refusal is. French must keep
        // MASCULINE and FEMININE, which its article forms are needed under, so only the
        // order changes.
        german.KeysText = "MASCULINE";
        french.KeysText = "FEMININE, MASCULINE, other";
        Assert.True(viewModel.HasError);
        Assert.True(german.HasError);
        Assert.False(french.HasError);
        german.Reset();
        Assert.False(viewModel.HasError);
        Assert.False(german.HasError);
    }

    [Fact]
    public void A_refused_argument_marks_its_row_and_the_arguments_block()
    {
        var viewModel = new GrammarDictionaryViewModel();
        var row = viewModel.AddArgument();
        row.BaseName = "vehicle";
        row.Roles.Single(role => role.Role == "gender").ArgumentName = "vehicleGender";

        row.SuppliesText = "fr: article";

        Assert.True(viewModel.ErrorInArguments);
        Assert.False(viewModel.ErrorInLanguages);
        Assert.True(row.HasError);

        row.Roles.Single(role => role.Role == "article").ArgumentName = "vehicleArticle";
        Assert.False(viewModel.HasError);
        Assert.False(row.HasError);

        row.BaseName = "";
        Assert.True(viewModel.ErrorInArguments);
        Assert.True(row.HasError);
        row.BaseName = "vehicle";
        Assert.False(row.HasError);
    }

    [Fact]
    public void A_refused_language_tag_marks_the_tag_box_and_a_stored_file_the_file_block()
    {
        var viewModel = new GrammarDictionaryViewModel();
        viewModel.AddLanguageKind = "gender";
        viewModel.AddLanguageLocale = "not a tag";

        Assert.Null(viewModel.AddLanguage());

        Assert.True(viewModel.AddLanguageHasError);
        Assert.True(viewModel.ErrorInLanguages);

        viewModel.AddLanguageLocale = "eu";
        Assert.NotNull(viewModel.AddLanguage());
        Assert.False(viewModel.AddLanguageHasError);
        Assert.False(viewModel.HasError);

        viewModel.Load("""{ "version": 1, "keys": { "mood": { } } }""", "mood.json", "");
        Assert.True(viewModel.ErrorInFile);
        Assert.False(viewModel.ErrorInLanguages);
        Assert.False(viewModel.AddLanguageHasError);
    }
}
