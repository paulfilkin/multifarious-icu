using Icu.Cldr;
using Icu.Core.Tree;

namespace Icu.Core.Tests;

/// <summary>
/// Wires the renderer's injected category decision to the real CLDR layer, the way
/// the app will: plural resolves against the cardinal table, selectordinal against
/// the ordinal one, per locale.
/// </summary>
internal static class CldrCategories
{
    public static Func<SelectorType, string, string> For(string languageTag) =>
        (type, number) => CldrPlurals.Default.Select(
                languageTag,
                type == SelectorType.SelectOrdinal ? SelectorKind.Ordinal : SelectorKind.Cardinal,
                number)
            .ToKeyword();
}
