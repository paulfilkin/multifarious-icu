using FsCheck.Xunit;
using Icu.Core;
using Icu.Core.Tree;

namespace Icu.Core.Tests;

/// <summary>
/// The escaping transform of section 5.8: unescape at expand so the translator sees
/// clean text, escape at finalise so ICU reads the translation back as exactly what
/// was typed. The apostrophe edge cases are enumerated explicitly, per section 14.2.
/// </summary>
public class IcuEscapingTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("plain text", "plain text")]
    [InlineData("''", "'")]
    [InlineData("don''t", "don't")]
    [InlineData("don't", "don't")]
    [InlineData("'{'", "{")]
    [InlineData("'}'", "}")]
    [InlineData("'{count}'", "{count}")]
    [InlineData("'{a''b}'", "{a'b}")]
    [InlineData("'{'oops", "{oops")]
    [InlineData("'{oops", "{oops")]
    [InlineData("rock'", "rock'")]
    public void UnescapeDecodesTheApostropheConstructs(string escaped, string plain)
    {
        Assert.Equal(plain, IcuEscaping.Unescape(escaped, inPluralContext: true));
        Assert.Equal(plain, IcuEscaping.Unescape(escaped, inPluralContext: false));
    }

    /// <summary>
    /// '#' quoting exists only inside a plural branch. Outside one the apostrophe was
    /// never a quote, so the text is already plain and must not change.
    /// </summary>
    [Fact]
    public void QuotedPoundDecodesOnlyInPluralContext()
    {
        Assert.Equal("# left", IcuEscaping.Unescape("'#' left", inPluralContext: true));
        Assert.Equal("'#' left", IcuEscaping.Unescape("'#' left", inPluralContext: false));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("plain text", "plain text")]
    [InlineData("100%", "100%")]
    [InlineData("'", "''")]
    [InlineData("it's", "it''s")]
    [InlineData("{", "'{'")]
    [InlineData("}", "'}'")]
    [InlineData("a{b}c", "a'{b}'c")]
    [InlineData("{count}", "'{count}'")]
    [InlineData("it's {here}", "it''s '{here}'")]
    public void EscapeEncodesPlainTextForIcu(string plain, string escaped)
    {
        Assert.Equal(escaped, IcuEscaping.Escape(plain, inPluralContext: false));
    }

    [Fact]
    public void EscapeQuotesPoundOnlyInPluralContext()
    {
        Assert.Equal("1 '#' 2", IcuEscaping.Escape("1 # 2", inPluralContext: true));
        Assert.Equal("1 # 2", IcuEscaping.Escape("1 # 2", inPluralContext: false));
    }

    /// <summary>
    /// The transform the translator lives inside: whatever they type, escape at
    /// finalise then unescape reads back exactly what they typed.
    /// </summary>
    [Property(MaxTest = 500)]
    public void UnescapeAfterEscapeIsTheIdentity(int seed)
    {
        var text = GeneratePlainText(new Random(seed));

        Assert.Equal(text, IcuEscaping.Unescape(IcuEscaping.Escape(text, true), true));
        Assert.Equal(text, IcuEscaping.Unescape(IcuEscaping.Escape(text, false), false));
    }

    /// <summary>
    /// Escaped text must agree with the parser, which is the authority on what ICU
    /// reads: parsed in the context it was escaped for, it comes back as text nodes
    /// only, carrying exactly the original content.
    /// </summary>
    [Property(MaxTest = 500)]
    public void EscapedTextParsesBackToTheSameContent(int seed)
    {
        var text = GeneratePlainText(new Random(seed));

        var topLevel = IcuMessage.Parse(IcuEscaping.Escape(text, inPluralContext: false));
        Assert.Equal(text, ConcatenatedText(topLevel.Nodes));

        var wrapped = IcuMessage.Parse(
            "{n, plural, other {" + IcuEscaping.Escape(text, inPluralContext: true) + "}}");
        var branch = Assert.IsType<SelectorNode>(Assert.Single(wrapped.Nodes)).Branches[0];
        Assert.Equal(text, ConcatenatedText(branch.Nodes));
    }

    /// <summary>
    /// Unescape must agree with the parser too: for any text-only fragment, decoding
    /// it directly gives what the parser decodes it to.
    /// </summary>
    [Property(MaxTest = 500)]
    public void UnescapeAgreesWithTheParser(int seed)
    {
        // Escaping arbitrary plain text produces a fragment covering every apostrophe
        // construct; parse it at the top level where the parser's own decoding applies.
        var fragment = IcuEscaping.Escape(GeneratePlainText(new Random(seed)), inPluralContext: false);

        var parsed = IcuMessage.Parse(fragment);

        Assert.Equal(ConcatenatedText(parsed.Nodes), IcuEscaping.Unescape(fragment, inPluralContext: false));
    }

    private static string ConcatenatedText(IReadOnlyList<MessageNode> nodes) =>
        string.Concat(nodes.Select(node => node switch
        {
            TextNode text => text.Value,
            QuotedTextNode quoted => quoted.Value,
            _ => throw new InvalidOperationException($"Unexpected node {node} in escaped text.")
        }));

    private static string GeneratePlainText(Random random)
    {
        // The same hostile alphabet the tree generator uses: apostrophes, braces, '#'
        // and the punctuation they sit amongst.
        const string alphabet = "ab c'#{}.,=!";
        var length = random.Next(0, 13);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = alphabet[random.Next(alphabet.Length)];
        }

        return new string(chars);
    }
}
