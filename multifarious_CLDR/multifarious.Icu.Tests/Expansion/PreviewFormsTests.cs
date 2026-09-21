using multifarious.Icu.Expansion;

namespace multifarious.Icu.Expansion.Tests;

/// <summary>The all-forms rows and the count resolver (design 12.1), as the cloud project's tests pin them.</summary>
public class PreviewFormsTests
{
    [Fact]
    public void TheRowsRenderEveryFormWithSubstitutedValues()
    {
        var forms = new PreviewForms();

        var rows = forms.Rows(
            "{count, plural, one {Hello {name}, you have # unread message!} other {Hello {name}, you have # unread messages!}}",
            "{count, plural, one {Привет, {name}, у вас # сообщение!} other {Привет, {name}, у вас # сообщений!}}",
            "en-GB", "ru-RU");

        Assert.Equal(["one", "few", "many", "other"], rows.Select(row => row.BranchKey));

        var one = rows[0];
        Assert.Equal("1", one.SampleCount);
        Assert.Equal("Hello Anna, you have 1 unread message!", one.SourceRendered);
        Assert.Equal("Привет, Anna, у вас 1 сообщение!", one.TargetRendered);

        var other = rows[3];
        Assert.True(other.FractionalOnly);
        Assert.NotEmpty(other.Counts);
    }

    [Fact]
    public void ExplicitBranchesGetTheirOwnRowsWithTheLiteralValue()
    {
        var forms = new PreviewForms();
        const string message = "{rabbitCount, plural, =0 {No rabbits.} =1 {One rabbit!} other {# rabbits!}}";

        var rows = forms.Rows(message, message, "en-GB", "ru-RU");

        Assert.Equal("=0", rows[0].BranchKey);
        Assert.True(rows[0].IsExplicit);
        Assert.Equal("No rabbits.", rows[0].SourceRendered);
        Assert.Equal("=1", rows[1].BranchKey);
        Assert.Equal(["one", "few", "many", "other"], rows.Skip(2).Select(row => row.BranchKey));
    }

    [Fact]
    public void ASelectOnlyMessageGetsOneRowPerBranch()
    {
        var forms = new PreviewForms();
        const string message = "{questionNumber, select, 1 {What is your name?} 2 {What is your quest?} other {Not a question.}}";

        var rows = forms.Rows(message, "", "en-GB", "ru-RU");

        Assert.Equal(["1", "2", "other"], rows.Select(row => row.BranchKey));
        Assert.Equal("What is your quest?", rows[1].SourceRendered);
    }

    [Fact]
    public void CategorySamplesDodgeExplicitValues()
    {
        var forms = new PreviewForms();
        const string message = "{count, plural, =0 {There are no knights here.} =1 {There is 1 knight here.} other {There are # knights here.}}";

        var rows = forms.Rows(message, message, "en-GB", "ru-RU");

        var many = rows.First(row => row.BranchKey == "many");
        Assert.Equal("5", many.SampleCount);
        Assert.Equal("There are 5 knights here.", many.SourceRendered);

        var one = rows.First(row => row.BranchKey == "one");
        Assert.NotEqual("1", one.SampleCount);
        Assert.Equal($"There are {one.SampleCount} knights here.", one.SourceRendered);
    }

    [Theory]
    [InlineData("21", "one", false)]
    [InlineData("3", "few", false)]
    [InlineData("0", "many", false)]
    [InlineData("1.5", "other", false)]
    public void TheCountResolverFollowsTheRussianRules(string count, string branch, bool isExplicit)
    {
        var forms = new PreviewForms();
        const string message = "{count, plural, one {a} few {b} many {c} other {d}}";

        var resolution = forms.ResolveCount(message, "ru-RU", count);

        Assert.NotNull(resolution);
        Assert.Equal(branch, resolution!.BranchKey);
        Assert.Equal(isExplicit, resolution.IsExplicit);
    }

    [Fact]
    public void ExplicitValuesMatchTheRawCountAndCategoriesUseTheOffset()
    {
        var forms = new PreviewForms();
        const string message = "{count, plural, offset:1 =0 {Nobody} =1 {Just you} one {You and # other} other {You and # others}}";

        Assert.Equal("=1", forms.ResolveCount(message, "ru-RU", "1")!.BranchKey);
        Assert.True(forms.ResolveCount(message, "ru-RU", "1")!.IsExplicit);
        Assert.Equal("one", forms.ResolveCount(message, "ru-RU", "2")!.BranchKey);

        var rows = forms.Rows(message, message, "en-GB", "ru-RU");
        var one = rows.First(row => row.BranchKey == "one");
        Assert.Equal("2", one.SampleCount);
        Assert.Equal("You and 1 other", one.SourceRendered);
    }

    /// <summary>
    /// A compact count such as 1c6 resolves through its written form: the exponent
    /// is a plural operand, and Spanish 'many' is reached through it.
    /// </summary>
    [Theory]
    [InlineData("1c6", "many")]
    [InlineData("1000000", "many")]
    [InlineData("5", "other")]
    public void ACompactCountResolvesThroughItsWrittenForm(string count, string branch)
    {
        var forms = new PreviewForms();
        const string message = "{count, plural, one {a} many {b} other {c}}";

        var resolution = forms.ResolveCount(message, "es-ES", count);

        Assert.NotNull(resolution);
        Assert.Equal(branch, resolution!.BranchKey);
    }

    [Theory]
    [InlineData("1c")]
    [InlineData("c6")]
    public void AMalformedCompactCountResolvesToNothing(string count)
    {
        var forms = new PreviewForms();

        Assert.Null(forms.ResolveCount("{count, plural, one {a} many {b} other {c}}", "es-ES", count));
    }

    /// <summary>
    /// A category whose samples are compact shows plain counts: the 'many' row must
    /// not print 1c6 at a translator, and rendering it must not throw.
    /// </summary>
    [Fact]
    public void ACompactSampleRowShowsPlainCounts()
    {
        var forms = new PreviewForms();
        var rows = forms.Rows(
            "{n, plural, one {# message} other {# messages}}",
            "{n, plural, one {# mensaje} many {# millones de mensajes} other {# mensajes}}",
            "en-GB", "es-ES");

        var many = rows.Single(row => row.BranchKey == "many");
        Assert.Equal("1000000", many.SampleCount);
        Assert.DoesNotContain(many.Counts, count => count.Contains('c'));
        Assert.Equal("1000000 millones de mensajes", many.TargetRendered);
    }

    [Fact]
    public void ACountThatIsNotANumberOrAMessageWithoutAPluralResolvesToNothing()
    {
        var forms = new PreviewForms();

        Assert.Null(forms.ResolveCount("{count, plural, one {a} other {b}}", "ru-RU", "many"));
        Assert.Null(forms.ResolveCount("{g, select, female {a} other {b}}", "ru-RU", "2"));
        Assert.Null(forms.ResolveCount("Hello {name}", "ru-RU", "2"));
        Assert.Equal("count", PreviewForms.OutermostPluralArgument("{count, plural, one {a} other {b}}"));
        Assert.Null(PreviewForms.OutermostPluralArgument("{g, select, female {a} other {b}}"));
    }
}
