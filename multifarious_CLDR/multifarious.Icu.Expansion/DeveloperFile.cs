using Icu.Cldr;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace multifarious.Icu.Expansion;

/// <summary>
/// One base argument the application supplies, as the developer's file declares it:
/// the argument passed for each kind (and for the count), the dependent kinds supplied
/// per language, and any key spelling that applies to this argument alone.
/// </summary>
public sealed class DeclaredArgument
{
    public DeclaredArgument(
        string baseName,
        IReadOnlyDictionary<string, string> names,
        IReadOnlyDictionary<string, IReadOnlyList<string>> supplies,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> keys)
    {
        Base = baseName;
        Names = names;
        Supplies = supplies;
        Keys = keys;

        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var map in keys.Values)
        {
            foreach (var pair in map)
            {
                paths[pair.Key] = pair.Value;
            }
        }

        PathRenames = paths;
    }

    /// <summary>The base argument, <c>vehicle</c>.</summary>
    public string Base { get; }

    /// <summary>The argument passed per role: a kind name or <see cref="GrammarDictionary.CountRole"/> to the argument name.</summary>
    public IReadOnlyDictionary<string, string> Names { get; }

    /// <summary>The dependent kinds the application supplies, by normalised language tag.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Supplies { get; }

    /// <summary>The key respelling for this argument alone, by kind: default key to the project's.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Keys { get; }

    /// <summary>Every rename of this argument in one map, for the branch path components a dependent kind narrows by.</summary>
    public IReadOnlyDictionary<string, string> PathRenames { get; }
}

/// <summary>The respelling a developer's file applies to one kind for the whole project.</summary>
public sealed class KindRespelling
{
    public KindRespelling(
        IReadOnlyDictionary<string, string> rename,
        IReadOnlyDictionary<string, IReadOnlyList<string>> languages)
    {
        Rename = rename;
        Languages = languages;
    }

    /// <summary>Default key to the project's spelling.</summary>
    public IReadOnlyDictionary<string, string> Rename { get; }

    /// <summary>A key set replacing the default's for a language, by CLDR locale key; a locale the default lacks is added.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Languages { get; }
}

/// <summary>
/// The developer's file: the application's contract with the dictionary, written to
/// the template the plugin publishes and imported into the project settings. The
/// settings keep the text as imported, so export writes it back unchanged.
/// </summary>
/// <remarks>
/// Parsing checks the shape only. Whether the kinds exist, the supplies make sense and
/// the respelling is sound is checked when the file is merged over the shipped default
/// (<see cref="GrammarDictionaryReader.Merge"/>), which knows the kinds.
/// </remarks>
public sealed class DeveloperFile
{
    public const int CurrentVersion = 1;

    public DeveloperFile(
        int version,
        IReadOnlyList<DeclaredArgument> arguments,
        IReadOnlyDictionary<string, KindRespelling> keys,
        string? other,
        string text)
    {
        Version = version;
        Arguments = arguments;
        Keys = keys;
        Other = other;
        Text = text;
    }

    public int Version { get; }

    public IReadOnlyList<DeclaredArgument> Arguments { get; }

    /// <summary>The respelling per kind.</summary>
    public IReadOnlyDictionary<string, KindRespelling> Keys { get; }

    /// <summary>The project's words for the <c>other</c> key, "the plural form"; null to keep the default hint.</summary>
    public string? Other { get; }

    /// <summary>The file as imported, which export writes back.</summary>
    public string Text { get; }

    public DeclaredArgument? Argument(string baseName) =>
        Arguments.FirstOrDefault(argument => argument.Base == baseName);

