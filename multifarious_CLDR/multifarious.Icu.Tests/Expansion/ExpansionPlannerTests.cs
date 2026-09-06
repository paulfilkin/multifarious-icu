using multifarious.Icu.Expansion;
using Icu.Cldr;
using Icu.Core;
using Icu.Core.Tree;

namespace multifarious.Icu.Expansion.Tests;

/// <summary>
/// The planner: category sets, seeding, explicit values, walking, budget, and the
/// comment and metadata payloads, all against real CLDR data. The worked example of
/// design section 4 is pinned end to end.
/// </summary>
public class ExpansionPlannerTests
{
    private const string WorkedExample =
        "Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!";

    private static ExpansionPlan Plan(string raw, string[] targets, ExpansionOptions? options = null)
    {
        options ??= ExpansionOptions.Default;
        var classification = MessageClassifier.Classify(raw, options);
        Assert.Equal(MessageKind.Expand, classification.Kind);
        return new ExpansionPlanner().Plan(classification, "en-GB", targets, options);
    }

    private static string Text(PlannedSegment segment) =>
        IcuSerialiser.Serialise(segment.Nodes, inPluralContext: true);

    [Fact]
    public void TheWorkedExamplePlansFourSegmentsForRussian()
    {
        var plan = Plan(WorkedExample, ["ru-RU"]);

        Assert.Equal(
            new[] { "count:one", "count:few", "count:many", "count:other" },
            plan.Segments.Select(segment => segment.Path));
        Assert.False(plan.ExceedsBudget);

        var one = plan.Segments[0];
        Assert.Equal("Hello {name}, you have # unread message!", Text(one));
        Assert.Equal("false", one.Metadata["icu:syntheticSource"]);
        Assert.Equal("", one.Metadata["icu:seededFrom"]);

        var few = plan.Segments[1];
        Assert.Equal("Hello {name}, you have # unread messages!", Text(few));
        Assert.Equal("true", few.Metadata["icu:syntheticSource"]);
        Assert.Equal("other", few.Metadata["icu:seededFrom"]);
        Assert.Equal("ru", few.Metadata["icu:locale"]);
        Assert.Equal("plural", few.Metadata["icu:selector"]);
        Assert.Equal("2,3,4,22,23,24", few.Metadata["icu:exampleIntegers"]);
    }

    [Fact]
    public void TheManyCommentReadsAsTheDesignSpecifies()
    {
        var plan = Plan(WorkedExample, ["ru-RU"]);
        var many = plan.Segments.Single(segment => segment.Path == "count:many");

        var hint = GrammaticalHints.Embedded.Find("ru", PluralCategory.Many);
        Assert.NotNull(hint);
        Assert.Equal(
            "CLDR category: many\n" +
            "Used when the count is: 0, 5, 6, 7, 8, 9\n" +
            "Source form: seeded from \"other\" - English does not distinguish this form\n" +
            $"Grammar: {hint}",
            many.Comment);
    }

    /// <summary>Russian 'other' is reachable only by fractional counts, and the comment says so.</summary>
    [Fact]
    public void AFractionalOnlyCategorySaysSoInsteadOfShowingNothing()
    {
        var plan = Plan(WorkedExample, ["ru-RU"]);
        var other = plan.Segments.Single(segment => segment.Path == "count:other");

        Assert.Contains("fractional counts only, e.g. 0.0, 0.1", other.Comment);
        Assert.Equal("", other.Metadata["icu:exampleIntegers"]);
        Assert.StartsWith("0.0,0.1", other.Metadata["icu:exampleDecimals"]);
    }

    /// <summary>The same message as a selectordinal produces one segment for Russian.</summary>
    [Fact]
    public void AnOrdinalUsesTheOrdinalTable()
    {
        var plan = Plan("You are {position, selectordinal, one {#st} other {#th}} in the queue.", ["ru-RU"]);

        var segment = Assert.Single(plan.Segments);
        Assert.Equal("position:other", segment.Path);
        Assert.Equal("selectordinal", segment.Metadata["icu:selector"]);
    }

    [Fact]
    public void AUnionProjectExpandsToTheUnionInCanonicalOrder()
    {
        var plan = Plan(WorkedExample, ["ru-RU", "ja-JP", "ar-SA"]);

        Assert.Equal(
            new[] { "count:zero", "count:one", "count:two", "count:few", "count:many", "count:other" },
            plan.Segments.Select(segment => segment.Path));
    }

    /// <summary>
    /// The locale that annotates a category is the first target language whose set
    /// contains it, in project order.
    /// </summary>
    [Fact]
    public void CategoryAnnotationsComeFromTheFirstLanguageThatNeedsThem()
    {
        var plan = Plan(WorkedExample, ["ja-JP", "ru-RU"]);

        Assert.Equal("ja", plan.Segments.Single(s => s.Path == "count:other").Metadata["icu:locale"]);
        Assert.Equal("ru", plan.Segments.Single(s => s.Path == "count:few").Metadata["icu:locale"]);
    }

