using System.Globalization;
using System.Text;
using Icu.Core.Tree;

namespace Icu.Core;

/// <summary>
/// Renders a message for given argument values: selects branches, substitutes '#' and
/// arguments, decodes quoted text. This is the preview's engine and the oracle for
/// the hoisting property, which is why it lives here: <c>render(m, n)</c> must equal
/// <c>render(hoist(m), n)</c> for every count.
/// </summary>
/// <remarks>
/// The category decision is injected as a delegate rather than taken from CLDR, so
/// this project keeps its no-dependency rule; the caller wires in
/// <c>Icu.Cldr</c>. Argument values are strings, never doubles, because plural
/// operands come from how a number is written: 1 and 1.0 are the same value and
/// different operands. That includes CLDR's compact notation, where 1c6 is 1000000
/// written compactly and the exponent is itself a plural operand, so a compact
/// count reaches the category decision as written. Numbers are substituted in
/// invariant format; locale-correct formatting for the preview is a presentation
/// concern layered on top.
///
/// Selection follows section 5.5: explicit value branches match the raw number,
/// keyword branches evaluate on the number minus the selector's offset, and '#'
/// renders that same offset number. A select matches its branch keyword against the
/// argument's value, falling back to 'other'.
/// </remarks>
public static class MessageRenderer
{
    public static string Render(
        IcuMessage message,
        IReadOnlyDictionary<string, string> arguments,
        Func<SelectorType, string, string> selectCategory)
    {
        if (message is null) throw new ArgumentNullException(nameof(message));
        return Render(message.Nodes, arguments, selectCategory);
    }

    public static string Render(
        IReadOnlyList<MessageNode> nodes,
        IReadOnlyDictionary<string, string> arguments,
        Func<SelectorType, string, string> selectCategory)
    {
        if (nodes is null) throw new ArgumentNullException(nameof(nodes));
        if (arguments is null) throw new ArgumentNullException(nameof(arguments));
        if (selectCategory is null) throw new ArgumentNullException(nameof(selectCategory));

        var builder = new StringBuilder();
        AppendNodes(builder, nodes, arguments, selectCategory, binding: null);
        return builder.ToString();
    }

    /// <summary>What '#' renders as: the nearest enclosing plural's number and offset.</summary>
    private sealed record PluralBinding(decimal Value, decimal Offset);

    private static void AppendNodes(
        StringBuilder builder,
        IReadOnlyList<MessageNode> nodes,
        IReadOnlyDictionary<string, string> arguments,
        Func<SelectorType, string, string> selectCategory,
        PluralBinding? binding)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case TextNode text:
                    builder.Append(text.Value);
                    break;
                case QuotedTextNode quoted:
                    builder.Append(quoted.Value);
                    break;
                case PoundNode:
                    if (binding is null)
                    {
                        throw new InvalidOperationException("'#' encountered outside any plural branch.");
                    }

