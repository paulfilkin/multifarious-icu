using System.Globalization;
using System.Resources;

namespace multifarious.Icu.BatchTasks.Resources
{
    /// <summary>
    /// Typed access to the settings page and report strings.
    ///
    /// Generated from UIStrings.resx - do not edit by hand. Regenerate with
    /// tools/generate-uistrings.ps1 after adding or renaming an entry, and a test asserts the
    /// two stay in step, so a string added to the resx and forgotten here fails the build
    /// rather than showing up blank in the dialog.
    ///
    /// Written out rather than produced by ResXFileCodeGenerator because that generator runs
    /// inside Visual Studio, not during a command-line build.
    /// </summary>
    public static class UIStrings
    {
        private static readonly ResourceManager Manager = new ResourceManager(
            "multifarious.Icu.BatchTasks.Resources.UIStrings", typeof(UIStrings).Assembly);

        /// <summary>The culture used for lookups. Null follows the thread's UI culture, which is what Studio sets.</summary>
        public static CultureInfo Culture { get; set; }

        /// <summary>Look a string up by name. Used by the tests; prefer the properties below.</summary>
        public static string Get(string name)
        {
            return Manager.GetString(name, Culture);
        }

        /// <summary>Paragraph at the top of the page.</summary>
        public static string Expand_Intro { get { return Manager.GetString("Expand_Intro", Culture); } }

        /// <summary>Group heading. Which kinds of ICU message the task restructures.</summary>
        public static string Expand_KindsHeader { get { return Manager.GetString("Expand_KindsHeader", Culture); } }

        /// <summary>Header of a collapsible help panel. Phrased as the question a puzzled user would ask.</summary>
        public static string Expand_KindsHelpHeader { get { return Manager.GetString("Expand_KindsHelpHeader", Culture); } }

        /// <summary>Help paragraph. "one" and "other" are ICU keywords and must not be translated.</summary>
        public static string Expand_KindsHelp1 { get { return Manager.GetString("Expand_KindsHelp1", Culture); } }

        /// <summary>Help paragraph. "select" is the ICU keyword.</summary>
        public static string Expand_KindsHelp2 { get { return Manager.GetString("Expand_KindsHelp2", Culture); } }

        /// <summary>Tick box. The bracketed part is ICU syntax and must not be translated.</summary>
        public static string Expand_Cardinal { get { return Manager.GetString("Expand_Cardinal", Culture); } }

        /// <summary>Tick box. Ordinals are 1st, 2nd, 3rd. The bracketed part is ICU syntax and must not be translated.</summary>
        public static string Expand_Ordinal { get { return Manager.GetString("Expand_Ordinal", Culture); } }

        /// <summary>Hint under the two tick boxes.</summary>
        public static string Expand_KindsHint { get { return Manager.GetString("Expand_KindsHint", Culture); } }

        /// <summary>Group heading. English has two plural forms; Russian needs four, so two of the Russian segments have no exact English counterpart and start from an English form chosen here.</summary>
        public static string Expand_SeedHeader { get { return Manager.GetString("Expand_SeedHeader", Culture); } }

        /// <summary>Radio button, the default. "other" is the ICU keyword and must not be translated.</summary>
        public static string Expand_SeedMatching { get { return Manager.GetString("Expand_SeedMatching", Culture); } }

        /// <summary>Radio button. "other" is the ICU keyword and must not be translated.</summary>
        public static string Expand_SeedAlwaysOther { get { return Manager.GetString("Expand_SeedAlwaysOther", Culture); } }

        /// <summary>Hint under the two radio buttons.</summary>
        public static string Expand_SeedHint { get { return Manager.GetString("Expand_SeedHint", Culture); } }

        /// <summary>Header of a collapsible help panel.</summary>
        public static string Expand_SeedHelpHeader { get { return Manager.GetString("Expand_SeedHelpHeader", Culture); } }

        /// <summary>Help paragraph. "one", "other" and "few" are ICU keywords and must not be translated.</summary>
        public static string Expand_SeedHelp1 { get { return Manager.GetString("Expand_SeedHelp1", Culture); } }

        /// <summary>Help paragraph. "one" and "other" are ICU keywords and must not be translated.</summary>
        public static string Expand_SeedHelp2 { get { return Manager.GetString("Expand_SeedHelp2", Culture); } }

