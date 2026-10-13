using Icu.Cldr;

namespace Icu.Cldr.Tests;

/// <summary>
/// The candidate keys a tag is tried against, in the order the plural lookup tries
/// them, so the grammar dictionary and the plural tables resolve a tag the same way.
/// </summary>
public class LocaleChainTests
{
    [Theory]
    [InlineData("de-AT", "de-AT|de")]
    [InlineData("ru", "ru")]
    [InlineData("ru_RU", "ru-RU|ru")]
    [InlineData("EN-gb", "en-GB|en")]
    [InlineData("sr-Latn-RS", "sr-Latn-RS|sr-Latn|sr")]
    [InlineData("kok-Latn", "kok-Latn|kok")]
    [InlineData("zh-Hans-CN", "zh-Hans-CN|zh-Hans|zh")]
    public void CandidatesRunFromTheWholeTagDownToTheLanguage(string tag, string expected)
    {
        Assert.Equal(expected.Split('|'), LocaleChain.Candidates(tag));
    }

    /// <summary>The legacy code is replaced before the chain is built, as the plural lookup does.</summary>
    [Theory]
    [InlineData("iw-IL", "he-IL|he")]
    [InlineData("in", "id")]
    public void ALegacyLanguageCodeIsReplacedFirst(string tag, string expected)
    {
        Assert.Equal(expected.Split('|'), LocaleChain.Candidates(tag));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234")]
    public void ATagThatDoesNotParseHasNoCandidates(string? tag)
    {
        Assert.Empty(LocaleChain.Candidates(tag));
    }

    [Fact]
    public void FirstMatchReturnsTheMostSpecificKeyTheCallerHas()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal) { "pt", "pt-PT" };

        Assert.Equal("pt-PT", LocaleChain.FirstMatch("pt-PT", keys.Contains));
        Assert.Equal("pt", LocaleChain.FirstMatch("pt-BR", keys.Contains));
        Assert.Null(LocaleChain.FirstMatch("cy-GB", keys.Contains));
    }

    /// <summary>The chain agrees with the plural resolver on every key it reaches.</summary>
    [Theory]
    [InlineData("ru-RU")]
    [InlineData("pt-BR")]
    [InlineData("pt-PT")]
    [InlineData("zh-Hans-CN")]
    [InlineData("kok-Latn-IN")]
    [InlineData("iw-IL")]
    public void TheChainReachesTheKeyThePluralResolverReaches(string tag)
    {
        var resolution = CldrPlurals.Default.Resolve(tag, SelectorKind.Cardinal);
        var ruleSets = CldrPluralData.Embedded;

        Assert.Equal(
            resolution.ResolvedKey,
            LocaleChain.FirstMatch(tag, key => ruleSets.GetExact(key, SelectorKind.Cardinal) is not null));
    }
}
