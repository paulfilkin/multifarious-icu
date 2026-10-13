using multifarious.Icu.Expansion;

namespace multifarious.Icu.Expansion.Tests;

/// <summary>
/// The grammar dictionary model: the shipped default as agreed on 13 October 2026,
/// resolution down the locale chain, recognition by suffix and by declaration, a
/// developer's file merged over the default, and what the import refuses.
/// </summary>
public class GrammarDictionaryTests
{
    private static readonly GrammarDictionary Default = GrammarDictionary.Default;

    /// <summary>The note's example file, with one argument spelt its own way and two replaced sets.</summary>
    private const string ExampleFile = """
        {
          "version": 1,
          "arguments": {
            "vehicle": {
              "gender": "vehicleGender",
              "count": "vehicleCount",
              "article": "vehicleArticle",
              "supplies": { "fr": ["article"], "it": ["article"] }
            },
            "line": {
              "gender": "lineGenus",
              "keys": { "gender": { "MASCULINE": "MASKULINUM", "FEMININE": "FEMININUM", "NEUTER": "NEUTRUM" } }
            }
          },
          "keys": {
            "gender": { "languages": { "cs": ["MASCULINE", "FEMININE", "NEUTER", "other"] } },
            "article": { "languages": { "it": ["IL", "LO", "L", "I", "GLI", "LA", "UN", "UNO", "UNA", "other"] } }
          },
          "other": "the plural form"
        }
        """;

    private static GrammarDictionary WithExampleFile() =>
        Default.WithDeveloperFile(DeveloperFile.Parse(ExampleFile));

    [Fact]
    public void TheShippedDefaultLoads()
    {
        Assert.Equal(1, Default.Version);
        Assert.Equal("48", Default.CldrVersion);
        Assert.Null(Default.DeveloperFile);
        Assert.Equal(["gender", "article"], Default.Kinds.Keys.OrderBy(name => name == "article").ToList());
    }

    /// <summary>61 CLDR locales: 35 with a gender set, 26 with other only (decision 1).</summary>
    [Fact]
    public void TheDefaultCoversTheCldrLocales()
    {
        var gender = Default.Kinds["gender"];

        Assert.Equal(61, gender.Languages.Count);
        Assert.Equal(35, gender.Languages.Values.Count(language => language.Keys.Count > 1));
        Assert.Equal(26, gender.Languages.Values.Count(language => language.Keys.Count == 1));
        Assert.All(gender.Languages.Values, language => Assert.Equal("other", language.Keys[^1]));
    }

    [Theory]
    [InlineData("de", "MASCULINE,FEMININE,NEUTER,other")]
    [InlineData("fr", "MASCULINE,FEMININE,other")]
    [InlineData("es", "MASCULINE,FEMININE,other")]
    [InlineData("da", "COMMON,NEUTER,other")]
    [InlineData("nl", "COMMON,NEUTER,other")]
    [InlineData("cs", "ANIMATE,INANIMATE,FEMININE,NEUTER,other")]
    [InlineData("pl", "ANIMATE,INANIMATE,PERSONAL,FEMININE,NEUTER,other")]
    [InlineData("en", "other")]
    [InlineData("ja", "other")]
    public void TheGenderSetsFollowCldr(string locale, string expected)
    {
        Assert.Equal(expected.Split(','), Default.Resolve("gender", locale)!.Keys);
    }

    [Theory]
    [InlineData("de-AT", "de")]
    [InlineData("pt-BR", "pt")]
    [InlineData("es-419", "es")]
    [InlineData("sr-Latn-RS", "sr")]
    [InlineData("zh-Hant-TW", "zh")]
    [InlineData("iw-IL", "he")]
    [InlineData("nb-NO", "nb")]
    public void ALanguageResolvesDownTheLocaleChain(string tag, string expectedKey)
    {
        Assert.Equal(expectedKey, Default.Resolve("gender", tag)!.LocaleKey);
    }

    [Theory]
    [InlineData("cy-GB")]
    [InlineData("ga")]
    [InlineData("")]
    [InlineData(null)]
    public void AnUnknownLanguageResolvesToNothing(string? tag)
    {
        Assert.Null(Default.Resolve("gender", tag));
    }