        /// <summary>Group heading. What the task writes into the comment on each source segment.</summary>
        public static string Expand_HintsHeader { get { return Manager.GetString("Expand_HintsHeader", Culture); } }

        /// <summary>Tick box, on by default. The comment is what the translator sees on the source segment.</summary>
        public static string Expand_Comments { get { return Manager.GetString("Expand_Comments", Culture); } }

        /// <summary>Hint under the tick box. "ICU Forms" is the name of the plugin's window under the editor.</summary>
        public static string Expand_CommentsHint { get { return Manager.GetString("Expand_CommentsHint", Culture); } }

        /// <summary>Help paragraph.</summary>
        public static string Expand_CommentsHelp1 { get { return Manager.GetString("Expand_CommentsHelp1", Culture); } }

        /// <summary>Tick box, on by default.</summary>
        public static string Expand_Hints { get { return Manager.GetString("Expand_Hints", Culture); } }

        /// <summary>Hint under the tick box. "genitive singular" is a grammatical case; translate it as a grammarian would.</summary>
        public static string Expand_HintsHint { get { return Manager.GetString("Expand_HintsHint", Culture); } }

        /// <summary>Header of a collapsible help panel.</summary>
        public static string Expand_HintsHelpHeader { get { return Manager.GetString("Expand_HintsHelpHeader", Culture); } }

        /// <summary>Help paragraph. "CLDR" is the Unicode Common Locale Data Repository and stays as it is. "few" is an ICU keyword.</summary>
        public static string Expand_HintsHelp1 { get { return Manager.GetString("Expand_HintsHelp1", Culture); } }

        /// <summary>Help paragraph.</summary>
        public static string Expand_HintsHelp2 { get { return Manager.GetString("Expand_HintsHelp2", Culture); } }

        /// <summary>Group heading. A cap on how many segments one message may become.</summary>
        public static string Expand_BudgetHeader { get { return Manager.GetString("Expand_BudgetHeader", Culture); } }

        /// <summary>Label before a number box. The default is 24.</summary>
        public static string Expand_BudgetLabel { get { return Manager.GetString("Expand_BudgetLabel", Culture); } }

        /// <summary>Hint under the number box.</summary>
        public static string Expand_BudgetHint { get { return Manager.GetString("Expand_BudgetHint", Culture); } }

        /// <summary>Validation message shown when the number box holds something else.</summary>
        public static string Expand_BudgetInvalid { get { return Manager.GetString("Expand_BudgetInvalid", Culture); } }

        /// <summary>Header of a collapsible help panel.</summary>
        public static string Expand_BudgetHelpHeader { get { return Manager.GetString("Expand_BudgetHelpHeader", Culture); } }

        /// <summary>Help paragraph.</summary>
        public static string Expand_BudgetHelp1 { get { return Manager.GetString("Expand_BudgetHelp1", Culture); } }

        /// <summary>Help paragraph.</summary>
        public static string Expand_BudgetHelp2 { get { return Manager.GetString("Expand_BudgetHelp2", Culture); } }

        /// <summary>Group heading. What the task does with a value that has braces but is not valid ICU.</summary>
        public static string Expand_ParseHeader { get { return Manager.GetString("Expand_ParseHeader", Culture); } }

        /// <summary>Radio button, the default.</summary>
        public static string Expand_ParsePassThrough { get { return Manager.GetString("Expand_ParsePassThrough", Culture); } }

        /// <summary>Radio button. The whole batch task fails on the first such value.</summary>
        public static string Expand_ParseFail { get { return Manager.GetString("Expand_ParseFail", Culture); } }

        /// <summary>Header of a collapsible help panel.</summary>
        public static string Expand_ParseHelpHeader { get { return Manager.GetString("Expand_ParseHelpHeader", Culture); } }

        /// <summary>Help paragraph.</summary>
        public static string Expand_ParseHelp1 { get { return Manager.GetString("Expand_ParseHelp1", Culture); } }

        /// <summary>Help paragraph.</summary>
        public static string Expand_ParseHelp2 { get { return Manager.GetString("Expand_ParseHelp2", Culture); } }

