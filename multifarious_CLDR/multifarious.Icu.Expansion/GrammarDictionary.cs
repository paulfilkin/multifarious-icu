using Icu.Cldr;

namespace multifarious.Icu.Expansion;

/// <summary>
/// A dependent selector kind a language needs on the same base argument, nested
/// under the given branches of the kind that declares it: French needs an article
/// form under MASCULINE and FEMININE.
/// </summary>
public sealed record DependentNeed(string Kind, IReadOnlyList<string> Under);

/// <summary>
/// What one target language needs for one selector kind: the keys in layout order,
/// a hint and an optional sample per key, the dependent kinds, and the narrowing of
/// the keys by the branch path the selector sits in.
/// </summary>
public sealed class GrammarLanguage
{
    public GrammarLanguage(
        string localeKey,
        IReadOnlyList<string> keys,
        IReadOnlyDictionary<string, string> hints,
        IReadOnlyDictionary<string, string> samples,
        IReadOnlyList<DependentNeed> needs,
        IReadOnlyDictionary<string, IReadOnlyList<string>> within)
    {
        LocaleKey = localeKey;
        Keys = keys;
        Hints = hints;
        Samples = samples;
        Needs = needs;
        Within = within;
    }

    /// <summary>The CLDR locale key the language resolved to, <c>de</c> for <c>de-AT</c>.</summary>
    public string LocaleKey { get; }

    /// <summary>The keys the language needs, in layout order, <c>other</c> last.</summary>
    public IReadOnlyList<string> Keys { get; }

    /// <summary>The hint per key, the grammatical term or the project's own words.</summary>
    public IReadOnlyDictionary<string, string> Hints { get; }

    /// <summary>A sample noun per key, only where a native reader has checked it.</summary>
    public IReadOnlyDictionary<string, string> Samples { get; }

    /// <summary>The dependent kinds this language needs on the same base argument.</summary>
    public IReadOnlyList<DependentNeed> Needs { get; }

    /// <summary>The keys narrowed by branch path, <c>MASCULINE/one</c>; a path not listed takes <see cref="Keys"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Within { get; }

    public IReadOnlyList<string> KeysWithin(string path) =>
        Within.TryGetValue(path, out var keys) ? keys : Keys;

    public string Hint(string key) =>
        Hints.TryGetValue(key, out var hint) ? hint : key;

    public string? Sample(string key) =>
        Samples.TryGetValue(key, out var sample) ? sample : null;

    /// <summary>
    /// The entry with its own keys respelt by <paramref name="own"/> and the branch path
    /// components, which are other kinds' keys, by <paramref name="paths"/>.
    /// </summary>
    internal GrammarLanguage Respell(IReadOnlyDictionary<string, string> own, IReadOnlyDictionary<string, string> paths)
    {
        if (own.Count == 0 && paths.Count == 0)
        {
            return this;
        }

        string Own(string key) => own.TryGetValue(key, out var to) ? to : key;
        string Path(string path) => string.Join("/", path.Split('/').Select(component =>
            paths.TryGetValue(component, out var to) ? to : component));

        return new GrammarLanguage(
            LocaleKey,
            Keys.Select(Own).ToList(),
            Hints.ToDictionary(pair => Own(pair.Key), pair => pair.Value, StringComparer.Ordinal),
            Samples.ToDictionary(pair => Own(pair.Key), pair => pair.Value, StringComparer.Ordinal),
            Needs.Select(need => new DependentNeed(need.Kind, need.Under.Select(Own).ToList())).ToList(),
            Within.ToDictionary(
                pair => Path(pair.Key),
                pair => (IReadOnlyList<string>)pair.Value.Select(Own).ToList(),
                StringComparer.Ordinal));
    }

    /// <summary>
    /// The entry with its key set replaced outright. Samples and narrowing entries for
    /// keys no longer in the set are dropped; a new key's hint is the kind's, or the key.
    /// </summary>
    internal GrammarLanguage WithKeys(IReadOnlyList<string> keys, IReadOnlyDictionary<string, string> kindHints)
    {
        var set = new HashSet<string>(keys, StringComparer.Ordinal);
        var hints = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            hints[key] = Hints.TryGetValue(key, out var own) ? own
                : kindHints.TryGetValue(key, out var kind) ? kind
                : key;
        }

