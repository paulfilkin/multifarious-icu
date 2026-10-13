using System;
using System.Globalization;
using System.IO;
using multifarious.Icu.BatchTasks.Resources;
using multifarious.Icu.Expansion;

namespace multifarious.Icu.BatchTasks.Settings.ViewModels
{
    /// <summary>
    /// The expand page. Enumerations are exposed as one bool per option so radio buttons bind
    /// without a converter; the budget is edited as text and validated here, so the page can
    /// refuse to save rather than the binding silently keeping the old number.
    /// </summary>
    public sealed class IcuExpandSettingsViewModel : ObservableObject
    {
        private readonly IcuExpandSettings _settings;

        private bool _expandCardinal;
        private bool _expandOrdinal;
        private SourceSeedStrategy _seedStrategy;
        private bool _lockPlaceholders;
        private string _budgetText = string.Empty;
        private ParseErrorBehaviour _onParseError;
        private bool _keepTranslated;
        private string _keepMinimumText = string.Empty;
        private bool _lockKept;
        private string _dictionaryText = string.Empty;
        private string _dictionaryName = string.Empty;
        private string _dictionaryImportedAt = string.Empty;
        private string _importError;

        public IcuExpandSettingsViewModel(IcuExpandSettings settings)
        {
            _settings = settings ?? new IcuExpandSettings();
            LoadFrom(_settings);
        }

        public bool ExpandCardinal
        {
            get { return _expandCardinal; }
            set { Set(ref _expandCardinal, value); }
        }

        public bool ExpandOrdinal
        {
            get { return _expandOrdinal; }
            set { Set(ref _expandOrdinal, value); }
        }

        public bool SeedIsMatchingElseOther
        {
            get { return _seedStrategy == SourceSeedStrategy.MatchingElseOther; }
            set { if (value) SetSeed(SourceSeedStrategy.MatchingElseOther); }
        }

        public bool SeedIsAlwaysOther
        {
            get { return _seedStrategy == SourceSeedStrategy.AlwaysOther; }
            set { if (value) SetSeed(SourceSeedStrategy.AlwaysOther); }
        }

        public bool LockPlaceholders
        {
            get { return _lockPlaceholders; }
            set { Set(ref _lockPlaceholders, value); }
        }

        public string BudgetText
        {
            get { return _budgetText; }
            set
            {
                if (Set(ref _budgetText, value ?? string.Empty))
                {
                    Raise(nameof(IsBudgetValid));
                    Raise(nameof(IsValid));
                }
            }
        }

        public bool IsBudgetValid
        {
            get
            {
                int parsed;
                return TryParseBudget(out parsed);
            }
        }

        /// <summary>Whether the page may be saved. Only the two number boxes can be wrong, and the match box only while it is in use.</summary>
        public bool IsValid
        {
            get { return IsBudgetValid && (!_keepTranslated || IsKeepMinimumValid); }
        }

        public bool KeepTranslated
        {
            get { return _keepTranslated; }
            set
            {
                if (Set(ref _keepTranslated, value))
                {
                    Raise(nameof(IsValid));
                }
            }
        }

        /// <summary>The minimum match as typed, a whole number from 0 to 100.</summary>
        public string KeepMinimumText
        {
            get { return _keepMinimumText; }
            set
            {
                if (Set(ref _keepMinimumText, value ?? string.Empty))
                {
                    Raise(nameof(IsKeepMinimumValid));
                    Raise(nameof(IsValid));
                }
            }
        }

        public bool IsKeepMinimumValid
        {
            get
            {
                int parsed;
                return TryParseKeepMinimum(out parsed);
            }
        }

        public bool LockKept
        {
            get { return _lockKept; }
            set { Set(ref _lockKept, value); }
        }

        // ---- the grammar dictionary ----------------------------------------------------------

