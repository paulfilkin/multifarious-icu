using Icu.Cldr;

namespace Icu.Cldr.Tests;

/// <summary>
/// Locale resolution, which runs per selector kind and can land in different places for
/// the same tag.
/// </summary>
public class LocaleResolutionTests
{
    private static readonly CldrPlurals Plurals = CldrPlurals.Default;

    [Theory]
    [InlineData("ru-RU", "ru", LocaleMatch.Language)]
    [InlineData("ru", "ru", LocaleMatch.Exact)]
    [InlineData("pt-BR", "pt", LocaleMatch.Language)]
    [InlineData("zh-Hans-CN", "zh", LocaleMatch.Language)]
    [InlineData("sr-Latn-RS", "sr", LocaleMatch.Language)]
    [InlineData("kok-Latn", "kok-Latn", LocaleMatch.Exact)]
    [InlineData("kok-Latn-IN", "kok-Latn", LocaleMatch.LanguageAndScript)]
    [InlineData("EN-gb", "en", LocaleMatch.Language)]
    [InlineData("ru_RU", "ru", LocaleMatch.Language)]
    public void TagsResolveDownTheFallbackChain(string tag, string expectedKey, LocaleMatch expectedMatch)
    {
        var resolution = Plurals.Resolve(tag, SelectorKind.Cardinal);

        Assert.Equal(expectedKey, resolution.ResolvedKey);
        Assert.Equal(expectedMatch, resolution.Match);
        Assert.False(resolution.IsFallback);
        Assert.Null(resolution.Warning);
    }

    /// <summary>
    /// The case the design calls out. <c>pt-PT</c> is a cardinal key in its own right and
    /// is absent from the ordinal table, so resolving it once for both kinds would give
    /// the wrong answer for one of them.
    /// </summary>
    [Fact]
    public void PortuguesePortugalResolvesDifferentlyForEachSelectorKind()
    {
        var cardinal = Plurals.Resolve("pt-PT", SelectorKind.Cardinal);
        var ordinal = Plurals.Resolve("pt-PT", SelectorKind.Ordinal);

        Assert.Equal("pt-PT", cardinal.ResolvedKey);
        Assert.Equal(LocaleMatch.Exact, cardinal.Match);

        Assert.Equal("pt", ordinal.ResolvedKey);
        Assert.Equal(LocaleMatch.Language, ordinal.Match);
    }

    [Theory]
    [InlineData("iw", "he")]
    [InlineData("iw-IL", "he")]
    [InlineData("in", "id")]
    [InlineData("ji", "yi")]
    public void LegacyLanguageCodesAreMappedBeforeLookup(string tag, string expectedKey)
    {
        var resolution = Plurals.Resolve(tag, SelectorKind.Cardinal);

        Assert.Equal(expectedKey, resolution.ResolvedKey);
        Assert.False(resolution.IsFallback);
    }

