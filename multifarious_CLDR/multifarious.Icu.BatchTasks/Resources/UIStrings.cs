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

        /// <summary>Group heading. Which kinds of ICU message the task restructures.</summary>
        public static string Expand_KindsHeader { get { return Manager.GetString("Expand_KindsHeader", Culture); } }

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

        /// <summary>Group heading. What the task writes into the comment on each source segment.</summary>
        public static string Expand_HintsHeader { get { return Manager.GetString("Expand_HintsHeader", Culture); } }

        /// <summary>Tick box, on by default.</summary>
        public static string Expand_Hints { get { return Manager.GetString("Expand_Hints", Culture); } }

        /// <summary>Hint under the tick box. "genitive singular" is a grammatical case; translate it as a grammarian would.</summary>
        public static string Expand_HintsHint { get { return Manager.GetString("Expand_HintsHint", Culture); } }

        /// <summary>Group heading. A cap on how many segments one message may become.</summary>
        public static string Expand_BudgetHeader { get { return Manager.GetString("Expand_BudgetHeader", Culture); } }

        /// <summary>Label before a number box. The default is 24.</summary>
        public static string Expand_BudgetLabel { get { return Manager.GetString("Expand_BudgetLabel", Culture); } }

        /// <summary>Hint under the number box.</summary>
        public static string Expand_BudgetHint { get { return Manager.GetString("Expand_BudgetHint", Culture); } }

        /// <summary>Validation message shown when the number box holds something else.</summary>
        public static string Expand_BudgetInvalid { get { return Manager.GetString("Expand_BudgetInvalid", Culture); } }

        /// <summary>Group heading. What the task does with a value that has braces but is not valid ICU.</summary>
        public static string Expand_ParseHeader { get { return Manager.GetString("Expand_ParseHeader", Culture); } }

        /// <summary>Radio button, the default.</summary>
        public static string Expand_ParsePassThrough { get { return Manager.GetString("Expand_ParsePassThrough", Culture); } }

        /// <summary>Radio button. The whole batch task fails on the first such value.</summary>
        public static string Expand_ParseFail { get { return Manager.GetString("Expand_ParseFail", Culture); } }

        /// <summary>Paragraph at the top of the page.</summary>
        public static string Finalise_Intro { get { return Manager.GetString("Finalise_Intro", Culture); } }

        /// <summary>Group heading. A required segment whose target is empty.</summary>
        public static string Finalise_EmptyHeader { get { return Manager.GetString("Finalise_EmptyHeader", Culture); } }

        /// <summary>Radio button, the default.</summary>
        public static string Finalise_EmptyUseSource { get { return Manager.GetString("Finalise_EmptyUseSource", Culture); } }

        /// <summary>Radio button. The whole batch task fails on the first empty form.</summary>
        public static string Finalise_EmptyFail { get { return Manager.GetString("Finalise_EmptyFail", Culture); } }

        /// <summary>Group heading. A placeholder such as {name} missing from the translation or added to it.</summary>
        public static string Finalise_MismatchHeader { get { return Manager.GetString("Finalise_MismatchHeader", Culture); } }

        /// <summary>Radio button, the default.</summary>
        public static string Finalise_MismatchWarn { get { return Manager.GetString("Finalise_MismatchWarn", Culture); } }

        /// <summary>Radio button. The whole batch task fails on the first mismatch.</summary>
        public static string Finalise_MismatchFail { get { return Manager.GetString("Finalise_MismatchFail", Culture); } }
    }
}
