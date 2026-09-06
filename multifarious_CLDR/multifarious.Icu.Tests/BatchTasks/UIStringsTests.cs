using System.Reflection;
using System.Xml.Linq;
using multifarious.Icu.BatchTasks.Resources;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// The string resources and their generated accessor. The accessor is written by
/// tools/generate-uistrings.ps1, so an entry added to the resx and not regenerated, or a
/// property left behind after an entry was removed, fails here rather than showing a blank
/// label in Studio.
/// </summary>
public class UIStringsTests
{
    private static string CodeRoot()
    {
        // bin\Debug\net48 -> multifarious.Icu.Tests -> the code root.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "multifarious.Icu.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    private static string ResxPath() =>
        Path.Combine(CodeRoot(), "multifarious.Icu.BatchTasks", "Resources", "UIStrings.resx");

    private static SortedSet<string> ResxKeys() =>
        new(XDocument.Load(ResxPath()).Root!.Elements("data")
            .Select(e => e.Attribute("name")!.Value), StringComparer.Ordinal);

    private static SortedSet<string> AccessorKeys() =>
        new(typeof(UIStrings).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name), StringComparer.Ordinal);

    [Fact]
    public void The_accessor_and_the_resx_carry_the_same_keys()
    {
        Assert.Equal(ResxKeys(), AccessorKeys());
    }

    [Fact]
    public void Every_string_resolves_to_text()
    {
        // Proves the resx is embedded under the name the accessor asks for.
        foreach (var key in AccessorKeys())
        {
            Assert.False(string.IsNullOrWhiteSpace(UIStrings.Get(key)), $"'{key}' resolved to nothing");
        }
    }

    [Fact]
    public void Every_resx_entry_carries_a_translator_comment()
    {
        var withoutComment = XDocument.Load(ResxPath()).Root!.Elements("data")
            .Where(e => string.IsNullOrWhiteSpace(e.Element("comment")?.Value))
            .Select(e => e.Attribute("name")!.Value)
            .ToList();

        Assert.Empty(withoutComment);
    }
}
