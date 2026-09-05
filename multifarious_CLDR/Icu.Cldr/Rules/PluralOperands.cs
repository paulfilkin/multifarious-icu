using System.Globalization;

namespace Icu.Cldr.Rules;

/// <summary>
/// The UTS #35 plural operands for one number, derived from how that number is written
/// rather than from its value.
/// </summary>
/// <remarks>
/// <para>
/// The distinction matters and is the reason this type exists at all. <c>1</c> and
/// <c>1.0</c> are the same value but different operands, and plural rules test the
/// difference: Russian reaches its <c>other</c> category only through numbers with
/// visible fraction digits, so <c>1</c> is <c>one</c> and <c>1.0</c> is <c>other</c>.
/// Anything that hands the evaluator a <see cref="double"/> has already thrown that
/// information away.
/// </para>
/// <para>
/// A compact decimal exponent is applied before the operands are read, so <c>1.2c6</c>
/// has the operands of 1200000 and additionally reports <c>c</c> as 6.
/// </para>
/// </remarks>
public readonly struct PluralOperands : IEquatable<PluralOperands>
{
    private PluralOperands(decimal n, long i, int v, int w, long f, long t, int c)
    {
        N = n;
        I = i;
        V = v;
        W = w;
        F = f;
        T = t;
        C = c;
        Source = null;
    }

    private PluralOperands(decimal n, long i, int v, int w, long f, long t, int c, string source)
        : this(n, i, v, w, f, t, c)
    {
        Source = source;
    }

    /// <summary>The absolute value of the number.</summary>
    public decimal N { get; }

    /// <summary>The integer digits.</summary>
    public long I { get; }

    /// <summary>The count of visible fraction digits, trailing zeros included.</summary>
    public int V { get; }

    /// <summary>The count of visible fraction digits, trailing zeros excluded.</summary>
    public int W { get; }

    /// <summary>The visible fraction digits as an integer, trailing zeros included.</summary>
    public long F { get; }

    /// <summary>The visible fraction digits as an integer, trailing zeros excluded.</summary>
    public long T { get; }

    /// <summary>The compact decimal exponent, zero when the number was not written compactly.</summary>
    public int C { get; }

    /// <summary>
    /// The literal the operands were parsed from, where they came from a string. Kept for
    /// diagnostics and for the failure message of a conformance assertion, which needs to
    /// name the sample rather than a reconstructed value.
    /// </summary>
    public string? Source { get; }

    /// <summary>Reads one operand as a number, for the evaluator.</summary>
    public decimal Get(PluralOperand operand) => operand switch
    {
        PluralOperand.N => N,
        PluralOperand.I => I,
        PluralOperand.V => V,
        PluralOperand.W => W,
        PluralOperand.F => F,
        PluralOperand.T => T,
        PluralOperand.C => C,
        _ => throw new ArgumentOutOfRangeException(nameof(operand), operand, "Not a plural operand.")
    };

    /// <summary>
    /// Operands for an integer written without a decimal point, which is the common case
    /// for an explicit value branch or a count typed into the preview.
    /// </summary>
    public static PluralOperands FromInteger(long value)
    {
        var magnitude = value == long.MinValue ? long.MaxValue : Math.Abs(value);
        return new PluralOperands(magnitude, magnitude, 0, 0, 0, 0, 0);
    }

    /// <summary>
    /// Parses a number as written: optional sign, digits, an optional fraction, and an
    /// optional compact exponent introduced by <c>c</c> or <c>e</c>.
    /// </summary>
    /// <exception cref="FormatException">The literal is not a number CLDR can describe.</exception>
    public static PluralOperands Parse(string source)
    {
        if (!TryParse(source, out var operands))
        {
            throw new FormatException($"'{source}' is not a number in the form CLDR plural samples use.");
        }

        return operands;
    }

    /// <summary>
    /// Parses a number as written. Returns false rather than throwing for anything
    /// malformed, and for values too large for the operand types, which matters because
    /// the preview's count box takes whatever a translator types.
    /// </summary>
    public static bool TryParse(string? source, out PluralOperands operands)
    {
        operands = default;
        if (source is null || source.Trim().Length == 0)
        {
            return false;
        }

        var text = source.Trim();

        // The sign is dropped rather than rejected: n is defined as the absolute value,
        // so -3 and 3 select the same category.
        if (text.Length > 0 && (text[0] == '-' || text[0] == '+'))
        {
            text = text[1..];
        }

        var integerDigits = string.Empty;
        var fractionDigits = string.Empty;
        var exponent = 0;

        var position = 0;
        var start = position;
        while (position < text.Length && Ascii.IsDigit(text[position]))
        {
            position++;
        }

        if (position == start)
        {
            return false;
        }

        integerDigits = text[start..position];

        if (position < text.Length && text[position] == '.')
        {
            position++;
            start = position;
            while (position < text.Length && Ascii.IsDigit(text[position]))
            {
                position++;
            }

            if (position == start)
            {
                return false;
            }

            fractionDigits = text[start..position];
        }

        if (position < text.Length && (text[position] == 'c' || text[position] == 'e' ||
                                       text[position] == 'C' || text[position] == 'E'))
        {
            position++;
            start = position;
            while (position < text.Length && Ascii.IsDigit(text[position]))
            {
                position++;
            }

            if (position == start || !int.TryParse(text[start..position], NumberStyles.None,
                                                   CultureInfo.InvariantCulture, out exponent))
            {
                return false;
            }

            // An exponent large enough to matter here would overflow every operand type
            // anyway. CLDR's own samples go no higher than c6.
            if (exponent > 28)
            {
                return false;
            }
        }

        if (position != text.Length)
        {
            return false;
        }

        // Applying the exponent moves the decimal point right, which is what makes 1.2c6
        // report v = 0: the fraction digit is consumed into the integer part rather than
        // remaining visible.
        if (exponent > 0)
        {
            var shift = Math.Min(exponent, fractionDigits.Length);
            integerDigits += fractionDigits[..shift];
            fractionDigits = fractionDigits[shift..];
            integerDigits += new string('0', exponent - shift);
        }

        var withoutTrailingZeros = fractionDigits.TrimEnd('0');

        if (!long.TryParse(integerDigits, NumberStyles.None, CultureInfo.InvariantCulture, out var i))
        {
            return false;
        }

        if (!TryParseFraction(fractionDigits, out var f) ||
            !TryParseFraction(withoutTrailingZeros, out var t))
        {
            return false;
        }

        var literal = fractionDigits.Length == 0 ? integerDigits : integerDigits + "." + fractionDigits;
        if (!decimal.TryParse(literal, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n))
        {
            return false;
        }

        operands = new PluralOperands(
            n, i, fractionDigits.Length, withoutTrailingZeros.Length, f, t, exponent, source);
        return true;
    }

    private static bool TryParseFraction(string digits, out long value)
    {
        if (digits.Length == 0)
        {
            value = 0;
            return true;
        }

        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    public bool Equals(PluralOperands other) =>
        N == other.N && I == other.I && V == other.V && W == other.W &&
        F == other.F && T == other.T && C == other.C;

    public override bool Equals(object? obj) => obj is PluralOperands other && Equals(other);

    public override int GetHashCode() => HashCodes.Combine(N, I, V, W, F, T, C);

    public override string ToString() =>
        Source ?? FormattableString.Invariant($"n={N} i={I} v={V} w={W} f={F} t={T} c={C}");
}
