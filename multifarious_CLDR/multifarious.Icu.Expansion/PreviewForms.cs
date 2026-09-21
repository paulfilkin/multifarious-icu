using System.Globalization;
using Icu.Cldr;
using Icu.Cldr.Rules;
using Icu.Core;
using Icu.Core.Tree;

namespace multifarious.Icu.Expansion;

/// <summary>One row of the all-forms table (design 12.1).</summary>
public sealed record PreviewForm(
    string BranchKey,
    bool IsExplicit,
    string SampleCount,
    IReadOnlyList<string> Counts,
    bool FractionalOnly,
    string SourceRendered,
    string TargetRendered);

/// <summary>What a tried count resolves to (design 12.1's count box).</summary>
public sealed record CountResolution(string Count, string BranchKey, bool IsExplicit);

/// <summary>
/// Renders every form a message can produce (design 12.1): one row per branch of
/// the outermost selector, sample counts from CLDR, '#' and arguments substituted,
/// and a resolver for a typed count. Copied from the cloud project's Icu.Bcm with the
/// net48 substitutions; the Studio view part shows rows per segment and uses the
/// resolver and the sample policy from here.
/// </summary>
public sealed class PreviewForms
{
    private static readonly string[] NameSamples = ["Anna", "Omar", "Maria", "Ken"];

    private readonly CldrPlurals _plurals;

    public PreviewForms(CldrPlurals? plurals = null)
    {
        _plurals = plurals ?? CldrPlurals.Default;
    }

    /// <summary>
    /// The rows for one message and one language side. The outermost selector
    /// drives the rows; nested selectors resolve with the same substitutions.
    /// A message without a selector renders as a single row.
    /// </summary>
    public IReadOnlyList<PreviewForm> Rows(string sourceMessage, string targetMessage, string sourceLanguageTag, string targetLanguageTag)
    {
        if (sourceMessage is null) throw new ArgumentNullException(nameof(sourceMessage));
        if (targetMessage is null) throw new ArgumentNullException(nameof(targetMessage));

        var source = IcuParser.Parse(sourceMessage);
        var target = ParseOrNull(targetMessage);
        var selector = OutermostSelector(source.Nodes);

        if (selector is null)
        {
            return
            [
                new PreviewForm("", false, "", [], false,
                    Render(source, sourceLanguageTag, count: null, selector: null),
                    target is null ? "" : Render(target, targetLanguageTag, count: null, selector: null))
            ];
        }

        var kind = selector.Type == SelectorType.SelectOrdinal ? SelectorKind.Ordinal : SelectorKind.Cardinal;
        var rows = new List<PreviewForm>();

        if (selector.Type == SelectorType.Select)
        {
            // A select's branches are the developer's; each row simply picks one.
            foreach (var branch in selector.Branches)
            {
                rows.Add(new PreviewForm(branch.Key.Text, false, branch.Key.Text, [], false,
                    Render(source, sourceLanguageTag, branch.Key.Text, selector),
                    target is null ? "" : Render(target, targetLanguageTag, branch.Key.Text, selector)));
            }

            return rows;
        }

        foreach (var branch in selector.Branches.Where(candidate => candidate.Key.IsExplicit))
        {
            var value = branch.Key.Text.Substring(1);
            rows.Add(new PreviewForm(branch.Key.Text, true, value, [value], false,
                Render(source, sourceLanguageTag, value, selector),
                target is null ? "" : Render(target, targetLanguageTag, value, selector)));
        }

        // A category row's sample must not collide with an explicit branch, or the
        // rendered form would be the explicit branch's (explicit matches the raw
        // number first, design 5.5): with '=0' present, 'many' cannot sample 0.
        var explicitValues = selector.Branches
            .Where(candidate => candidate.Key.IsExplicit)
            .Select(candidate => decimal.Parse(candidate.Key.Text.Substring(1), NumberStyles.Number, CultureInfo.InvariantCulture))
            .ToList();

        var resolution = _plurals.Resolve(targetLanguageTag, kind);
        foreach (var category in resolution.Categories)
        {
            var integers = SampleDisplay.IntegerExamples(resolution.RuleSet, category, 6);
            var decimals = SampleDisplay.DecimalExamples(resolution.RuleSet, category, 6);
            var fractionalOnly = integers.Count == 0 && decimals.Count > 0;
            var counts = fractionalOnly ? decimals : integers;
            var usable = counts.FirstOrDefault(count =>
                !explicitValues.Contains(decimal.Parse(
                    ApplyOffset(count, selector), NumberStyles.Number, CultureInfo.InvariantCulture)));
            var sample = ApplyOffset(usable ?? counts.FirstOrDefault() ?? "0", selector);

            rows.Add(new PreviewForm(category.ToKeyword(), false, sample, counts, fractionalOnly,
                Render(source, sourceLanguageTag, sample, selector),
                target is null ? "" : Render(target, targetLanguageTag, sample, selector)));
        }

        return rows;
    }

