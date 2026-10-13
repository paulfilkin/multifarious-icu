using Icu.Cldr;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace multifarious.Icu.Expansion;

/// <summary>
/// Reads the shipped grammar dictionary and merges a developer's file over it. Both
/// results are validated the same way, so a file that respells a key set out from
/// under a dependent kind is refused at import rather than mis-laid at expand time.
/// </summary>
public static class GrammarDictionaryReader
{
    private const string DefaultSource = "the shipped grammar dictionary";
    private const string FileSource = "the developer's file";

    public static GrammarDictionary ReadDefault() =>
        Read(GrammarDictionaryResource.ReadJson(), DefaultSource);

    /// <summary>Reads a dictionary in the shipped default's shape.</summary>
    public static GrammarDictionary Read(string json, string source = DefaultSource)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));
        if (source is null) throw new ArgumentNullException(nameof(source));

        JObject root;
        try
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
            root = JObject.Load(reader);
        }
        catch (JsonException error)
        {
            throw new GrammarDictionaryException($"{Capitalise(source)} is not valid JSON: {error.Message}", error);
        }

        var version = root["version"] is JValue { Type: JTokenType.Integer } versionValue
            ? (int)versionValue
            : throw new GrammarDictionaryException($"{Capitalise(source)} has no integer 'version'.");
        if (version != DeveloperFile.CurrentVersion)
        {
            throw new GrammarDictionaryException($"{Capitalise(source)} is version {version}; this plugin reads version {DeveloperFile.CurrentVersion}.");
        }

        var cldr = root["cldr"] is JValue { Type: JTokenType.String } cldrValue && ((string?)cldrValue.Value)?.Length > 0
            ? (string)cldrValue.Value!
            : throw new GrammarDictionaryException($"{Capitalise(source)} does not declare its CLDR version.");

        if (root["kinds"] is not JObject kindsObject || !kindsObject.HasValues)
        {
            throw new GrammarDictionaryException($"{Capitalise(source)} has no 'kinds'.");
        }

        var kinds = new Dictionary<string, GrammarKind>(StringComparer.Ordinal);
        foreach (var property in kindsObject.Properties())
        {
            kinds.Add(property.Name, ReadKind(property.Name, property.Value, source));
        }

        Validate(kinds, source);
        return new GrammarDictionary(version, cldr, kinds, null);
    }

    /// <summary>
    /// The default with a developer's file over it: the file's respelling applied to
    /// every entry, its replaced sets in place, its words for <c>other</c> in every
    /// hint, and its arguments checked against the kinds.
    /// </summary>
    public static GrammarDictionary Merge(GrammarDictionary dictionary, DeveloperFile file)
    {
        if (dictionary is null) throw new ArgumentNullException(nameof(dictionary));
        if (file is null) throw new ArgumentNullException(nameof(file));

        foreach (var kindName in file.Keys.Keys)
        {
            if (!dictionary.Kinds.ContainsKey(kindName))
            {
                throw new GrammarDictionaryException($"Keys for '{kindName}': the dictionary has no kind of that name.");
            }
        }

        // Every rename in one map for the branch path components, which name other
        // kinds' keys: the article kind narrows by the gender kind's keys.
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in file.Keys)
        {
            ValidateRename(pair.Value.Rename, $"Keys for '{pair.Key}', 'rename'");
            foreach (var rename in pair.Value.Rename)
            {
                paths[rename.Key] = rename.Value;
            }
        }

        var kinds = new Dictionary<string, GrammarKind>(StringComparer.Ordinal);
        foreach (var kind in dictionary.Kinds.Values)
        {
            var respelling = file.Keys.TryGetValue(kind.Name, out var found) ? found : null;
            var own = respelling?.Rename ?? EmptyMap;
            var hints = kind.Hints.ToDictionary(
                pair => own.TryGetValue(pair.Key, out var to) ? to : pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);

            var languages = new Dictionary<string, GrammarLanguage>(StringComparer.Ordinal);
            foreach (var pair in kind.Languages)
            {
                languages[pair.Key] = pair.Value.Respell(own, paths);
            }

            if (respelling is not null)
            {
                foreach (var replaced in respelling.Languages)
                {
                    languages[replaced.Key] = languages.TryGetValue(replaced.Key, out var existing)
                        ? existing.WithKeys(replaced.Value, hints)
                        : new GrammarLanguage(
                            replaced.Key,
                            replaced.Value,
                            replaced.Value.ToDictionary(key => key, key => hints.TryGetValue(key, out var hint) ? hint : key, StringComparer.Ordinal),
                            EmptyMap,
                            Array.Empty<DependentNeed>(),
                            EmptyWithin);
                }
            }

            if (file.Other is { } other)
            {
                foreach (var key in languages.Keys.ToList())
                {
                    languages[key] = languages[key].WithOther(other);
                }
            }

            kinds.Add(kind.Name, new GrammarKind(kind.Name, kind.Suffix, hints, languages));
        }

        Validate(kinds, FileSource);
        ValidateArguments(file, kinds);
        return new GrammarDictionary(dictionary.Version, dictionary.CldrVersion, kinds, file);
    }

    private static GrammarKind ReadKind(string name, JToken token, string source)
    {
        if (token is not JObject entry)
        {
            throw new GrammarDictionaryException($"{Capitalise(source)}: kind '{name}' must be an object.");
        }

        string? suffix = null;
        if (entry["match"] is { } matchToken)
        {
            if (matchToken is not JObject match)
            {
                throw new GrammarDictionaryException($"{Capitalise(source)}: kind '{name}', 'match' must be an object.");
            }

            if (match["suffix"] is { } suffixToken)
            {
                suffix = suffixToken is JValue { Type: JTokenType.String } suffixValue && ((string?)suffixValue.Value)?.Length > 0
                    ? (string)suffixValue.Value!
                    : throw new GrammarDictionaryException($"{Capitalise(source)}: kind '{name}', 'suffix' must be a non-empty string.");
            }
        }

        var hints = entry["hints"] is { } hintsToken
            ? DeveloperFile.StringMap(hintsToken, $"{Capitalise(source)}: kind '{name}', 'hints'")
            : EmptyMap;

        if (entry["languages"] is not JObject languagesObject)
        {
            throw new GrammarDictionaryException($"{Capitalise(source)}: kind '{name}' has no 'languages' object.");
        }

        var languages = new Dictionary<string, GrammarLanguage>(StringComparer.Ordinal);
        foreach (var property in languagesObject.Properties())
        {
            languages.Add(property.Name, ReadLanguage(name, property.Name, property.Value, hints, source));
        }

        return new GrammarKind(name, suffix, hints, languages);
    }

    private static GrammarLanguage ReadLanguage(
        string kindName, string localeKey, JToken token, IReadOnlyDictionary<string, string> kindHints, string source)
    {
        var where = $"{Capitalise(source)}: kind '{kindName}', language '{localeKey}'";
        if (token is not JObject entry)
        {
            throw new GrammarDictionaryException($"{where} must be an object.");
        }

        var keys = entry["keys"] is { } keysToken
            ? DeveloperFile.StringList(keysToken, $"{where}, 'keys'")
            : throw new GrammarDictionaryException($"{where} has no 'keys'.");

        var samples = entry["samples"] is { } samplesToken
            ? DeveloperFile.StringMap(samplesToken, $"{where}, 'samples'")
            : EmptyMap;

        var needs = new List<DependentNeed>();
        if (entry["needs"] is { } needsToken)
        {
            if (needsToken is not JArray needsArray)
            {
                throw new GrammarDictionaryException($"{where}, 'needs' must be a list.");
            }

            foreach (var item in needsArray)
            {
                if (item is not JObject need
                    || need["kind"] is not JValue { Type: JTokenType.String } kindValue
                    || ((string?)kindValue.Value)?.Length == 0)
                {
                    throw new GrammarDictionaryException($"{where}, each 'needs' entry must name a 'kind'.");
                }

                var under = need["under"] is { } underToken
                    ? DeveloperFile.StringList(underToken, $"{where}, 'needs' / 'under'")
                    : throw new GrammarDictionaryException($"{where}, each 'needs' entry must list the branches 'under' which the kind is needed.");
                needs.Add(new DependentNeed((string)kindValue.Value!, under));
            }
        }

        var within = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (entry["within"] is { } withinToken)
        {
            if (withinToken is not JObject withinObject)
            {
                throw new GrammarDictionaryException($"{where}, 'within' must be an object keyed by branch path.");
            }

            foreach (var property in withinObject.Properties())
            {
                within[property.Name] = DeveloperFile.StringList(property.Value, $"{where}, 'within' / '{property.Name}'");
            }
        }

        var hints = keys.ToDictionary(
            key => key,
            key => kindHints.TryGetValue(key, out var hint) ? hint : key,
            StringComparer.Ordinal);

        return new GrammarLanguage(localeKey, keys, hints, samples, needs, within);
    }

    /// <summary>
    /// The invariants the planner relies on: every key set ends in <c>other</c> without
    /// repeats, a narrowing names only keys in the set, and a dependent kind exists,
    /// is needed under keys in the set and has an entry for the language.
    /// </summary>
    private static void Validate(IReadOnlyDictionary<string, GrammarKind> kinds, string source)
    {
        foreach (var kind in kinds.Values)
        {
            foreach (var language in kind.Languages.Values)
            {
                var where = $"{Capitalise(source)}: kind '{kind.Name}', language '{language.LocaleKey}'";
                ValidateKeySet(language.Keys, where);
                var set = new HashSet<string>(language.Keys, StringComparer.Ordinal);

                foreach (var pair in language.Within)
                {
                    foreach (var key in pair.Value)
                    {
                        if (!set.Contains(key))
                        {
                            throw new GrammarDictionaryException($"{where}: 'within' / '{pair.Key}' names '{key}', which is not one of the keys.");
                        }
                    }

                    if (!pair.Value.Contains(GrammarDictionary.OtherKey, StringComparer.Ordinal))
                    {
                        throw new GrammarDictionaryException($"{where}: 'within' / '{pair.Key}' must keep '{GrammarDictionary.OtherKey}'.");
                    }
                }

                foreach (var need in language.Needs)
                {
                    if (!kinds.TryGetValue(need.Kind, out var needed))
                    {
                        throw new GrammarDictionaryException($"{where}: needs kind '{need.Kind}', which the dictionary does not have.");
                    }

                    if (needed.Resolve(language.LocaleKey) is null)
                    {
                        throw new GrammarDictionaryException($"{where}: needs kind '{need.Kind}', which has no entry for that language.");
                    }

                    foreach (var key in need.Under)
                    {
                        if (!set.Contains(key))
                        {
                            throw new GrammarDictionaryException($"{where}: needs kind '{need.Kind}' under '{key}', which is not one of the keys.");
                        }
                    }
                }
            }
        }
    }

    private static void ValidateKeySet(IReadOnlyList<string> keys, string where)
    {
        if (keys.Count == 0)
        {
            throw new GrammarDictionaryException($"{where}: the key set is empty.");
        }

        if (keys[keys.Count - 1] != GrammarDictionary.OtherKey)
        {
            throw new GrammarDictionaryException($"{where}: the key set must end in '{GrammarDictionary.OtherKey}'.");
        }

        if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Count)
        {
            throw new GrammarDictionaryException($"{where}: the key set repeats a key.");
        }
    }

    private static void ValidateRename(IReadOnlyDictionary<string, string> rename, string where)
    {
        foreach (var pair in rename)
        {
            if (pair.Key == GrammarDictionary.OtherKey || pair.Value == GrammarDictionary.OtherKey)
            {
                throw new GrammarDictionaryException($"{where}: '{GrammarDictionary.OtherKey}' cannot be renamed; ICU requires it.");
            }
        }

        if (rename.Values.Distinct(StringComparer.Ordinal).Count() != rename.Count)
        {
            throw new GrammarDictionaryException($"{where}: two keys are renamed to the same spelling.");
        }
    }

    /// <summary>
    /// The declared arguments against the merged kinds: every role is a kind or the
    /// count, no argument name is claimed twice, and a supplied dependent kind is one
    /// the argument names and some kind of the argument needs for that language.
    /// </summary>
    private static void ValidateArguments(DeveloperFile file, IReadOnlyDictionary<string, GrammarKind> kinds)
    {
        var claimed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var argument in file.Arguments)
        {
            var where = $"Argument '{argument.Base}'";
            foreach (var pair in argument.Names)
            {
                if (pair.Key != GrammarDictionary.CountRole && !kinds.ContainsKey(pair.Key))
                {
                    throw new GrammarDictionaryException($"{where}: '{pair.Key}' is not a kind in the dictionary or '{GrammarDictionary.CountRole}'.");
                }

                if (claimed.TryGetValue(pair.Value, out var owner))
                {
                    throw new GrammarDictionaryException($"{where}: '{pair.Value}' is already the {owner}.");
                }

                claimed.Add(pair.Value, $"{pair.Key} of '{argument.Base}'");
            }

            foreach (var pair in argument.Keys)
            {
                if (!kinds.ContainsKey(pair.Key))
                {
                    throw new GrammarDictionaryException($"{where}: keys for '{pair.Key}', which is not a kind in the dictionary.");
                }

                ValidateRename(pair.Value, $"{where}, keys for '{pair.Key}'");
            }

            foreach (var supplied in argument.Supplies)
            {
                foreach (var kindName in supplied.Value)
                {
                    if (!kinds.ContainsKey(kindName))
                    {
                        throw new GrammarDictionaryException($"{where}: supplies '{kindName}' for '{supplied.Key}', which is not a kind in the dictionary.");
                    }

                    if (!argument.Names.ContainsKey(kindName))
                    {
                        throw new GrammarDictionaryException($"{where}: supplies '{kindName}' for '{supplied.Key}' but does not name the argument the application passes for it.");
                    }

                    var needed = argument.Names.Keys
                        .Where(role => role != kindName && kinds.ContainsKey(role))
                        .Select(role => kinds[role].Resolve(supplied.Key))
                        .Any(language => language is not null && language.Needs.Any(need => need.Kind == kindName));
                    if (!needed)
                    {
                        throw new GrammarDictionaryException($"{where}: supplies '{kindName}' for '{supplied.Key}', but no kind of the argument needs it for that language.");
                    }
                }
            }
        }
    }

    private static string Capitalise(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static readonly IReadOnlyDictionary<string, string> EmptyMap =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyWithin =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
}
