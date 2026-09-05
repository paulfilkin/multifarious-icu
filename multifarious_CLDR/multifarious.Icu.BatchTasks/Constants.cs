namespace multifarious.Icu.BatchTasks
{
    public static class Constants
    {
        /// <summary>
        /// Identifies the expand task to Studio. Stored in every project template and task
        /// sequence that names the task, so changing it is a breaking change made deliberately,
        /// never as housekeeping.
        /// </summary>
        public const string ExpandTaskId = "multifarious_Icu_ExpandTask";

        /// <summary>Identifies the finalise task to Studio. Same rules as the expand id.</summary>
        public const string FinaliseTaskId = "multifarious_Icu_FinaliseTask";

        /// <summary>
        /// The file type definition ids the tasks process by default: Studio's own JSON and Java
        /// Resources filters, which are the two formats proven end to end. A project can hold
        /// anything, and a brace in a Word document is not ICU, so the tasks work from an
        /// allowlist rather than from the classifier alone.
        ///
        /// Confirmed against ProjectFile.FileTypeId in the Phase 0 diagnostics log (5 Sep 2026).
        /// </summary>
        public static readonly string[] DefaultFileTypeIds =
        {
            "JSON v 1.0.0.0",
            "Java Resources v 2.0.0.0",
        };

        /// <summary>The author name on the comments the tasks write.</summary>
        public const string CommentAuthor = "multifariousICU Support";

        /// <summary>
        /// The context type of the one context this plugin adds to every paragraph unit it
        /// expands. It carries the unit-level metadata (the original pattern, the hoisted form,
        /// the resource key, the versions) and is how the finalise task finds the units it owns
        /// and how a second expand run knows to leave a unit alone.
        /// </summary>
        public const string IcuContextType = "multifarious:icu";

        public const string IcuContextDisplayName = "ICU message";

        public const string IcuContextDisplayCode = "ICU";
    }
}