    /// <summary>
    /// Resolves a typed count (design 5.5): explicit branches match the raw number,
    /// categories evaluate on number minus offset. Null where the message has no
    /// plural-kind outermost selector or the count is not a number.
    /// </summary>
    public CountResolution? ResolveCount(string message, string targetLanguageTag, string count)
    {
        if (message is null) throw new ArgumentNullException(nameof(message));
        if (count is null) throw new ArgumentNullException(nameof(count));

        if (ParseOrNull(message) is not { } parsed
            || OutermostSelector(parsed.Nodes) is not { } selector
            || selector.Type == SelectorType.Select)
        {
            return null;
        }

        if (selector.Branches.Any(branch => branch.Key.IsExplicit && branch.Key.Text.Substring(1) == count))
        {
            return new CountResolution(count, $"={count}", true);
        }

        var kind = selector.Type == SelectorType.SelectOrdinal ? SelectorKind.Ordinal : SelectorKind.Cardinal;
        var ruleSet = _plurals.Resolve(targetLanguageTag, kind).RuleSet;
        if (ruleSet is null || !PluralOperands.TryParse(count, out _))
        {
            return null;
        }

        var category = ruleSet.Select(Subtract(count, selector.Offset));
        return new CountResolution(count, category.ToKeyword(), false);
    }

    /// <summary>The argument the outermost plural-kind selector switches on, or null.</summary>
    public static string? OutermostPluralArgument(string message)
    {
        if (message is null) throw new ArgumentNullException(nameof(message));
        return ParseOrNull(message) is { } parsed
            && OutermostSelector(parsed.Nodes) is { } selector
            && selector.Type != SelectorType.Select
            ? selector.ArgumentName
            : null;
    }

    /// <summary>
    /// A sample value for an argument, by type (design 12.1): names for plain arguments,
    /// shaped values for typed ones. The same policy the rows use, for callers rendering
    /// one segment at a time; <paramref name="ordinal"/> counts plain arguments so a message
    /// cycles through the names.
    /// </summary>
    public static string SampleFor(string? type, ref int ordinal)
    {
        switch (type)
        {
            case "number": return "2";
            case "date": return "2026-09-02";
            case "time": return "12:30";
            default: return NameSamples[ordinal++ % NameSamples.Length];
        }
    }

    private string Render(IcuMessage message, string languageTag, string? count, SelectorNode? selector)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        var nameIndex = 0;
        CollectArguments(message.Nodes, arguments, ref nameIndex);

        if (selector is not null && count is not null)
        {
            arguments[selector.ArgumentName] = count;
        }

        var cardinal = _plurals.Resolve(languageTag, SelectorKind.Cardinal).RuleSet;
        var ordinal = _plurals.Resolve(languageTag, SelectorKind.Ordinal).RuleSet;

        return MessageRenderer.Render(message, arguments, (type, value) => type switch
        {
            SelectorType.Plural => (cardinal?.Select(value) ?? PluralCategory.Other).ToKeyword(),
            SelectorType.SelectOrdinal => (ordinal?.Select(value) ?? PluralCategory.Other).ToKeyword(),
            _ => value
        });
    }

    private static void CollectArguments(IReadOnlyList<MessageNode> nodes, Dictionary<string, string> arguments, ref int nameIndex)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case ArgumentNode argument when !arguments.ContainsKey(argument.Name):
                    arguments[argument.Name] = SampleFor(null, ref nameIndex);
                    break;
                case TypedArgumentNode typed when !arguments.ContainsKey(typed.Name):
                    arguments[typed.Name] = SampleFor(typed.Type, ref nameIndex);
                    break;
                case SelectorNode selector:
                    if (!arguments.ContainsKey(selector.ArgumentName))
                    {
                        arguments[selector.ArgumentName] = selector.Type == SelectorType.Select
                            ? selector.Branches[0].Key.Text
                            : "2";
                    }

                    foreach (var branch in selector.Branches)
                    {
                        CollectArguments(branch.Nodes, arguments, ref nameIndex);
                    }

                    break;
            }
        }
    }

    private static SelectorNode? OutermostSelector(IReadOnlyList<MessageNode> nodes) =>
        nodes.OfType<SelectorNode>().FirstOrDefault();

    private static IcuMessage? ParseOrNull(string message) =>
        message.Length > 0 && IcuParser.TryParse(message, out var parsed, out _) ? parsed : null;

    /// <summary>
    /// The '#' sample for a category row must land in that category after the
    /// selector's offset is applied, so the raw count shown is sample + offset.
    /// </summary>
    private static string ApplyOffset(string sample, SelectorNode selector) =>
        selector.Offset == 0 ? sample : Add(sample, selector.Offset);

    private static string Add(string number, decimal offset) =>
        (decimal.Parse(number, NumberStyles.Number, CultureInfo.InvariantCulture) + offset)
        .ToString(CultureInfo.InvariantCulture);

    private static string Subtract(string number, decimal offset) =>
        offset == 0
            ? number
            : (Numeric(number) - offset).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The signed numeric value of a count as written, compact exponent applied.
    /// The operand parser drops the sign because n is an absolute value, so it is
    /// read off the text; the offset subtraction is the one place the sign matters.
    /// </summary>
    private static decimal Numeric(string number)
    {
        var magnitude = PluralOperands.Parse(number).N;
        return number.TrimStart().StartsWith("-", StringComparison.Ordinal) ? -magnitude : magnitude;
    }
}
