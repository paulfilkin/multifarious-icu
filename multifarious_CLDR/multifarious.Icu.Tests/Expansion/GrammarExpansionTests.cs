using multifarious.Icu.Expansion;
using Icu.Core;
using Icu.Core.Tree;
using Newtonsoft.Json.Linq;

namespace multifarious.Icu.Expansion.Tests;

/// <summary>
/// The planner with a grammar dictionary, over the Phase 0 corpus
/// (<c>test-corpus\grammar.json</c>): a recognised select is category expanded to the
/// target language's keys, a dependent kind is inserted where the developer's file
/// declares it supplied, and everything else is walked as before.
/// </summary>
public class GrammarExpansionTests
{
    private static readonly IReadOnlyDictionary<string, string> Corpus = LoadCorpus();

    /// <summary>The developer's file of the design note: the article supplied for French and Italian, one argument spelt its own way.</summary>
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
              "article": "lineArticle",
              "keys": { "gender": { "MASCULINE": "MASKULINUM", "FEMININE": "FEMININUM", "NEUTER": "NEUTRUM" } }
            }
          }
        }
        """;

    private static readonly GrammarDictionary WithFile = GrammarDictionary.Default.WithDeveloperFile(DeveloperFile.Parse(ExampleFile));

    private static ExpansionPlan Plan(string raw, string target, GrammarDictionary? dictionary, ExpansionOptions? options = null, string source = "en-GB") =>
        Plan(raw, [target], dictionary, options, source);

    private static ExpansionPlan Plan(string raw, string[] targets, GrammarDictionary? dictionary, ExpansionOptions? options = null, string source = "en-GB")
    {
        options ??= ExpansionOptions.Default;
        var classification = MessageClassifier.Classify(raw, options);
        Assert.Equal(MessageKind.Expand, classification.Kind);
        return new ExpansionPlanner(dictionary: dictionary).Plan(classification, source, targets, options);
    }

    private static string Text(PlannedSegment segment) => IcuSerialiser.Serialise(segment.Nodes, inPluralContext: true);

    private static IEnumerable<string> Paths(ExpansionPlan plan) => plan.Segments.Select(segment => segment.Path);

    private static IEnumerable<PlannedSelector> Selectors(PlannedNode node)
    {
        if (node is not PlannedSelector selector)
        {
            yield break;
        }

        yield return selector;
        foreach (var branch in selector.Branches)
        {
            foreach (var nested in Selectors(branch.Content))
            {
                yield return nested;
            }
        }
    }

    private static IReadOnlyDictionary<string, string> LoadCorpus()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "test-corpus", "grammar.json")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var document = JObject.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "test-corpus", "grammar.json")));
        return ((JObject)document["boarding"]!).Properties()
            .ToDictionary(property => property.Name, property => (string)property.Value!, StringComparer.Ordinal);
    }

    [Fact]
    public void WithoutADictionaryASelectIsWalkedAsBefore()
    {
        var plan = Plan(Corpus["readyMinimal"], "de-DE", dictionary: null);

        var root = Assert.IsType<PlannedSelector>(plan.Root);
        Assert.False(root.IsExpanded);
        Assert.Null(root.Kind);
        Assert.Null(root.Keys);
        Assert.Equal(["vehicleGender:other"], Paths(plan));
        Assert.Empty(plan.Notes);
    }

    [Theory]
    [InlineData("de-DE", "MASCULINE,FEMININE,NEUTER,other")]
    [InlineData("fr-FR", "MASCULINE,FEMININE,other")]
    [InlineData("it-IT", "MASCULINE,FEMININE,other")]
    [InlineData("ja-JP", "other")]
    [InlineData("en-US", "other")]
    public void AMinimalSourceIsLaidOutWithTheKeysTheLanguageNeeds(string target, string expectedKeys)
    {
        var plan = Plan(Corpus["readyMinimal"], target, GrammarDictionary.Default);

        var keys = expectedKeys.Split(',');
        Assert.Equal(keys.Select(key => $"vehicleGender:{key}"), Paths(plan));

        var root = Assert.IsType<PlannedSelector>(plan.Root);
        Assert.True(root.IsExpanded);
        Assert.Equal("gender", root.Kind);
        Assert.Equal(keys, root.Keys);
        Assert.All(plan.Segments, segment => Assert.Equal("Your {vehicle} is ready to board.", Text(segment)));
    }

    [Fact]
    public void AKeyTheSourceLacksIsSeededFromOtherAndSaysSo()
    {
        var plan = Plan(Corpus["readyMinimal"], "de-DE", GrammarDictionary.Default);

        var masculine = plan.Segments[0];
        Assert.Equal("Grammar form: MASCULINE (masculine, e.g. der Zug)\nSource form: seeded from \"other\"", masculine.Comment);
        Assert.Equal("gender", masculine.Metadata["icu:kind"]);
        Assert.Equal("MASCULINE", masculine.Metadata["icu:category"]);
        Assert.Equal("select", masculine.Metadata["icu:selector"]);
        Assert.Equal("de", masculine.Metadata["icu:locale"]);
        Assert.Equal("masculine", masculine.Metadata["icu:hint"]);
        Assert.Equal("der Zug", masculine.Metadata["icu:sample"]);
        Assert.Equal("other", masculine.Metadata["icu:seededFrom"]);
        Assert.Equal("true", masculine.Metadata["icu:syntheticSource"]);
        Assert.Equal("", masculine.Metadata["icu:needs"]);

        var other = plan.Segments[3];
        Assert.Equal("Grammar form: other (any other value)", other.Comment);
        Assert.Equal("", other.Metadata["icu:seededFrom"]);
        Assert.Equal("false", other.Metadata["icu:syntheticSource"]);

        var root = (PlannedSelector)plan.Root;
        Assert.Equal("other", root.Branches[0].SeededFrom);
        Assert.True(root.Branches[0].SyntheticSource);
        Assert.Null(root.Branches[3].SeededFrom);
    }

    /// <summary>A source with the four German keys: French prunes NEUTER and keeps its own forms; Japanese keeps one.</summary>
    [Fact]
    public void AKeyTheLanguageDoesNotNeedIsPruned()
    {
        var french = Plan(Corpus["ready"], "fr-FR", GrammarDictionary.Default);
        Assert.Equal(["vehicleGender:MASCULINE", "vehicleGender:FEMININE", "vehicleGender:other"], Paths(french));
        Assert.All(french.Segments, segment => Assert.Equal("", segment.Metadata["icu:seededFrom"]));

        var japanese = Plan(Corpus["ready"], "ja-JP", GrammarDictionary.Default);
        Assert.Equal(["vehicleGender:other"], Paths(japanese));
    }

    [Theory]
    [InlineData("de-DE", 8)]
    [InlineData("fr-FR", 9)]
    [InlineData("it-IT", 9)]
    [InlineData("ja-JP", 1)]
    public void APluralUnderAGenderExpandsUnderEveryKey(string target, int expected)
    {
        var plan = Plan(Corpus["waiting"], target, GrammarDictionary.Default);

        Assert.Equal(expected, plan.Segments.Count);
        Assert.False(plan.ExceedsBudget);
    }

    [Fact]
    public void ALeafUnderAPluralUnderAGenderIsAnnotatedByThePlural()
    {
        var plan = Plan(Corpus["waiting"], "fr-FR", GrammarDictionary.Default);

        var many = plan.Segments.Single(segment => segment.Path == "vehicleGender:FEMININE/vehicleCount:many");
        Assert.Equal("plural", many.Metadata["icu:selector"]);
        Assert.Equal("many", many.Metadata["icu:category"]);
        Assert.Equal("Your # {vehicle}s are waiting at the platform.", Text(many));
        Assert.False(many.Metadata.ContainsKey("icu:kind"));

        var selectors = Selectors(plan.Root).ToList();
        Assert.Equal("gender", selectors[0].Kind);
        Assert.All(selectors.Skip(1), selector => Assert.Null(selector.Kind));
        Assert.All(selectors, selector => Assert.True(selector.IsExpanded));
    }

    /// <summary>An article select the source already has is recognised by its own name and narrowed by the gender above it.</summary>
    [Fact]
    public void AnArticleSelectInTheSourceIsExpandedInPlace()
    {
        var french = Plan(Corpus["readyWithArticle"], "fr-FR", GrammarDictionary.Default);
        Assert.Equal(
            [
                "vehicleGender:MASCULINE/vehicleArticle:LE", "vehicleGender:MASCULINE/vehicleArticle:L", "vehicleGender:MASCULINE/vehicleArticle:other",
                "vehicleGender:FEMININE/vehicleArticle:LA", "vehicleGender:FEMININE/vehicleArticle:L", "vehicleGender:FEMININE/vehicleArticle:other",
                "vehicleGender:other"
            ],
            Paths(french));
        Assert.Empty(french.Notes);

        var article = Selectors(french.Root).First(selector => selector.ArgumentName == "vehicleArticle");
        Assert.Equal("article", article.Kind);
        Assert.Equal(["LE", "L", "other"], article.Keys);
        Assert.Equal("singular before a vowel sound", french.Segments[1].Metadata["icu:hint"]);

        // No count beneath the gender: the singular entries.
        var italian = Plan(Corpus["readyWithArticle"], "it-IT", GrammarDictionary.Default);
        Assert.Equal(4 + 3 + 1, italian.Segments.Count);
        Assert.Contains("vehicleGender:MASCULINE/vehicleArticle:LO", Paths(italian));

        // German has no article kind: the select is walked with the one key the source gave it,
        // and NEUTER, seeded from the source's other, has none.
        var german = Plan(Corpus["readyWithArticle"], "de-DE", GrammarDictionary.Default);
        Assert.Equal(
            ["vehicleGender:MASCULINE/vehicleArticle:other", "vehicleGender:FEMININE/vehicleArticle:other",
             "vehicleGender:NEUTER", "vehicleGender:other"],
            Paths(german));
        Assert.Contains(german.Notes, note => note.StartsWith("German is not in the grammar dictionary for article", StringComparison.Ordinal));
    }

    [Fact]
    public void AFragmentIsLaidOutLikeASentence()
    {
        var plan = Plan(Corpus["fragment"], "fr-FR", GrammarDictionary.Default);

        Assert.Equal(["vehicleGender:MASCULINE", "vehicleGender:FEMININE", "vehicleGender:other"], Paths(plan));
        Assert.Equal("your {vehicle}", Text(plan.Segments[0]));
        Assert.Equal("your {vehicle}s", Text(plan.Segments[2]));
    }

    /// <summary>A source key outside the dictionary's set is the developer's own and leads, as an explicit value leads a plural.</summary>
    [Fact]
    public void TheDevelopersOwnKeyIsKeptFirst()
    {
        var german = Plan(Corpus["readyExtraKey"], "de-DE", GrammarDictionary.Default);
        Assert.Equal(
            ["vehicleGender:UNKNOWN", "vehicleGender:MASCULINE", "vehicleGender:FEMININE", "vehicleGender:NEUTER", "vehicleGender:other"],
            Paths(german));
        Assert.Equal("Grammar form: UNKNOWN (the developer's own key)", german.Segments[0].Comment);
        Assert.Equal("Your transport is ready to board.", Text(german.Segments[0]));
        Assert.Equal("", german.Segments[0].Metadata["icu:hint"]);

        var japanese = Plan(Corpus["readyExtraKey"], "ja-JP", GrammarDictionary.Default);
        Assert.Equal(["vehicleGender:UNKNOWN", "vehicleGender:other"], Paths(japanese));
    }

    [Theory]
    [InlineData("countAsSelect", "vehicleCount:one|vehicleCount:other")]
    [InlineData("status", "status:DELAYED|status:CANCELLED|status:other")]
    public void ASelectTheDictionaryDoesNotRecogniseIsWalked(string value, string expectedPaths)
    {
        var plan = Plan(Corpus[value], "fr-FR", GrammarDictionary.Default);

        Assert.Equal(expectedPaths.Split('|'), Paths(plan));
        var root = Assert.IsType<PlannedSelector>(plan.Root);
        Assert.False(root.IsExpanded);
        Assert.Null(root.Kind);
        Assert.Empty(plan.Notes);
        Assert.StartsWith("Select branch:", plan.Segments[0].Comment, StringComparison.Ordinal);
    }

    /// <summary>The article is inserted under each gender where the file declares it supplied, each form seeded from the gender's.</summary>
    [Fact]
    public void ADependentSelectorIsInsertedWhereTheFileDeclaresItSupplied()
    {
        var french = Plan(Corpus["readyMinimal"], "fr-FR", WithFile);

        Assert.Equal(
            [
                "vehicleGender:MASCULINE/vehicleArticle:LE", "vehicleGender:MASCULINE/vehicleArticle:L", "vehicleGender:MASCULINE/vehicleArticle:other",
                "vehicleGender:FEMININE/vehicleArticle:LA", "vehicleGender:FEMININE/vehicleArticle:L", "vehicleGender:FEMININE/vehicleArticle:other",
                "vehicleGender:other"
            ],
            Paths(french));
        Assert.All(french.Segments, segment => Assert.Equal("Your {vehicle} is ready to board.", Text(segment)));
        Assert.Empty(french.Notes);

        var article = Selectors(french.Root).First(selector => selector.ArgumentName == "vehicleArticle");
        Assert.Equal(SelectorType.Select, article.Type);
        Assert.Equal("article", article.Kind);
        Assert.True(article.IsExpanded);
        Assert.Equal("other", article.Branches[0].SeededFrom);
        Assert.Equal("vehicleGender:MASCULINE/vehicleArticle", article.Path);

        var le = french.Segments[0];
        Assert.Equal("Grammar form: LE (masculine singular before a consonant)\nSource form: seeded from \"other\"", le.Comment);
        Assert.Equal("article", le.Metadata["icu:kind"]);

        // German needs no article: nothing is inserted, nothing noted.
        var german = Plan(Corpus["readyMinimal"], "de-DE", WithFile);
        Assert.Equal(4, german.Segments.Count);
        Assert.Empty(german.Notes);
    }

    /// <summary>Italian gender by number by article, the article narrowed by gender and category: sixteen forms, plus the three plural forms the source keeps under other.</summary>
    [Fact]
    public void TheArticleIsInsertedUnderThePluralBeneathTheGender()
    {
        var plan = Plan(Corpus["waiting"], "it-IT", WithFile);

        Assert.Equal(18, plan.Segments.Count);
        Assert.False(plan.ExceedsBudget);
        Assert.Empty(plan.Notes);

        string[] Under(string gender, string category) => plan.Segments
            .Select(segment => segment.Path)
            .Where(path => path.StartsWith($"vehicleGender:{gender}/vehicleCount:{category}/vehicleArticle:", StringComparison.Ordinal))
            .Select(path => path[(path.LastIndexOf(':') + 1)..])
            .ToArray();

        Assert.Equal(["IL", "LO", "L", "other"], Under("MASCULINE", "one"));
        Assert.Equal(["I", "GLI", "other"], Under("MASCULINE", "many"));
        Assert.Equal(["I", "GLI", "other"], Under("MASCULINE", "other"));
        Assert.Equal(["LA", "L", "other"], Under("FEMININE", "one"));
        Assert.Equal(["other"], Under("FEMININE", "many"));
        Assert.Equal(["other"], Under("FEMININE", "other"));
        Assert.Equal(
            ["vehicleGender:other/vehicleCount:one", "vehicleGender:other/vehicleCount:many", "vehicleGender:other/vehicleCount:other"],
            Paths(plan).Where(path => path.StartsWith("vehicleGender:other", StringComparison.Ordinal)));

        var lo = plan.Segments.Single(segment => segment.Path == "vehicleGender:MASCULINE/vehicleCount:one/vehicleArticle:LO");
        Assert.Equal("Your {vehicle} is waiting at the platform.", Text(lo));
        Assert.Equal("LO", lo.Metadata["icu:category"]);
    }

    [Fact]
    public void AnArticleTheSourceAlreadyHasIsNotInsertedTwice()
    {
        var plan = Plan(Corpus["readyWithArticle"], "fr-FR", WithFile);

        Assert.Equal(7, plan.Segments.Count);
        Assert.Equal(2, Selectors(plan.Root).Count(selector => selector.ArgumentName == "vehicleArticle"));
        Assert.All(Paths(plan), path => Assert.True(path.Split('/').Count(component => component.StartsWith("vehicleArticle:", StringComparison.Ordinal)) <= 1));
    }

    /// <summary>Where the application does not supply the article, the forms go without it and the plan and the forms say so.</summary>
    [Fact]
    public void AnUnsuppliedDependentKindIsNotedAndLaidOutWithout()
    {
        var plan = Plan(Corpus["readyMinimal"], "fr-FR", GrammarDictionary.Default);

        Assert.Equal(3, plan.Segments.Count);
        var note = Assert.Single(plan.Notes);
        Assert.Equal("French needs an article form for {vehicle}; the application does not supply vehicleArticle", note);
        Assert.Equal(note, plan.Segments[0].Metadata["icu:needs"]);
        Assert.Equal(note, plan.Segments[1].Metadata["icu:needs"]);
        Assert.Equal("", plan.Segments[2].Metadata["icu:needs"]);
        Assert.EndsWith($"\nNeeds: {note}", plan.Segments[0].Comment, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNoteNamesTheDeclaredArgumentWhereTheFileNamesButDoesNotSupplyIt()
    {
        var dictionary = GrammarDictionary.Default.WithDeveloperFile(DeveloperFile.Parse("""
            { "version": 1,
              "arguments": { "vehicle": { "gender": "vehicleGender", "article": "theArticle", "supplies": { "it": ["article"] } } } }
            """));

        var french = Plan(Corpus["readyMinimal"], "fr-FR", dictionary);
        Assert.Equal(3, french.Segments.Count);
        Assert.Equal("French needs an article form for {vehicle}; the application does not supply theArticle", Assert.Single(french.Notes));

        var italian = Plan(Corpus["readyMinimal"], "it-IT", dictionary);
        Assert.Equal(8, italian.Segments.Count);
        Assert.Contains("vehicleGender:MASCULINE/theArticle:LO", Paths(italian));
    }

    /// <summary>The note once per message: the plural under the gender does not repeat it per form.</summary>
    [Fact]
    public void TheNoteIsGivenOncePerMessage()
    {
        var plan = Plan(Corpus["waiting"], "fr-FR", GrammarDictionary.Default);

        Assert.Equal(9, plan.Segments.Count);
        Assert.Single(plan.Notes);
        Assert.Equal(6, plan.Segments.Count(segment => segment.Metadata["icu:needs"].Length > 0));
    }

    [Fact]
    public void ADeclaredArgumentIsLaidOutInItsOwnSpelling()
    {
        var plan = Plan("{lineGenus, select, other {The {line} is closed today.}}", "de-DE", WithFile);

        Assert.Equal(["lineGenus:MASKULINUM", "lineGenus:FEMININUM", "lineGenus:NEUTRUM", "lineGenus:other"], Paths(plan));
        Assert.Equal("masculine", plan.Segments[0].Metadata["icu:hint"]);
        Assert.Equal(["MASKULINUM", "FEMININUM", "NEUTRUM", "other"], ((PlannedSelector)plan.Root).Keys);
    }

    [Fact]
    public void AnUnknownTargetLanguageWalksTheSelectWithANote()
    {
        var plan = Plan(Corpus["ready"], "cy-GB", GrammarDictionary.Default);

        Assert.Equal(4, plan.Segments.Count);
        Assert.False(((PlannedSelector)plan.Root).IsExpanded);
        Assert.Equal("Welsh is not in the grammar dictionary for gender; {vehicleGender} is laid out as written", Assert.Single(plan.Notes));
    }

    /// <summary>Several targets take the union of their keys, with each key's hint from the first language that has it.</summary>
    [Fact]
    public void SeveralTargetsTakeTheUnionOfTheirKeys()
    {
        var plan = Plan(Corpus["readyMinimal"], ["fr-FR", "de-DE"], GrammarDictionary.Default);

        Assert.Equal(["MASCULINE", "FEMININE", "NEUTER", "other"], ((PlannedSelector)plan.Root).Keys);
        Assert.Equal("fr", plan.Segments[0].Metadata["icu:locale"]);
        Assert.Equal("le train", plan.Segments[0].Metadata["icu:sample"]);
        Assert.Equal("de", plan.Segments[2].Metadata["icu:locale"]);
    }

    [Fact]
    public void AlwaysOtherSeedsEveryKeyFromOther()
    {
        var options = ExpansionOptions.Default with { SourceSeedStrategy = SourceSeedStrategy.AlwaysOther };
        var plan = Plan(Corpus["readyExtraKey"], "de-DE", GrammarDictionary.Default, options);

        var root = (PlannedSelector)plan.Root;
        Assert.All(root.Branches.Where(branch => branch.KeyText is not ("other" or "UNKNOWN")), branch =>
        {
            Assert.Equal("other", branch.SeededFrom);
            Assert.False(branch.SyntheticSource);
        });
    }

    /// <summary>Two French gender arguments, each with its article, is 49 forms: over the budget, so the message is laid out walked.</summary>
    [Fact]
    public void TwoGrammarArgumentsWithArticlesExceedTheBudget()
    {
        const string raw = "{vehicleGender, select, other {{lineGenus, select, other {Your {vehicle} on the {line} is ready.}}}}";
        var dictionary = GrammarDictionary.Default.WithDeveloperFile(DeveloperFile.Parse("""
            { "version": 1,
              "arguments": {
                "vehicle": { "gender": "vehicleGender", "article": "vehicleArticle", "supplies": { "fr": ["article"] } },
                "line": { "gender": "lineGenus", "article": "lineArticle", "supplies": { "fr": ["article"] } } } }
            """));
        var options = ExpansionOptions.Default;
        var classification = MessageClassifier.Classify(raw, options);
        var planner = new ExpansionPlanner(dictionary: dictionary);

        var plan = planner.Plan(classification, "en-GB", ["fr-FR"], options);
        Assert.Equal(49, plan.Segments.Count);
        Assert.True(plan.ExceedsBudget);

        var walked = planner.PlanWalked(classification, "en-GB", ["fr-FR"], options, "over budget");
        Assert.Equal(["vehicleGender:other/lineGenus:other"], Paths(walked));
        Assert.Empty(walked.Notes);
    }

    /// <summary>An article under the line's gender narrows by the line's keys, not the vehicle's above it.</summary>
    [Fact]
    public void TheWithinPathBelongsToTheBaseArgument()
    {
        const string raw = "{vehicleGender, select, other {{lineGenus, select, other {Your {vehicle} on the {line} is ready.}}}}";
        var plan = Plan(raw, "fr-FR", GrammarDictionary.Default.WithDeveloperFile(DeveloperFile.Parse("""
            { "version": 1,
              "arguments": {
                "vehicle": { "gender": "vehicleGender" },
                "line": { "gender": "lineGenus", "article": "lineArticle", "supplies": { "fr": ["article"] },
                          "keys": { "gender": { "MASCULINE": "MASKULINUM", "FEMININE": "FEMININUM" } } } } }
            """)));

        Assert.Equal(3 * 7, plan.Segments.Count);
        Assert.Contains("vehicleGender:FEMININE/lineGenus:MASKULINUM/lineArticle:LE", Paths(plan));
        Assert.Contains("vehicleGender:FEMININE/lineGenus:FEMININUM/lineArticle:LA", Paths(plan));
        Assert.DoesNotContain(Paths(plan), path => path.IndexOf("lineGenus:MASKULINUM/lineArticle:LA", StringComparison.Ordinal) >= 0);
    }
}
