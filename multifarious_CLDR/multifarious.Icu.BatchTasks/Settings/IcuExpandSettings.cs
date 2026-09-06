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
        private const string IncludeHintsId = "IncludeHints";
        private const string WriteSegmentCommentsId = "WriteSegmentComments";
        private const string MaxUnitsPerMessageId = "MaxUnitsPerMessage";
        private const string OnParseErrorId = "OnParseError";

        public const int MinimumBudget = 1;
        public const int MaximumBudget = 999;

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

        public bool IncludeHints
        {
            get { return GetSetting<bool>(IncludeHintsId).Value; }
            set { GetSetting<bool>(IncludeHintsId).Value = value; }
        }

        public bool WriteSegmentComments
        {
            get { return GetSetting<bool>(WriteSegmentCommentsId).Value; }
            set { GetSetting<bool>(WriteSegmentCommentsId).Value = value; }
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

        /// <summary>
        /// The options record the processor runs on. The tag construct is not a setting: the
        /// placeholder variant produces JSON without its placeholders and is kept for the tests.
        /// The file types are not a setting either: only the two proven ones are processed, by
        /// decision (Paul, 6 September 2026), and more join under a controlled change. An
        /// out-of-range budget is clamped rather than refused, in case a project file was edited
        /// by hand.
        /// </summary>
        public ExpansionOptions ToOptions()
        {
            return new ExpansionOptions
            {
                ExpandCardinal = ExpandCardinal,
                ExpandOrdinal = ExpandOrdinal,
                SourceSeedStrategy = SeedStrategy,
                IncludeHints = IncludeHints,
                WriteSegmentComments = WriteSegmentComments,
                MaxUnitsPerMessage = Math.Max(MinimumBudget, Math.Min(MaximumBudget, MaxUnitsPerMessage)),
                OnParseError = OnParseError,
            };
        }

        protected override object GetDefaultValue(string settingId)
        {
            switch (settingId)
            {
                case ExpandCardinalId: return ExpansionOptions.Default.ExpandCardinal;
                case ExpandOrdinalId: return ExpansionOptions.Default.ExpandOrdinal;
                case SeedStrategyId: return ExpansionOptions.Default.SourceSeedStrategy.ToString();
                case IncludeHintsId: return ExpansionOptions.Default.IncludeHints;
                case WriteSegmentCommentsId: return ExpansionOptions.Default.WriteSegmentComments;
                case MaxUnitsPerMessageId: return ExpansionOptions.Default.MaxUnitsPerMessage;
                case OnParseErrorId: return ExpansionOptions.Default.OnParseError.ToString();
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