        /// <summary>Paragraph at the top of the page.</summary>
        public static string Finalise_Intro { get { return Manager.GetString("Finalise_Intro", Culture); } }

        /// <summary>Group heading. A required segment whose target is empty.</summary>
        public static string Finalise_EmptyHeader { get { return Manager.GetString("Finalise_EmptyHeader", Culture); } }

        /// <summary>Radio button, the default.</summary>
        public static string Finalise_EmptyUseSource { get { return Manager.GetString("Finalise_EmptyUseSource", Culture); } }

        /// <summary>Radio button. The whole batch task fails on the first empty form.</summary>
        public static string Finalise_EmptyFail { get { return Manager.GetString("Finalise_EmptyFail", Culture); } }

        /// <summary>Header of a collapsible help panel.</summary>
        public static string Finalise_EmptyHelpHeader { get { return Manager.GetString("Finalise_EmptyHelpHeader", Culture); } }

        /// <summary>Help paragraph.</summary>
        public static string Finalise_EmptyHelp1 { get { return Manager.GetString("Finalise_EmptyHelp1", Culture); } }

        /// <summary>Help paragraph.</summary>
        public static string Finalise_EmptyHelp2 { get { return Manager.GetString("Finalise_EmptyHelp2", Culture); } }

        /// <summary>Group heading. A placeholder such as {name} missing from the translation or added to it.</summary>
        public static string Finalise_MismatchHeader { get { return Manager.GetString("Finalise_MismatchHeader", Culture); } }

        /// <summary>Radio button, the default.</summary>
        public static string Finalise_MismatchWarn { get { return Manager.GetString("Finalise_MismatchWarn", Culture); } }

        /// <summary>Radio button. The whole batch task fails on the first mismatch.</summary>
        public static string Finalise_MismatchFail { get { return Manager.GetString("Finalise_MismatchFail", Culture); } }

        /// <summary>Header of a collapsible help panel.</summary>
        public static string Finalise_MismatchHelpHeader { get { return Manager.GetString("Finalise_MismatchHelpHeader", Culture); } }

        /// <summary>Help paragraph. "{name}" and "#" are ICU syntax and must not be translated.</summary>
        public static string Finalise_MismatchHelp1 { get { return Manager.GetString("Finalise_MismatchHelp1", Culture); } }

        /// <summary>Help paragraph.</summary>
        public static string Finalise_MismatchHelp2 { get { return Manager.GetString("Finalise_MismatchHelp2", Culture); } }

        /// <summary>Report name and title, one per language pair. Same wording as the task name in Studio's batch task list.</summary>
        public static string Report_ExpandName { get { return Manager.GetString("Report_ExpandName", Culture); } }

        /// <summary>One-line description shown beside the report in Studio's Reports view. {0} messages, {1} files, {2} warnings; keep the placeholders.</summary>
        public static string Report_ExpandDescription { get { return Manager.GetString("Report_ExpandDescription", Culture); } }

        /// <summary>Report name and title, one per language pair. Same wording as the task name.</summary>
        public static string Report_FinaliseName { get { return Manager.GetString("Report_FinaliseName", Culture); } }

        /// <summary>One-line description shown beside the report. {0} messages, {1} files, {2} forms removed, {3} segments filled, {4} warnings; keep the placeholders.</summary>
        public static string Report_FinaliseDescription { get { return Manager.GetString("Report_FinaliseDescription", Culture); } }

        /// <summary>Report heading, as in Studio's own reports.</summary>
        public static string Report_Summary { get { return Manager.GetString("Report_Summary", Culture); } }

        /// <summary>Row label in the summary.</summary>
        public static string Report_Project { get { return Manager.GetString("Report_Project", Culture); } }

        /// <summary>Row label in the summary: source and target language of the report.</summary>
        public static string Report_Languages { get { return Manager.GetString("Report_Languages", Culture); } }

        /// <summary>Row label in the summary: number of files covered.</summary>
        public static string Report_Files { get { return Manager.GetString("Report_Files", Culture); } }

        /// <summary>Row label in the summary: when the task ran.</summary>
        public static string Report_CreatedAt { get { return Manager.GetString("Report_CreatedAt", Culture); } }

