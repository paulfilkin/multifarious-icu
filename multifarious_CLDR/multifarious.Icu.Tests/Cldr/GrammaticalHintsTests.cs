using Icu.Cldr;

namespace Icu.Cldr.Tests;

/// <summary>
/// The curated hints. The content is a stub pending open question Q11, so these test the
/// mechanism rather than the vocabulary.
/// </summary>
public class GrammaticalHintsTests
{
    [Fact]
    public void TheEmbeddedFileLoads()
    {
        Assert.NotEmpty(GrammaticalHints.Embedded.Keys);
    }

    [Fact]
    public void AHintIsFoundByLocaleAndCategory()
    {
        Assert.Equal("genitive plural agreement", GrammaticalHints.Embedded.Find("ru", PluralCategory.Many));
    }

    [Fact]
    public void ACategoryWithNoHintReturnsNothing()
    {
        Assert.Null(GrammaticalHints.Embedded.Find("ru", PluralCategory.One));
    }

    [Fact]
    public void ALocaleWithNoHintsReturnsNothing()
    {
        Assert.Null(GrammaticalHints.Embedded.Find("en", PluralCategory.Other));
        Assert.Null(GrammaticalHints.Embedded.Find(null, PluralCategory.Other));
        Assert.Null(GrammaticalHints.Embedded.Find(string.Empty, PluralCategory.Other));
    }

    /// <summary>
    /// A hint written for a language covers its regional keys, so the curated file does
    /// not have to repeat itself for every CLDR key that falls back to the same language.
    /// </summary>
    [Fact]
    public void AHintOnTheLanguageCoversAScriptedOrRegionalKey()
    {
        Assert.Equal("genitive plural agreement", GrammaticalHints.Embedded.Find("ru-Cyrl", PluralCategory.Many));
    }

    [Fact]
    public void TheEmptySetIsUsedWhenHintsAreTurnedOff()
    {
        Assert.Empty(GrammaticalHints.None.Keys);
        Assert.Null(GrammaticalHints.None.Find("ru", PluralCategory.Many));
    }
}
