namespace multifarious.Icu.Expansion;

/// <summary>What restore does when a required branch has no translation (design 8.6).</summary>
public enum EmptyBranchBehaviour
{
    UseSource,
    FailTask
}

/// <summary>What restore does when source and target placeholders differ (design 8.6).</summary>
public enum PlaceholderMismatchBehaviour
{
    Warn,
    FailTask
}

/// <summary>The restore task's options, with the design's defaults.</summary>
public sealed record RestoreOptions
{
    public static RestoreOptions Default { get; } = new();

    public EmptyBranchBehaviour OnEmptyBranch { get; init; } = EmptyBranchBehaviour.UseSource;

    public PlaceholderMismatchBehaviour OnPlaceholderMismatch { get; init; } = PlaceholderMismatchBehaviour.Warn;
}