    [Fact]
    public void AnUnknownKindResolvesToNothing()
    {
        Assert.Null(Default.Resolve("definiteness", "de"));
    }

    /// <summary>The article kind ships for French and Italian only (decision 3).</summary>
    [Fact]
    public void TheArticleKindCoversFrenchAndItalian()
    {
        Assert.Equal(["fr", "it"], Default.Kinds["article"].Languages.Keys.OrderBy(key => key).ToList());
        Assert.Null(Default.Resolve("article", "es"));
        Assert.Null(Default.Resolve("article", "de"));
    }

    [Theory]
    [InlineData("fr", "MASCULINE", "LE,L,other")]
    [InlineData("fr", "FEMININE", "LA,L,other")]
    [InlineData("fr", "MASCULINE/one", "LE,L,other")]
    [InlineData("fr", "MASCULINE/many", "other")]
    [InlineData("fr", "FEMININE/other", "other")]
    [InlineData("it", "MASCULINE", "IL,LO,L,other")]
    [InlineData("it", "FEMININE", "LA,L,other")]
    [InlineData("it", "MASCULINE/one", "IL,LO,L,other")]
    [InlineData("it", "MASCULINE/many", "I,GLI,other")]
    [InlineData("it", "MASCULINE/other", "I,GLI,other")]
    [InlineData("it", "FEMININE/one", "LA,L,other")]
    [InlineData("it", "FEMININE/many", "other")]
    [InlineData("it", "FEMININE/other", "other")]
    public void TheArticleKeysNarrowByBranchPath(string locale, string path, string expected)
    {
        Assert.Equal(expected.Split(','), Default.Resolve("article", locale)!.KeysWithin(path));
    }

    [Fact]
    public void APathWithNoNarrowingTakesTheWholeKeySet()
    {
        var italian = Default.Resolve("article", "it")!;

        Assert.Equal(italian.Keys, italian.KeysWithin("other"));
        Assert.Equal(["IL", "LO", "L", "I", "GLI", "LA", "other"], italian.Keys);
    }

    [Fact]
    public void FrenchAndItalianNeedAnArticleUnderEachGender()
    {
        foreach (var locale in new[] { "fr", "it" })
        {
            var need = Assert.Single(Default.Resolve("gender", locale)!.Needs);
            Assert.Equal("article", need.Kind);
            Assert.Equal(["MASCULINE", "FEMININE"], need.Under);
        }

        Assert.Empty(Default.Resolve("gender", "de")!.Needs);
        Assert.Empty(Default.Resolve("gender", "es")!.Needs);
    }

    /// <summary>Hints are the grammatical term; samples ship for the four reviewed languages only (decision 5).</summary>
    [Fact]
    public void HintsAreTheTermAndSamplesAreShippedForReviewedLanguagesOnly()
    {
        var german = Default.Resolve("gender", "de-DE")!;
        Assert.Equal("masculine", german.Hint("MASCULINE"));
        Assert.Equal("der Zug", german.Sample("MASCULINE"));
        Assert.Equal("das Taxi", german.Sample("NEUTER"));
        Assert.Equal("any other value", german.Hint("other"));
        Assert.Null(german.Sample("other"));

        var arabic = Default.Resolve("gender", "ar")!;
        Assert.Equal("masculine", arabic.Hint("MASCULINE"));
        Assert.Null(arabic.Sample("MASCULINE"));

        Assert.Equal("masculine animate", Default.Resolve("gender", "cs")!.Hint("ANIMATE"));
        Assert.Equal("singular before a vowel sound", Default.Resolve("article", "fr")!.Hint("L"));
    }

    [Theory]
    [InlineData("vehicleGender", "gender", "vehicle")]
    [InlineData("vehicleArticle", "article", "vehicle")]
    [InlineData("deviceTypeGender", "gender", "deviceType")]
    public void AnArgumentIsRecognisedByItsSuffix(string name, string kind, string baseArgument)
    {
        var recognition = Default.Recognise(name);

        Assert.NotNull(recognition);
        Assert.Equal(kind, recognition!.Kind);
        Assert.Equal(baseArgument, recognition.BaseArgument);
        Assert.Equal(name, recognition.ArgumentName);
        Assert.False(recognition.Declared);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("vehicleCount")]
    [InlineData("Gender")]
    [InlineData("vehiclegender")]
    [InlineData("")]
    [InlineData(null)]
    public void AnApplicationStateIsNotRecognised(string? name)
    {
        Assert.Null(Default.Recognise(name));
    }

