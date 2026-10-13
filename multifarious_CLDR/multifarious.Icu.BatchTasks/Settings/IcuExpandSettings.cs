using System;
using multifarious.Icu.Expansion;
using Sdl.Core.Settings;

namespace multifarious.Icu.BatchTasks.Settings
{
    /// <summary>
    /// The expand task's settings as Studio stores them: in the project's settings bundle, per
    /// project or per language pair, under the class name. The bundle keys a group on
    /// <c>typeof(T).Name</c> and the base class reports that stored id, so the class name is the
    /// id and is not overridden here; Project 44 (6 September 2026) stored the group as
    /// "IcuExpandSettings" whatever an override said. Every value falls back to the design's
    /// default through <see cref="GetDefaultValue"/>, so a project that has never seen the page
    /// behaves exactly as before the page existed. Enumerations are stored by name so the project
    /// file stays readable.
    /// </summary>
    public class IcuExpandSettings : SettingsGroup
    {
        private const string ExpandCardinalId = "ExpandCardinal";
        private const string ExpandOrdinalId = "ExpandOrdinal";
        private const string SeedStrategyId = "SeedStrategy";
        private const string LockPlaceholdersId = "LockPlaceholders";
        private const string MaxUnitsPerMessageId = "MaxUnitsPerMessage";
        private const string OnParseErrorId = "OnParseError";
        private const string KeepTranslatedId = "KeepTranslated";
        private const string KeepMinimumMatchId = "KeepMinimumMatch";
        private const string LockKeptId = "LockKept";
        private const string GrammarDictionaryTextId = "GrammarDictionaryText";
        private const string GrammarDictionaryNameId = "GrammarDictionaryName";
        private const string GrammarDictionaryImportedAtId = "GrammarDictionaryImportedAt";

        public const int MinimumBudget = 1;
        public const int MaximumBudget = 999;

        public const int MinimumMatch = 0;
        public const int MaximumMatch = 100;

        public bool ExpandCardinal
        {
            get { return GetSetting<bool>(ExpandCardinalId).Value; }
            set { GetSetting<bool>(ExpandCardinalId).Value = value; }
        }

        public bool ExpandOrdinal
        {
            get { return GetSetting<bool>(ExpandOrdinalId).Value; }
            set { GetSetting<bool>(ExpandOrdinalId).Value = value; }
        }

        public SourceSeedStrategy SeedStrategy
        {
            get { return Parse(GetSetting<string>(SeedStrategyId).Value, ExpansionOptions.Default.SourceSeedStrategy); }
            set { GetSetting<string>(SeedStrategyId).Value = value.ToString(); }
        }

        public bool LockPlaceholders
        {
            get { return GetSetting<bool>(LockPlaceholdersId).Value; }
            set { GetSetting<bool>(LockPlaceholdersId).Value = value; }
        }

        public int MaxUnitsPerMessage
        {
            get { return GetSetting<int>(MaxUnitsPerMessageId).Value; }
            set { GetSetting<int>(MaxUnitsPerMessageId).Value = value; }
        }

        public ParseErrorBehaviour OnParseError
        {
            get { return Parse(GetSetting<string>(OnParseErrorId).Value, ExpansionOptions.Default.OnParseError); }
            set { GetSetting<string>(OnParseErrorId).Value = value.ToString(); }
        }

        public bool KeepTranslated
        {
            get { return GetSetting<bool>(KeepTranslatedId).Value; }
            set { GetSetting<bool>(KeepTranslatedId).Value = value; }
        }

        public int KeepMinimumMatch
        {
            get { return GetSetting<int>(KeepMinimumMatchId).Value; }
            set { GetSetting<int>(KeepMinimumMatchId).Value = value; }
        }

        public bool LockKept
        {
            get { return GetSetting<bool>(LockKeptId).Value; }
            set { GetSetting<bool>(LockKeptId).Value = value; }
        }

        /// <summary>
        /// The developer's grammar dictionary file as imported, kept as text so export writes
        /// it back unchanged and the effective dictionary is always the shipped default plus
        /// this (grammar dictionary design v2, 13 October 2026). Empty means the default alone.
        /// </summary>
        public string GrammarDictionaryText
        {
            get { return GetSetting<string>(GrammarDictionaryTextId).Value ?? string.Empty; }
            set { GetSetting<string>(GrammarDictionaryTextId).Value = value ?? string.Empty; }
        }

