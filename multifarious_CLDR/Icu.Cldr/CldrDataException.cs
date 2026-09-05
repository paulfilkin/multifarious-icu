namespace Icu.Cldr;

/// <summary>
/// Thrown when the embedded CLDR data cannot be read as the code expects.
/// </summary>
/// <remarks>
/// The data is pinned and embedded, so in a shipped build this exception means the
/// pinned files and the code that reads them have diverged, which is a build-time
/// mistake rather than anything a caller did. It is thrown rather than swallowed on
/// purpose: a scheduled job diffs upstream CLDR and opens a pull request when a
/// locale changes, and the parser failing loudly on a construct it has not seen is
/// how that change gets noticed rather than silently mis-handled.
/// </remarks>
public sealed class CldrDataException : Exception
{
    public CldrDataException(string message) : base(message)
    {
    }

    public CldrDataException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
