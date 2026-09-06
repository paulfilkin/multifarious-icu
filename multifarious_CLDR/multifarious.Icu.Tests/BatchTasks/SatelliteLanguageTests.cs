using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using multifarious.Icu.BatchTasks.Resources;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// Checks every translated resource file against the English one, and that each satellite
/// really answers for its language. Written to discover the languages rather than list them,
/// so a language added later is covered the moment its resx exists. A translation that
/// silently falls back to English, because a key was missed or the satellite was not built,
/// looks like a working page in a language the user did not ask for, which is the quietest
/// failure of the lot. Same suite as the YAML plugin's, minus the button rules: these pages
/// have no buttons.
/// </summary>
public class SatelliteLanguageTests
{
    private static string ResourceDirectory() => Path.Combine(AppContext.BaseDirectory, "resources");

    /// <summary>Culture names for which a translated resx exists, e.g. "de".</summary>
    private static List<string> TranslatedCultures()
    {
        var directory = ResourceDirectory();
        if (!Directory.Exists(directory)) return [];

        return Directory.GetFiles(directory, "UIStrings.*.resx")
            .Select(file => Path.GetFileNameWithoutExtension(file).Substring("UIStrings.".Length))
            .Where(culture => culture.Length > 0)
            .OrderBy(culture => culture, StringComparer.Ordinal)
            .ToList();
    }

    private static Dictionary<string, string> Entries(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .Where(e => e.Attribute("name") != null)
            .ToDictionary(e => e.Attribute("name")!.Value, e => e.Element("value")!.Value, StringComparer.Ordinal);

    private static Dictionary<string, string> English() => Entries(Path.Combine(ResourceDirectory(), "UIStrings.resx"));

    private static Dictionary<string, string> Translated(string culture) =>
        Entries(Path.Combine(ResourceDirectory(), "UIStrings." + culture + ".resx"));

    /// <summary>
    /// Through a resource manager of its own rather than the accessor's shared culture, which
    /// the other test classes read at the same time: xunit runs classes in parallel.
    /// </summary>
    private static string Lookup(string culture, string key) =>
        new ResourceManager("multifarious.Icu.BatchTasks.Resources.UIStrings", typeof(UIStrings).Assembly)
            .GetString(key, new CultureInfo(culture))!;

    [Fact]
    public void The_eight_languages_the_sibling_plugins_ship_are_all_present()
    {
        Assert.Equal(new[] { "de", "es", "fr", "it-IT", "ja", "ko-KR", "ru-RU", "zh-CN" }, TranslatedCultures());
    }

    [Fact]
    public void Every_language_translates_every_string()
    {
        var english = English();
        foreach (var culture in TranslatedCultures())
        {
            var translated = Translated(culture);
            var missing = english.Keys.Except(translated.Keys).OrderBy(k => k).ToList();
            var extra = translated.Keys.Except(english.Keys).OrderBy(k => k).ToList();

            Assert.True(missing.Count == 0, culture + " is missing: " + string.Join(", ", missing));
            Assert.True(extra.Count == 0, culture + " has entries English does not: " + string.Join(", ", extra));
            Assert.All(translated, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value), culture + "/" + pair.Key + " is blank"));
        }
    }

    [Fact]
    public void Every_satellite_is_built_and_answers_for_its_language()
    {
        // Through the resource manager, so a resx that exists but did not become a satellite
        // assembly is caught: the lookup would fall back to English.
        foreach (var culture in TranslatedCultures())
        {
            var translated = Translated(culture);
            foreach (var key in new[] { "Expand_KindsHeader", "Report_Summary", "Finalise_EmptyHeader" })
            {
                Assert.Equal(translated[key], Lookup(culture, key));
            }
        }
    }

    [Fact]
    public void Every_language_keeps_the_placeholders_it_was_given()
    {
        // Dropping a placeholder throws at run time; adding one throws too.
        var english = English();
        foreach (var culture in TranslatedCultures())
        {
            foreach (var pair in Translated(culture))
            {
                if (!english.TryGetValue(pair.Key, out var source)) continue;
                Assert.True(Placeholders(source).SequenceEqual(Placeholders(pair.Value)),
                    culture + "/" + pair.Key + " changed its placeholders: " + pair.Value);
            }
        }
    }

    private static List<string> Placeholders(string value) =>
        Regex.Matches(value, @"\{(\d+)\}").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().OrderBy(v => v).ToList();

    [Fact]
    public void Every_language_leaves_icu_syntax_alone()
    {
        // The keywords and the syntax fragments in the labels are code; a translated "other"
        // would name a form that does not exist.
        var mustSurvive = new Dictionary<string, string[]>
        {
            ["Expand_Cardinal"] = ["{count, plural, ...}"],
            ["Expand_Ordinal"] = ["{count, selectordinal, ...}"],
            ["Expand_SeedMatching"] = ["other"],
            ["Expand_SeedAlwaysOther"] = ["other"],
            ["Expand_KindsHelp1"] = ["one", "other"],
            ["Expand_SeedHelp1"] = ["one", "other", "few"],
            ["Expand_LockHint"] = ["QuickPlace"],
            ["Expand_PlaceholdersHelp1"] = ["{name}", "#", "QuickPlace"],
            ["Finalise_MismatchHelp1"] = ["{name}", "#"],
        };

        foreach (var culture in TranslatedCultures())
        {
            var translated = Translated(culture);
            foreach (var rule in mustSurvive)
            {
                foreach (var token in rule.Value)
                {
                    Assert.True(translated[rule.Key].Contains(token),
                        culture + "/" + rule.Key + " lost the literal '" + token + "'");
                }
            }
        }
    }

    [Fact]
    public void The_report_names_match_the_task_names_in_every_language()
    {
        // The task names in Studio's list are not localised (PluginResources is neutral), and a
        // report named differently from the task that produced it would not be found beside it.
        foreach (var culture in TranslatedCultures())
        {
            var translated = Translated(culture);
            Assert.Equal("ICU Expand Plural Forms", translated["Report_ExpandName"]);
            Assert.Equal("ICU Finalise Messages", translated["Report_FinaliseName"]);
        }
    }

    [Fact]
    public void No_language_contains_a_script_it_has_no_business_using()
    {
        // A stray character from another script is always a mistake, never a translation
        // choice, so it is worth catching mechanically rather than by eye across eight files.
        var scripts = new[]
        {
            new { Name = "Cyrillic", Pattern = @"\p{IsCyrillic}", Cultures = new[] { "ru" } },
            new { Name = "Hangul", Pattern = @"[\p{IsHangulSyllables}\p{IsHangulJamo}]", Cultures = new[] { "ko" } },
            new { Name = "kana", Pattern = @"[\p{IsHiragana}\p{IsKatakana}]", Cultures = new[] { "ja" } },
            new { Name = "Han", Pattern = @"\p{IsCJKUnifiedIdeographs}", Cultures = new[] { "ja", "zh" } },
        };

        foreach (var culture in TranslatedCultures())
        {
            var language = culture.Split('-')[0];
            foreach (var script in scripts)
            {
                if (script.Cultures.Contains(language)) continue;
                foreach (var pair in Translated(culture))
                {
                    var match = Regex.Match(pair.Value, script.Pattern);
                    Assert.False(match.Success,
                        culture + "/" + pair.Key + " contains " + script.Name + " (\"" + match.Value + "\"): " + pair.Value);
                }
            }
        }
    }
}
