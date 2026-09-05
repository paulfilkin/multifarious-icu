using System.Globalization;
using System.Text;
using Icu.Core.Tree;

namespace Icu.Core;

/// <summary>
/// Recursive-descent parser for ICU MessageFormat, producing a tree whose every node
/// records its source offsets. There is no separate token stream, because the syntax
/// is context-sensitive in two ways a flat lexer cannot carry: '#' is syntax only
/// inside a plural or selectordinal branch, and an apostrophe opens a quote only when
/// the character after it is syntax in the current context.
/// </summary>
/// <remarks>
/// The apostrophe rules, as ICU defines them and section 5.1 of the design restates
/// them: a doubled apostrophe is a literal apostrophe; a single apostrophe followed by
/// a syntax character opens a quoted literal that runs to the next single apostrophe,
/// or to the end of the message where none follows, exactly as ICU behaves; any other
/// apostrophe is literal text.
///
/// Two places this parser is deliberately stricter than ICU's own, both because a
/// lenient parse would produce a message this app cannot expand correctly: every
/// selector must contain an 'other' branch, which ICU only enforces at format time,
/// and duplicate branch keys are rejected, which ICU tolerates but which would make
/// two segments share one branch path.
/// </remarks>
public sealed class IcuParser
{
    private readonly string _source;
    private int _position;

    private IcuParser(string source)
    {
        _source = source;
    }

    /// <summary>Parses a message, throwing <see cref="IcuParseException"/> with the offset on failure.</summary>
    public static IcuMessage Parse(string source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var parser = new IcuParser(source);
        var nodes = parser.ParseNodes(inPlural: false);

        // ParseNodes stops at a '}' it did not open. At the top level there is nothing
        // it could belong to.
        if (parser._position < source.Length)
        {
            throw new IcuParseException("Unmatched '}' in message text", parser._position);
        }

        return new IcuMessage(source, nodes);
    }

    /// <summary>Parses a message, reporting failure instead of throwing.</summary>
    public static bool TryParse(string source, out IcuMessage? message, out IcuParseException? error)
    {
        try
        {
            message = Parse(source);
            error = null;
            return true;
        }
        catch (IcuParseException exception)
        {
            message = null;
            error = exception;
            return false;
        }
    }

    /// <summary>
    /// Parses a node sequence until the end of the source or a '}' belonging to the
    /// caller, which is left unconsumed so the caller can check for it.
    /// </summary>
    private List<MessageNode> ParseNodes(bool inPlural)
    {
        var nodes = new List<MessageNode>();

        while (_position < _source.Length)
        {
            var c = _source[_position];

            if (c == '}')
            {
                break;
            }

            if (c == '{')
            {
                nodes.Add(ParseArgument(inPlural));
            }
            else if (c == '#' && inPlural)
            {
                nodes.Add(new PoundNode(new SourceSpan(_position, 1)));
                _position++;
            }
            else if (c == '\'' && IsQuoteStart(inPlural))
            {
                nodes.Add(ParseQuoted());
            }
            else
            {
                nodes.Add(ParseText(inPlural));
            }
        }

        return nodes;
    }

    /// <summary>
    /// Whether the apostrophe at the current position opens a quoted construct. True
    /// for a doubled apostrophe and for an apostrophe followed by a character that is
    /// syntax here; '#' only counts inside a plural branch, so '#' quoting outside one
    /// is not a quote at all.
    /// </summary>
    private bool IsQuoteStart(bool inPlural)
    {
        if (_position + 1 >= _source.Length)
        {
            return false;
        }

        var next = _source[_position + 1];
        return next == '\'' || next == '{' || next == '}' || (next == '#' && inPlural);
    }