    [Fact]
    public void TheDefaultSuppliesNoDependentArgument()
    {
        Assert.False(Default.Supplies("vehicle", "fr", "article"));
        Assert.Null(Default.DependentArgumentName("vehicle", "article"));
    }

    [Fact]
    public void ADeclaredArgumentIsRecognisedByName()
    {
        var recognition = WithExampleFile().Recognise("lineGenus");

        Assert.NotNull(recognition);
        Assert.Equal("gender", recognition!.Kind);
        Assert.Equal("line", recognition.BaseArgument);
        Assert.True(recognition.Declared);
    }

    [Fact]
    public void TheFileDeclaresWhatTheApplicationSupplies()
    {
        var dictionary = WithExampleFile();

        Assert.True(dictionary.Supplies("vehicle", "fr", "article"));
        Assert.True(dictionary.Supplies("vehicle", "fr-CA", "article"));
        Assert.True(dictionary.Supplies("vehicle", "it-IT", "article"));
        Assert.False(dictionary.Supplies("vehicle", "de", "article"));
        Assert.False(dictionary.Supplies("line", "fr", "article"));
        Assert.Equal("vehicleArticle", dictionary.DependentArgumentName("vehicle", "article"));
        Assert.Equal("vehicleCount", dictionary.DependentArgumentName("vehicle", "count"));
        Assert.Null(dictionary.DependentArgumentName("line", "article"));
    }

    /// <summary>A rename on one argument respells that argument alone, keys, hints and the paths a dependent kind narrows by.</summary>
    [Fact]
    public void ARenameOnOneArgumentAppliesToThatArgumentAlone()
    {
        var dictionary = WithExampleFile();

        var line = dictionary.Resolve("gender", "de", "line")!;
        Assert.Equal(["MASKULINUM", "FEMININUM", "NEUTRUM", "other"], line.Keys);
        Assert.Equal("masculine", line.Hint("MASKULINUM"));
        Assert.Equal("der Zug", line.Sample("MASKULINUM"));

        Assert.Equal(["MASCULINE", "FEMININE", "NEUTER", "other"], dictionary.Resolve("gender", "de", "vehicle")!.Keys);
        Assert.Equal(["MASCULINE", "FEMININE", "NEUTER", "other"], dictionary.Resolve("gender", "de")!.Keys);

        var frenchLine = dictionary.Resolve("gender", "fr", "line")!;
        Assert.Equal(["MASKULINUM", "FEMININUM"], Assert.Single(frenchLine.Needs).Under);
        Assert.Equal(["LE", "L", "other"], dictionary.Resolve("article", "fr", "line")!.KeysWithin("MASKULINUM"));
        Assert.Equal(["IL", "LO", "L", "other"], dictionary.Resolve("article", "it", "line")!.KeysWithin("MASKULINUM/one"));
    }

    [Fact]
    public void AReplacedSetStandsInForTheDefaultsForThatLanguage()
    {
        var dictionary = WithExampleFile();

        Assert.Equal(["MASCULINE", "FEMININE", "NEUTER", "other"], dictionary.Resolve("gender", "cs-CZ")!.Keys);
        Assert.Equal(["ANIMATE", "INANIMATE", "FEMININE", "NEUTER", "other"], dictionary.Resolve("gender", "sk")!.Keys);

        var italian = dictionary.Resolve("article", "it")!;
        Assert.Equal(["IL", "LO", "L", "I", "GLI", "LA", "UN", "UNO", "UNA", "other"], italian.Keys);
        Assert.Equal("UNO", italian.Hint("UNO"));
        Assert.Equal(["IL", "LO", "L", "other"], italian.KeysWithin("MASCULINE/one"));
    }

    [Fact]
    public void TheFileNamesWhatOtherMeans()
    {
        var dictionary = WithExampleFile();

        Assert.Equal("the plural form", dictionary.Resolve("gender", "de")!.Hint("other"));
        Assert.Equal("the plural form", dictionary.Resolve("article", "fr")!.Hint("other"));
        Assert.Equal("masculine", dictionary.Resolve("gender", "de")!.Hint("MASCULINE"));
    }

