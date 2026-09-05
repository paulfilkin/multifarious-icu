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
/// different operands. Numbers are substituted in invariant format; locale-correct
/// formatting for the preview is a presentation concern layered on top.
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
            && TryParseNumber(value, out var number)
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

        if (!TryParseNumber(value, out var number))
        {
            throw new InvalidOperationException(
                $"Argument '{selector.ArgumentName}' has value '{value}', which is not a number a plural can select on.");
        }

        var branch2 = selector.Branches.FirstOrDefault(branch =>
            branch.Key.IsExplicit && branch.Key.ExplicitValue == number);

        if (branch2 is null)
        {
            var keyword = selectCategory(
                selector.Type, (number - selector.Offset).ToString(CultureInfo.InvariantCulture));
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

    private static bool TryParseNumber(string value, out decimal number) =>
        decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out number);

    private static string ValueOf(IReadOnlyDictionary<string, string> arguments, string name) =>
        arguments.TryGetValue(name, out var value)
            ? value
            : throw new InvalidOperationException($"No value supplied for argument '{name}'.");
}