    [Theory]
    [InlineData("qqq")]
    [InlineData("zxx")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!")]
    [InlineData("123")]
    public void AnUnknownTagFallsBackToOtherAloneAndWarns(string tag)
    {
        var resolution = Plurals.Resolve(tag, SelectorKind.Cardinal);

        Assert.Null(resolution.ResolvedKey);
        Assert.Equal(LocaleMatch.None, resolution.Match);
        Assert.True(resolution.IsFallback);
        Assert.Equal(new[] { PluralCategory.Other }, resolution.Categories);
        Assert.NotNull(resolution.Warning);
    }

    /// <summary>
    /// The design document lists <c>no-such</c> among the tags to check, evidently as a
    /// nonsense tag that should find nothing. It does not: <c>no</c> is Norwegian and is a
    /// CLDR key in both tables, and <c>such</c> is four letters and so parses as a script
    /// subtag, which the chain then drops. The tag resolves to Norwegian.
    /// </summary>
    /// <remarks>
    /// Recorded as a test rather than corrected, because it is a property of the fallback
    /// chain the design specifies and not a defect: any tag whose first subtag is a real
    /// language resolves, whatever follows it. Anything relying on an unresolvable tag
    /// being reported has to check <see cref="LocaleResolution.Match"/> rather than assume
    /// nonsense will fail.
    /// </remarks>
    [Fact]
    public void ATagWhoseFirstSubtagIsARealLanguageResolvesHoweverOddTheRestIs()
    {
        var resolution = Plurals.Resolve("no-such", SelectorKind.Cardinal);

        Assert.Equal("no", resolution.ResolvedKey);
        Assert.Equal(LocaleMatch.Language, resolution.Match);
        Assert.False(resolution.IsFallback);
    }

    /// <summary>
    /// The category sets the design document tabulates. English having more ordinal forms
    /// than cardinal, and Russian fewer, is the pair that catches an implementation which
    /// resolves one set per locale.
    /// </summary>
    [Theory]
    [InlineData("en", "one,other", "one,two,few,other")]
    [InlineData("ru-RU", "one,few,many,other", "other")]
    [InlineData("pl", "one,few,many,other", "other")]
    [InlineData("fr", "one,many,other", "one,other")]
    [InlineData("ar", "zero,one,two,few,many,other", "other")]
    [InlineData("cy", "zero,one,two,few,many,other", "zero,one,two,few,many,other")]
    [InlineData("ja", "other", "other")]
    [InlineData("zh", "other", "other")]
    public void CategorySetsMatchTheDesignDocument(string tag, string cardinal, string ordinal)
    {
        Assert.Equal(cardinal, Keywords(Plurals.Categories(tag, SelectorKind.Cardinal)));
        Assert.Equal(ordinal, Keywords(Plurals.Categories(tag, SelectorKind.Ordinal)));
    }

    /// <summary>
    /// What a <c>bcmSource</c> expansion targets, because that task runs before the
    /// project fans out into its target languages.
    /// </summary>
    [Fact]
    public void TheUnionOverAProjectIsTakenInCanonicalOrder()
    {
        var union = Plurals.UnionOfCategories(new[] { "ru-RU", "ja-JP", "ar-SA" }, SelectorKind.Cardinal);

        Assert.Equal("zero,one,two,few,many,other", Keywords(union));
    }

    [Fact]
    public void TheUnionOfOneLanguageIsThatLanguagesOwnSet()
    {
        var union = Plurals.UnionOfCategories(new[] { "ru-RU" }, SelectorKind.Cardinal);

        Assert.Equal("one,few,many,other", Keywords(union));
    }

    [Fact]
    public void TheUnionOfNoLanguagesIsOtherAlone()
    {
        Assert.Equal("other", Keywords(Plurals.UnionOfCategories(Array.Empty<string>(), SelectorKind.Cardinal)));
    }

    /// <summary>
    /// The count the preview's box resolves, and the offset case: an offset is applied by
    /// the caller before selection, so 27 and 27 minus an offset can differ.
    /// </summary>
    [Theory]
    [InlineData(1, PluralCategory.One)]
    [InlineData(2, PluralCategory.Few)]
    [InlineData(5, PluralCategory.Many)]
    [InlineData(11, PluralCategory.Many)]
    [InlineData(21, PluralCategory.One)]
    [InlineData(27, PluralCategory.Many)]
    [InlineData(102, PluralCategory.Few)]
    public void RussianCountsSelectTheExpectedCategory(long count, PluralCategory expected)
    {
        var ruleSet = Plurals.GetRuleSet("ru-RU", SelectorKind.Cardinal);
        Assert.NotNull(ruleSet);

        Assert.Equal(expected, ruleSet.Select(count));
    }

    [Fact]
    public void ARussianCountWithVisibleFractionDigitsIsOther()
    {
        var ruleSet = Plurals.GetRuleSet("ru-RU", SelectorKind.Cardinal);
        Assert.NotNull(ruleSet);

        Assert.Equal(PluralCategory.One, ruleSet.Select("1"));
        Assert.Equal(PluralCategory.Other, ruleSet.Select("1.0"));
    }

    [Fact]
    public void TheTwoTablesCoverDifferentLocaleSets()
    {
        Assert.Equal(224, CldrPluralData.Embedded.Keys(SelectorKind.Cardinal).Count);
        Assert.Equal(108, CldrPluralData.Embedded.Keys(SelectorKind.Ordinal).Count);
    }

    private static string Keywords(IEnumerable<PluralCategory> categories) =>
        string.Join(",", categories.Select(c => c.ToKeyword()));
}
