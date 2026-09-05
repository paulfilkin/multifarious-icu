namespace multifarious.Icu.Expansion;

/// <summary>What finalise does when a required branch has no translation (design 8.6).</summary>
public enum EmptyBranchBehaviour
{
    UseSource,
    FailTask
}

/// <summary>What finalise does when source and target placeholders differ (design 8.6).</summary>
public enum PlaceholderMismatchBehaviour
{
    Warn,
    FailTask
}

/// <summary>The finalise task's options, with the design's defaults.</summary>
public sealed record FinaliseOptions
{
    public static FinaliseOptions Default { get; } = new();

    public EmptyBranchBehaviour OnEmptyBranch { get; init; } = EmptyBranchBehaviour.UseSource;

    public PlaceholderMismatchBehaviour OnPlaceholderMismatch { get; init; } = PlaceholderMismatchBehaviour.Warn;
}
