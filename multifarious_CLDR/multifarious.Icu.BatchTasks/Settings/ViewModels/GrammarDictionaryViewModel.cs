using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Icu.Cldr;
using multifarious.Icu.BatchTasks.Resources;
using multifarious.Icu.Expansion;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace multifarious.Icu.BatchTasks.Settings.ViewModels
{
    /// <summary>
    /// The Grammar dictionary tab: the effective dictionary, the shipped default with the
    /// project's developer's file merged over it, shown as a table of languages and their
    /// keys and a table of the arguments the application passes, both editable. Every edit
    /// is written into the developer's file, the text the settings store, by round-tripping
    /// the imported JSON, so the note and anything the tables do not touch stay as imported
    /// and the shipped default is never changed. A key set that differs from the default
    /// becomes a <c>keys.&lt;kind&gt;.languages.&lt;locale&gt;</c> entry; the argument table
    /// is the file's <c>arguments</c>. The merge validates after every edit; a refused edit
    /// shows its reason and blocks saving, as the number boxes on the Options tab do.
    /// </summary>
    public sealed class GrammarDictionaryViewModel : ObservableObject
    {
        private const string VersionKey = "version";
        private const string ArgumentsKey = "arguments";
        private const string KeysKey = "keys";
        private const string LanguagesKey = "languages";
        private const string RenameKey = "rename";
        private const string SuppliesKey = "supplies";
        private const string OtherKey = "other";

        private string _text = string.Empty;
        private string _name = string.Empty;
        private string _importedAt = string.Empty;
        private bool _edited;
        private JObject _document;
        private GrammarDictionary _effective = GrammarDictionary.Default;
        private string _error;
        private string _errorArea = ErrorAreaFile;
        private object _errorRow;
        private string _markedError;
        private bool _addLanguageHasError;
        private string _importError;
        private string _otherText = string.Empty;
        private string _addLanguageKind;
        private string _addLanguageLocale = string.Empty;
        private bool _loading;

        public GrammarDictionaryViewModel()
        {
            KindNames = GrammarDictionary.Default.Kinds.Keys.ToList();
            RoleNames = KindNames.Concat(new[] { GrammarDictionary.CountRole }).ToList();
            RoleHeaders = RoleNames.Select(role => string.Format(CultureInfo.CurrentCulture, UIStrings.Grammar_RoleColumn, role)).ToList();
            _addLanguageKind = KindNames.FirstOrDefault();
            Load(string.Empty, string.Empty, string.Empty);
        }

        /// <summary>The kinds the shipped default knows, in its order: gender, article.</summary>
        public IReadOnlyList<string> KindNames { get; }

        /// <summary>The roles an argument entry names: the kinds, then the count.</summary>
        public IReadOnlyList<string> RoleNames { get; }

        /// <summary>The column headers of the argument table's role columns.</summary>
        public IReadOnlyList<string> RoleHeaders { get; }

        public ObservableCollection<GrammarLanguageRow> Languages { get; } = new ObservableCollection<GrammarLanguageRow>();

        public ObservableCollection<GrammarArgumentRow> Arguments { get; } = new ObservableCollection<GrammarArgumentRow>();

        // ---- status --------------------------------------------------------------------------

        /// <summary>What the shipped default covers, for the line above the file's.</summary>
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

        /// <summary>Whether the project has a developer's file: imported, or made by edits on this page.</summary>
        public bool HasDictionary
        {
            get { return _text.Trim().Length > 0; }
        }

        /// <summary>The file's name and date, that it was edited here, or that there is none.</summary>
        public string DictionaryStatus
        {
            get
            {
                if (!HasDictionary) return UIStrings.Expand_GrammarNone;
                if (_name.Length == 0) return UIStrings.Grammar_Edited;

                DateTime imported;
                var when = DateTime.TryParse(_importedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out imported)
                    ? imported.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                    : _importedAt;
                return string.Format(CultureInfo.CurrentCulture, _edited ? UIStrings.Grammar_ImportedEdited : UIStrings.Expand_GrammarImported, _name, when);
            }
        }

        /// <summary>The developer's file as it now stands: the imported text until an edit, then the edited document.</summary>
        public string DictionaryText
        {
            get { return _text; }
        }

        public string DictionaryName
        {
            get { return _name; }
        }

        public string DictionaryImportedAt
        {
            get { return _importedAt; }
        }

        /// <summary>Why the last import was refused; null once an import succeeds or the file is removed.</summary>
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

        public const string ErrorAreaFile = "file";
        public const string ErrorAreaLanguages = "languages";
        public const string ErrorAreaArguments = "arguments";
        public const string ErrorAreaOther = "other";

        /// <summary>Why the file as edited does not validate; null while it does. Blocks saving.</summary>
        public string Error
        {
            get { return _error; }
            private set
            {
                if (Set(ref _error, value))
                {
                    Raise(nameof(HasError));
                    Raise(nameof(IsValid));
                    RaiseErrorAreas();
                }
            }
        }

        public bool HasError
        {
            get { return _error != null; }
        }

        /// <summary>
        /// Which block of the page the message belongs under, so it shows beside the edit that
        /// caused it rather than at the top of a long page (Paul, test plan S4a and C17 to C20,
        /// 14 October 2026): the languages table, the arguments table, the other box, or the
        /// file itself for a stored text that does not validate.
        /// </summary>
        public string ErrorArea
        {
            get { return _errorArea; }
        }

        public bool ErrorInFile
        {
            get { return HasError && _errorArea == ErrorAreaFile; }
        }

        public bool ErrorInLanguages
        {
            get { return HasError && _errorArea == ErrorAreaLanguages; }
        }

        public bool ErrorInArguments
        {
            get { return HasError && _errorArea == ErrorAreaArguments; }
        }

        public bool ErrorInOther
        {
            get { return HasError && _errorArea == ErrorAreaOther; }
        }

        /// <summary>True while the typed language tag was refused.</summary>
        public bool AddLanguageHasError
        {
            get { return _addLanguageHasError; }
            private set { Set(ref _addLanguageHasError, value); }
        }

        private void RaiseErrorAreas()
        {
            Raise(nameof(ErrorArea));
            Raise(nameof(ErrorInFile));
            Raise(nameof(ErrorInLanguages));
            Raise(nameof(ErrorInArguments));
            Raise(nameof(ErrorInOther));
        }

        /// <summary>
        /// Places the current error: the block it shows under and the row or box that carries
        /// the red border. Called after every edit with what was edited; a valid state clears
        /// the previous mark.
        /// </summary>
        private void MarkError(string area, object row)
        {
            // The refusal is the same one as before the edit: the cause has not moved, so the
            // mark stays where it is. Typing a good set into French while German is still
            // wrong keeps German red.
            if (!IsValid && _errorRow != null && _error == _markedError) return;
            _markedError = IsValid ? null : _error;

            var languageRow = _errorRow as GrammarLanguageRow;
            if (languageRow != null) languageRow.SetHasError(false);
            var argumentRow = _errorRow as GrammarArgumentRow;
            if (argumentRow != null) argumentRow.SetHasError(false);
            AddLanguageHasError = false;
            _errorRow = null;

            _errorArea = area;
            RaiseErrorAreas();
            if (IsValid) return;

            _errorRow = row;
            languageRow = row as GrammarLanguageRow;
            if (languageRow != null) languageRow.SetHasError(true);
            argumentRow = row as GrammarArgumentRow;
            if (argumentRow != null) argumentRow.SetHasError(true);
            if (row is string && (string)row == ErrorAreaLanguages) AddLanguageHasError = true;
        }

        public bool IsValid
        {
            get { return _error == null; }
        }

        /// <summary>The project's words for the <c>other</c> key; empty keeps the default hint.</summary>
        public string OtherText
        {
            get { return _otherText; }
            set
            {
                if (!Set(ref _otherText, value ?? string.Empty) || _loading) return;
                var document = Document();
                if (_otherText.Trim().Length == 0) document.Remove(OtherKey);
                else document[OtherKey] = _otherText;
                AfterEdit();
                MarkError(ErrorAreaOther, null);
            }
        }

        /// <summary>The kind-level renames the file declares, read-only; empty where there are none.</summary>
        public string RenamesLine
        {
            get
            {
                var parts = new List<string>();
                var keys = _document == null ? null : _document[KeysKey] as JObject;
                if (keys != null)
                {
                    foreach (var kind in keys.Properties())
                    {
                        var rename = (kind.Value as JObject)?[RenameKey] as JObject;
                        if (rename == null || !rename.HasValues) continue;
                        parts.Add(kind.Name + ": " + string.Join(", ", rename.Properties().Select(pair =>
                            string.Format(CultureInfo.CurrentCulture, UIStrings.Grammar_RenameAs, pair.Name, (string)pair.Value))));
                    }
                }

                return parts.Count == 0 ? string.Empty : string.Format(CultureInfo.CurrentCulture, UIStrings.Grammar_RenamesLine, string.Join("; ", parts));
            }
        }

        public bool HasRenames
        {
            get { return RenamesLine.Length > 0; }
        }

        // ---- adding a language -----------------------------------------------------------------

        public string AddLanguageKind
        {
            get { return _addLanguageKind; }
            set { Set(ref _addLanguageKind, value); }
        }

        public string AddLanguageLocale
        {
            get { return _addLanguageLocale; }
            set
            {
                if (Set(ref _addLanguageLocale, value ?? string.Empty)) Raise(nameof(CanAddLanguage));
            }
        }

        public bool CanAddLanguage
        {
            get { return _addLanguageKind != null && _addLanguageLocale.Trim().Length > 0; }
        }

        /// <summary>
        /// Adds the typed locale to the chosen kind with a key set of <c>other</c> alone, as a
        /// project entry, for the user to fill in. A locale the kind already lists is selected
        /// rather than added twice; one that is not a language tag is refused in place.
        /// </summary>
        public GrammarLanguageRow AddLanguage()
        {
            if (!CanAddLanguage) return null;
            var kind = _addLanguageKind;
            var locale = _addLanguageLocale.Trim();
            LanguageTag tag;
            if (LanguageTag.TryParse(locale, out tag)) locale = tag.Normalised;

            var existing = Languages.FirstOrDefault(row => row.Kind == kind && string.Equals(row.LocaleKey, locale, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                AddLanguageLocale = string.Empty;
                return existing;
            }

            var languages = Ensure(Ensure(Ensure(Document(), KeysKey), kind), LanguagesKey);
            languages[locale] = new JArray(GrammarDictionary.OtherKey);
            AddLanguageLocale = string.Empty;
            AfterEdit();
            if (!IsValid)
            {
                // Not a language tag, or some other refusal: take it out again and show why.
                languages.Remove(locale);
                var reason = Error;
                AfterEdit();
                Error = reason;
                MarkError(ErrorAreaLanguages, ErrorAreaLanguages);
                return null;
            }

            MarkError(ErrorAreaLanguages, null);
            var row = LanguageRow(kind, locale);
            Languages.Insert(InsertionIndex(kind, locale), row);
            return row;
        }

        /// <summary>Where a new row goes: after the rows of earlier kinds and the same kind's rows that sort before it.</summary>
        private int InsertionIndex(string kind, string locale)
        {
            var kindIndex = KindNames.ToList().IndexOf(kind);
            var index = 0;
            for (var i = 0; i < Languages.Count; i++)
            {
                var other = Languages[i];
                var otherKind = KindNames.ToList().IndexOf(other.Kind);
                if (otherKind < kindIndex || (otherKind == kindIndex && string.CompareOrdinal(other.LocaleKey, locale) < 0))
                {
                    index = i + 1;
                }
            }

            return index;
        }

        // ---- the file ----------------------------------------------------------------------------

        /// <summary>
        /// Takes a developer's file: parsed and merged over the shipped default here, so a
        /// file that does not validate is refused with its reason and the page keeps what it
        /// had. The text is stored as given; export writes it back unchanged until an edit.
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

            Load(text, fileName ?? string.Empty, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            ImportError = null;
            return true;
        }

        public void RemoveDictionary()
        {
            Load(string.Empty, string.Empty, string.Empty);
            ImportError = null;
        }

        /// <summary>The developer's file template, embedded in the plugin.</summary>
        public static string TemplateText()
        {
            using (var stream = typeof(GrammarDictionaryViewModel).Assembly.GetManifestResourceStream(Constants.GrammarTemplateResource))
            {
                if (stream == null) throw new InvalidOperationException("The grammar dictionary template is missing from the plugin.");
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>Loads the stored file, or none. Rows are rebuilt from the effective dictionary.</summary>
        public void Load(string text, string name, string importedAt)
        {
            _loading = true;
            try
            {
                _text = text ?? string.Empty;
                _name = name ?? string.Empty;
                _importedAt = importedAt ?? string.Empty;
                _edited = false;
                _document = _text.Trim().Length > 0 ? TryParse(_text) : null;
                _otherText = _document != null && _document[OtherKey] is JValue other ? (string)other ?? string.Empty : string.Empty;
                Validate();
                MarkError(ErrorAreaFile, null);
                BuildRows();
                RaiseStatus();
                Raise(nameof(OtherText));
            }
            finally
            {
                _loading = false;
            }
        }

        // ---- edits from the rows -------------------------------------------------------------

        internal void KeysEdited(GrammarLanguageRow row)
        {
            if (_loading) return;
            var keys = ParseKeys(row.KeysText);
            var languages = Ensure(Ensure(Ensure(Document(), KeysKey), row.Kind), LanguagesKey);

            GrammarKind kind;
            GrammarLanguage shipped;
            var isDefault = GrammarDictionary.Default.Kinds.TryGetValue(row.Kind, out kind)
                && kind.Languages.TryGetValue(row.LocaleKey, out shipped)
                && shipped.Keys.SequenceEqual(keys, StringComparer.Ordinal)
                && !HasRenamesFor(row.Kind);
            if (isDefault) languages.Remove(row.LocaleKey);
            else languages[row.LocaleKey] = new JArray(keys.Cast<object>().ToArray());

            AfterEdit();
            MarkError(ErrorAreaLanguages, row);
            row.SetIsProject(!isDefault);
            if (IsValid) row.SetDetail(Describe(row.Kind, row.LocaleKey));
        }

        internal void ResetLanguage(GrammarLanguageRow row)
        {
            var languages = _document == null ? null : (_document[KeysKey] as JObject)?[row.Kind] is JObject kind ? kind[LanguagesKey] as JObject : null;
            if (languages != null) languages.Remove(row.LocaleKey);
            AfterEdit();
            MarkError(ErrorAreaLanguages, row);

            if (row.IsAddedByProject)
            {
                Languages.Remove(row);
                return;
            }

            if (!IsValid) return;
            var language = Resolve(row.Kind, row.LocaleKey);
            row.SetKeysSilently(string.Join(", ", language.Keys));
            row.SetIsProject(false);
            row.SetDetail(Describe(row.Kind, row.LocaleKey));
        }

        internal void ArgumentRenamed(GrammarArgumentRow row, string previous)
        {
            if (_loading) return;
            var name = (row.BaseName ?? string.Empty).Trim();
            var arguments = Ensure(Document(), ArgumentsKey);

            if (name.Length == 0)
            {
                Error = string.Format(CultureInfo.CurrentCulture, UIStrings.Grammar_Invalid, UIStrings.Grammar_EmptyArgument);
                MarkError(ErrorAreaArguments, row);
                return;
            }

            if (name != previous && arguments[name] != null)
            {
                Error = string.Format(CultureInfo.CurrentCulture, UIStrings.Grammar_Invalid,
                    string.Format(CultureInfo.CurrentCulture, UIStrings.Grammar_DuplicateArgument, name));
                MarkError(ErrorAreaArguments, row);
                return;
            }

            // Rebuilt in order rather than removed and re-added, so the entry keeps its place.
            var entries = arguments.Properties().ToList();
            arguments.RemoveAll();
            foreach (var entry in entries)
            {
                arguments.Add(entry.Name == previous ? name : entry.Name, entry.Value);
            }

            row.SetStoredName(name);
            AfterEdit();
            MarkError(ErrorAreaArguments, row);
        }

        internal void RoleEdited(GrammarArgumentRow row, GrammarRoleCell cell)
        {
            if (_loading) return;
            var entry = Ensure(Ensure(Document(), ArgumentsKey), row.StoredName);
            var name = (cell.ArgumentName ?? string.Empty).Trim();
            if (name.Length == 0) entry.Remove(cell.Role);
            else entry[cell.Role] = name;
            AfterEdit();
            MarkError(ErrorAreaArguments, row);
        }

        internal void SuppliesEdited(GrammarArgumentRow row)
        {
            if (_loading) return;
            var entry = Ensure(Ensure(Document(), ArgumentsKey), row.StoredName);
            var supplies = ParseSupplies(row.SuppliesText);
            if (supplies.Count == 0) entry.Remove(SuppliesKey);
            else
            {
                var value = new JObject();
                foreach (var pair in supplies) value[pair.Key] = new JArray(pair.Value.Cast<object>().ToArray());
                entry[SuppliesKey] = value;
            }

            AfterEdit();
            MarkError(ErrorAreaArguments, row);
        }

        public GrammarArgumentRow AddArgument()
        {
            var arguments = Ensure(Document(), ArgumentsKey);
            var name = UIStrings.Grammar_NewArgumentName;
            for (var n = 2; arguments[name] != null; n++)
            {
                name = UIStrings.Grammar_NewArgumentName + n.ToString(CultureInfo.InvariantCulture);
            }

            arguments[name] = new JObject();
            AfterEdit();
            MarkError(ErrorAreaArguments, null);
            var row = ArgumentRow(name, (JObject)arguments[name]);
            Arguments.Add(row);
            return row;
        }

        public void RemoveArgument(GrammarArgumentRow row)
        {
            var arguments = _document == null ? null : _document[ArgumentsKey] as JObject;
            if (arguments != null) arguments.Remove(row.StoredName);
            Arguments.Remove(row);
            AfterEdit();
            MarkError(ErrorAreaArguments, null);
        }

        // ---- internals -------------------------------------------------------------------------

        /// <summary>The document the edits go into; made on the first edit where there is no file.</summary>
        private JObject Document()
        {
            if (_document == null)
            {
                _document = new JObject { [VersionKey] = DeveloperFile.CurrentVersion };
            }

            return _document;
        }

        private static JObject Ensure(JObject parent, string key)
        {
            var child = parent[key] as JObject;
            if (child == null)
            {
                child = new JObject();
                parent[key] = child;
            }

            return child;
        }

        /// <summary>After an edit: the text is the document, validated; the status and the renames line follow.</summary>
        private void AfterEdit()
        {
            _edited = true;
            PruneKeys();
            _text = _document == null ? string.Empty : _document.ToString(Formatting.Indented);
            Validate();
            RaiseStatus();
        }

        /// <summary>Drops the empty objects a reset leaves under <c>keys</c>, so the file reads as if they were never there.</summary>
        private void PruneKeys()
        {
            var keys = _document == null ? null : _document[KeysKey] as JObject;
            if (keys == null) return;

            foreach (var kind in keys.Properties().ToList())
            {
                var entry = kind.Value as JObject;
                if (entry == null) continue;
                foreach (var child in entry.Properties().ToList())
                {
                    if (child.Value is JObject inner && !inner.HasValues) entry.Remove(child.Name);
                }

                if (!entry.HasValues) keys.Remove(kind.Name);
            }

            if (!keys.HasValues) _document.Remove(KeysKey);
        }

        private void Validate()
        {
            if (!HasDictionary)
            {
                _effective = GrammarDictionary.Default;
                Error = null;
                return;
            }

            try
            {
                _effective = GrammarDictionary.Default.WithDeveloperFile(DeveloperFile.Parse(_text));
                Error = null;
            }
            catch (GrammarDictionaryException exception)
            {
                Error = string.Format(CultureInfo.CurrentCulture, UIStrings.Grammar_Invalid, exception.Message);
            }
        }

        private void RaiseStatus()
        {
            Raise(nameof(HasDictionary));
            Raise(nameof(DictionaryStatus));
            Raise(nameof(DictionaryText));
            Raise(nameof(DictionaryName));
            Raise(nameof(DictionaryImportedAt));
            Raise(nameof(RenamesLine));
            Raise(nameof(HasRenames));
        }

        private void BuildRows()
        {
            Languages.Clear();
            foreach (var kind in KindNames)
            {
                var locales = new SortedSet<string>(StringComparer.Ordinal);
                GrammarKind shipped;
                if (GrammarDictionary.Default.Kinds.TryGetValue(kind, out shipped))
                {
                    foreach (var locale in shipped.Languages.Keys) locales.Add(locale);
                }

                var overrides = OverridesFor(kind);
                if (overrides != null)
                {
                    foreach (var property in overrides.Properties()) locales.Add(property.Name);
                }

                foreach (var locale in locales) Languages.Add(LanguageRow(kind, locale));
            }

            Arguments.Clear();
            var arguments = _document == null ? null : _document[ArgumentsKey] as JObject;
            if (arguments != null)
            {
                foreach (var property in arguments.Properties())
                {
                    Arguments.Add(ArgumentRow(property.Name, property.Value as JObject ?? new JObject()));
                }
            }
        }

        private GrammarLanguageRow LanguageRow(string kind, string locale)
        {
            var language = Resolve(kind, locale);
            var overrides = OverridesFor(kind);
            GrammarKind shipped;
            var inDefault = GrammarDictionary.Default.Kinds.TryGetValue(kind, out shipped) && shipped.Languages.ContainsKey(locale);
            var keys = language != null ? string.Join(", ", language.Keys)
                : overrides != null && overrides[locale] is JArray array ? string.Join(", ", array.Select(item => (string)item))
                : string.Empty;

            return new GrammarLanguageRow(this, kind, locale, DisplayName(locale), keys,
                overrides != null && overrides[locale] != null, !inDefault, Describe(kind, locale));
        }

        private GrammarArgumentRow ArgumentRow(string name, JObject entry)
        {
            var roles = RoleNames.Select(role => new GrammarRoleCell(role, entry[role] is JValue value ? (string)value ?? string.Empty : string.Empty)).ToList();
            var supplies = entry[SuppliesKey] as JObject;
            var suppliesText = supplies == null ? string.Empty : string.Join("; ", supplies.Properties().Select(language =>
                language.Name + ": " + string.Join(", ", (language.Value as JArray ?? new JArray()).Select(item => (string)item))));
            var keys = entry[KeysKey] as JObject;
            var respelling = keys == null ? string.Empty : string.Join("; ", keys.Properties().Select(kind =>
                kind.Name + ": " + string.Join(", ", (kind.Value as JObject ?? new JObject()).Properties().Select(pair =>
                    string.Format(CultureInfo.CurrentCulture, UIStrings.Grammar_RenameAs, pair.Name, (string)pair.Value)))));

            return new GrammarArgumentRow(this, name, roles, suppliesText, respelling);
        }

        private JObject OverridesFor(string kind)
        {
            var keys = _document == null ? null : _document[KeysKey] as JObject;
            var entry = keys == null ? null : keys[kind] as JObject;
            return entry == null ? null : entry[LanguagesKey] as JObject;
        }

        private bool HasRenamesFor(string kind)
        {
            var keys = _document == null ? null : _document[KeysKey] as JObject;
            var entry = keys == null ? null : keys[kind] as JObject;
            var rename = entry == null ? null : entry[RenameKey] as JObject;
            return rename != null && rename.HasValues;
        }

        /// <summary>The language's entry in the effective dictionary, or null where the file does not validate or the entry is unknown.</summary>
        private GrammarLanguage Resolve(string kind, string locale)
        {
            GrammarKind found;
            GrammarLanguage language;
            return _effective.Kinds.TryGetValue(kind, out found) && found.Languages.TryGetValue(locale, out language) ? language : null;
        }

        /// <summary>The tooltip: each key with its hint and sample, then the dependent kinds the language needs.</summary>
        private string Describe(string kind, string locale)
        {
            var language = Resolve(kind, locale);
            if (language == null) return string.Empty;

            var builder = new StringBuilder();
            foreach (var key in language.Keys)
            {
                if (builder.Length > 0) builder.AppendLine();
                var sample = language.Sample(key);
                builder.Append(sample != null
                    ? string.Format(CultureInfo.CurrentCulture, UIStrings.Grammar_KeyWithSample, key, language.Hint(key), sample)
                    : string.Format(CultureInfo.CurrentCulture, UIStrings.Grammar_KeyHint, key, language.Hint(key)));
            }

            foreach (var need in language.Needs)
            {
                builder.AppendLine();
                builder.Append(string.Format(CultureInfo.CurrentCulture, UIStrings.Grammar_Needs, need.Kind, string.Join(", ", need.Under)));
            }

            return builder.ToString();
        }

        private static string DisplayName(string locale)
        {
            try
            {
                return CultureInfo.GetCultureInfo(locale).DisplayName;
            }
            catch (CultureNotFoundException)
            {
                return locale;
            }
        }

        private static JObject TryParse(string text)
        {
            try
            {
                using (var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None })
                {
                    return JObject.Load(reader);
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static List<string> ParseKeys(string text)
        {
            return (text ?? string.Empty).Split(',', ';', ' ', '\t')
                .Select(key => key.Trim())
                .Where(key => key.Length > 0)
                .ToList();
        }

        /// <summary>"fr: article; it: article" to a map; an entry without a colon or without kinds is dropped.</summary>
        public static List<KeyValuePair<string, List<string>>> ParseSupplies(string text)
        {
            var result = new List<KeyValuePair<string, List<string>>>();
            foreach (var entry in (text ?? string.Empty).Split(';'))
            {
                var colon = entry.IndexOf(':');
                if (colon < 0) continue;
                var language = entry.Substring(0, colon).Trim();
                var kinds = ParseKeys(entry.Substring(colon + 1));
                if (language.Length == 0 || kinds.Count == 0) continue;
                result.Add(new KeyValuePair<string, List<string>>(language, kinds));
            }

            return result;
        }
    }

    /// <summary>One language of one kind: its keys, editable, and where they come from.</summary>
    public sealed class GrammarLanguageRow : ObservableObject
    {
        private readonly GrammarDictionaryViewModel _owner;
        private string _keysText;
        private bool _isProject;
        private string _detail;
        private bool _hasError;

        internal GrammarLanguageRow(GrammarDictionaryViewModel owner, string kind, string localeKey, string languageName,
            string keysText, bool isProject, bool isAddedByProject, string detail)
        {
            _owner = owner;
            Kind = kind;
            LocaleKey = localeKey;
            LanguageName = languageName;
            _keysText = keysText;
            _isProject = isProject;
            IsAddedByProject = isAddedByProject;
            _detail = detail;
        }

        public string Kind { get; }

        public string LocaleKey { get; }

        public string LanguageName { get; }

        /// <summary>The language as shown: its name and CLDR key.</summary>
        public string Language
        {
            get { return LanguageName == LocaleKey ? LocaleKey : LanguageName + " (" + LocaleKey + ")"; }
        }

        /// <summary>The keys in layout order, comma-separated; edited here, written to the file.</summary>
        public string KeysText
        {
            get { return _keysText; }
            set
            {
                if (Set(ref _keysText, value ?? string.Empty)) _owner.KeysEdited(this);
            }
        }

        /// <summary>True where the project's file sets this language's keys.</summary>
        public bool IsProject
        {
            get { return _isProject; }
        }

        /// <summary>True where the shipped default has no entry for the language: the project added it.</summary>
        public bool IsAddedByProject { get; }

        public string Source
        {
            get { return _isProject ? UIStrings.Grammar_SourceProject : UIStrings.Grammar_SourceDefault; }
        }

        /// <summary>The tooltip: hints, samples and dependent kinds.</summary>
        public string Detail
        {
            get { return _detail; }
        }

        /// <summary>True while this row's keys are what the dictionary refuses; the cell shows a red border.</summary>
        public bool HasError
        {
            get { return _hasError; }
        }

        public void Reset()
        {
            _owner.ResetLanguage(this);
        }

        internal void SetHasError(bool value)
        {
            Set(ref _hasError, value, nameof(HasError));
        }

        internal void SetIsProject(bool value)
        {
            if (Set(ref _isProject, value, nameof(IsProject))) Raise(nameof(Source));
        }

        internal void SetKeysSilently(string keys)
        {
            _keysText = keys;
            Raise(nameof(KeysText));
        }

        internal void SetDetail(string detail)
        {
            Set(ref _detail, detail, nameof(Detail));
        }
    }

    /// <summary>The argument the application passes for one role of a base argument.</summary>
    public sealed class GrammarRoleCell : ObservableObject
    {
        private string _argumentName;

        internal GrammarRoleCell(string role, string argumentName)
        {
            Role = role;
            _argumentName = argumentName;
        }

        internal GrammarArgumentRow Row { get; set; }

        /// <summary>The kind name, or "count".</summary>
        public string Role { get; }

        public string ArgumentName
        {
            get { return _argumentName; }
            set
            {
                if (Set(ref _argumentName, value ?? string.Empty) && Row != null) Row.RoleEdited(this);
            }
        }
    }

    /// <summary>One base argument of the developer's file.</summary>
    public sealed class GrammarArgumentRow : ObservableObject
    {
        private readonly GrammarDictionaryViewModel _owner;
        private string _baseName;
        private string _suppliesText;
        private bool _hasError;

        internal GrammarArgumentRow(GrammarDictionaryViewModel owner, string baseName, IReadOnlyList<GrammarRoleCell> roles, string suppliesText, string respelling)
        {
            _owner = owner;
            StoredName = baseName;
            _baseName = baseName;
            Roles = roles;
            foreach (var role in roles) role.Row = this;
            _suppliesText = suppliesText;
            Respelling = respelling;
        }

        /// <summary>The name the file holds, which <see cref="BaseName"/> may be about to change.</summary>
        public string StoredName { get; private set; }

        public string BaseName
        {
            get { return _baseName; }
            set
            {
                var previous = StoredName;
                if (Set(ref _baseName, value ?? string.Empty)) _owner.ArgumentRenamed(this, previous);
            }
        }

        public IReadOnlyList<GrammarRoleCell> Roles { get; }

        /// <summary>The dependent kinds supplied per language, "fr: article; it: article".</summary>
        public string SuppliesText
        {
            get { return _suppliesText; }
            set
            {
                if (Set(ref _suppliesText, value ?? string.Empty)) _owner.SuppliesEdited(this);
            }
        }

        /// <summary>The argument's own key spelling, read-only; empty where it has none.</summary>
        public string Respelling { get; }

        public bool HasRespelling
        {
            get { return Respelling.Length > 0; }
        }

        /// <summary>True while this row is what the dictionary refuses; its cells show a red border.</summary>
        public bool HasError
        {
            get { return _hasError; }
        }

        public void Remove()
        {
            _owner.RemoveArgument(this);
        }

        internal void SetHasError(bool value)
        {
            Set(ref _hasError, value, nameof(HasError));
        }

        internal void RoleEdited(GrammarRoleCell cell)
        {
            _owner.RoleEdited(this, cell);
        }

        internal void SetStoredName(string name)
        {
            StoredName = name;
        }
    }
}