    public static DeveloperFile Parse(string json)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));

        JObject root;
        try
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
            root = JObject.Load(reader);
        }
        catch (JsonException error)
        {
            throw new GrammarDictionaryException($"The developer's file is not valid JSON: {error.Message}", error);
        }

        var version = root["version"] is JValue { Type: JTokenType.Integer } versionValue
            ? (int)versionValue
            : throw new GrammarDictionaryException("The developer's file has no integer 'version'.");
        if (version != CurrentVersion)
        {
            throw new GrammarDictionaryException($"The developer's file is version {version}; this plugin reads version {CurrentVersion}.");
        }

        var arguments = new List<DeclaredArgument>();
        if (root["arguments"] is { } argumentsToken)
        {
            if (argumentsToken is not JObject argumentsObject)
            {
                throw new GrammarDictionaryException("'arguments' must be an object keyed by base argument.");
            }

            foreach (var property in argumentsObject.Properties())
            {
                arguments.Add(ParseArgument(property.Name, property.Value));
            }
        }

        var keys = new Dictionary<string, KindRespelling>(StringComparer.Ordinal);
        if (root["keys"] is { } keysToken)
        {
            if (keysToken is not JObject keysObject)
            {
                throw new GrammarDictionaryException("'keys' must be an object keyed by kind.");
            }

            foreach (var property in keysObject.Properties())
            {
                keys.Add(property.Name, ParseRespelling(property.Name, property.Value));
            }
        }

        string? other = null;
        if (root["other"] is { } otherToken)
        {
            other = otherToken is JValue { Type: JTokenType.String } otherValue && ((string?)otherValue.Value)?.Trim().Length > 0
                ? (string)otherValue.Value!
                : throw new GrammarDictionaryException("'other' must be a non-empty string.");
        }

        return new DeveloperFile(version, arguments, keys, other, json);
    }

    private static DeclaredArgument ParseArgument(string baseName, JToken token)
    {
        if (baseName.Trim().Length == 0)
        {
            throw new GrammarDictionaryException("An argument entry has an empty base name.");
        }

        if (token is not JObject entry)
        {
            throw new GrammarDictionaryException($"Argument '{baseName}' must be an object.");
        }

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var supplies = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var keys = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);

        foreach (var property in entry.Properties())
        {
            switch (property.Name)
            {
                case "supplies":
                    if (property.Value is not JObject suppliesObject)
                    {
                        throw new GrammarDictionaryException($"Argument '{baseName}': 'supplies' must be an object keyed by language.");
                    }

                    foreach (var language in suppliesObject.Properties())
                    {
                        if (!LanguageTag.TryParse(language.Name, out var tag))
                        {
                            throw new GrammarDictionaryException($"Argument '{baseName}': '{language.Name}' is not a language tag.");
                        }

                        supplies[tag.Normalised] = StringList(language.Value, $"Argument '{baseName}', supplies for '{language.Name}'");
                    }

                    break;

                case "keys":
                    if (property.Value is not JObject keysObject)
                    {
                        throw new GrammarDictionaryException($"Argument '{baseName}': 'keys' must be an object keyed by kind.");
                    }

                    foreach (var kind in keysObject.Properties())
                    {
                        keys[kind.Name] = StringMap(kind.Value, $"Argument '{baseName}', keys for '{kind.Name}'");
                    }

                    break;

                default:
                    names[property.Name] = property.Value is JValue { Type: JTokenType.String } name && ((string?)name.Value)?.Trim().Length > 0
                        ? (string)name.Value!
                        : throw new GrammarDictionaryException($"Argument '{baseName}': '{property.Name}' must name the argument the application passes.");
                    break;
            }
        }

        return new DeclaredArgument(baseName, names, supplies, keys);
    }

    private static KindRespelling ParseRespelling(string kindName, JToken token)
    {
        if (token is not JObject entry)
        {
            throw new GrammarDictionaryException($"Keys for '{kindName}' must be an object.");
        }

        var rename = new Dictionary<string, string>(StringComparer.Ordinal);
        var languages = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var property in entry.Properties())
        {
            switch (property.Name)
            {
                case "rename":
                    foreach (var pair in StringMap(property.Value, $"Keys for '{kindName}', 'rename'"))
                    {
                        rename[pair.Key] = pair.Value;
                    }

                    break;

                case "languages":
                    if (property.Value is not JObject languagesObject)
                    {
                        throw new GrammarDictionaryException($"Keys for '{kindName}': 'languages' must be an object keyed by language.");
                    }

                    foreach (var language in languagesObject.Properties())
                    {
                        if (!LanguageTag.TryParse(language.Name, out var tag))
                        {
                            throw new GrammarDictionaryException($"Keys for '{kindName}': '{language.Name}' is not a language tag.");
                        }

                        languages[tag.Normalised] = StringList(language.Value, $"Keys for '{kindName}', language '{language.Name}'");
                    }

                    break;

                default:
                    throw new GrammarDictionaryException($"Keys for '{kindName}': '{property.Name}' is not 'rename' or 'languages'.");
            }
        }

        return new KindRespelling(rename, languages);
    }

    internal static IReadOnlyList<string> StringList(JToken token, string where)
    {
        if (token is not JArray array)
        {
            throw new GrammarDictionaryException($"{where}: expected a list of strings.");
        }

        var list = new List<string>();
        foreach (var item in array)
        {
            if (item is not JValue { Type: JTokenType.String } value || ((string?)value.Value)?.Trim().Length == 0)
            {
                throw new GrammarDictionaryException($"{where}: expected a list of non-empty strings.");
            }

            list.Add((string)value.Value!);
        }

        return list;
    }

    internal static IReadOnlyDictionary<string, string> StringMap(JToken token, string where)
    {
        if (token is not JObject map)
        {
            throw new GrammarDictionaryException($"{where}: expected an object of strings.");
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in map.Properties())
        {
            if (property.Value is not JValue { Type: JTokenType.String } value || ((string?)value.Value)?.Trim().Length == 0)
            {
                throw new GrammarDictionaryException($"{where}: '{property.Name}' must be a non-empty string.");
            }

            result[property.Name] = (string)value.Value!;
        }

        return result;
    }
}
