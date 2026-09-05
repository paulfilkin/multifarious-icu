namespace Icu.Cldr;

/// <summary>
/// Stands in for System.HashCode.Combine, which .NET Framework 4.8 does not have. The
/// operands are plural rule operands, so the quality of the mix matters less than
/// having one; this is the usual multiply-and-add.
/// </summary>
internal static class HashCodes
{
    public static int Combine<T1, T2, T3, T4, T5, T6, T7>(T1 a, T2 b, T3 c, T4 d, T5 e, T6 f, T7 g)
        where T1 : notnull where T2 : notnull where T3 : notnull where T4 : notnull
        where T5 : notnull where T6 : notnull where T7 : notnull
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + a.GetHashCode();
            hash = hash * 31 + b.GetHashCode();
            hash = hash * 31 + c.GetHashCode();
            hash = hash * 31 + d.GetHashCode();
            hash = hash * 31 + e.GetHashCode();
            hash = hash * 31 + f.GetHashCode();
            hash = hash * 31 + g.GetHashCode();
            return hash;
        }
    }
}