                    builder.Append((binding.Value - binding.Offset).ToString(CultureInfo.InvariantCulture));
                    break;
                case ArgumentNode argument:
                    builder.Append(ValueOf(arguments, argument.Name));
                    break;
                case TypedArgumentNode typed:
                    builder.Append(RenderTypedArgument(typed, arguments));
                    break;
                case SelectorNode selector:
                    AppendSelector(builder, selector, arguments, selectCategory, binding);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown node type '{node.GetType().Name}'.");
            }
        }
    }

    /// <summary>
    /// A '#' rewritten by hoisting to <c>{argument, number}</c> must render exactly
    /// as the '#' did, so a numeric value is normalised through decimal the same way;
    /// any other typed value is substituted as given.
    /// </summary>
    private static string RenderTypedArgument(
        TypedArgumentNode node, IReadOnlyDictionary<string, string> arguments)
    {
        var value = ValueOf(arguments, node.Name);
        return string.Equals(node.Type, "number", StringComparison.Ordinal)
            && TryParseNumber(value, out var number, out _)
            ? number.ToString(CultureInfo.InvariantCulture)
            : value;
    }

    private static void AppendSelector(
        StringBuilder builder,
        SelectorNode selector,
        IReadOnlyDictionary<string, string> arguments,
        Func<SelectorType, string, string> selectCategory,
        PluralBinding? binding)
    {
        var value = ValueOf(arguments, selector.ArgumentName);

        if (selector.Type == SelectorType.Select)
        {
            var chosen = selector.Branches.FirstOrDefault(branch =>
                    string.Equals(branch.Key.Text, value, StringComparison.Ordinal))
                ?? OtherBranch(selector);

            // A select neither starts nor ends the plural context, so the binding
            // passes straight through.
            AppendNodes(builder, chosen.Nodes, arguments, selectCategory, binding);
            return;
        }

        if (!TryParseNumber(value, out var number, out var isCompact))
        {
            throw new InvalidOperationException(
                $"Argument '{selector.ArgumentName}' has value '{value}', which is not a number a plural can select on.");
        }

        var branch2 = selector.Branches.FirstOrDefault(branch =>
            branch.Key.IsExplicit && branch.Key.ExplicitValue == number);

        if (branch2 is null)
        {
            // A compact count goes to the category decision as written: the exponent
            // is a plural operand, and reformatting through decimal would zero it.
            // An offset forces numeric subtraction, which the exponent cannot
            // survive; ICU applies its offset numerically too.
            var operand = isCompact && selector.Offset == 0
                ? value
                : (number - selector.Offset).ToString(CultureInfo.InvariantCulture);
            var keyword = selectCategory(selector.Type, operand);
            branch2 = selector.Branches.FirstOrDefault(branch =>
                    !branch.Key.IsExplicit && string.Equals(branch.Key.Text, keyword, StringComparison.Ordinal))
                ?? OtherBranch(selector);
        }

        AppendNodes(builder, branch2.Nodes, arguments, selectCategory,
            new PluralBinding(number, selector.Offset));
    }

    private static Branch OtherBranch(SelectorNode selector) =>
        selector.Branches.First(branch =>
            !branch.Key.IsExplicit && string.Equals(branch.Key.Text, "other", StringComparison.Ordinal));

    private static readonly char[] CompactExponentMarkers = ['c', 'e', 'C', 'E'];

    /// <summary>
    /// An exponent large enough to matter here would overflow decimal anyway. CLDR's
    /// own samples go no higher than c6; the bound mirrors Icu.Cldr's operand parser.
    /// </summary>
    private const int MaximumCompactExponent = 28;

    /// <summary>
    /// Parses a number as written: sign, digits, an optional fraction, and the
    /// optional compact exponent introduced by 'c' or 'e'. Applying the exponent
    /// consumes fraction digits into the integer part, so the numeric value of 1.2c6
    /// is 1200000 with no fractional scale, matching the operands CLDR derives.
    /// </summary>
    private static bool TryParseNumber(string value, out decimal number, out bool isCompact)
    {
        isCompact = false;
        if (decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out number))
        {
            return true;
        }

        var split = value.IndexOfAny(CompactExponentMarkers);
        if (split <= 0
            || !decimal.TryParse(value[..split], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var mantissa)
            || !int.TryParse(value[(split + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var exponent)
            || exponent > MaximumCompactExponent)
        {
            return false;
        }

        try
        {
            number = decimal.Round(mantissa * Pow10(exponent), Math.Max(0, ScaleOf(mantissa) - exponent));
        }
        catch (OverflowException)
        {
            return false;
        }

        isCompact = true;
        return true;
    }

    /// <summary>
    /// The number of digits after the decimal point, as written. The reference reads
    /// <c>decimal.Scale</c>, which .NET 7 added; on net48 the scale is bits 16 to 23 of
    /// the flags word.
    /// </summary>
    private static int ScaleOf(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;

    private static decimal Pow10(int exponent)
    {
        var result = 1m;
        for (var index = 0; index < exponent; index++)
        {
            result *= 10m;
        }

        return result;
    }

    private static string ValueOf(IReadOnlyDictionary<string, string> arguments, string name) =>
        arguments.TryGetValue(name, out var value)
            ? value
            : throw new InvalidOperationException($"No value supplied for argument '{name}'.");
}