        /// <summary>What the shipped default covers, for the line above the imported file's.</summary>
        public string DictionaryDefault
        {
            get
            {
                var dictionary = GrammarDictionary.Default;
                GrammarKind gender;
                var languages = dictionary.Kinds.TryGetValue("gender", out gender) ? gender.Languages.Count : 0;
                return string.Format(CultureInfo.CurrentCulture, UIStrings.Expand_GrammarDefault, dictionary.CldrVersion, languages);
            }
        }

        public bool HasDictionary
        {
            get { return _dictionaryText.Trim().Length > 0; }
        }

        /// <summary>The imported file's name and date, or that there is none.</summary>
        public string DictionaryStatus
        {
            get
            {
                if (!HasDictionary) return UIStrings.Expand_GrammarNone;

                DateTime imported;
                var when = DateTime.TryParse(_dictionaryImportedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out imported)
                    ? imported.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                    : _dictionaryImportedAt;
                return string.Format(CultureInfo.CurrentCulture, UIStrings.Expand_GrammarImported, _dictionaryName, when);
            }
        }

        /// <summary>Why the last import was refused, shown under the buttons; null once an import succeeds or the file is removed.</summary>
        public string ImportError
        {
            get { return _importError; }
            private set
            {
                if (Set(ref _importError, value)) Raise(nameof(HasImportError));
            }
        }

        public bool HasImportError
        {
            get { return _importError != null; }
        }

        /// <summary>The imported text, for export; empty where there is none.</summary>
        public string DictionaryText
        {
            get { return _dictionaryText; }
        }

        public string DictionaryName
        {
            get { return _dictionaryName; }
        }

        /// <summary>
        /// Takes a developer's file: parsed and merged over the shipped default here, so a
        /// file that does not validate is refused with its reason and the page keeps what it
        /// had. The text is stored as given; export writes it back unchanged.
        /// </summary>
        public bool TryImport(string fileName, string text)
        {
            try
            {
                GrammarDictionary.Default.WithDeveloperFile(DeveloperFile.Parse(text ?? string.Empty));
            }
            catch (GrammarDictionaryException exception)
            {
                ImportError = string.Format(CultureInfo.CurrentCulture, UIStrings.Expand_GrammarInvalid, exception.Message);
                return false;
            }

            _dictionaryText = text;
            _dictionaryName = fileName ?? string.Empty;
            _dictionaryImportedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            ImportError = null;
            RaiseDictionary();
            return true;
        }

        public void RemoveDictionary()
        {
            _dictionaryText = string.Empty;
            _dictionaryName = string.Empty;
            _dictionaryImportedAt = string.Empty;
            ImportError = null;
            RaiseDictionary();
        }

