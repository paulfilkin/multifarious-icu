using System.Globalization;
using System.Text;
using Icu.Cldr;
using Icu.Cldr.Rules;
using Icu.Core;
using Icu.Core.Tree;

namespace multifarious.Icu.Expansion;

/// <summary>
/// Turns a hoisted message into an <see cref="ExpansionPlan"/>: which branches the
/// output message carries, what seeds each one, and the comment and metadata every
/// segment gets. Pure decision-making over Icu.Core and Icu.Cldr; no BCM is read or
/// written here.
/// </summary>
/// <remarks>
/// An expanded plural-kind selector's keyword branches are exactly the union category
/// set across the target languages (design 5.3, 7.2), in CLDR's canonical order,
/// after any explicit value branches, which are copied verbatim and never duplicated
/// (5.5). A source keyword outside the union set is dropped: no target language can
/// ever select it. A select, or a plural kind whose expansion is disabled, is walked:
/// its source branches pass through as path components (5.6).
///
/// A select whose argument the grammar dictionary recognises is category expanded the
/// same way, with the key set the target language needs for the kind in place of the
/// CLDR categories, and a dependent selector the language needs inserted where the
/// developer's file declares the application supplies it (grammar dictionary design
/// v2, 13 October 2026). Without a dictionary every select is walked.
///
/// Example counts and the locale named in metadata come from the first target
/// language, in project order, whose category set contains the segment's category.
/// A union expansion serves several languages whose samples differ; picking the
/// first that needs the category makes the choice deterministic and recorded.
/// </remarks>
public sealed class ExpansionPlanner
{
    private const int ExampleCount = 6;

    private readonly CldrPlurals _plurals;
    private readonly GrammaticalHints _hints;
    private readonly GrammarDictionary? _dictionary;

    /// <param name="dictionary">The grammar dictionary in force, or null to walk every select as the source wrote it.</param>
    public ExpansionPlanner(CldrPlurals? plurals = null, GrammaticalHints? hints = null, GrammarDictionary? dictionary = null)
    {
        _plurals = plurals ?? CldrPlurals.Default;
        _hints = hints ?? GrammaticalHints.Embedded;
        _dictionary = dictionary;
    }

    public ExpansionPlan Plan(
        MessageClassification classification,
        string sourceLanguageTag,
        IReadOnlyList<string> targetLanguageTags,
        ExpansionOptions options) =>
        PlanCore(classification, sourceLanguageTag, targetLanguageTags, options, walkedNote: null);

    /// <summary>
    /// The plan for a message over the branch budget (design 5.7): the hoisted message with
    /// every selector walked, so the syntax reaches the translator protected and every
    /// branch is a whole sentence, but no category is expanded and the branches stay the
    /// source's. The note says why in every segment comment. Added for Studio (6 September
    /// 2026, Project 41) after a passed-through value was garbled in translation like any
    /// plain text.
    /// </summary>
    public ExpansionPlan PlanWalked(
        MessageClassification classification,
        string sourceLanguageTag,
        IReadOnlyList<string> targetLanguageTags,
        ExpansionOptions options,
        string walkedNote)
    {
        if (walkedNote is null) throw new ArgumentNullException(nameof(walkedNote));
        return PlanCore(classification, sourceLanguageTag, targetLanguageTags, options, walkedNote);
    }

    private ExpansionPlan PlanCore(
        MessageClassification classification,
        string sourceLanguageTag,
        IReadOnlyList<string> targetLanguageTags,
        ExpansionOptions options,
        string? walkedNote)
    {
        if (classification is null) throw new ArgumentNullException(nameof(classification));
        if (sourceLanguageTag is null) throw new ArgumentNullException(nameof(sourceLanguageTag));
        if (targetLanguageTags is null) throw new ArgumentNullException(nameof(targetLanguageTags));
        if (options is null) throw new ArgumentNullException(nameof(options));

        if (classification.Kind != MessageKind.Expand)
        {
            throw new ArgumentException(
                $"Only an Expand classification can be planned; this one is {classification.Kind}.",
                nameof(classification));
        }

        if (targetLanguageTags.Count == 0)
        {
            throw new ArgumentException("At least one target language is required.", nameof(targetLanguageTags));
        }

        var hoisted = classification.Hoisted!;
        var state = new PlanningState(sourceLanguageTag, targetLanguageTags, options, walkedNote);
        var root = PlanSequence(hoisted.Nodes, path: "", context: null, walked: null, GrammarScope.None, state);

        return new ExpansionPlan(
            classification.RawValue,
            IcuSerialiser.Serialise(hoisted),
            root,
            state.Segments,
            options.MaxUnitsPerMessage,
            state.Notes);
    }