    [Fact]
    public void TheFileTextIsKeptForExport()
    {
        var dictionary = WithExampleFile();

        Assert.Equal(ExampleFile, dictionary.DeveloperFile!.Text);
        Assert.Equal(1, dictionary.DeveloperFile.Version);
    }

    /// <summary>A rename for the whole project respells every language of the kind and every path that names its keys.</summary>
    [Fact]
    public void ARenameForTheProjectAppliesEverywhere()
    {
        var dictionary = Default.WithDeveloperFile(DeveloperFile.Parse("""
            { "version": 1,
              "keys": { "gender": { "rename": { "MASCULINE": "M", "FEMININE": "F", "NEUTER": "N" } } } }
            """));

        Assert.Equal(["M", "F", "N", "other"], dictionary.Resolve("gender", "de")!.Keys);
        Assert.Equal(["M", "F", "other"], dictionary.Resolve("gender", "fr")!.Keys);
        Assert.Equal("feminine", dictionary.Resolve("gender", "fr")!.Hint("F"));
        Assert.Equal("la voiture", dictionary.Resolve("gender", "fr")!.Sample("F"));
        Assert.Equal(["M", "F"], Assert.Single(dictionary.Resolve("gender", "fr")!.Needs).Under);
        Assert.Equal(["LE", "L", "other"], dictionary.Resolve("article", "fr")!.KeysWithin("M"));
        Assert.Equal(["LA", "L", "other"], dictionary.Resolve("article", "it")!.KeysWithin("F/one"));
    }

    /// <summary>A replaced set for a language the default lacks adds that language.</summary>
    [Fact]
    public void AReplacedSetAddsALanguageTheDefaultLacks()
    {
        var dictionary = Default.WithDeveloperFile(DeveloperFile.Parse("""
            { "version": 1,
              "keys": { "gender": { "languages": { "cy": ["MASCULINE", "FEMININE", "other"] } } } }
            """));

        var welsh = dictionary.Resolve("gender", "cy-GB")!;
        Assert.Equal("cy", welsh.LocaleKey);
        Assert.Equal(["MASCULINE", "FEMININE", "other"], welsh.Keys);
        Assert.Equal("masculine", welsh.Hint("MASCULINE"));
        Assert.Empty(welsh.Needs);
    }

    [Fact]
    public void AFileIsMergedOverTheDefaultOnly()
    {
        var dictionary = WithExampleFile();

        Assert.Throws<InvalidOperationException>(() => dictionary.WithDeveloperFile(DeveloperFile.Parse("""{ "version": 1 }""")));
    }

    [Theory]
    [InlineData("""{ "version": 2 }""", "version 2")]
    [InlineData("""{ "version": 1, "keys": { "mood": { } } }""", "no kind of that name")]
    [InlineData("""{ "version": 1, "keys": { "gender": { "languages": { "de": ["MASCULINE", "FEMININE"] } } } }""", "must end in 'other'")]
    [InlineData("""{ "version": 1, "keys": { "gender": { "languages": { "de": ["MASCULINE", "MASCULINE", "other"] } } } }""", "repeats a key")]
    [InlineData("""{ "version": 1, "keys": { "gender": { "rename": { "other": "REST" } } } }""", "cannot be renamed")]
    [InlineData("""{ "version": 1, "keys": { "gender": { "rename": { "MASCULINE": "X", "FEMININE": "X" } } } }""", "same spelling")]
    [InlineData("""{ "version": 1, "keys": { "gender": { "languages": { "fr": ["FEMININE", "other"] } } } }""", "under 'MASCULINE'")]
    [InlineData("""{ "version": 1, "keys": { "gender": { "spell": { } } } }""", "not 'rename' or 'languages'")]
    [InlineData("""{ "version": 1, "arguments": { "vehicle": { "mood": "vehicleMood" } } }""", "not a kind")]
    [InlineData("""{ "version": 1, "arguments": { "a": { "gender": "g" }, "b": { "gender": "g" } } }""", "already the gender of 'a'")]
    [InlineData("""{ "version": 1, "arguments": { "vehicle": { "gender": "vehicleGender", "supplies": { "fr": ["article"] } } } }""", "does not name the argument")]
    [InlineData("""{ "version": 1, "arguments": { "vehicle": { "gender": "vehicleGender", "article": "vehicleArticle", "supplies": { "de": ["article"] } } } }""", "no kind of the argument needs it")]
    [InlineData("""{ "version": 1, "arguments": { "vehicle": { "gender": "vehicleGender", "supplies": { "fr": ["mood"] } } } }""", "not a kind")]
    [InlineData("""{ "version": 1, "arguments": { "vehicle": { "gender": "vehicleGender", "supplies": { "12": ["article"] } } } }""", "not a language tag")]
    [InlineData("""{ "version": 1, "arguments": { "vehicle": { "gender": "" } } }""", "must name the argument")]
    [InlineData("""{ "version": 1, "other": "" }""", "non-empty string")]
    [InlineData("""{ "version": 1, """, "not valid JSON")]
    public void AFaultyFileIsRefusedWithAPlainMessage(string json, string expectedFragment)
    {
        var error = Assert.Throws<GrammarDictionaryException>(() => Default.WithDeveloperFile(DeveloperFile.Parse(json)));

        Assert.Contains(expectedFragment, error.Message, StringComparison.Ordinal);
    }

