namespace Icu.Cldr.Rules;

/// <summary>
/// The operands a plural rule may test, as defined by UTS #35 part 5.
/// </summary>
public enum PluralOperand
{
    /// <summary>The absolute value of the source number.</summary>
    N,

    /// <summary>The integer digits of the source number.</summary>
    I,

    /// <summary>The count of visible fraction digits, trailing zeros included.</summary>
    V,

    /// <summary>The count of visible fraction digits, trailing zeros excluded.</summary>
    W,

    /// <summary>The visible fraction digits as an integer, trailing zeros included.</summary>
    F,

    /// <summary>The visible fraction digits as an integer, trailing zeros excluded.</summary>
    T,

    /// <summary>
    /// The compact decimal exponent. <c>e</c> is a synonym that UTS #35 reserves the
    /// right to redefine; the CLDR 48 rule strings use <c>e</c> and never <c>c</c>, and
    /// both parse to this operand.
    /// </summary>
    C
}