    private QuotedTextNode ParseQuoted()
    {
        var start = _position;
        _position++;

        if (_source[_position] == '\'')
        {
            _position++;
            return new QuotedTextNode("'", new SourceSpan(start, 2));
        }

        var value = new StringBuilder();
        while (_position < _source.Length)
        {
            var c = _source[_position];
            if (c == '\'')
            {
                if (_position + 1 < _source.Length && _source[_position + 1] == '\'')
                {
                    value.Append('\'');
                    _position += 2;
                    continue;
                }

                _position++;
                return new QuotedTextNode(value.ToString(), SpanFrom(start));
            }

            value.Append(c);
            _position++;
        }

        // Unterminated: ICU runs the quote to the end of the message, and so do we.
        return new QuotedTextNode(value.ToString(), SpanFrom(start));
    }

    private TextNode ParseText(bool inPlural)
    {
        var start = _position;

        while (_position < _source.Length)
        {
            var c = _source[_position];
            if (c == '{' || c == '}')
            {
                break;
            }

            if (c == '#' && inPlural)
            {
                break;
            }

            if (c == '\'' && IsQuoteStart(inPlural))
            {
                break;
            }

            _position++;
        }

        var span = SpanFrom(start);
        return new TextNode(span.TextIn(_source), span);
    }

    private MessageNode ParseArgument(bool inPlural)
    {
        var start = _position;
        _position++;
        SkipWhitespace();

        var name = ReadIdentifier("an argument name");
        SkipWhitespace();

        if (TryConsume('}'))
        {
            return new ArgumentNode(name, SpanFrom(start));
        }

        Expect(',', "',' or '}' after the argument name");
        SkipWhitespace();

        var type = ReadIdentifier("an argument type");
        SkipWhitespace();

        return type switch
        {
            "plural" => ParseSelector(SelectorType.Plural, name, inPlural, start),
            "selectordinal" => ParseSelector(SelectorType.SelectOrdinal, name, inPlural, start),
            "select" => ParseSelector(SelectorType.Select, name, inPlural, start),
            _ => ParseTypedArgument(name, type, start)
        };
    }

    private TypedArgumentNode ParseTypedArgument(string name, string type, int start)
    {
        if (TryConsume('}'))
        {
            return new TypedArgumentNode(name, type, null, SpanFrom(start));
        }

        Expect(',', "',' or '}' after the argument type");
        SkipWhitespace();

        var style = ReadStyleText();
        Expect('}', "'}' after the argument style");
        return new TypedArgumentNode(name, type, style, SpanFrom(start));
    }

    /// <summary>
    /// Reads a style as raw text, up to the '}' that closes the argument. The style is
    /// preserved as written, never interpreted, but the scan still has to understand
    /// two things about it: number and date patterns can contain apostrophe-quoted
    /// sections ('#'## is a valid number pattern), and braces inside the style must
    /// balance before a '}' can close the argument.
    /// </summary>
    private string ReadStyleText()
    {
        var start = _position;
        var depth = 0;
        var quoted = false;

        while (_position < _source.Length)
        {
            var c = _source[_position];
            if (quoted)
            {
                if (c == '\'')
                {
                    quoted = false;
                }
            }
            else if (c == '\'')
            {
                quoted = true;
            }
            else if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                if (depth == 0)
                {
                    break;
                }

                depth--;
            }

            _position++;
        }

        var style = _source[start.._position].TrimEnd();
        if (style.Length == 0)
        {
            throw Error("Expected an argument style", start);
        }