        /// <summary>Row label in the summary. The value is the CLDR version the plural rules came from, such as "CLDR 47 (Unicode 16.0)".</summary>
        public static string Report_Cldr { get { return Manager.GetString("Report_Cldr", Culture); } }

        /// <summary>Report heading: the settings the task ran with.</summary>
        public static string Report_Settings { get { return Manager.GetString("Report_Settings", Culture); } }

        /// <summary>Report heading over the per-file table.</summary>
        public static string Report_Totals { get { return Manager.GetString("Report_Totals", Culture); } }

        /// <summary>Column title.</summary>
        public static string Report_File { get { return Manager.GetString("Report_File", Culture); } }

        /// <summary>Column title: number of ICU messages in the file.</summary>
        public static string Report_Messages { get { return Manager.GetString("Report_Messages", Culture); } }

        /// <summary>Column title: number of Studio segments.</summary>
        public static string Report_Segments { get { return Manager.GetString("Report_Segments", Culture); } }

        /// <summary>Column title and report heading.</summary>
        public static string Report_Warnings { get { return Manager.GetString("Report_Warnings", Culture); } }

        /// <summary>Report heading over the per-message tables.</summary>
        public static string Report_Details { get { return Manager.GetString("Report_Details", Culture); } }

        /// <summary>Column title: the resource key of a message, such as inbox.unreadCount.</summary>
        public static string Report_Key { get { return Manager.GetString("Report_Key", Culture); } }

        /// <summary>Row label of the totals row.</summary>
        public static string Report_Total { get { return Manager.GetString("Report_Total", Culture); } }

        /// <summary>Column title: what the expand task did with the message.</summary>
        public static string Report_Outcome { get { return Manager.GetString("Report_Outcome", Culture); } }

        /// <summary>Column title: the warning text where there is one.</summary>
        public static string Report_Note { get { return Manager.GetString("Report_Note", Culture); } }

        /// <summary>Outcome and column title: one segment per form the target language needs.</summary>
        public static string Report_Expanded { get { return Manager.GetString("Report_Expanded", Culture); } }

        /// <summary>Outcome and column title: no plural, the arguments were protected in one segment.</summary>
        public static string Report_Protected { get { return Manager.GetString("Report_Protected", Culture); } }

        /// <summary>Outcome and column title: the message exceeded the branch budget and was laid out with its syntax protected but no forms added.</summary>
        public static string Report_Walked { get { return Manager.GetString("Report_Walked", Culture); } }

        /// <summary>Outcome and column title: left untouched with a warning.</summary>
        public static string Report_PassedThrough { get { return Manager.GetString("Report_PassedThrough", Culture); } }

        /// <summary>Outcome and column title: expanded by an earlier run and left alone.</summary>
        public static string Report_Skipped { get { return Manager.GetString("Report_Skipped", Culture); } }

        /// <summary>Column title in the finalise report: plural forms removed because the target language does not use them.</summary>
        public static string Report_Pruned { get { return Manager.GetString("Report_Pruned", Culture); } }

        /// <summary>Column title in the finalise report: untranslated segments filled with the source text.</summary>
        public static string Report_Filled { get { return Manager.GetString("Report_Filled", Culture); } }

        /// <summary>Shown under the Warnings heading when there are none.</summary>
        public static string Report_NoWarnings { get { return Manager.GetString("Report_NoWarnings", Culture); } }

        /// <summary>Value of a tick-box setting in the report's settings table.</summary>
        public static string Report_On { get { return Manager.GetString("Report_On", Culture); } }

        /// <summary>Value of a tick-box setting in the report's settings table.</summary>
        public static string Report_Off { get { return Manager.GetString("Report_Off", Culture); } }

        /// <summary>Body of the empty states: what the window is for. "ICU MessageFormat" is a technology name and stays as it is.</summary>
        public static string Forms_Purpose { get { return Manager.GetString("Forms_Purpose", Culture); } }

        /// <summary>Title of the empty state when the editor has no document.</summary>
        public static string Forms_NoDocumentTitle { get { return Manager.GetString("Forms_NoDocumentTitle", Culture); } }

        /// <summary>Note of the empty state when the editor has no document. "ICU Expand Plural Forms" is the batch task's name as Studio lists it.</summary>
        public static string Forms_NoDocument { get { return Manager.GetString("Forms_NoDocument", Culture); } }

