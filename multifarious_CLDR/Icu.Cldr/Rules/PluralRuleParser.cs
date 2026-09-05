using System.Globalization;

namespace Icu.Cldr.Rules;

/// <summary>
/// Parses one CLDR plural rule string: a condition, then the samples.
/// </summary>
/// <remarks>
/// <para>
/// The grammar implemented is the modern subset of UTS #35 part 5, which is what the
/// CLDR 48 data actually uses: the operands <c>n i v w f t c e</c>, the operators
/// <c>=</c>, <c>!=</c> and <c>%</c>, integer values and <c>..</c> ranges, and the
/// connectives <c>and</c> and <c>or</c>.
/// </para>
/// <para>
/// The deprecated <c>is</c>, <c>in</c>, <c>within</c>, <c>mod</c> and <c>not</c> forms
/// appear nowhere in the pinned data and are rejected rather than implemented. They do
/// not mean quite the same thing as the modern operators, and supporting a form nothing
/// exercises would be untested code with a subtly wrong reading waiting in it.
/// </para>
/// </remarks>
public static class PluralRuleParser
{
    /// <summary>
    /// Splits a rule string into its condition and its samples and parses both.
    /// </summary>
    public static (PluralCondition Condition, PluralSamples Samples) Parse(string ruleText)
    {
        if (ruleText is null) throw new ArgumentNullException(nameof(ruleText));

        var samplesStart = FindSamplesStart(ruleText);
        var conditionText = samplesStart < 0 ? ruleText : ruleText[..samplesStart];
        var samplesText = samplesStart < 0 ? string.Empty : ruleText[samplesStart..];

        return (ParseCondition(conditionText), ParseSamples(samplesText));
    }

    /// <summary>
    /// Parses the condition. An empty or whitespace-only condition is always true, which
    /// is how a locale's <c>other</c> rule is written.
    /// </summary>
    public static PluralCondition ParseCondition(string conditionText)
    {
        if (conditionText is null) throw new ArgumentNullException(nameof(conditionText));

        var scanner = new Scanner(conditionText);
        scanner.SkipWhitespace();
        if (scanner.AtEnd)
        {
            return PluralCondition.AlwaysTrue;
        }

        var clauses = new List<IReadOnlyList<PluralRelation>>();
        while (true)
        {
            clauses.Add(ParseAndClause(ref scanner));

            scanner.SkipWhitespace();
            if (scanner.AtEnd)
            {
                break;
            }

            var word = scanner.ReadWord();
            if (word != "or")
            {
                throw Unexpected(conditionText, word);
            }
        }

        return new PluralCondition(clauses);
    }

    /// <summary>Parses the sample section, which may name integers, decimals or both.</summary>
    public static PluralSamples ParseSamples(string samplesText)
    {
        if (samplesText is null) throw new ArgumentNullException(nameof(samplesText));

        if (samplesText.Length == 0)
        {
            return PluralSamples.Empty;
        }

        var integers = PluralSampleList.Empty;
        var decimals = PluralSampleList.Empty;

        var integerStart = samplesText.IndexOf("@integer", StringComparison.Ordinal);
        var decimalStart = samplesText.IndexOf("@decimal", StringComparison.Ordinal);

        if (integerStart >= 0)
        {
            var end = decimalStart > integerStart ? decimalStart : samplesText.Length;
            integers = ParseSampleList(samplesText[(integerStart + "@integer".Length)..end]);
        }

        if (decimalStart >= 0)
        {
            var end = integerStart > decimalStart ? integerStart : samplesText.Length;
            decimals = ParseSampleList(samplesText[(decimalStart + "@decimal".Length)..end]);
        }

        return new PluralSamples(integers, decimals);
    }

    private static PluralSampleList ParseSampleList(string text)
    {
        var ranges = new List<PluralSampleRange>();
        var openEnded = false;

        foreach (var rawItem in text.Split(','))
        {
            var item = rawItem.Trim();
            if (item.Length == 0)
            {
                continue;
            }

            if (item is "…" or "...")
            {
                openEnded = true;
                continue;
            }

            var tilde = item.IndexOf('~');
            ranges.Add(tilde < 0
                ? new PluralSampleRange(item, item)
                : new PluralSampleRange(item[..tilde].Trim(), item[(tilde + 1)..].Trim()));
        }

        return ranges.Count == 0 && !openEnded ? PluralSampleList.Empty : new PluralSampleList(ranges, openEnded);
    }

    private static IReadOnlyList<PluralRelation> ParseAndClause(ref Scanner scanner)
    {
        var relations = new List<PluralRelation> { ParseRelation(ref scanner) };

        while (true)
        {
            var save = scanner;
            scanner.SkipWhitespace();
            if (scanner.AtEnd)
            {
                return relations;
            }

            if (!scanner.PeekIsLetter)
            {
                throw Unexpected(scanner.Text, scanner.Rest);
            }

            var word = scanner.ReadWord();
            if (word == "and")
            {
                relations.Add(ParseRelation(ref scanner));
                continue;
            }

            // 'or' belongs to the caller, so hand the scanner back untouched.
            scanner = save;
            return relations;
        }
    }