        return style;
    }

    private SelectorNode ParseSelector(SelectorType type, string name, bool inPlural, int start)
    {
        Expect(',', $"',' after '{type.ToKeyword()}'");

        // '#' binds to the nearest enclosing plural or selectordinal. A select neither
        // starts nor ends that context: its branches inherit whatever surrounds it.
        var pluralContext = type != SelectorType.Select || inPlural;

        string? offsetRaw = null;
        var offset = 0m;
        var branches = new List<Branch>();

        while (true)
        {
            SkipWhitespace();
            if (TryConsume('}'))
            {
                break;
            }

            if (_position >= _source.Length)
            {
                throw Error("Unclosed selector: expected a branch or '}'", _position);
            }

            // ICU only permits the offset before the first branch, and only on the
            // plural kinds; anywhere else "offset" is just a branch keyword that will
            // fail on the ':' after it.
            if (branches.Count == 0 && offsetRaw is null && type != SelectorType.Select
                && string.CompareOrdinal(_source, _position, "offset:", 0, "offset:".Length) == 0)
            {
                _position += "offset:".Length;
                SkipWhitespace();
                (offsetRaw, offset) = ReadNumber("a number after 'offset:'");
                continue;
            }

            branches.Add(ParseBranch(type, pluralContext));
        }

        if (!branches.Any(branch => !branch.Key.IsExplicit
            && string.Equals(branch.Key.Text, "other", StringComparison.Ordinal)))
        {
            throw Error($"A '{type.ToKeyword()}' argument must contain an 'other' branch", start);
        }

        var duplicate = branches
            .GroupBy(branch => branch.Key.Text, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw Error($"Duplicate branch '{duplicate.Key}' in '{type.ToKeyword()}' argument", start);
        }

        return new SelectorNode(type, name, offsetRaw, offset, branches, SpanFrom(start));
    }

    private Branch ParseBranch(SelectorType type, bool pluralContext)
    {
        var start = _position;
        BranchKey key;

        if (_source[_position] == '=')
        {
            if (type == SelectorType.Select)
            {
                throw Error("'select' takes keyword branches only; explicit values such as '=0' belong to plural and selectordinal", _position);
            }

            _position++;
            var (raw, value) = ReadNumber("a number after '='");
            key = BranchKey.Explicit("=" + raw, value);
        }
        else
        {
            key = BranchKey.Keyword(ReadIdentifier("a branch selector"));
        }

        SkipWhitespace();
        Expect('{', "'{' opening the branch");
        var nodes = ParseNodes(pluralContext);
        Expect('}', "'}' closing the branch");

        return new Branch(key, nodes, SpanFrom(start));
    }

    private string ReadIdentifier(string what)
    {
        var start = _position;
        while (_position < _source.Length && !IsIdentifierTerminator(_source[_position]))
        {
            _position++;
        }

        if (_position == start)
        {
            throw Error($"Expected {what}", start);
        }

        return _source[start.._position];
    }

    /// <summary>
    /// Where an identifier stops: whitespace or a character that is syntax somewhere
    /// in the grammar. ':' is included so that "offset" reads as an identifier even
    /// where the offset check did not consume it, and '=' so that an explicit value
    /// never fuses with a keyword.
    /// </summary>
    private static bool IsIdentifierTerminator(char c) =>
        char.IsWhiteSpace(c) || c is '{' or '}' or ',' or '=' or '\'' or '#' or ':';

    private (string Raw, decimal Value) ReadNumber(string what)
    {
        var start = _position;

        if (_position < _source.Length && _source[_position] == '-')
        {
            _position++;
        }

        while (_position < _source.Length && Ascii.IsDigit(_source[_position]))
        {
            _position++;
        }

        if (_position < _source.Length && _source[_position] == '.')
        {
            _position++;
            while (_position < _source.Length && Ascii.IsDigit(_source[_position]))
            {
                _position++;
            }
        }

        var raw = _source[start.._position];
        if (!decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value))
        {
            throw Error($"Expected {what}", start);
        }

        return (raw, value);
    }

    private void SkipWhitespace()
    {
        while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
        {
            _position++;
        }
    }

    private bool TryConsume(char c)
    {
        if (_position < _source.Length && _source[_position] == c)
        {
            _position++;
            return true;
        }

        return false;
    }

    private void Expect(char c, string what)
    {
        if (!TryConsume(c))
        {
            throw Error($"Expected {what}", _position);
        }
    }

    private SourceSpan SpanFrom(int start) => new(start, _position - start);

    private IcuParseException Error(string description, int position) =>
        new(description, position);
}
