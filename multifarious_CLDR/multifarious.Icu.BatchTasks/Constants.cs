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

        /// <summary>
        /// Identifies the restore task to Studio. Same rules as the expand id. Release 2.0 replaced
        /// the finalise task (id multifarious_Icu_FinaliseTask) with this one, by decision (Paul,
        /// 11 October 2026); a template or sequence naming the old id no longer resolves.
        /// </summary>
        public const string RestoreTaskId = "multifarious_Icu_RestoreTask";

        /// <summary>The ICU Forms view part, its ribbon group and the action that shows it. Studio stores window layouts against the view part id.</summary>
        public const string FormsViewPartId = "multifarious_Icu_FormsViewPart";

        public const string RibbonGroupId = "multifarious_Icu_RibbonGroup";

        public const string ShowFormsActionId = "multifarious_Icu_ShowFormsAction";

        /// <summary>
        /// The group on the Projects view's Add-ins tab and its one action, the ICU Settings
        /// dialog (13 October 2026). Studio shows a plugin batch task's settings page only inside
        /// its two wizards, never in Project Settings or the template editor, so this is the
        /// route to the Expand and Restore settings of an existing project.
        /// </summary>
        public const string ProjectsRibbonGroupId = "multifarious_Icu_ProjectsRibbonGroup";

        public const string ShowSettingsActionId = "multifarious_Icu_ShowSettingsAction";

        /// <summary>The ICU verifier Studio lists under Verification.</summary>
        public const string VerifierId = "multifarious_Icu_Verifier";

        /// <summary>
        /// The verifier's settings group: the class name of IcuVerifierSettings, because Studio
        /// keys a group on its class name and the verification framework reads the Enabled flag
        /// from the group named by the verifier's settings id. The two must be the same string.
        /// </summary>
        public const string VerifierSettingsId = "IcuVerifierSettings";

        public const string VerifierSettingsPageId = "multifarious_Icu_VerifierSettingsPage";

        /// <summary>
        /// The file type definition ids the tasks process: Studio's own JSON and Java Resources
        /// filters, which are the two formats proven end to end. A project can hold anything,
        /// and a brace in a Word document is not ICU, so the tasks work from this list rather
        /// than from the classifier alone. Not a setting, by decision (6 Sep 2026): a file type
        /// that has not been run end to end is not known to work, and a customised copy of a
        /// shipped filter carries a different id and is therefore skipped. New ids join here
        /// once proven.
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
        /// Metadata key on the placeholder tags the expansion writes for an argument or '#',
        /// which tells them apart from a filter's own placeholder tags: the restore task turns
        /// the former back into text and keeps the latter as tags. Tag metadata persists in
        /// SDLXLIFF.
        /// </summary>
        public const string ArgumentTagKey = "multifarious:argument";

        /// <summary>
        /// The context type of the one context this plugin adds to every paragraph unit it
        /// expands. It carries the unit-level metadata (the original pattern, the hoisted form,
        /// the resource key, the versions) and is how the restore task finds the units it owns
        /// and how a second expand run knows to leave a unit alone.
        /// </summary>
        public const string IcuContextType = "multifarious:icu";

        public const string IcuContextDisplayName = "ICU message";

        public const string IcuContextDisplayCode = "ICU";

        /// <summary>
        /// The context metadata key naming the unit's state: <see cref="StateExpanded"/> while the
        /// message is laid out one segment per form, <see cref="StateRestored"/> once the restore
        /// task has put it back together as one plain segment. Absent on a file expanded before
        /// the key existed, which means expanded.
        /// </summary>
        public const string StateKey = "icu:state";

        public const string StateExpanded = "expanded";

        public const string StateRestored = "restored";

        /// <summary>
        /// The context metadata key under which the restore task records each form segment's
        /// confirmation level, lock and translation origin by branch path, so the next expansion
        /// puts them back. See <see cref="Services.SegmentStates"/> for the format.
        /// </summary>
        public const string SegmentStatesKey = "icu:segmentStates";

        /// <summary>
        /// The context metadata key recording every select the grammar dictionary category
        /// expanded: one entry per line, the selector path, the kind and the comma-separated
        /// key set, tab-separated. Beside <c>icu:expandedSelectors</c>, which lists the paths
        /// alone. The ICU Forms window reads it so a file says what it was laid out with on a
        /// machine without the project's dictionary.
        /// </summary>
        public const string GrammarKey = "icu:grammar";

        /// <summary>
        /// The context metadata key recording, per segment number, the dictionary's hint, the
        /// sample noun and the dependent forms the language needs that the application does
        /// not supply: one entry per line, tab-separated, only for segments that have any.
        /// </summary>
        public const string GrammarFormsKey = "icu:grammarForms";

        /// <summary>The developer's file template the Expand page saves for a project to fill in.</summary>
        public const string GrammarTemplateResource = "multifarious.Icu.BatchTasks.Resources.grammar-dictionary-template.json";

        public const string GrammarTemplateFileName = "grammar-dictionary.json";
    }
}