    private static PluralRelation ParseRelation(ref Scanner scanner)
    {
        scanner.SkipWhitespace();
        var operand = ParseOperand(ref scanner);

        long modulus = 0;
        scanner.SkipWhitespace();
        if (scanner.TryConsume('%'))
        {
            scanner.SkipWhitespace();
            modulus = scanner.ReadInteger();
            if (modulus == 0)
            {
                throw new CldrDataException($"Rule '{scanner.Text}' has a modulus of zero.");
            }

            scanner.SkipWhitespace();
        }

        bool negated;
        if (scanner.TryConsume('!'))
        {
            if (!scanner.TryConsume('='))
            {
                throw Unexpected(scanner.Text, scanner.Rest);
            }

            negated = true;
        }
        else if (scanner.TryConsume('='))
        {
            negated = false;
        }
        else
        {
            throw Unexpected(scanner.Text, scanner.Rest);
        }

        return new PluralRelation(operand, modulus, negated, ParseRangeList(ref scanner));
    }

    private static IReadOnlyList<NumberRange> ParseRangeList(ref Scanner scanner)
    {
        var ranges = new List<NumberRange>();
        while (true)
        {
            scanner.SkipWhitespace();
            var low = scanner.ReadInteger();
            var high = low;

            if (scanner.TryConsume('.'))
            {
                if (!scanner.TryConsume('.'))
                {
                    throw Unexpected(scanner.Text, scanner.Rest);
                }

                high = scanner.ReadInteger();
                if (high < low)
                {
                    throw new CldrDataException($"Rule '{scanner.Text}' has the range {low}..{high} the wrong way round.");
                }
            }

            ranges.Add(new NumberRange(low, high));

            scanner.SkipWhitespace();
            if (!scanner.TryConsume(','))
            {
                return ranges;
            }
        }
    }

    private static PluralOperand ParseOperand(ref Scanner scanner)
    {
        if (!scanner.PeekIsLetter)
        {
            throw Unexpected(scanner.Text, scanner.Rest);
        }

        var word = scanner.ReadWord();
        return word switch
        {
            "n" => PluralOperand.N,
            "i" => PluralOperand.I,
            "v" => PluralOperand.V,
            "w" => PluralOperand.W,
            "f" => PluralOperand.F,
            "t" => PluralOperand.T,

            // UTS #35 makes e a synonym of c and reserves the right to redefine it. CLDR 48
            // writes e; both are accepted and both mean the compact decimal exponent.
            "c" or "e" => PluralOperand.C,

            "is" or "in" or "within" or "mod" or "not" => throw new CldrDataException(
                $"Rule '{scanner.Text}' uses the deprecated '{word}' form, which this parser does not " +
                "implement. The pinned CLDR 48 data uses only '=', '!=' and '%'."),

            _ => throw Unexpected(scanner.Text, word)
        };
    }

    /// <summary>
    /// Finds where the samples begin. Splitting on the first <c>@</c> would be enough for
    /// the current data, but naming the two markers makes it obvious what is being looked
    /// for and fails clearly if some other annotation is ever added.
    /// </summary>
    private static int FindSamplesStart(string ruleText)
    {
        var integerStart = ruleText.IndexOf("@integer", StringComparison.Ordinal);
        var decimalStart = ruleText.IndexOf("@decimal", StringComparison.Ordinal);

        if (integerStart < 0)
        {
            return decimalStart;
        }

        return decimalStart < 0 ? integerStart : Math.Min(integerStart, decimalStart);
    }

    private static CldrDataException Unexpected(string ruleText, string what) =>
        new($"Rule '{ruleText}' is not valid UTS #35 plural rule syntax: unexpected '{what}'.");

    /// <summary>
    /// A cursor over the condition text. A struct passed by reference so the parse costs
    /// no allocations beyond the nodes it produces.
    /// </summary>
    private struct Scanner(string text)
    {
        private int _position = 0;

        public string Text { get; } = text;

        public readonly bool AtEnd => _position >= Text.Length;

        public readonly string Rest => AtEnd ? "<end of rule>" : Text[_position..];

        public readonly bool PeekIsLetter => !AtEnd && Ascii.IsLetter(Text[_position]);

        public void SkipWhitespace()
        {
            while (_position < Text.Length && char.IsWhiteSpace(Text[_position]))
            {
                _position++;
            }
        }

        public bool TryConsume(char expected)
        {
            if (AtEnd || Text[_position] != expected)
            {
                return false;
            }

            _position++;
            return true;
        }

        public string ReadWord()
        {
            var start = _position;
            while (_position < Text.Length && Ascii.IsLetter(Text[_position]))
            {
                _position++;
            }

            if (start == _position)
            {
                throw Unexpected(Text, Rest);
            }

            return Text[start.._position];
        }

        public long ReadInteger()
        {
            var start = _position;
            while (_position < Text.Length && Ascii.IsDigit(Text[_position]))
            {
                _position++;
            }

            if (start == _position ||
                !long.TryParse(Text[start.._position], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                throw Unexpected(Text, Rest);
            }

            return value;
        }
    }
}