        return new GrammarLanguage(
            LocaleKey,
            keys,
            hints,
            Samples.Where(pair => set.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            Needs,
            Within.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value.Where(set.Contains).ToList(),
                StringComparer.Ordinal));
    }

    /// <summary>The entry with the project's own words as the hint for <c>other</c>.</summary>
    internal GrammarLanguage WithOther(string description)
    {
        var hints = Hints.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        hints[GrammarDictionary.OtherKey] = description;
        return new GrammarLanguage(LocaleKey, Keys, hints, Samples, Needs, Within);
    }
}

/// <summary>
/// One selector kind beyond plural and ordinal: how its argument is recognised by
/// name, the hint per key, and an entry per language the dictionary knows.
/// </summary>
public sealed class GrammarKind
{
    public GrammarKind(
        string name,
        string? suffix,
        IReadOnlyDictionary<string, string> hints,
        IReadOnlyDictionary<string, GrammarLanguage> languages)
    {
        Name = name;
        Suffix = suffix;
        Hints = hints;
        Languages = languages;
    }

    public string Name { get; }

    /// <summary>The argument name suffix that marks the kind, <c>Gender</c>; null where the kind is recognised by declaration only.</summary>
    public string? Suffix { get; }

    /// <summary>The hint per key, the same words for every language.</summary>
    public IReadOnlyDictionary<string, string> Hints { get; }

    /// <summary>The entries by CLDR locale key.</summary>
    public IReadOnlyDictionary<string, GrammarLanguage> Languages { get; }

    /// <summary>The entry a language tag resolves to down the locale chain, or null where the language is unknown to the kind.</summary>
    public GrammarLanguage? Resolve(string? languageTag)
    {
        var key = LocaleChain.FirstMatch(languageTag, Languages.ContainsKey);
        return key is null ? null : Languages[key];
    }
}

/// <summary>A select argument recognised as a grammar kind, and the base argument it belongs to.</summary>
public sealed record ArgumentRecognition(string Kind, string BaseArgument, string ArgumentName, bool Declared);

/// <summary>
/// The grammar dictionary in force: the shipped default alone, or the default with a
/// developer's file merged over it. Answers the three questions the planner asks: is
/// this select argument a grammar kind, what does this language need for the kind,
/// and does the application supply a dependent argument for this language.
/// </summary>
/// <remarks>
/// Design: <c>Documentation\Design\Grammar-Dictionary-Design-v2.md</c> (local). The
/// dictionary knows nothing of what a kind means; gender and article are entries in
/// a data file, and a project may declare any enumeration whose key set depends on the
/// target language.
/// </remarks>
public sealed class GrammarDictionary
{
    public const string OtherKey = "other";

    /// <summary>The argument role for the plural count, named in a developer's file beside the kinds; not a kind.</summary>
    public const string CountRole = "count";

    private static readonly Lazy<GrammarDictionary> LazyDefault =
        new(GrammarDictionaryReader.ReadDefault, isThreadSafe: true);

    public GrammarDictionary(
        int version,
        string cldrVersion,
        IReadOnlyDictionary<string, GrammarKind> kinds,
        DeveloperFile? developerFile)
    {
        Version = version;
        CldrVersion = cldrVersion;
        Kinds = kinds;
        DeveloperFile = developerFile;
    }

    /// <summary>The shipped default, read from the embedded file once.</summary>
    public static GrammarDictionary Default => LazyDefault.Value;

    public int Version { get; }

    /// <summary>The CLDR release the gender sets came from.</summary>
    public string CldrVersion { get; }

    public IReadOnlyDictionary<string, GrammarKind> Kinds { get; }

    /// <summary>The developer's file merged over the default, or null for the default alone.</summary>
    public DeveloperFile? DeveloperFile { get; }

    /// <summary>The default with a developer's file merged over it. Only the default takes a file.</summary>
    public GrammarDictionary WithDeveloperFile(DeveloperFile file)
    {
        if (file is null) throw new ArgumentNullException(nameof(file));
        if (DeveloperFile is not null)
        {
            throw new InvalidOperationException("A developer's file is merged over the shipped default, not over another file.");
        }

        return GrammarDictionaryReader.Merge(this, file);
    }

