using System.Collections.Generic;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>What the expand task did with one paragraph unit, for the report.</summary>
    public enum ExpandOutcome
    {
        /// <summary>Category-expanded: one segment per form the target language needs.</summary>
        Expanded,

        /// <summary>No selector; the arguments were protected in a single segment.</summary>
        Protected,

        /// <summary>Over the branch budget: laid out with the syntax protected and the source's own branches.</summary>
        Walked,

        /// <summary>Left as it was, with a warning: a parse failure, a hoist refusal, or a budget even the source's branches exceed.</summary>
        PassedThrough,

        /// <summary>Already expanded by an earlier run and left alone.</summary>
        Skipped
    }

    public sealed class ExpandUnitOutcome
    {
        public ExpandUnitOutcome(string paragraphUnitId, string key, ExpandOutcome outcome, int segments, string detail)
        {
            ParagraphUnitId = paragraphUnitId;
            Key = key ?? string.Empty;
            Outcome = outcome;
            Segments = segments;
            Detail = detail ?? string.Empty;
        }

        public string ParagraphUnitId { get; }

        /// <summary>The resource key, such as <c>inbox.unreadCount</c>, or empty where the filter gave none.</summary>
        public string Key { get; }

        public ExpandOutcome Outcome { get; }

        /// <summary>Segments the unit holds after the task; zero where it was not touched.</summary>
        public int Segments { get; }

        /// <summary>The warning text where there is one.</summary>
        public string Detail { get; }
    }

    public sealed class FinaliseUnitOutcome
    {
        public FinaliseUnitOutcome(string paragraphUnitId, string key, int segments, int pruned, int filled, IReadOnlyList<string> warnings)
        {
            ParagraphUnitId = paragraphUnitId;
            Key = key ?? string.Empty;
            Segments = segments;
            Pruned = pruned;
            Filled = filled;
            Warnings = warnings ?? new List<string>();
        }

        public string ParagraphUnitId { get; }

        public string Key { get; }

        /// <summary>Segments left after pruning.</summary>
        public int Segments { get; }

        /// <summary>Branch triples removed.</summary>
        public int Pruned { get; }

        /// <summary>Target segments filled from the source.</summary>
        public int Filled { get; }

        public IReadOnlyList<string> Warnings { get; }
    }
}
