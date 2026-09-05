namespace Icu.Core.Tree;

/// <summary>
/// The key of one branch: a keyword such as <c>one</c> or <c>female</c>, or an
/// explicit value such as <c>=0</c>. The two are different things. Explicit values are
/// exact matches on the raw number, never categories: expansion never duplicates them
/// and finalise never prunes them, while keyword branches evaluate on the number minus
/// the selector's offset.
/// </summary>
public sealed class BranchKey : IEquatable<BranchKey>
{
    private BranchKey(string text, decimal? explicitValue)
    {
        Text = text;
        ExplicitValue = explicitValue;
    }

    public static BranchKey Keyword(string keyword)
    {
        if (keyword is null) throw new ArgumentNullException(nameof(keyword));
        return new BranchKey(keyword, null);
    }

    public static BranchKey Explicit(string raw, decimal value)
    {
        if (raw is null) throw new ArgumentNullException(nameof(raw));
        return new BranchKey(raw, value);
    }

    /// <summary>
    /// The key as written, <c>few</c> or <c>=0</c>. Branch paths are built from this,
    /// which is why the raw form is kept rather than only the parsed value.
    /// </summary>
    public string Text { get; }

    public bool IsExplicit => ExplicitValue is not null;

    /// <summary>The exact number an explicit branch matches, or null for a keyword.</summary>
    public decimal? ExplicitValue { get; }

    public bool Equals(BranchKey? other) =>
        other is not null && string.Equals(Text, other.Text, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as BranchKey);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Text);

    public override string ToString() => Text;
}
