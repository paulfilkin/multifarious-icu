using Icu.Core.Tree;

namespace Icu.Core.Tests;

/// <summary>
/// Builds random well-formed message trees for the round-trip properties, covering
/// nesting, offsets, explicit values, all three selector types, '#' in and out of
/// plural context, and text containing every character the escaping rules exist for.
/// FsCheck supplies the seed and the iteration count; the builder itself is a plain
/// seeded generator, because useful shrinking over trees needs custom shrinkers and a
/// seed already reproduces a failure exactly.
/// </summary>
internal static class MessageGenerator
{
    private static readonly string[] ArgumentNames = ["count", "name", "n", "total", "x0"];
    private static readonly string[] PluralKeywords = ["zero", "one", "two", "few", "many"];
    private static readonly string[] SelectKeywords = ["male", "female", "red", "blue"];
    private static readonly (string Raw, decimal Value)[] ExplicitKeys =
        [("=0", 0m), ("=1", 1m), ("=2", 2m), ("=0.5", 0.5m)];
    private static readonly string[] ArgumentTypes = ["number", "date", "time"];
    private static readonly string?[] Styles = [null, "short", "full", "::currency/EUR", "'#'##", "a{b}c"];

    // Deliberately hostile alphabet: apostrophes, braces, '#', and the ordinary
    // punctuation they have to coexist with.
    private const string TextAlphabet = "ab c'#{}.,=!";

    public static IReadOnlyList<MessageNode> GenerateNodes(Random random, int depth, bool inPlural)
    {
        var count = random.Next(0, 5);
        var nodes = new List<MessageNode>(count);
        for (var i = 0; i < count; i++)
        {
            nodes.Add(GenerateNode(random, depth, inPlural));
        }

        return nodes;
    }

    private static MessageNode GenerateNode(Random random, int depth, bool inPlural)
    {
        var roll = random.Next(depth > 0 ? 10 : 7);
        return roll switch
        {
            <= 3 => GenerateText(random),
            4 => new ArgumentNode(Pick(random, ArgumentNames), default),
            5 => GenerateTypedArgument(random),
            6 => inPlural ? new PoundNode(default) : GenerateText(random),
            _ => GenerateSelector(random, depth, inPlural)
        };
    }

    private static TextNode GenerateText(Random random)
    {
        var length = random.Next(1, 9);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = TextAlphabet[random.Next(TextAlphabet.Length)];
        }

        return new TextNode(new string(chars), default);
    }

    private static TypedArgumentNode GenerateTypedArgument(Random random) =>
        new(Pick(random, ArgumentNames), Pick(random, ArgumentTypes), Pick(random, Styles), default);

    private static SelectorNode GenerateSelector(Random random, int depth, bool inPlural)
    {
        var type = (SelectorType)random.Next(3);
        var name = Pick(random, ArgumentNames);
        var childContext = type != SelectorType.Select || inPlural;

        string? offsetRaw = null;
        var offset = 0m;
        if (type != SelectorType.Select && random.Next(3) == 0)
        {
            offset = random.Next(0, 3);
            offsetRaw = offset.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var branches = new List<Branch>();

        if (type != SelectorType.Select)
        {
            foreach (var (raw, value) in PickDistinct(random, ExplicitKeys, random.Next(0, 3)))
            {
                branches.Add(GenerateBranch(random, BranchKey.Explicit(raw, value), depth, childContext));
            }
        }

        var keywords = type == SelectorType.Select ? SelectKeywords : PluralKeywords;
        foreach (var keyword in PickDistinct(random, keywords, random.Next(0, 4)))
        {
            branches.Add(GenerateBranch(random, BranchKey.Keyword(keyword), depth, childContext));
        }

        // 'other' is mandatory and the parser enforces it; its position is not, so it
        // lands anywhere to prove order does not matter to the round trip.
        branches.Insert(
            random.Next(branches.Count + 1),
            GenerateBranch(random, BranchKey.Keyword("other"), depth, childContext));

        return new SelectorNode(type, name, offsetRaw, offset, branches, default);
    }

    private static Branch GenerateBranch(Random random, BranchKey key, int depth, bool childContext) =>
        new(key, GenerateNodes(random, depth - 1, childContext), default);

    private static T Pick<T>(Random random, IReadOnlyList<T> pool) => pool[random.Next(pool.Count)];

    private static List<T> PickDistinct<T>(Random random, IReadOnlyList<T> pool, int count)
    {
        var shuffled = pool.ToList();
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled.Take(count).ToList();
    }
}
