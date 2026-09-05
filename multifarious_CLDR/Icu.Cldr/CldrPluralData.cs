using System.Reflection;
using Icu.Cldr.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Icu.Cldr;

/// <summary>
/// The two pinned CLDR plural tables, parsed.
/// </summary>
/// <remarks>
/// <para>
/// The data is embedded in this assembly rather than fetched, so a given build always
/// answers the same way. The version is surfaced because it has to travel with the
/// output: it goes in the paragraph unit metadata, so that a message expanded under one
/// CLDR release can be recognised later as having been expanded under it.
/// </para>
/// <para>
/// Both files are parsed on first use and the rules held for the life of the process.
/// There are 689 rule strings between them and parsing all of them takes a few
/// milliseconds, so nothing is gained by doing it lazily per locale, and doing it
/// eagerly means a data problem surfaces at once rather than on whichever locale first
/// reaches it.
/// </para>
/// <para>
/// Parsed with Newtonsoft rather than System.Text.Json because this build targets
/// .NET Framework 4.8 inside Trados Studio, which ships Newtonsoft itself. JObject
/// keeps properties in document order, which the rule tables depend on: the order the
/// categories appear in is the order they are evaluated in.
/// </para>
/// </remarks>
public sealed class CldrPluralData
{
    private const string CardinalResource = "Icu.Cldr.Data.plurals.json";
    private const string OrdinalResource = "Icu.Cldr.Data.ordinals.json";
    private const string CardinalRoot = "plurals-type-cardinal";
    private const string OrdinalRoot = "plurals-type-ordinal";
    private const string RulePrefix = "pluralRule-count-";

    private static readonly Lazy<CldrPluralData> LazyEmbedded = new(LoadEmbedded, isThreadSafe: true);

    private readonly IReadOnlyDictionary<string, PluralRuleSet> _cardinal;
    private readonly IReadOnlyDictionary<string, PluralRuleSet> _ordinal;

    private CldrPluralData(
        string cldrVersion,
        string unicodeVersion,
        IReadOnlyDictionary<string, PluralRuleSet> cardinal,
        IReadOnlyDictionary<string, PluralRuleSet> ordinal)
    {
        CldrVersion = cldrVersion;
        UnicodeVersion = unicodeVersion;
        _cardinal = cardinal;
        _ordinal = ordinal;
    }

    /// <summary>The pinned data, parsed once for the process.</summary>
    public static CldrPluralData Embedded => LazyEmbedded.Value;

    /// <summary>The CLDR release the data was taken from, for example <c>48</c>.</summary>
    public string CldrVersion { get; }

    /// <summary>The Unicode version that release corresponds to, for example <c>16.0.0</c>.</summary>
    public string UnicodeVersion { get; }

    /// <summary>The pinned version in the form that goes into metadata.</summary>
    public string VersionDescription => $"CLDR {CldrVersion} (Unicode {UnicodeVersion})";

    /// <summary>
    /// The CLDR keys covered for one selector kind. The two sets differ: 224 locales have
    /// cardinal rules and 108 have ordinal rules.
    /// </summary>
    public IReadOnlyCollection<string> Keys(SelectorKind kind) => Table(kind).Keys.ToList();

    /// <summary>
    /// Looks up a rule set by exact CLDR key. This does no locale fallback; use
    /// <see cref="CldrPlurals"/> for a Trados language tag.
    /// </summary>
    public PluralRuleSet? GetExact(string cldrKey, SelectorKind kind) =>
        Table(kind).TryGetValue(cldrKey, out var ruleSet) ? ruleSet : null;

    /// <summary>All rule sets for one selector kind, which is what the conformance suite walks.</summary>
    public IEnumerable<PluralRuleSet> All(SelectorKind kind) => Table(kind).Values;

    private IReadOnlyDictionary<string, PluralRuleSet> Table(SelectorKind kind) =>
        kind == SelectorKind.Cardinal ? _cardinal : _ordinal;

