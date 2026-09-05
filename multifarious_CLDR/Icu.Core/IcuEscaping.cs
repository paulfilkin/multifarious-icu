using System.Text;

namespace Icu.Core;

/// <summary>
/// The escaping transform of the design's section 5.8, both directions.
/// <see cref="Unescape"/> runs at expand, so the translator sees clean text: doubled
/// apostrophes become one and quoted literals become their bare characters.
/// <see cref="Escape"/> runs at finalise, on target segment text only: apostrophes are
/// doubled and syntax characters typed as text are wrapped in quotes as ICU requires.
/// This is a file-level concern and must never reach translation memory, which is why
/// TM Update runs before finalise.
/// </summary>
/// <remarks>
/// Both directions are context-sensitive the same way the parser is: '#' is syntax
/// only inside a plural or selectordinal branch, so '#' quoting means nothing outside
/// one and is left alone there. The serialiser escapes through this class, so there is
/// exactly one implementation of the rules.
/// </remarks>
public static class IcuEscaping
{
    /// <summary>
    /// Decodes ICU escape constructs into plain text: '' becomes ', a quoted literal
    /// becomes its content, and an apostrophe that neither doubles nor opens a quote
    /// stays as it is. An unterminated quote runs to the end, exactly as ICU reads it.
    /// </summary>
    public static string Unescape(string text, bool inPluralContext)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));

        var builder = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c != '\'')
            {
                builder.Append(c);
                i++;
                continue;
            }

            if (i + 1 >= text.Length)
            {
                builder.Append('\'');
                i++;
                continue;
            }

            var next = text[i + 1];
            if (next == '\'')
            {
                builder.Append('\'');
                i += 2;
                continue;
            }

            if (IsSyntax(next, inPluralContext))
            {
                i++;
                while (i < text.Length)
                {
                    if (text[i] == '\'')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '\'')
                        {
                            builder.Append('\'');
                            i += 2;
                            continue;
                        }

                        i++;
                        break;
                    }

                    builder.Append(text[i]);
                    i++;
                }

                continue;
            }

            builder.Append('\'');
            i++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Encodes plain text so ICU reads it back as exactly that text: apostrophes
    /// doubled, syntax characters quoted.
    /// </summary>
    public static string Escape(string text, bool inPluralContext)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));

        var builder = new StringBuilder(text.Length);
        AppendEscaped(builder, text, inPluralContext);
        return builder.ToString();
    }

    /// <summary>
    /// The escape direction, writing into an existing builder: apostrophes are
    /// doubled, and the stretch from the first syntax character to the last is wrapped
    /// in one quoted literal. The quote must open immediately before a syntax character
    /// or the parser would read the apostrophe as literal, which is why it opens at
    /// the first syntax character rather than around the whole run; it closes after
    /// the last so that plain text is not quoted needlessly.
    /// </summary>
    internal static void AppendEscaped(StringBuilder builder, string text, bool inPlural)
    {
        var firstSyntax = IndexOfSyntax(text, inPlural);
        if (firstSyntax < 0)
        {
            AppendDoublingApostrophes(builder, text, 0, text.Length);
            return;
        }

        var lastSyntax = LastIndexOfSyntax(text, inPlural);

        AppendDoublingApostrophes(builder, text, 0, firstSyntax);
        builder.Append('\'');
        AppendDoublingApostrophes(builder, text, firstSyntax, lastSyntax - firstSyntax + 1);
        builder.Append('\'');
        AppendDoublingApostrophes(builder, text, lastSyntax + 1, text.Length - lastSyntax - 1);
    }

    private static int IndexOfSyntax(string text, bool inPlural)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (IsSyntax(text[i], inPlural))
            {
                return i;
            }
        }

        return -1;
    }

    private static int LastIndexOfSyntax(string text, bool inPlural)
    {
        for (var i = text.Length - 1; i >= 0; i--)
        {
            if (IsSyntax(text[i], inPlural))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsSyntax(char c, bool inPlural) =>
        c == '{' || c == '}' || (c == '#' && inPlural);

    private static void AppendDoublingApostrophes(StringBuilder builder, string text, int start, int length)
    {
        for (var i = start; i < start + length; i++)
        {
            var c = text[i];
            builder.Append(c);
            if (c == '\'')
            {
                builder.Append('\'');
            }
        }
    }
}
