using System.Globalization;

namespace Icu.Cldr.Rules;

/// <summary>
/// One entry in a sample list: either a single value such as <c>7</c>, or an inclusive
/// range such as <c>2~4</c> or <c>0.0~1.5</c>.
/// </summary>
/// <remarks>
/// The values are held as they were written, not as numbers. <c>1</c> and <c>1.0</c>
/// select different categories in several languages, and the difference is carried by
/// the written form alone.
/// </remarks>
public sealed class PluralSampleRange
{
    /// <summary>
    /// No range in CLDR 48 spans more than sixteen values. The cap exists so that a
    /// future release which changed that fails here, loudly, rather than expanding into
    /// something enormous inside a comment or a test.
    /// </summary>
    private const int MaximumExpansion = 1000;

    public PluralSampleRange(string start, string end)
    {
        Start = start;
        End = end;
        DecimalPlaces = CountDecimalPlaces(start);

        if (CountDecimalPlaces(end) != DecimalPlaces)
        {
            throw new CldrDataException(
                $"Sample range '{start}~{end}' has a different number of decimal places at each end, " +
                "so the step between its values is undefined.");
        }
    }

    public string Start { get; }

    public string End { get; }

    /// <summary>
    /// The number of decimal places both endpoints are written to, which is also what
    /// sets the step: <c>0.0~1.5</c> steps by 0.1, <c>2~4</c> steps by 1.
    /// </summary>
    public int DecimalPlaces { get; }

    public bool IsSingleValue => Start == End;

    /// <summary>Every value in the range, written the way CLDR writes its samples.</summary>
    public IEnumerable<string> Expand()
    {
        if (IsSingleValue)
        {
            yield return Start;
            yield break;
        }

        var step = DecimalPlaces == 0 ? 1m : 1m / Pow10(DecimalPlaces);
        var start = ParseEndpoint(Start);
        var end = ParseEndpoint(End);
        var format = DecimalPlaces == 0 ? "0" : "0." + new string('0', DecimalPlaces);

        var produced = 0;
        for (var value = start; value <= end; value += step)
        {
            if (++produced > MaximumExpansion)
            {
                throw new CldrDataException(
                    $"Sample range '{Start}~{End}' expands to more than {MaximumExpansion} values.");
            }

            yield return value.ToString(format, CultureInfo.InvariantCulture);
        }
    }

    public override string ToString() => IsSingleValue ? Start : $"{Start}~{End}";

    private decimal ParseEndpoint(string text)
    {
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            throw new CldrDataException(
                $"Sample range endpoint '{text}' is not a plain decimal. Compact exponents are not " +
                "used in range endpoints in CLDR 48 and are not supported here.");
        }

        return value;
    }

    private static decimal Pow10(int places)
    {
        var result = 1m;
        for (var index = 0; index < places; index++)
        {
            result *= 10m;
        }

        return result;
    }

    private static int CountDecimalPlaces(string text)
    {
        var point = text.IndexOf('.');
        return point < 0 ? 0 : text.Length - point - 1;
    }
}

/// <summary>
/// The samples given after one of <c>@integer</c> or <c>@decimal</c>.
/// </summary>
public sealed class PluralSampleList
{
    public static readonly PluralSampleList Empty = new(Array.Empty<PluralSampleRange>(), false);

    public PluralSampleList(IReadOnlyList<PluralSampleRange> ranges, bool isOpenEnded)
    {
        Ranges = ranges;
        IsOpenEnded = isOpenEnded;
    }

    public IReadOnlyList<PluralSampleRange> Ranges { get; }

    /// <summary>
    /// True when the list ended with an ellipsis, meaning the values shown are examples
    /// and not the whole set. Every non-empty list in CLDR 48 is open-ended.
    /// </summary>
    public bool IsOpenEnded { get; }

    public bool IsEmpty => Ranges.Count == 0;

    /// <summary>Every listed value, in document order, ranges expanded.</summary>
    public IEnumerable<string> Expand() => Ranges.SelectMany(range => range.Expand());

    public override string ToString() =>
        string.Join(", ", Ranges.Select(r => r.ToString()).Concat(IsOpenEnded ? new[] { "…" } : Array.Empty<string>()));
}

/// <summary>
/// The sample values CLDR publishes with a rule, which are what the comment on an
/// expanded segment shows the translator and what the conformance suite runs on.
/// </summary>
public sealed class PluralSamples
{
    public static readonly PluralSamples Empty = new(PluralSampleList.Empty, PluralSampleList.Empty);

    public PluralSamples(PluralSampleList integers, PluralSampleList decimals)
    {
        Integers = integers;
        Decimals = decimals;
    }

    /// <summary>The values listed after <c>@integer</c>.</summary>
    public PluralSampleList Integers { get; }

    /// <summary>The values listed after <c>@decimal</c>.</summary>
    public PluralSampleList Decimals { get; }

    /// <summary>
    /// True where the category is reachable only by a number with visible fraction
    /// digits. Russian <c>other</c> is the case the design calls out: it has no integer
    /// samples at all, and a comment that showed nothing there would be worse than one
    /// that says why.
    /// </summary>
    public bool IsFractionalOnly => Integers.IsEmpty && !Decimals.IsEmpty;

    /// <summary>Every sample, integers first, as written.</summary>
    public IEnumerable<string> Expand() => Integers.Expand().Concat(Decimals.Expand());

    /// <summary>
    /// The first <paramref name="count"/> integer samples, for the comment payload.
    /// </summary>
    /// <remarks>
    /// Taken in document order rather than by any cleverer selection. CLDR lists the
    /// small values first and then reaches upwards, so the first few are the ones a
    /// translator recognises. The design document's worked example shows a hand-picked
    /// list instead; that difference is deliberate and recorded, because a stated rule
    /// beats an unreproducible one.
    /// </remarks>
    public IReadOnlyList<string> TakeIntegerExamples(int count) => Integers.Expand().Take(count).ToList();

    /// <summary>The first <paramref name="count"/> decimal samples, for the comment payload.</summary>
    public IReadOnlyList<string> TakeDecimalExamples(int count) => Decimals.Expand().Take(count).ToList();
}
