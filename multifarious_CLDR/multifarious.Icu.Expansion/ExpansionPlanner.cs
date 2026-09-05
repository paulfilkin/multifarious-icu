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

    public ExpansionPlanner(CldrPlurals? plurals = null, GrammaticalHints? hints = null)
    {
        _plurals = plurals ?? CldrPlurals.Default;
        _hints = hints ?? GrammaticalHints.Embedded;
    }

    public ExpansionPlan Plan(
        MessageClassification classification,
        string sourceLanguageTag,
        IReadOnlyList<string> targetLanguageTags,
        ExpansionOptions options)
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
        var state = new PlanningState(sourceLanguageTag, targetLanguageTags, options);
        var root = PlanSequence(hoisted.Nodes, path: "", context: null, walked: null, state);

        return new ExpansionPlan(
            classification.RawValue,
            IcuSerialiser.Serialise(hoisted),
            root,
            state.Segments,
            options.MaxUnitsPerMessage);
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
        public PlanningState(string sourceLanguageTag, IReadOnlyList<string> targetLanguageTags, ExpansionOptions options)
        {
            SourceLanguageTag = sourceLanguageTag;
            TargetLanguageTags = targetLanguageTags;
            Options = options;
            SourceLanguageDisplay = DisplayNameOf(sourceLanguageTag);
        }

        public string SourceLanguageTag { get; }
        public IReadOnlyList<string> TargetLanguageTags { get; }
        public ExpansionOptions Options { get; }
        public string SourceLanguageDisplay { get; }
        public List<PlannedSegment> Segments { get; } = [];
    }

    /// <summary>The innermost expanded selector component above a leaf, which is what the comment and metadata describe.</summary>
    private sealed record SegmentContext(
        SelectorNode Selector,
        string KeyText,
        PluralCategory? Category,
        string? ExplicitValueText,
        string? SeededFrom,
        bool SyntheticSource);

    /// <summary>
    /// The innermost walked selector component above a leaf. Annotates a leaf that
    /// has no expanded selector at all: a select-only message, or a branch whose
    /// only enclosing selectors are walked (design 5.6).
    /// </summary>
    private sealed record WalkedContext(SelectorNode Selector, string KeyText);

    private PlannedNode PlanSequence(
        IReadOnlyList<MessageNode> nodes, string path, SegmentContext? context, WalkedContext? walked, PlanningState state)
    {
        // A hoisted sequence containing a selector is exactly that selector; anything
        // else is a leaf sentence.
        if (nodes.Count == 1 && nodes[0] is SelectorNode selector)
        {
            return PlanSelector(selector, path, context, walked, state);
        }

        var segment = BuildSegment(path, nodes, context, walked, state);
        state.Segments.Add(segment);
        return segment;
    }

    private PlannedSelector PlanSelector(
        SelectorNode selector, string path, SegmentContext? context, WalkedContext? walked, PlanningState state)
    {
        var expanded = selector.IsPluralKind && (selector.Type == SelectorType.Plural
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
                var content = PlanSequence(branch.Nodes, branchPath, context,
                    new WalkedContext(selector, branch.Key.Text), state);
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
            var content = PlanSequence(branch.Nodes, branchPath, branchContext, walked, state);
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
            var content = PlanSequence(seed.Nodes, branchPath, branchContext, walked, state);
            branches.Add(new PlannedBranch(keyword, false, seededFrom, synthetic, branchPath, content));
        }

        return new PlannedSelector(
            selector.Type, selector.ArgumentName, selector.OffsetRaw, true, selectorPath, branches);
    }

    private PlannedSegment BuildSegment(
        string path, IReadOnlyList<MessageNode> nodes, SegmentContext? context, WalkedContext? walked, PlanningState state)
    {
        if (context is null)
        {
            if (walked is null)
            {
                // Unreachable for an Expand classification: a leaf sits inside an
                // expanded or a walked selector. Kept as a guard.
                throw new InvalidOperationException($"Segment '{path}' has no selector above it.");
            }

            return BuildWalkedSegment(path, nodes, walked);
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
            var samples = resolution.RuleSet?.GetRule(category)?.Samples ?? PluralSamples.Empty;
            var integers = samples.TakeIntegerExamples(ExampleCount);
            var decimals = samples.TakeDecimalExamples(ExampleCount);

            comment.Append($"CLDR category: {context.KeyText}").Append('\n');
            comment.Append(samples.IsFractionalOnly
                ? $"Used when the count is: fractional counts only, e.g. {string.Join(", ", decimals)}"
                : $"Used when the count is: {string.Join(", ", integers)}");

            if (context.SeededFrom is not null)
            {
                comment.Append('\n');
                comment.Append(context.SyntheticSource
                    ? $"Source form: seeded from \"{context.SeededFrom}\" - {state.SourceLanguageDisplay} does not distinguish this form"
                    : $"Source form: seeded from \"{context.SeededFrom}\"");
            }

            if (state.Options.IncludeHints
                && _hints.Find(resolution.ResolvedKey, category) is { } hint)
            {
                comment.Append('\n');
                comment.Append($"Grammar: {hint}");
            }

            metadata["icu:category"] = context.KeyText;
            metadata["icu:locale"] = resolution.ResolvedKey ?? resolution.RequestedTag;
            metadata["icu:exampleIntegers"] = string.Join(",", integers);
            metadata["icu:exampleDecimals"] = string.Join(",", decimals);
        }

        return new PlannedSegment(path, nodes, comment.ToString(), metadata);
    }

    /// <summary>
    /// A leaf whose every enclosing selector is walked: a select branch, or a branch
    /// of a plural kind whose expansion is disabled. The branch is the developer's,
    /// so there is no CLDR involvement and no seeding; the comment names the branch
    /// and the metadata carries what finalise and the preview read (design 5.6).
    /// </summary>
    private static PlannedSegment BuildWalkedSegment(
        string path, IReadOnlyList<MessageNode> nodes, WalkedContext walked)
    {
        string comment = walked.Selector.Type == SelectorType.Select
            ? $"Select branch: {walked.Selector.ArgumentName} = {walked.KeyText}"
            : $"Branch kept as authored: {walked.Selector.ArgumentName} = {walked.KeyText} (expansion disabled)";

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

        return new PlannedSegment(path, nodes, comment, metadata);
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

    private static string DisplayNameOf(string languageTag)
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
            return "the source language";
        }
    }
}
