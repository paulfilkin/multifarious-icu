namespace Icu.Core;

/// <summary>
/// A message cannot be hoisted. The one known cause is a '#' bound to a plural with a
/// non-zero offset that hoisting would push inside another plural's scope: '#' would
/// rebind there, and ICU has no way to write "this argument minus an offset" as a
/// plain argument. The caller passes the message through untouched and reports it,
/// the same contract as a parse failure.
/// </summary>
public sealed class IcuHoistException : Exception
{
    public IcuHoistException(string message)
        : base(message)
    {
    }
}