        /// <summary>Title of the empty state when the open file is of a file type the plugin does not handle.</summary>
        public static string Forms_UnsupportedTitle { get { return Manager.GetString("Forms_UnsupportedTitle", Culture); } }

        /// <summary>Note of the unsupported-file state. {0} is Studio's file type identifier, such as "Word 2007-2019 v 1.0.0.0"; keep the placeholder.</summary>
        public static string Forms_UnsupportedWhy { get { return Manager.GetString("Forms_UnsupportedWhy", Culture); } }

        /// <summary>Note of the unsupported-file state when Studio reports no file type identifier for the file.</summary>
        public static string Forms_UnsupportedUnknown { get { return Manager.GetString("Forms_UnsupportedUnknown", Culture); } }

        /// <summary>Title of the empty state when the file is a supported type but the expand task has not run on it.</summary>
        public static string Forms_NotExpandedTitle { get { return Manager.GetString("Forms_NotExpandedTitle", Culture); } }

        /// <summary>Note of the not-expanded state. "ICU Expand Plural Forms" is the batch task's name.</summary>
        public static string Forms_NotExpanded { get { return Manager.GetString("Forms_NotExpanded", Culture); } }

        /// <summary>Title of the empty state when the selected segment belongs to an ordinary paragraph of an expanded file.</summary>
        public static string Forms_NotIcuTitle { get { return Manager.GetString("Forms_NotIcuTitle", Culture); } }

        /// <summary>Note of the not-an-ICU-message state.</summary>
        public static string Forms_NotIcu { get { return Manager.GetString("Forms_NotIcu", Culture); } }

        /// <summary>Line under the message key. {0} is the target language name, {1} the number of forms; keep the placeholders.</summary>
        public static string Forms_Summary { get { return Manager.GetString("Forms_Summary", Culture); } }

        /// <summary>Column title: the plural form, explicit value or select key of the row.</summary>
        public static string Forms_ColumnForm { get { return Manager.GetString("Forms_ColumnForm", Culture); } }

        /// <summary>Column title: example counts that select the form.</summary>
        public static string Forms_ColumnCounts { get { return Manager.GetString("Forms_ColumnCounts", Culture); } }

        /// <summary>Column title: the source sentence with sample values.</summary>
        public static string Forms_ColumnSource { get { return Manager.GetString("Forms_ColumnSource", Culture); } }

        /// <summary>Column title: the translation with sample values.</summary>
        public static string Forms_ColumnTarget { get { return Manager.GetString("Forms_ColumnTarget", Culture); } }

        /// <summary>Appended to the counts of a form only fractional numbers select, such as Russian "other".</summary>
        public static string Forms_FractionalOnly { get { return Manager.GetString("Forms_FractionalOnly", Culture); } }

        /// <summary>Shown in the Target column for a form with no translation yet.</summary>
        public static string Forms_EmptyTarget { get { return Manager.GetString("Forms_EmptyTarget", Culture); } }

        /// <summary>Label before a small box where the translator types a number to see which form it selects.</summary>
        public static string Forms_TryCount { get { return Manager.GetString("Forms_TryCount", Culture); } }

        /// <summary>Result beside the count box. {0} is the form name, such as "few" or "=0"; keep the placeholder.</summary>
        public static string Forms_ResolvesTo { get { return Manager.GetString("Forms_ResolvesTo", Culture); } }

        /// <summary>Result beside the count box when the text is not a number or the message has no plural.</summary>
        public static string Forms_ResolvesNothing { get { return Manager.GetString("Forms_ResolvesNothing", Culture); } }

        /// <summary>Label over the read-only box showing the whole target message as it will be written to the file.</summary>
        public static string Forms_Projection { get { return Manager.GetString("Forms_Projection", Culture); } }

        /// <summary>Status beside the reassembled message when it parses.</summary>
        public static string Forms_Parses { get { return Manager.GetString("Forms_Parses", Culture); } }

        /// <summary>Status beside the reassembled message when it does not parse. {0} is the parser's description; keep the placeholder.</summary>
        public static string Forms_ParseError { get { return Manager.GetString("Forms_ParseError", Culture); } }

