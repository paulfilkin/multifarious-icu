namespace Icu.Core;

/// <summary>
/// A message failed to parse. Carries the zero-based character offset, because the
/// task contract reports the paragraph unit and offset for an unparseable value and
/// passes it through untouched, never repairing it.
/// </summary>
public sealed class IcuParseException : Exception
{
    public IcuParseException(string description, int position)
        : base($"{description} (offset {position}).")
    {
        Description = description;
        Position = position;
    }

    /// <summary>What went wrong, without the offset suffix.</summary>
    public string Description { get; }

    /// <summary>The zero-based character offset the parse failed at.</summary>
    public int Position { get; }
}