    /// <summary>
    /// Whether a select argument is a grammar kind: by the developer's declaration
    /// first, then by the kind's name suffix. Null for an application state.
    /// </summary>
    public ArgumentRecognition? Recognise(string? argumentName)
    {
        if (argumentName is null || argumentName.Length == 0)
        {
            return null;
        }

        if (DeveloperFile is not null)
        {
            foreach (var argument in DeveloperFile.Arguments)
            {
                foreach (var pair in argument.Names)
                {
                    if (pair.Value == argumentName && Kinds.ContainsKey(pair.Key))
                    {
                        return new ArgumentRecognition(pair.Key, argument.Base, argumentName, true);
                    }
                }
            }
        }

        foreach (var kind in Kinds.Values)
        {
            if (kind.Suffix is { Length: > 0 } suffix
                && argumentName.Length > suffix.Length
                && argumentName.EndsWith(suffix, StringComparison.Ordinal))
            {
                return new ArgumentRecognition(kind.Name, argumentName[..^suffix.Length], argumentName, false);
            }
        }

        return null;
    }

    /// <summary>
    /// What a language needs for a kind, respelt for the base argument where the
    /// developer's file spells that one argument differently. Null where the kind or
    /// the language is unknown.
    /// </summary>
    public GrammarLanguage? Resolve(string kindName, string? languageTag, string? baseArgument = null)
    {
        if (kindName is null) throw new ArgumentNullException(nameof(kindName));

        if (!Kinds.TryGetValue(kindName, out var kind))
        {
            return null;
        }

        var language = kind.Resolve(languageTag);
        if (language is null || baseArgument is null || DeveloperFile?.Argument(baseArgument) is not { Keys.Count: > 0 } argument)
        {
            return language;
        }

        var own = argument.Keys.TryGetValue(kindName, out var map) ? map : EmptyMap;
        return language.Respell(own, argument.PathRenames);
    }

    /// <summary>
    /// Every key any language of the kind uses, respelt for the base argument. A source
    /// key outside this set is the developer's own and is kept; one inside it that the
    /// target language does not need is pruned.
    /// </summary>
    public IReadOnlyList<string> KnownKeys(string kindName, string? baseArgument = null)
    {
        if (kindName is null) throw new ArgumentNullException(nameof(kindName));

        if (!Kinds.TryGetValue(kindName, out var kind))
        {
            return Array.Empty<string>();
        }

        var own = baseArgument is not null
            && DeveloperFile?.Argument(baseArgument) is { } argument
            && argument.Keys.TryGetValue(kindName, out var map)
            ? map
            : EmptyMap;

        var known = new List<string>();
        foreach (var key in kind.Languages.Values.SelectMany(language => language.Keys))
        {
            var spelt = own.TryGetValue(key, out var to) ? to : key;
            if (!known.Contains(spelt, StringComparer.Ordinal))
            {
                known.Add(spelt);
            }
        }

        return known;
    }

    /// <summary>Whether the developer's file declares the dependent argument of a kind supplied for a language.</summary>
    public bool Supplies(string baseArgument, string? languageTag, string kindName)
    {
        if (baseArgument is null) throw new ArgumentNullException(nameof(baseArgument));
        if (kindName is null) throw new ArgumentNullException(nameof(kindName));

        if (DeveloperFile?.Argument(baseArgument) is not { } argument)
        {
            return false;
        }

        var key = LocaleChain.FirstMatch(languageTag, argument.Supplies.ContainsKey);
        return key is not null && argument.Supplies[key].Contains(kindName, StringComparer.Ordinal);
    }

    /// <summary>The argument the application passes for a kind of a base argument, as declared, or null.</summary>
    public string? DependentArgumentName(string baseArgument, string kindName)
    {
        if (baseArgument is null) throw new ArgumentNullException(nameof(baseArgument));
        if (kindName is null) throw new ArgumentNullException(nameof(kindName));

        return DeveloperFile?.Argument(baseArgument) is { } argument
            && argument.Names.TryGetValue(kindName, out var name)
            ? name
            : null;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyMap =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