        /// <summary>Tooltip on a target cell whose protected placeholders differ from the source. {0} lists what is missing or extra; keep the placeholder.</summary>
        public static string Forms_PlaceholderWarning { get { return Manager.GetString("Forms_PlaceholderWarning", Culture); } }

        /// <summary>Tooltip of the plus button in the window's corner.</summary>
        public static string Forms_ZoomIn { get { return Manager.GetString("Forms_ZoomIn", Culture); } }

        /// <summary>Tooltip of the minus button in the window's corner.</summary>
        public static string Forms_ZoomOut { get { return Manager.GetString("Forms_ZoomOut", Culture); } }

        /// <summary>Tooltip of the button showing the current zoom, which resets it when clicked.</summary>
        public static string Forms_ZoomReset { get { return Manager.GetString("Forms_ZoomReset", Culture); } }

        /// <summary>The Origin column of the Messages window. Same wording as the verifier's name in the Verification settings.</summary>
        public static string Verifier_Origin { get { return Manager.GetString("Verifier_Origin", Culture); } }

        /// <summary>Warning. {0} is the form's path, such as "count:few"; keep the placeholder.</summary>
        public static string Verifier_EmptyForm { get { return Manager.GetString("Verifier_EmptyForm", Culture); } }

        /// <summary>Error. {0} lists what is missing or extra, such as "missing {name}"; keep the placeholder.</summary>
        public static string Verifier_Placeholders { get { return Manager.GetString("Verifier_Placeholders", Culture); } }

        /// <summary>Warning. '#' is ICU syntax and must not be translated.</summary>
        public static string Verifier_TypedPound { get { return Manager.GetString("Verifier_TypedPound", Culture); } }

        /// <summary>Error. {0} is the parser's description of the problem; keep the placeholder.</summary>
        public static string Verifier_Invalid { get { return Manager.GetString("Verifier_Invalid", Culture); } }

        /// <summary>Paragraph at the top of the verifier's settings page.</summary>
        public static string VerifierPage_Intro { get { return Manager.GetString("VerifierPage_Intro", Culture); } }

        /// <summary>Tick box, on by default, as every Studio verifier's page offers.</summary>
        public static string VerifierPage_Enabled { get { return Manager.GetString("VerifierPage_Enabled", Culture); } }

        /// <summary>Heading over the list of checks, each with a severity choice beside it.</summary>
        public static string VerifierPage_SeverityHeader { get { return Manager.GetString("VerifierPage_SeverityHeader", Culture); } }

        /// <summary>One of the four checks. Its severity is chosen in a drop-down beside it.</summary>
        public static string VerifierPage_CheckEmptyForm { get { return Manager.GetString("VerifierPage_CheckEmptyForm", Culture); } }

        /// <summary>One of the four checks.</summary>
        public static string VerifierPage_CheckPlaceholders { get { return Manager.GetString("VerifierPage_CheckPlaceholders", Culture); } }

        /// <summary>One of the four checks. The count marker is the ICU '#'.</summary>
        public static string VerifierPage_CheckTypedPound { get { return Manager.GetString("VerifierPage_CheckTypedPound", Culture); } }

        /// <summary>One of the four checks.</summary>
        public static string VerifierPage_CheckInvalid { get { return Manager.GetString("VerifierPage_CheckInvalid", Culture); } }

        /// <summary>Hint under the checks. "Error", "Warning" and "Note" follow Studio's own translation of its message severities.</summary>
        public static string VerifierPage_Hint { get { return Manager.GetString("VerifierPage_Hint", Culture); } }

        /// <summary>A severity choice; follow Studio's own translation.</summary>
        public static string Severity_Error { get { return Manager.GetString("Severity_Error", Culture); } }

        /// <summary>A severity choice; follow Studio's own translation.</summary>
        public static string Severity_Warning { get { return Manager.GetString("Severity_Warning", Culture); } }

        /// <summary>A severity choice; follow Studio's own translation.</summary>
        public static string Severity_Note { get { return Manager.GetString("Severity_Note", Culture); } }

        /// <summary>A severity choice that drops the finding.</summary>
        public static string Severity_Ignore { get { return Manager.GetString("Severity_Ignore", Culture); } }
    }
}
