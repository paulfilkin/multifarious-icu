using System.Reflection;

namespace Icu.Cldr;

/// <summary>
/// The shipped grammar dictionary, <c>Data\grammar-dictionary.json</c>, as text. The
/// gender sets in it are CLDR's grammatical features data for the pinned release, which
/// is why the file lives beside the plural tables; the model that reads it belongs to the
/// expansion layer, which owns what a selector kind means.
/// </summary>
public static class GrammarDictionaryResource
{
    public const string ResourceName = "Icu.Cldr.Data.grammar-dictionary.json";

    public static string ReadJson()
    {
        using var stream = typeof(GrammarDictionaryResource).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new CldrDataException($"The embedded resource '{ResourceName}' is missing from the assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