        /// <summary>The imported file's name, for the page and the report.</summary>
        public string GrammarDictionaryName
        {
            get { return GetSetting<string>(GrammarDictionaryNameId).Value ?? string.Empty; }
            set { GetSetting<string>(GrammarDictionaryNameId).Value = value ?? string.Empty; }
        }

        /// <summary>When the file was imported, as an ISO 8601 date and time; empty where none is.</summary>
        public string GrammarDictionaryImportedAt
        {
            get { return GetSetting<string>(GrammarDictionaryImportedAtId).Value ?? string.Empty; }
            set { GetSetting<string>(GrammarDictionaryImportedAtId).Value = value ?? string.Empty; }
        }

        public bool HasGrammarDictionaryFile
        {
            get { return GrammarDictionaryText.Trim().Length > 0; }
        }

        /// <summary>
        /// The dictionary in force: the shipped default with the imported file merged over
        /// it, or the default alone where there is no file or it no longer validates. A file
        /// that validated on import can fail here only if it was edited by hand in the project
        /// file or the shipped default changed under it; the failure is returned, not thrown,
        /// so the task runs on the default and says so.
        /// </summary>
        public GrammarDictionary ToDictionary(out string failure)
        {
            failure = null;
            if (!HasGrammarDictionaryFile) return GrammarDictionary.Default;

            try
            {
                return GrammarDictionary.Default.WithDeveloperFile(DeveloperFile.Parse(GrammarDictionaryText));
            }
            catch (GrammarDictionaryException exception)
            {
                failure = exception.Message;
                return GrammarDictionary.Default;
            }
        }

        /// <summary>
        /// The options record the processor runs on. The file types are not a setting: only the
        /// two proven ones are processed, by decision (Paul, 6 September 2026), and more join
        /// under a controlled change. An out-of-range budget is clamped rather than refused, in
        /// case a project file was edited by hand.
        /// </summary>
        public ExpansionOptions ToOptions()
        {
            return new ExpansionOptions
            {
                ExpandCardinal = ExpandCardinal,
                ExpandOrdinal = ExpandOrdinal,
                SourceSeedStrategy = SeedStrategy,
                LockPlaceholders = LockPlaceholders,
                MaxUnitsPerMessage = Math.Max(MinimumBudget, Math.Min(MaximumBudget, MaxUnitsPerMessage)),
                OnParseError = OnParseError,
                KeepTranslated = KeepTranslated,
                KeepMinimumMatch = Math.Max(MinimumMatch, Math.Min(MaximumMatch, KeepMinimumMatch)),
                LockKept = LockKept,
            };
        }

        protected override object GetDefaultValue(string settingId)
        {
            switch (settingId)
            {
                case ExpandCardinalId: return ExpansionOptions.Default.ExpandCardinal;
                case ExpandOrdinalId: return ExpansionOptions.Default.ExpandOrdinal;
                case SeedStrategyId: return ExpansionOptions.Default.SourceSeedStrategy.ToString();
                case LockPlaceholdersId: return ExpansionOptions.Default.LockPlaceholders;
                case MaxUnitsPerMessageId: return ExpansionOptions.Default.MaxUnitsPerMessage;
                case OnParseErrorId: return ExpansionOptions.Default.OnParseError.ToString();
                case KeepTranslatedId: return ExpansionOptions.Default.KeepTranslated;
                case KeepMinimumMatchId: return ExpansionOptions.Default.KeepMinimumMatch;
                case LockKeptId: return ExpansionOptions.Default.LockKept;
                case GrammarDictionaryTextId: return string.Empty;
                case GrammarDictionaryNameId: return string.Empty;
                case GrammarDictionaryImportedAtId: return string.Empty;
            }

            return base.GetDefaultValue(settingId);
        }

        private static T Parse<T>(string value, T fallback) where T : struct
        {
            T parsed;
            return value != null && Enum.TryParse(value, true, out parsed) && Enum.IsDefined(typeof(T), parsed)
                ? parsed
                : fallback;
        }
    }
}