    private static CldrPluralData LoadEmbedded()
    {
        var (cardinalVersion, cardinal) = LoadTable(CardinalResource, CardinalRoot, SelectorKind.Cardinal);
        var (ordinalVersion, ordinal) = LoadTable(OrdinalResource, OrdinalRoot, SelectorKind.Ordinal);

        // The two files are pinned together and updated together. Disagreeing versions
        // would mean a half-applied update, which is worth catching here rather than
        // reporting a version that only half the answers came from.
        if (cardinalVersion != ordinalVersion)
        {
            throw new CldrDataException(
                $"The embedded CLDR files disagree on their version: plurals.json says " +
                $"{cardinalVersion.Cldr}/{cardinalVersion.Unicode}, ordinals.json says " +
                $"{ordinalVersion.Cldr}/{ordinalVersion.Unicode}.");
        }

        return new CldrPluralData(cardinalVersion.Cldr, cardinalVersion.Unicode, cardinal, ordinal);
    }

    private static ((string Cldr, string Unicode) Version, IReadOnlyDictionary<string, PluralRuleSet> Table)
        LoadTable(string resourceName, string rootKey, SelectorKind kind)
    {
        var document = ReadEmbeddedObject(typeof(CldrPluralData), resourceName);

        if (document["supplemental"] is not JObject supplemental)
        {
            throw new CldrDataException($"'{resourceName}' has no 'supplemental' object.");
        }

        if (supplemental["version"] is not JObject version ||
            version["_cldrVersion"] is not JValue { Type: JTokenType.String } cldrVersion ||
            version["_unicodeVersion"] is not JValue { Type: JTokenType.String } unicodeVersion)
        {
            throw new CldrDataException($"'{resourceName}' does not declare its CLDR and Unicode versions.");
        }

        if (supplemental[rootKey] is not JObject locales)
        {
            throw new CldrDataException($"'{resourceName}' has no '{rootKey}' object.");
        }

        var table = new Dictionary<string, PluralRuleSet>(StringComparer.Ordinal);
        foreach (var locale in locales.Properties())
        {
            if (locale.Value is not JObject rules)
            {
                throw new CldrDataException($"'{resourceName}' has a non-object entry for '{locale.Name}'.");
            }

            table.Add(locale.Name, ParseRuleSet(locale.Name, kind, rules));
        }

        return (((string)cldrVersion.Value!, (string)unicodeVersion.Value!), table);
    }

    private static PluralRuleSet ParseRuleSet(string cldrKey, SelectorKind kind, JObject rules)
    {
        var parsed = new List<PluralRule>();

        // Property order is document order, and document order is evaluation order.
        foreach (var entry in rules.Properties())
        {
            if (!entry.Name.StartsWith(RulePrefix, StringComparison.Ordinal))
            {
                throw new CldrDataException($"'{cldrKey}' has an unrecognised {kind} entry '{entry.Name}'.");
            }

            var keyword = entry.Name[RulePrefix.Length..];
            if (!PluralCategories.TryParse(keyword, out var category))
            {
                throw new CldrDataException($"'{cldrKey}' declares the unknown {kind} category '{keyword}'.");
            }

            var source = entry.Value is JValue { Type: JTokenType.String } value ? (string?)value.Value : null;
            if (source is null)
            {
                throw new CldrDataException($"'{cldrKey}' has a null {kind} rule for '{keyword}'.");
            }

            try
            {
                var (condition, samples) = PluralRuleParser.Parse(source);
                parsed.Add(new PluralRule(category, condition, samples, source));
            }
            catch (CldrDataException exception)
            {
                throw new CldrDataException($"'{cldrKey}' {kind} rule for '{keyword}': {exception.Message}", exception);
            }
        }

        return new PluralRuleSet(cldrKey, kind, parsed);
    }

    /// <summary>
    /// Reads one embedded JSON resource as an object. Shared with the hints loader, so
    /// both files are read the same way: no date parsing, since nothing in them is a
    /// date and Newtonsoft would otherwise turn a version-like string into one.
    /// </summary>
    internal static JObject ReadEmbeddedObject(Type anchor, string resourceName)
    {
        using var stream = anchor.GetTypeInfo().Assembly.GetManifestResourceStream(resourceName)
            ?? throw new CldrDataException($"The embedded resource '{resourceName}' is missing from the assembly.");
        using var text = new StreamReader(stream);
        using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None };

        return JObject.Load(reader);
    }
}
