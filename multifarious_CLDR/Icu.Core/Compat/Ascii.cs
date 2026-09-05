namespace Icu.Core;

/// <summary>
/// The ASCII character classes the parser needs. .NET 8 has char.IsAsciiDigit and
/// char.IsAsciiLetter; .NET Framework 4.8 does not, and char.IsDigit or char.IsLetter
/// would accept every Unicode digit and letter, which ICU syntax does not.
/// </summary>
internal static class Ascii
{
    public static bool IsDigit(char c) => c >= '0' && c <= '9';

    public static bool IsLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
}
