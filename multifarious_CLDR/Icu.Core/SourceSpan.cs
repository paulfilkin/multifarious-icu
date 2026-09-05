namespace Icu.Core;

/// <summary>
/// A half-open range of character offsets into the original message text. Every node
/// carries one, which is what makes the parse lossless: any node can point back at
/// exactly the text it was produced from, and a parse error can name the offset it
/// happened at.
/// </summary>
public readonly record struct SourceSpan(int Start, int Length)
{
    /// <summary>The offset one past the last character covered.</summary>
    public int End => Start + Length;

    /// <summary>The exact text this span covers in the message it was parsed from.</summary>
    public string TextIn(string source) => source.Substring(Start, Length);

    public override string ToString() => $"[{Start}..{End})";
}
