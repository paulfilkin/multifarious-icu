namespace multifarious.Icu.Expansion;

/// <summary>
/// Thrown when the shipped grammar dictionary or a developer's file cannot be read as
/// the model expects. For the shipped file that is a build-time mistake; for a
/// developer's file the message is what the import shows the user, so it names the
/// entry at fault in plain words.
/// </summary>
public sealed class GrammarDictionaryException : Exception
{
    public GrammarDictionaryException(string message) : base(message)
    {
    }

    public GrammarDictionaryException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