    [Fact]
    public void ASelectIsWalkedAndMultipliesThePaths()
    {
        var plan = Plan(
            "{gender, select, female {{count, plural, one {She has # item} other {She has # items}}} " +
            "male {{count, plural, one {He has # item} other {He has # items}}} " +
            "other {{count, plural, one {They have # item} other {They have # items}}}} in the basket.",
            ["ru-RU"]);

        Assert.Equal(12, plan.Segments.Count);

        var root = Assert.IsType<PlannedSelector>(plan.Root);
        Assert.False(root.IsExpanded);
        Assert.Equal(SelectorType.Select, root.Type);

        var few = plan.Segments.Single(s => s.Path == "gender:female/count:few");
        Assert.Equal("She has # items in the basket.", Text(few));
        Assert.Equal("plural", few.Metadata["icu:selector"]);
    }

    [Fact]
    public void ExplicitValuesComeFirstAndAreNeverCategories()
    {
        var plan = Plan(
            "{count, plural, offset:1 =0 {Nobody is coming} =1 {Only you are coming} " +
            "one {You and # other guest are coming} other {You and # other guests are coming}}.",
            ["ru-RU"]);

        Assert.Equal(
            new[] { "count:=0", "count:=1", "count:one", "count:few", "count:many", "count:other" },
            plan.Segments.Select(segment => segment.Path));

        var zero = plan.Segments[0];
        Assert.Equal("Nobody is coming.", Text(zero));
        Assert.Equal("Exact match: =0\nUsed only when the count is: 0", zero.Comment);
        Assert.Equal("1", zero.Metadata["icu:offset"]);
        Assert.Equal("0", zero.Metadata["icu:exampleIntegers"]);
    }

    [Fact]
    public void TwoIndependentPluralsMultiplyAndTheBudgetCatchesTheUnion()
    {
        const string message =
            "{files, plural, one {# file} other {# files}} synchronised across " +
            "{devices, plural, one {# device} other {# devices}}.";

        var russian = Plan(message, ["ru-RU"]);
        Assert.Equal(16, russian.Segments.Count);
        Assert.False(russian.ExceedsBudget);

        var union = Plan(message, ["ru-RU", "ar-SA"]);
        Assert.Equal(36, union.Segments.Count);
        Assert.True(union.ExceedsBudget);
    }

    [Fact]
    public void AlwaysOtherSeedsEveryCategoryFromOther()
    {
        var plan = Plan(WorkedExample, ["ru-RU"],
            new ExpansionOptions { SourceSeedStrategy = SourceSeedStrategy.AlwaysOther });

        var one = plan.Segments.Single(segment => segment.Path == "count:one");
        Assert.Equal("Hello {name}, you have # unread messages!", Text(one));
        Assert.Equal("other", one.Metadata["icu:seededFrom"]);
        // The source did have a 'one' branch; only the seed came from elsewhere.
        Assert.Equal("false", one.Metadata["icu:syntheticSource"]);
    }

    [Fact]
    public void ADisabledCardinalIsWalkedWhileTheOrdinalExpands()
    {
        var options = new ExpansionOptions { ExpandCardinal = false };
        var plan = Plan(
            "{n, plural, one {a} other {b}} and {p, selectordinal, one {x} other {y}}",
            ["ru-RU"], options);

        var root = Assert.IsType<PlannedSelector>(plan.Root);
        Assert.False(root.IsExpanded);
        Assert.Equal(SelectorType.Plural, root.Type);

        Assert.Equal(
            new[] { "n:one/p:other", "n:other/p:other" },
            plan.Segments.Select(segment => segment.Path));
    }

    [Fact]
    public void ThePlanCarriesThePatternAndTheHoistedForm()
    {
        var plan = Plan(WorkedExample, ["ru-RU"]);

        Assert.Equal(WorkedExample, plan.RawValue);
        Assert.Equal(
            "{count, plural, " +
            "one {Hello {name}, you have # unread message!} " +
            "other {Hello {name}, you have # unread messages!}}",
            plan.HoistedText);
    }

    [Fact]
    public void AnOverBudgetMessageIsPlannedWalkedWithTheReasonInEveryComment()
    {
        const string message =
            "{files, plural, one {# file} other {# files}} synchronised across " +
            "{devices, plural, one {# device} other {# devices}}.";
        const string note = "over the branch budget: 36 segments needed, 24 allowed";
        var classification = MessageClassifier.Classify(message, ExpansionOptions.Default);

        var plan = new ExpansionPlanner().PlanWalked(classification, "en-GB", ["ar-SA"], ExpansionOptions.Default, note);

        // The source's own branches, hoisted, and nothing category-expanded.
        Assert.False(plan.ExceedsBudget);
        Assert.Equal(
            new[] { "files:one/devices:one", "files:one/devices:other", "files:other/devices:one", "files:other/devices:other" },
            plan.Segments.Select(segment => segment.Path));
        var root = Assert.IsType<PlannedSelector>(plan.Root);
        Assert.False(root.IsExpanded);
        Assert.All(root.Branches, branch => Assert.False(Assert.IsType<PlannedSelector>(branch.Content).IsExpanded));

        // Whole sentences: the outer '#' is rewritten to the argument inside the inner branches.
        Assert.Equal("{files, number} file synchronised across # device.", Text(plan.Segments[0]));

        Assert.Equal($"Branch kept as authored: devices = one ({note})", plan.Segments[0].Comment);
        Assert.All(plan.Segments, segment => Assert.EndsWith($"({note})", segment.Comment));
        Assert.All(plan.Segments, segment => Assert.Equal("", segment.Metadata["icu:locale"]));
    }
}
