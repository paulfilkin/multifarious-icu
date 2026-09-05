using Icu.Core;
using Icu.Core.Tree;

namespace multifarious.Icu.Expansion;

public enum MessageKind
{
    /// <summary>
    /// Nothing to restructure: no selector at all, or only plural kinds whose
    /// expansion is disabled. The paragraph unit is left untouched.
    /// </summary>
    NoIcu,

    /// <summary>Parsed, hoisted and ready to plan.</summary>
    Expand,

    /// <summary>
    /// Cannot be expanded: a parse failure or a hoist refusal. Left untouched and
    /// reported (D10); under <see cref="ParseErrorBehaviour.FailTask"/> the task
    /// orchestrator fails the job instead.
    /// </summary>
    PassThrough,

    /// <summary>
    /// No selector, but arguments: the message is laid out as one segment with the
    /// arguments protected, so a translator cannot damage them. Added for Studio (5
    /// September 2026) after pseudo-translation garbled the arguments of every
    /// argument-only value; the cloud design left these to the filter.
    /// </summary>
    Protect
}

/// <summary>What one reconstructed value is, and everything later stages need of it.</summary>
public sealed class MessageClassification
{
    private MessageClassification(MessageKind kind, string rawValue, IcuMessage? hoisted, string? reason, int? errorOffset,
        IcuMessage? message = null)
    {
        Kind = kind;
        RawValue = rawValue;
        Hoisted = hoisted;
        Reason = reason;
        ErrorOffset = errorOffset;
        Message = message;
    }

    public MessageKind Kind { get; }

    public string RawValue { get; }

    /// <summary>The hoisted message, present only for <see cref="MessageKind.Expand"/>.</summary>
    public IcuMessage? Hoisted { get; }

    /// <summary>The parsed message as written, present only for <see cref="MessageKind.Protect"/>.</summary>
    public IcuMessage? Message { get; }

    /// <summary>Why the message passes through, for the warning and the manifest.</summary>
    public string? Reason { get; }

    /// <summary>The zero-based offset of a parse failure, where that is the reason.</summary>
    public int? ErrorOffset { get; }

    public static MessageClassification NoIcu(string rawValue) =>
        new(MessageKind.NoIcu, rawValue, null, null, null);

    public static MessageClassification Expand(string rawValue, IcuMessage hoisted) =>
        new(MessageKind.Expand, rawValue, hoisted, null, null);

    public static MessageClassification PassThrough(string rawValue, string reason, int? errorOffset = null) =>
        new(MessageKind.PassThrough, rawValue, null, reason, errorOffset);

    public static MessageClassification Protect(string rawValue, IcuMessage message) =>
        new(MessageKind.Protect, rawValue, null, null, null, message);
}

/// <summary>
/// Decides what to do with one reconstructed value: nothing, expand, or pass through
/// with a reason. Parsing, the enabled-kind check and hoisting all happen here, so a
/// value that reaches the planner is guaranteed plannable.
/// </summary>
public static class MessageClassifier
{
    public static MessageClassification Classify(string rawValue, ExpansionOptions options)
    {
        if (rawValue is null) throw new ArgumentNullException(nameof(rawValue));
        if (options is null) throw new ArgumentNullException(nameof(options));

        if (!IcuParser.TryParse(rawValue, out var message, out var error))
        {
            return MessageClassification.PassThrough(rawValue,
                $"The value does not parse as ICU: {error!.Description}.", error.Position);
        }

        // A message with an enabled plural kind expands; a message whose only
        // selectors are selects (or plural kinds nested under them) is restructured
        // the same way with every selector walked, so the syntax reaches the
        // translator as protected tags rather than editable text (design 5.6,
        // extended by T4 evidence: raw pass-through values get segmented mid-syntax
        // and invite corruption).
        if (!ContainsEnabledPluralKind(message!.Nodes, options) && !ContainsSelect(message.Nodes))
        {
            // No selector at all but at least one argument: protect the arguments in a
            // single segment. A message whose only selectors are disabled plural kinds
            // stays untouched, as does one with no braces at all.
            if (!ContainsSelector(message.Nodes) && ContainsArgument(message.Nodes))
            {
                return MessageClassification.Protect(rawValue, message);
            }

            return MessageClassification.NoIcu(rawValue);
        }

        try
        {
            return MessageClassification.Expand(rawValue, Hoister.Hoist(message));
        }
        catch (IcuHoistException exception)
        {
            return MessageClassification.PassThrough(rawValue, exception.Message);
        }
    }

    /// <summary>
    /// Whether the tree contains a plural-kind selector whose kind is enabled. A
    /// disabled kind is invisible here: a message carrying only that kind is left
    /// untouched, exactly as a message with no ICU at all is.
    /// </summary>
    private static bool ContainsEnabledPluralKind(IReadOnlyList<MessageNode> nodes, ExpansionOptions options)
    {
        foreach (var node in nodes)
        {
            if (node is not SelectorNode selector)
            {
                continue;
            }

            var enabled = selector.Type switch
            {
                SelectorType.Plural => options.ExpandCardinal,
                SelectorType.SelectOrdinal => options.ExpandOrdinal,
                _ => false
            };

            if (enabled || selector.Branches.Any(branch => ContainsEnabledPluralKind(branch.Nodes, options)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSelector(IReadOnlyList<MessageNode> nodes) =>
        nodes.Any(node => node is SelectorNode);

    private static bool ContainsArgument(IReadOnlyList<MessageNode> nodes) =>
        nodes.Any(node => node is ArgumentNode or TypedArgumentNode);

    private static bool ContainsSelect(IReadOnlyList<MessageNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is not SelectorNode selector)
            {
                continue;
            }

            if (selector.Type == SelectorType.Select
                || selector.Branches.Any(branch => ContainsSelect(branch.Nodes)))
            {
                return true;
            }
        }

        return false;
    }
}
