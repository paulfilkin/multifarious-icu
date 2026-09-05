using FsCheck.Xunit;
using Icu.Core;
using Icu.Core.Tree;

namespace Icu.Core.Tests;

/// <summary>
/// The T1 hoisting properties of section 14.2: hoisting is idempotent, preserves the
/// argument set, and preserves rendering for every count, checked against real CLDR
/// category decisions across locales with one to six categories. A generated tree the
/// hoister refuses (a '#' bound through a non-zero offset that would cross scope) is
/// skipped: refusal is the specified behaviour and has its own directed test.
/// </summary>
public class HoistingPropertyTests
{
    private const int MaxDepth = 3;

    private static readonly string[] Counts =
        ["0", "1", "2", "3", "5", "11", "21", "100", "0.5", "1.5"];

    private static readonly string[] Locales = ["en", "ru", "ar", "cy", "ja"];

    [Property(MaxTest = 500)]
    public void HoistingIsIdempotent(int seed)
    {
        var nodes = MessageGenerator.GenerateNodes(new Random(seed), MaxDepth, inPlural: false);
        if (!TryHoist(nodes, out var once))
        {
            return;
        }

        var twice = Hoister.Hoist(once);

        Assert.True(MessageComparer.StructurallyEqual(once, twice),
            $"Hoisting a second time changed the tree for seed {seed}.");
    }

    [Property(MaxTest = 500)]
    public void HoistingPreservesTheArgumentSet(int seed)
    {
        var nodes = MessageGenerator.GenerateNodes(new Random(seed), MaxDepth, inPlural: false);
        if (!TryHoist(nodes, out var hoisted))
        {
            return;
        }

        Assert.Equal(ArgumentNamesOf(nodes), ArgumentNamesOf(hoisted));
    }

    [Property(MaxTest = 300)]
    public void HoistingPreservesRendering(int seed)
    {
        var nodes = MessageGenerator.GenerateNodes(new Random(seed), MaxDepth, inPlural: false);
        if (!TryHoist(nodes, out var hoisted))
        {
            return;
        }

        var names = ArgumentNamesOf(nodes);
        foreach (var count in Counts)
        {
            var arguments = names.ToDictionary(name => name, _ => count, StringComparer.Ordinal);
            foreach (var locale in Locales)
            {
                var categories = CldrCategories.For(locale);
                Assert.Equal(
                    MessageRenderer.Render(nodes, arguments, categories),
                    MessageRenderer.Render(hoisted, arguments, categories));
            }
        }
    }

    private static bool TryHoist(IReadOnlyList<MessageNode> nodes, out IReadOnlyList<MessageNode> hoisted)
    {
        try
        {
            hoisted = Hoister.Hoist(nodes);
            return true;
        }
        catch (IcuHoistException)
        {
            hoisted = [];
            return false;
        }
    }

    private static HashSet<string> ArgumentNamesOf(IReadOnlyList<MessageNode> nodes)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        Walk(nodes);
        return names;

        void Walk(IReadOnlyList<MessageNode> list)
        {
            foreach (var node in list)
            {
                switch (node)
                {
                    case ArgumentNode argument:
                        names.Add(argument.Name);
                        break;
                    case TypedArgumentNode typed:
                        names.Add(typed.Name);
                        break;
                    case SelectorNode selector:
                        names.Add(selector.ArgumentName);
                        foreach (var branch in selector.Branches)
                        {
                            Walk(branch.Nodes);
                        }

                        break;
                }
            }
        }
    }
}