    /// <summary>
    /// The plan for a <see cref="MessageKind.Protect"/> message: one segment holding the
    /// whole message with its arguments as protected syntax, no selector and no CLDR
    /// annotation. The comment tells the translator what the protection means.
    /// </summary>
    public ExpansionPlan PlanProtected(MessageClassification classification)
    {
        if (classification is null) throw new ArgumentNullException(nameof(classification));

        if (classification.Kind != MessageKind.Protect)
        {
            throw new ArgumentException(
                $"Only a Protect classification can be planned this way; this one is {classification.Kind}.",
                nameof(classification));
        }

        var message = classification.Message!;
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["icu:path"] = "",
            ["icu:selector"] = "none"
        };
        var segment = new PlannedSegment(
            "",
            message.Nodes,
            "ICU message: the arguments in this segment are protected syntax and must be kept.",
            metadata);

        return new ExpansionPlan(
            classification.RawValue,
            IcuSerialiser.Serialise(message.Nodes),
            segment,
            [segment],
            int.MaxValue);
    }

    /// <summary>Everything one Plan call carries down the walk.</summary>
    private sealed class PlanningState
    {
        public PlanningState(
            string sourceLanguageTag, IReadOnlyList<string> targetLanguageTags, ExpansionOptions options, string? walkedNote)
        {
            SourceLanguageTag = sourceLanguageTag;
            TargetLanguageTags = targetLanguageTags;
            Options = options;
            WalkedNote = walkedNote;
            SourceLanguageDisplay = DisplayNameOf(sourceLanguageTag);
        }

        public string SourceLanguageTag { get; }
        public IReadOnlyList<string> TargetLanguageTags { get; }
        public ExpansionOptions Options { get; }

        /// <summary>Set where every selector is walked whatever its kind, and why; null for a normal plan.</summary>
        public string? WalkedNote { get; }

        public string SourceLanguageDisplay { get; }
        public List<PlannedSegment> Segments { get; } = [];

        /// <summary>The plan's notes, each once; see <see cref="ExpansionPlan.Notes"/>.</summary>
        public List<string> Notes { get; } = [];

        public void Note(string note)
        {
            if (!Notes.Contains(note))
            {
                Notes.Add(note);
            }
        }
    }

    /// <summary>The innermost expanded selector component above a leaf, which is what the comment and metadata describe.</summary>
    private sealed record SegmentContext(
        SelectorNode Selector,
        string KeyText,
        PluralCategory? Category,
        string? ExplicitValueText,
        string? SeededFrom,
        bool SyntheticSource,
        GrammarForm? Grammar = null);

    /// <summary>A grammar select's key as laid out: the kind, and the hint and sample the dictionary gives it, or none for the developer's own key.</summary>
    private sealed record GrammarForm(string Kind, string? LocaleKey, string? Hint, string? Sample, string BaseArgument);

    /// <summary>
    /// The innermost walked selector component above a leaf. Annotates a leaf that
    /// has no expanded selector at all: a select-only message, or a branch whose
    /// only enclosing selectors are walked (design 5.6).
    /// </summary>
    private sealed record WalkedContext(SelectorNode Selector, string KeyText);

    /// <summary>
    /// A grammar select above a leaf: what the dictionary recognised it as, the entry
    /// per target language, and the branch key the leaf sits under. The frames are
    /// what dependent insertion consults.
    /// </summary>
    private sealed record GrammarFrame(ArgumentRecognition Recognition, IReadOnlyList<GrammarLanguage> Languages, string KeyText);

    /// <summary>
    /// What the walk carries for the grammar dictionary: the frames above a leaf, and
    /// per base argument the branch path that a dependent kind narrows its keys by,
    /// <c>MASCULINE/one</c>. A base's path collects the keys of the recognised selectors
    /// with that base and of the plural kinds beneath them; an unrelated select does not
    /// enter it, and a second base argument keeps its own.
    /// </summary>
    private sealed record GrammarScope(IReadOnlyList<GrammarFrame> Frames, IReadOnlyDictionary<string, string> WithinPaths)
    {
        public static GrammarScope None { get; } = new([], new Dictionary<string, string>(StringComparer.Ordinal));

        public string WithinPath(string baseArgument) =>
            WithinPaths.TryGetValue(baseArgument, out var path) ? path : "";

        public GrammarScope EnterGrammar(GrammarFrame frame)
        {
            var frames = new List<GrammarFrame>(Frames) { frame };
            var paths = WithinPaths.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            var baseArgument = frame.Recognition.BaseArgument;
            paths[baseArgument] = Append(WithinPath(baseArgument), frame.KeyText);
            return new GrammarScope(frames, paths);
        }

        public GrammarScope EnterPlural(string keyText)
        {
            if (WithinPaths.Count == 0)
            {
                return this;
            }

            var paths = WithinPaths.ToDictionary(pair => pair.Key, pair => Append(pair.Value, keyText), StringComparer.Ordinal);
            return new GrammarScope(Frames, paths);
        }

        private static string Append(string path, string keyText) =>
            path.Length == 0 ? keyText : $"{path}/{keyText}";
    }

    private PlannedNode PlanSequence(
        IReadOnlyList<MessageNode> nodes, string path, SegmentContext? context, WalkedContext? walked,
        GrammarScope scope, PlanningState state)
    {
        // A hoisted sequence containing a selector is exactly that selector; anything
        // else is a leaf sentence.
        if (nodes.Count == 1 && nodes[0] is SelectorNode selector)
        {
            return PlanSelector(selector, path, context, walked, scope, state);
        }

        // A dependent kind a grammar select above needs under this branch is inserted
        // here, at the leaf, so it lands under the gender directly or under the plural
        // beneath it alike; where the application does not supply it the leaf says so.
        var needs = new List<string>();
        foreach (var frame in scope.Frames)
        {
            foreach (var need in frame.Languages.SelectMany(language => language.Needs)
                         .Where(need => need.Under.Contains(frame.KeyText, StringComparer.Ordinal))
                         .GroupBy(need => need.Kind, StringComparer.Ordinal)
                         .Select(group => group.First()))
            {
                var baseArgument = frame.Recognition.BaseArgument;
                if (scope.Frames.Any(above => above.Recognition.Kind == need.Kind && above.Recognition.BaseArgument == baseArgument))
                {
                    // The source has the dependent select already, or it was inserted above.
                    continue;
                }

                var dependentName = _dictionary!.DependentArgumentName(baseArgument, need.Kind);
                var supplied = dependentName is not null
                    && state.TargetLanguageTags.Any(target => _dictionary.Supplies(baseArgument, target, need.Kind));
                if (supplied)
                {
                    var synthetic = new SelectorNode(
                        SelectorType.Select, dependentName!, null, 0,
                        [new Branch(BranchKey.Keyword(GrammarDictionary.OtherKey), nodes, default)],
                        default);
                    return PlanSelector(synthetic, path, context, walked, scope, state);
                }

                var languageTag = state.TargetLanguageTags[0];
                for (var index = 0; index < frame.Languages.Count; index++)
                {
                    if (frame.Languages[index].Needs.Any(entryNeed => entryNeed.Kind == need.Kind))
                    {
                        languageTag = state.TargetLanguageTags[index];
                        break;
                    }
                }

                var note = $"{DisplayNameOf(languageTag, languageTag)} needs {Article(need.Kind)} {need.Kind} form for {{{baseArgument}}}; " +
                           $"the application does not supply {dependentName ?? ConventionalName(baseArgument, need.Kind)}";
                needs.Add(note);
                state.Note(note);
            }
        }

        var segment = BuildSegment(path, nodes, context, walked, needs, state);
        state.Segments.Add(segment);
        return segment;
    }

    private PlannedSelector PlanSelector(
        SelectorNode selector, string path, SegmentContext? context, WalkedContext? walked,
        GrammarScope scope, PlanningState state)
    {
        if (selector.Type == SelectorType.Select && state.WalkedNote is null && _dictionary is not null
            && _dictionary.Recognise(selector.ArgumentName) is { } recognition)
        {
            var resolved = state.TargetLanguageTags
                .Select(target => _dictionary.Resolve(recognition.Kind, target, recognition.BaseArgument))
                .ToList();
            var unknown = state.TargetLanguageTags.Where((_, index) => resolved[index] is null).ToList();
            if (unknown.Count == 0)
            {
                var languages = resolved.Select(language => language!).ToList();
                return PlanGrammarSelector(selector, recognition, languages, path, context, walked, scope, state);
            }

            state.Note($"{string.Join(", ", unknown.Select(tag => DisplayNameOf(tag, tag)))} is not in the grammar dictionary for {recognition.Kind}; " +
                       $"{{{selector.ArgumentName}}} is laid out as written");
        }

        var expanded = state.WalkedNote is null && selector.IsPluralKind && (selector.Type == SelectorType.Plural
            ? state.Options.ExpandCardinal
            : state.Options.ExpandOrdinal);

        var selectorPath = path.Length == 0 ? selector.ArgumentName : $"{path}/{selector.ArgumentName}";
        var branches = new List<PlannedBranch>();

        if (!expanded)
        {
            // Walked, never expanded: the branches are the developer's, or belong to a
            // kind this task leaves alone. The annotation context stays with the
            // innermost expanded selector.
            foreach (var branch in selector.Branches)
            {
                var branchPath = Join(path, selector.ArgumentName, branch.Key.Text);
                var branchScope = selector.IsPluralKind ? scope.EnterPlural(branch.Key.Text) : scope;
                var content = PlanSequence(branch.Nodes, branchPath, context,
                    new WalkedContext(selector, branch.Key.Text), branchScope, state);
                branches.Add(new PlannedBranch(
                    branch.Key.Text, branch.Key.IsExplicit, null, false, branchPath, content));
            }

            return new PlannedSelector(
                selector.Type, selector.ArgumentName, selector.OffsetRaw, false, selectorPath, branches);
        }

        var kind = selector.Type == SelectorType.Plural ? SelectorKind.Cardinal : SelectorKind.Ordinal;
        var union = _plurals.UnionOfCategories(state.TargetLanguageTags, kind);
        var other = selector.Branches.First(branch =>
            !branch.Key.IsExplicit && branch.Key.Text == "other");

        foreach (var branch in selector.Branches.Where(branch => branch.Key.IsExplicit))
        {
            var branchPath = Join(path, selector.ArgumentName, branch.Key.Text);
            var branchContext = new SegmentContext(
                selector, branch.Key.Text, null, branch.Key.Text[1..], null, false);
            var content = PlanSequence(branch.Nodes, branchPath, branchContext, walked, scope.EnterPlural(branch.Key.Text), state);
            branches.Add(new PlannedBranch(branch.Key.Text, true, null, false, branchPath, content));
        }

        foreach (var category in union)
        {
            var keyword = category.ToKeyword();
            var own = selector.Branches.FirstOrDefault(branch =>
                !branch.Key.IsExplicit && branch.Key.Text == keyword);
            var seed = state.Options.SourceSeedStrategy == SourceSeedStrategy.AlwaysOther
                ? other
                : own ?? other;
            var seededFrom = ReferenceEquals(seed, own) ? null : seed.Key.Text;
            var synthetic = own is null;

            var branchPath = Join(path, selector.ArgumentName, keyword);
            var branchContext = new SegmentContext(selector, keyword, category, null, seededFrom, synthetic);
            var content = PlanSequence(seed.Nodes, branchPath, branchContext, walked, scope.EnterPlural(keyword), state);
            branches.Add(new PlannedBranch(keyword, false, seededFrom, synthetic, branchPath, content));
        }

        return new PlannedSelector(
            selector.Type, selector.ArgumentName, selector.OffsetRaw, true, selectorPath, branches);
    }

    /// <summary>
    /// A select the dictionary recognised, category expanded: the developer's own keys
    /// first, kept as explicit values are on a plural, then the keys the target
    /// language needs for the kind, narrowed by the branch path within the base
    /// argument, each seeded from its own source branch or from <c>other</c>.
    /// </summary>
    private PlannedSelector PlanGrammarSelector(
        SelectorNode selector, ArgumentRecognition recognition, IReadOnlyList<GrammarLanguage> languages,
        string path, SegmentContext? context, WalkedContext? walked, GrammarScope scope, PlanningState state)
    {
        var withinPath = scope.WithinPath(recognition.BaseArgument);
        var keys = UnionOfKeys(languages.Select(language => language.KeysWithin(withinPath)));
        var known = new HashSet<string>(_dictionary!.KnownKeys(recognition.Kind, recognition.BaseArgument), StringComparer.Ordinal);
        var other = selector.Branches.First(branch => branch.Key.Text == GrammarDictionary.OtherKey);

        var selectorPath = path.Length == 0 ? selector.ArgumentName : $"{path}/{selector.ArgumentName}";
        var branches = new List<PlannedBranch>();

        // A source key no language of the kind uses is the developer's own; one the kind
        // knows but this language does not need is pruned by not being laid out.
        foreach (var branch in selector.Branches.Where(branch => !known.Contains(branch.Key.Text)))
        {
            var branchPath = Join(path, selector.ArgumentName, branch.Key.Text);
            var branchContext = new SegmentContext(
                selector, branch.Key.Text, null, null, null, false, new GrammarForm(recognition.Kind, null, null, null, recognition.BaseArgument));
            var branchScope = scope.EnterGrammar(new GrammarFrame(recognition, languages, branch.Key.Text));
            var content = PlanSequence(branch.Nodes, branchPath, branchContext, walked, branchScope, state);
            branches.Add(new PlannedBranch(branch.Key.Text, false, null, false, branchPath, content));
        }

        foreach (var key in keys)
        {
            var own = selector.Branches.FirstOrDefault(branch => branch.Key.Text == key);
            var seed = state.Options.SourceSeedStrategy == SourceSeedStrategy.AlwaysOther
                ? other
                : own ?? other;
            var seededFrom = ReferenceEquals(seed, own) ? null : seed.Key.Text;
            var synthetic = own is null;

            // The hint and sample come from the first target language whose set has the
            // key, as the plural samples do.
            var language = languages.First(language => language.Keys.Contains(key, StringComparer.Ordinal));
            var form = new GrammarForm(recognition.Kind, language.LocaleKey, language.Hint(key), language.Sample(key), recognition.BaseArgument);

            var branchPath = Join(path, selector.ArgumentName, key);
            var branchContext = new SegmentContext(selector, key, null, null, seededFrom, synthetic, form);
            var branchScope = scope.EnterGrammar(new GrammarFrame(recognition, languages, key));
            var content = PlanSequence(seed.Nodes, branchPath, branchContext, walked, branchScope, state);
            branches.Add(new PlannedBranch(key, false, seededFrom, synthetic, branchPath, content));
        }

        return new PlannedSelector(
            selector.Type, selector.ArgumentName, selector.OffsetRaw, true, selectorPath, branches,
            recognition.Kind, keys);
    }

    /// <summary>The keys of several languages in first-seen order, <c>other</c> last.</summary>
    private static IReadOnlyList<string> UnionOfKeys(IEnumerable<IReadOnlyList<string>> keySets)
    {
        var union = new List<string>();
        foreach (var key in keySets.SelectMany(keys => keys))
        {
            if (key != GrammarDictionary.OtherKey && !union.Contains(key, StringComparer.Ordinal))
            {
                union.Add(key);
            }
        }

        union.Add(GrammarDictionary.OtherKey);
        return union;
    }

    /// <summary>The name an undeclared dependent argument would have by the suffix convention, for the note.</summary>
    private string ConventionalName(string baseArgument, string kindName) =>
        _dictionary!.Kinds.TryGetValue(kindName, out var kind) && kind.Suffix is { Length: > 0 } suffix
            ? baseArgument + suffix
            : baseArgument + char.ToUpperInvariant(kindName[0]) + kindName[1..];

    private static string Article(string word) =>
        "aeiou".IndexOf(char.ToLowerInvariant(word[0])) >= 0 ? "an" : "a";

    private PlannedSegment BuildSegment(
        string path, IReadOnlyList<MessageNode> nodes, SegmentContext? context, WalkedContext? walked,
        IReadOnlyList<string> needs, PlanningState state)
    {
        if (context is null)
        {
            if (walked is null)
            {
                // Unreachable for an Expand classification: a leaf sits inside an
                // expanded or a walked selector. Kept as a guard.
                throw new InvalidOperationException($"Segment '{path}' has no selector above it.");
            }

            return BuildWalkedSegment(path, nodes, walked, needs, state.WalkedNote);
        }

        if (context.Grammar is { } grammar)
        {
            return BuildGrammarSegment(path, nodes, context, grammar, needs);
        }

        var kind = context.Selector.Type == SelectorType.Plural ? SelectorKind.Cardinal : SelectorKind.Ordinal;
        var comment = new StringBuilder();
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["icu:path"] = path,
            ["icu:selector"] = context.Selector.Type.ToKeyword(),
            ["icu:offset"] = context.Selector.OffsetRaw ?? "0",
            ["icu:syntheticSource"] = context.SyntheticSource ? "true" : "false",
            ["icu:seededFrom"] = context.SeededFrom ?? ""
        };

        // Comment lines are joined with '\n' explicitly: the text lands in BCM comment
        // definitions and must not vary with the operating system the task ran on.
        if (context.ExplicitValueText is not null)
        {
            comment.Append($"Exact match: {context.KeyText}").Append('\n');
            comment.Append($"Used only when the count is: {context.ExplicitValueText}");

            var resolution = _plurals.Resolve(state.TargetLanguageTags[0], kind);
            metadata["icu:category"] = context.KeyText;
            metadata["icu:locale"] = resolution.ResolvedKey ?? state.TargetLanguageTags[0];
            metadata["icu:exampleIntegers"] = context.ExplicitValueText;
            metadata["icu:exampleDecimals"] = "";
        }
        else
        {
            var category = context.Category!.Value;
            var resolution = ResolutionFor(category, kind, state);
            var integers = SampleDisplay.IntegerExamples(resolution.RuleSet, category, ExampleCount);
            var decimals = SampleDisplay.DecimalExamples(resolution.RuleSet, category, ExampleCount);

            comment.Append($"CLDR category: {context.KeyText}").Append('\n');
            comment.Append(integers.Count == 0 && decimals.Count > 0
                ? $"Used when the count is: fractional counts only, e.g. {string.Join(", ", decimals)}"
                : $"Used when the count is: {string.Join(", ", integers)}");

            if (context.SeededFrom is not null)
            {
                comment.Append('\n');
                comment.Append(context.SyntheticSource
                    ? $"Source form: seeded from \"{context.SeededFrom}\" - {state.SourceLanguageDisplay} does not distinguish this form"
                    : $"Source form: seeded from \"{context.SeededFrom}\"");
            }

            if (_hints.Find(resolution.ResolvedKey, category) is { } hint)
            {
                comment.Append('\n');
                comment.Append($"Grammar: {hint}");
            }

            metadata["icu:category"] = context.KeyText;
            metadata["icu:locale"] = resolution.ResolvedKey ?? resolution.RequestedTag;
            metadata["icu:exampleIntegers"] = string.Join(",", integers);
            metadata["icu:exampleDecimals"] = string.Join(",", decimals);
        }

        AppendNeeds(comment, metadata, needs);
        return new PlannedSegment(path, nodes, comment.ToString(), metadata);
    }

    /// <summary>
    /// A leaf whose innermost expanded selector is a grammar select: the key, the
    /// dictionary's hint and sample for it, and where it was seeded from. The developer's
    /// own key, outside the dictionary's set, is named as such.
    /// </summary>
    private static PlannedSegment BuildGrammarSegment(
        string path, IReadOnlyList<MessageNode> nodes, SegmentContext context, GrammarForm grammar, IReadOnlyList<string> needs)
    {
        var comment = new StringBuilder();
        comment.Append($"Grammar form: {context.KeyText}");
        if (grammar.Hint is null)
        {
            comment.Append(" (the developer's own key)");
        }
        else
        {
            comment.Append($" ({grammar.Hint}");
            if (grammar.Sample is not null)
            {
                comment.Append($", e.g. {grammar.Sample}");
            }

            comment.Append(')');
        }

        if (context.SeededFrom is not null)
        {
            comment.Append('\n');
            comment.Append($"Source form: seeded from \"{context.SeededFrom}\"");
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["icu:path"] = path,
            ["icu:selector"] = SelectorType.Select.ToKeyword(),
            ["icu:category"] = context.KeyText,
            ["icu:kind"] = grammar.Kind,
            ["icu:locale"] = grammar.LocaleKey ?? "",
            ["icu:hint"] = grammar.Hint ?? "",
            ["icu:sample"] = grammar.Sample ?? "",
            ["icu:baseArgument"] = grammar.BaseArgument,
            ["icu:exampleIntegers"] = "",
            ["icu:exampleDecimals"] = "",
            ["icu:syntheticSource"] = context.SyntheticSource ? "true" : "false",
            ["icu:seededFrom"] = context.SeededFrom ?? "",
            ["icu:offset"] = "0"
        };

        AppendNeeds(comment, metadata, needs);
        return new PlannedSegment(path, nodes, comment.ToString(), metadata);
    }

    /// <summary>
    /// A leaf whose every enclosing selector is walked: a select branch, a branch of a
    /// plural kind whose expansion is disabled, or any branch of a plan walked for being
    /// over budget. The branch is the developer's, so there is no CLDR involvement and
    /// no seeding; the comment names the branch and the metadata carries what finalise
    /// and the preview read (design 5.6).
    /// </summary>
    private static PlannedSegment BuildWalkedSegment(
        string path, IReadOnlyList<MessageNode> nodes, WalkedContext walked, IReadOnlyList<string> needs, string? walkedNote)
    {
        string comment = walked.Selector.Type == SelectorType.Select
            ? $"Select branch: {walked.Selector.ArgumentName} = {walked.KeyText}"
            : $"Branch kept as authored: {walked.Selector.ArgumentName} = {walked.KeyText}";
        if (walkedNote is not null)
        {
            comment += $" ({walkedNote})";
        }
        else if (walked.Selector.Type != SelectorType.Select)
        {
            comment += " (expansion disabled)";
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["icu:path"] = path,
            ["icu:selector"] = walked.Selector.Type.ToKeyword(),
            ["icu:category"] = walked.KeyText,
            ["icu:locale"] = "",
            ["icu:exampleIntegers"] = "",
            ["icu:exampleDecimals"] = "",
            ["icu:syntheticSource"] = "false",
            ["icu:seededFrom"] = "",
            ["icu:offset"] = walked.Selector.OffsetRaw ?? "0"
        };

        var builder = new StringBuilder(comment);
        AppendNeeds(builder, metadata, needs);
        return new PlannedSegment(path, nodes, builder.ToString(), metadata);
    }

    /// <summary>The dependent forms a language needs here and the application does not supply, on the comment and as <c>icu:needs</c>.</summary>
    private static void AppendNeeds(StringBuilder comment, Dictionary<string, string> metadata, IReadOnlyList<string> needs)
    {
        foreach (var need in needs)
        {
            comment.Append('\n').Append($"Needs: {need}");
        }

        metadata["icu:needs"] = string.Join("\n", needs);
    }

    /// <summary>
    /// The resolution whose samples annotate a category: the first target language,
    /// in project order, whose set contains it. The union guarantees one exists.
    /// </summary>
    private LocaleResolution ResolutionFor(PluralCategory category, SelectorKind kind, PlanningState state)
    {
        LocaleResolution? first = null;
        foreach (var tag in state.TargetLanguageTags)
        {
            var resolution = _plurals.Resolve(tag, kind);
            first ??= resolution;
            if (resolution.Categories.Contains(category))
            {
                return resolution;
            }
        }

        return first!;
    }

    private static string Join(string path, string argumentName, string keyText)
    {
        var component = $"{argumentName}:{keyText}";
        return path.Length == 0 ? component : $"{path}/{component}";
    }

    private static string DisplayNameOf(string languageTag, string fallback = "the source language")
    {
        try
        {
            var culture = new CultureInfo(languageTag);
            while (!culture.Parent.Equals(CultureInfo.InvariantCulture))
            {
                culture = culture.Parent;
            }

            return culture.EnglishName;
        }
        catch (CultureNotFoundException)
        {
            return fallback;
        }
    }
}