        /// <summary>The developer's file template, embedded in the plugin.</summary>
        public static string TemplateText()
        {
            using (var stream = typeof(IcuExpandSettingsViewModel).Assembly.GetManifestResourceStream(Constants.GrammarTemplateResource))
            {
                if (stream == null) throw new InvalidOperationException("The grammar dictionary template is missing from the plugin.");
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private void RaiseDictionary()
        {
            Raise(nameof(HasDictionary));
            Raise(nameof(DictionaryStatus));
            Raise(nameof(DictionaryText));
            Raise(nameof(DictionaryName));
        }

        public bool ParseErrorPassesThrough
        {
            get { return _onParseError == ParseErrorBehaviour.PassThrough; }
            set { if (value) SetParseError(ParseErrorBehaviour.PassThrough); }
        }

        public bool ParseErrorFailsTask
        {
            get { return _onParseError == ParseErrorBehaviour.FailTask; }
            set { if (value) SetParseError(ParseErrorBehaviour.FailTask); }
        }

        public SourceSeedStrategy SeedStrategy
        {
            get { return _seedStrategy; }
        }

        public ParseErrorBehaviour OnParseError
        {
            get { return _onParseError; }
        }

        /// <summary>Writes the page into the settings group, for saving. Returns the group.</summary>
        public IcuExpandSettings Apply()
        {
            int budget;
            if (!TryParseBudget(out budget)) budget = _settings.MaxUnitsPerMessage;
            int minimum;
            if (!TryParseKeepMinimum(out minimum)) minimum = _settings.KeepMinimumMatch;

            _settings.ExpandCardinal = _expandCardinal;
            _settings.ExpandOrdinal = _expandOrdinal;
            _settings.SeedStrategy = _seedStrategy;
            _settings.LockPlaceholders = _lockPlaceholders;
            _settings.MaxUnitsPerMessage = budget;
            _settings.OnParseError = _onParseError;
            _settings.KeepTranslated = _keepTranslated;
            _settings.KeepMinimumMatch = minimum;
            _settings.LockKept = _lockKept;
            _settings.GrammarDictionaryText = _dictionaryText;
            _settings.GrammarDictionaryName = _dictionaryName;
            _settings.GrammarDictionaryImportedAt = _dictionaryImportedAt;
            return _settings;
        }

        public IcuExpandSettings ResetToDefaults()
        {
            _settings.Reset();
            LoadFrom(_settings);
            RaiseAll();
            return _settings;
        }

        private bool TryParseBudget(out int budget)
        {
            return int.TryParse(_budgetText.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out budget)
                && budget >= IcuExpandSettings.MinimumBudget
                && budget <= IcuExpandSettings.MaximumBudget;
        }

        private bool TryParseKeepMinimum(out int minimum)
        {
            return int.TryParse(_keepMinimumText.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out minimum)
                && minimum >= IcuExpandSettings.MinimumMatch
                && minimum <= IcuExpandSettings.MaximumMatch;
        }

        private void SetSeed(SourceSeedStrategy strategy)
        {
            if (_seedStrategy == strategy) return;
            _seedStrategy = strategy;
            Raise(nameof(SeedIsMatchingElseOther));
            Raise(nameof(SeedIsAlwaysOther));
        }

        private void SetParseError(ParseErrorBehaviour behaviour)
        {
            if (_onParseError == behaviour) return;
            _onParseError = behaviour;
            Raise(nameof(ParseErrorPassesThrough));
            Raise(nameof(ParseErrorFailsTask));
        }

        private void LoadFrom(IcuExpandSettings settings)
        {
            _expandCardinal = settings.ExpandCardinal;
            _expandOrdinal = settings.ExpandOrdinal;
            _seedStrategy = settings.SeedStrategy;
            _lockPlaceholders = settings.LockPlaceholders;
            _budgetText = settings.MaxUnitsPerMessage.ToString(CultureInfo.CurrentCulture);
            _onParseError = settings.OnParseError;
            _keepTranslated = settings.KeepTranslated;
            _keepMinimumText = settings.KeepMinimumMatch.ToString(CultureInfo.CurrentCulture);
            _lockKept = settings.LockKept;
            _dictionaryText = settings.GrammarDictionaryText;
            _dictionaryName = settings.GrammarDictionaryName;
            _dictionaryImportedAt = settings.GrammarDictionaryImportedAt;
            _importError = null;
        }

        private void RaiseAll()
        {
            Raise(nameof(ExpandCardinal));
            Raise(nameof(ExpandOrdinal));
            Raise(nameof(SeedIsMatchingElseOther));
            Raise(nameof(SeedIsAlwaysOther));
            Raise(nameof(LockPlaceholders));
            Raise(nameof(BudgetText));
            Raise(nameof(IsBudgetValid));
            Raise(nameof(IsValid));
            Raise(nameof(ParseErrorPassesThrough));
            Raise(nameof(ParseErrorFailsTask));
            Raise(nameof(KeepTranslated));
            Raise(nameof(KeepMinimumText));
            Raise(nameof(IsKeepMinimumValid));
            Raise(nameof(LockKept));
            Raise(nameof(ImportError));
            Raise(nameof(HasImportError));
            RaiseDictionary();
        }
    }
}