    private const string MinimalDefault = """
        { "version": 1, "cldr": "48",
          "kinds": {
            "gender": { "match": { "suffix": "Gender" },
                        "languages": { "fr": { "keys": ["MASCULINE", "FEMININE", "other"],
                                               "needs": [ { "kind": "article", "under": ["MASCULINE"] } ] } } },
            "article": { "languages": { "fr": { "keys": ["LE", "L", "other"],
                                                "within": { "MASCULINE": ["LE", "L", "other"] } } } } } }
        """;

    [Fact]
    public void ADictionaryInTheDefaultShapeReads()
    {
        var dictionary = GrammarDictionaryReader.Read(MinimalDefault);

        Assert.Equal(["LE", "L", "other"], dictionary.Resolve("article", "fr-FR")!.KeysWithin("MASCULINE"));
        Assert.Null(dictionary.Kinds["article"].Suffix);
        Assert.Null(dictionary.Recognise("vehicleArticle"));
    }

    [Theory]
    [InlineData("\"within\": { \"MASCULINE\": [\"LE\", \"L\", \"other\"] }", "\"within\": { \"MASCULINE\": [\"LA\", \"other\"] }", "not one of the keys")]
    [InlineData("\"within\": { \"MASCULINE\": [\"LE\", \"L\", \"other\"] }", "\"within\": { \"MASCULINE\": [\"LE\", \"L\"] }", "must keep 'other'")]
    [InlineData("\"kind\": \"article\", \"under\": [\"MASCULINE\"]", "\"kind\": \"mood\", \"under\": [\"MASCULINE\"]", "does not have")]
    [InlineData("\"kind\": \"article\", \"under\": [\"MASCULINE\"]", "\"kind\": \"article\", \"under\": [\"NEUTER\"]", "not one of the keys")]
    [InlineData("\"article\": { \"languages\": { \"fr\":", "\"article\": { \"languages\": { \"de\":", "no entry for that language")]
    [InlineData("\"cldr\": \"48\",", "", "CLDR version")]
    public void AFaultyDefaultIsRefused(string from, string to, string expectedFragment)
    {
        var json = MinimalDefault.Replace(from, to);
        Assert.NotEqual(MinimalDefault, json);

        var error = Assert.Throws<GrammarDictionaryException>(() => GrammarDictionaryReader.Read(json));

        Assert.Contains(expectedFragment, error.Message, StringComparison.Ordinal);
    }

    /// <summary>Every argument in the Phase 0 corpus is recognised or not as the design says.</summary>
    [Theory]
    [InlineData("vehicleGender", true)]
    [InlineData("vehicleArticle", true)]
    [InlineData("vehicleCount", false)]
    [InlineData("status", false)]
    public void TheCorpusArgumentsAreRecognisedAsDesigned(string name, bool recognised)
    {
        Assert.Equal(recognised, Default.Recognise(name) is not null);
    }
}
