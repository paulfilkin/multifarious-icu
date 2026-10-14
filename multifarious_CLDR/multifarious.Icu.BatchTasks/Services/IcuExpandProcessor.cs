using System;
using System.Collections.Generic;
using Icu.Cldr;
using multifarious.Icu.Expansion;
using Sdl.Core.Globalization;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>A paragraph unit the expansion left untouched, and why. Becomes a comment on the unit and a line in the report.</summary>
    public sealed class ExpansionWarning
    {
        public ExpansionWarning(string paragraphUnitId, string reason)
        {
            ParagraphUnitId = paragraphUnitId;
            Reason = reason;
        }

        public string ParagraphUnitId { get; }

        public string Reason { get; }
    }

    /// <summary>A parse failure under the fail-task setting: the run stops, naming the unit and the offset.</summary>
    public sealed class ExpansionFailedException : Exception
    {
        public ExpansionFailedException(string paragraphUnitId, string reason, int? offset)
            : base("Paragraph unit " + paragraphUnitId + ": " + reason
                   + (offset.HasValue ? " (offset " + offset.Value + ")" : string.Empty))
        {
            ParagraphUnitId = paragraphUnitId;
        }

        public string ParagraphUnitId { get; }
    }

    /// <summary>
    /// The expand task's engine, as a bilingual content processor: for every translatable
    /// paragraph unit, reconstruct the raw value, classify it, plan the expansion for the file's
    /// target language and rewrite the unit. Structure units, empty values, non-ICU values and
    /// units this plugin has already expanded are left exactly as they were.
    ///
    /// A unit the restore task has put back together is expanded again, with each form given
    /// the translation and the state it had before, so expand and restore can alternate without
    /// loss. A unit that arrives pre-translated as a whole message has that message split into
    /// its forms the same way. Either is kept as it stands when the task is set to keep matches
    /// at or above its threshold and every state the unit holds, its segments' or the forms'
    /// the restore recorded, is at that match (Paul, 11 and 13 October 2026).
    /// </summary>
    public class IcuExpandProcessor : AbstractBilingualContentProcessor
    {
        private readonly string _sourceLanguageTag;
        private readonly string _targetLanguageTag;
        private readonly ExpansionOptions _options;
        private readonly string _appVersion;
        private readonly CldrPlurals _plurals;
        private readonly ExpansionPlanner _planner;
        private readonly List<ExpansionWarning> _warnings = new List<ExpansionWarning>();
        private readonly List<ExpandUnitOutcome> _outcomes = new List<ExpandUnitOutcome>();

        private ExpansionWriter _writer;
        private string _fileSourceLanguage;
        private string _fileTargetLanguage;

        /// <summary>The next segment number in the file; see <see cref="SegmentNumbering"/>.</summary>
        private int _nextSegmentNumber = 1;

        /// <param name="sourceLanguageTag">The project's source language, or null to take it from the file.</param>
        /// <param name="targetLanguageTag">The file's target language, or null to take it from the file.</param>
        /// <param name="dictionary">The grammar dictionary in force, or null to walk every select as the source wrote it.</param>
        public IcuExpandProcessor(string sourceLanguageTag, string targetLanguageTag, ExpansionOptions options,
            string appVersion, GrammarDictionary dictionary = null)
        {
            _sourceLanguageTag = sourceLanguageTag;
            _targetLanguageTag = targetLanguageTag;
            _options = options ?? ExpansionOptions.Default;
            _appVersion = appVersion ?? string.Empty;
            _plurals = CldrPlurals.Default;
            _planner = new ExpansionPlanner(_plurals, GrammaticalHints.Embedded, dictionary);
        }

        private readonly List<ExpansionWarning> _notes = new List<ExpansionWarning>();

        /// <summary>
        /// What the planner could not do with a grammar select and the translator should know:
        /// a dependent form the language needs that the application does not supply, or a
        /// target language the dictionary does not know. Not warnings: the unit was expanded.
        /// </summary>
        public IReadOnlyList<ExpansionWarning> Notes { get { return _notes; } }

        /// <summary>Translatable units seen.</summary>
        public int Units { get; private set; }

        /// <summary>Units rewritten.</summary>
        public int Expanded { get; private set; }

        /// <summary>Units skipped because this plugin had already expanded them.</summary>
        public int AlreadyExpanded { get; private set; }

        /// <summary>Units left as they stood because their translation was already matched at the keep threshold or better.</summary>
        public int Kept { get; private set; }

        /// <summary>Units expanded again after a restore, with their translations handed back to the forms.</summary>
        public int Reexpanded { get; private set; }

        public IReadOnlyList<ExpansionWarning> Warnings { get { return _warnings; } }

        /// <summary>What happened to every translatable unit that held ICU, in document order, for the report.</summary>
        public IReadOnlyList<ExpandUnitOutcome> Outcomes { get { return _outcomes; } }

        public override void SetFileProperties(IFileProperties fileInfo)
        {
            base.SetFileProperties(fileInfo);

            // The languages Studio recorded when it converted the file, used when the task did not
            // pass any. The YAML plugin has seen these arrive empty in a project, so the task's
            // own knowledge is preferred where it exists.
            var conversion = fileInfo == null ? null : fileInfo.FileConversionProperties;
            _fileSourceLanguage = LanguageTagOf(conversion == null ? null : conversion.SourceLanguage);
            _fileTargetLanguage = LanguageTagOf(conversion == null ? null : conversion.TargetLanguage);
        }

        public override void ProcessParagraphUnit(IParagraphUnit paragraphUnit)
        {
            if (paragraphUnit != null && !paragraphUnit.IsStructure && paragraphUnit.Source != null)
            {
                Expand(paragraphUnit);

                // The numbers run on through the file. A unit the writer laid out already
                // carries them, since the writer started from the same count; the rest are
                // renumbered here so the sequence has no gaps and no restarts.
                SegmentNumbering.Renumber(paragraphUnit, ref _nextSegmentNumber);
            }

            base.ProcessParagraphUnit(paragraphUnit);
        }

        private void Expand(IParagraphUnit unit)
        {
            Units++;
            var unitId = unit.Properties.ParagraphUnitId.Id;

            var key = ResourceKey.Of(unit);
            var icuContext = ResourceKey.IcuContextOf(unit);
            var restored = icuContext != null && ResourceKey.IsRestored(unit);
            if (icuContext != null && !restored)
            {
                AlreadyExpanded++;
                _outcomes.Add(new ExpandUnitOutcome(unitId, key, ExpandOutcome.Skipped, CountSegments(unit.Source), null));
                Diagnostics.Write("  unit " + unitId + ": already expanded, skipped");
                return;
            }

            // The value with the filter's tags, if any, as sentinel characters: the parser and
            // the planner see them as text, and the writer puts a clone of each tag back.
            var sourceValue = RawValueReconstruction.Encode(unit.Source);
            var raw = sourceValue.RawValue;
            if (raw.Length == 0) return;

            // What the unit already holds for its forms: the record a restore left, or the
            // translation and match a pre-translated whole message arrived with. The states
            // are also what the keep rule judges, once the value is known to be a message the
            // task would lay out: a value with no ICU is never kept or locked (Project 61,
            // 13 October 2026, where two comment lines matched at 100 were).
            var targetValue = unit.Target == null ? null : RawValueReconstruction.Encode(unit.Target);
            var targetRaw = targetValue == null ? string.Empty : targetValue.RawValue;
            var seed = new ExpansionSeed { FirstSegmentNumber = _nextSegmentNumber };
            if (sourceValue.HasTags) seed.SourceTags = sourceValue;
            var states = new List<SegmentState>();
            if (restored)
            {
                seed.OriginalPattern = Metadata(icuContext, "icu:pattern");
                var recorded = SegmentStates.Parse(Metadata(icuContext, Constants.SegmentStatesKey));
                seed.States = recorded;
                if (recorded != null) states.AddRange(recorded.Values);
            }
            else if (!string.IsNullOrWhiteSpace(targetRaw))
            {
                states = TargetStates(unit);
                seed.StateForAll = StateForAll(states);
            }

            var classification = MessageClassifier.Classify(raw, _options);
            switch (classification.Kind)
            {
                case MessageKind.NoIcu:
                    return;

                case MessageKind.PassThrough:
                    if (classification.ErrorOffset.HasValue && _options.OnParseError == ParseErrorBehaviour.FailTask)
                    {
                        throw new ExpansionFailedException(unitId, classification.Reason, classification.ErrorOffset);
                    }

                    Warn(unit, unitId, classification.Reason);
                    _outcomes.Add(new ExpandUnitOutcome(unitId, key, ExpandOutcome.PassedThrough, 0, classification.Reason));
                    return;

                case MessageKind.Protect:
                    if (ShouldKeep(states))
                    {
                        Keep(unit, unitId, key);
                        return;
                    }
                    ReadTargetForms(unitId, targetValue, seed);
                    WriteResult protected_;
                    if (!TryWrite(unit, unitId, key, _planner.PlanProtected(classification), seed, out protected_)) return;
                    Record(unitId, key, ExpandOutcome.Protected, restored, protected_, seed);
                    Diagnostics.Write("  unit " + unitId + ": arguments protected" + TagNote(protected_));
                    return;

                case MessageKind.Expand:
                    var targetLanguage = _targetLanguageTag ?? _fileTargetLanguage;
                    var sourceLanguage = _sourceLanguageTag ?? _fileSourceLanguage ?? "en";
                    if (string.IsNullOrEmpty(targetLanguage))
                    {
                        throw new InvalidOperationException(
                            "The target language is unknown: neither the task nor the file supplied one.");
                    }

                    if (ShouldKeep(states))
                    {
                        Keep(unit, unitId, key);
                        return;
                    }
                    ReadTargetForms(unitId, targetValue, seed);
                    var plan = _planner.Plan(classification, sourceLanguage, new[] { targetLanguage }, _options);
                    if (plan.ExceedsBudget)
                    {
                        LayOutWalked(unit, unitId, key, classification, plan, sourceLanguage, targetLanguage, restored, seed);
                        return;
                    }

                    WriteResult written;
                    if (!TryWrite(unit, unitId, key, plan, seed, out written)) return;
                    foreach (var note in plan.Notes)
                    {
                        _notes.Add(new ExpansionWarning(unitId, note));
                        Diagnostics.Write("  unit " + unitId + ": note: " + note);
                    }
                    Record(unitId, key, ExpandOutcome.Expanded, restored, written, seed, plan.Notes);
                    Diagnostics.Write("  unit " + unitId + ": expanded to " + written.Segments + " segments for " + targetLanguage
                        + " numbered from " + seed.FirstSegmentNumber
                        + (seed.TargetForms != null ? ", " + written.TargetFormsFilled + " forms filled from the target" : string.Empty)
                        + (written.CommentsCarried > 0 ? ", " + written.CommentsCarried + " comment markers carried" : string.Empty)
                        + TagNote(written));
                    return;
            }
        }

        /// <summary>
        /// Writes the plan, or leaves the unit as it stands where a filter tag pair would be
        /// split by the layout (<see cref="TagPairSplitException"/>): the unit gets the
        /// warning, as a pass-through does, and nothing in it is changed.
        /// </summary>
        private bool TryWrite(IParagraphUnit unit, string unitId, string key, ExpansionPlan plan, ExpansionSeed seed,
            out WriteResult written)
        {
            try
            {
                written = Writer.Write(unit, plan, key, seed);
                return true;
            }
            catch (TagPairSplitException exception)
            {
                written = null;
                Warn(unit, unitId, exception.Message);
                _outcomes.Add(new ExpandUnitOutcome(unitId, key, ExpandOutcome.PassedThrough, 0, exception.Message));
                return false;
            }
        }

        private static string TagNote(WriteResult written)
        {
            return written.TagsCloned > 0 ? ", " + written.TagsCloned + " filter tags kept" : string.Empty;
        }

        /// <summary>
        /// Reads the target message into forms for the seed, with the ranges of its comment
        /// markers so the writer can put each back around the text it wrapped, and the table
        /// of its filter tags; a target that does not read leaves the forms empty and is
        /// reported.
        /// </summary>
        private static void ReadTargetForms(string unitId, EncodedValue target, ExpansionSeed seed)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.RawValue)) return;

            string failure;
            seed.TargetForms = TargetBranches.Read(target.RawValue, out failure);
            if (seed.TargetForms == null)
            {
                seed.TargetFailure = "The target held a message that could not be split into forms (" + failure + "); the forms start empty.";
                Diagnostics.Write("  unit " + unitId + ": " + seed.TargetFailure);
                return;
            }
            seed.TargetComments = target.Comments;
            if (target.HasTags) seed.TargetTags = target;
        }

        /// <summary>
        /// Records a written unit, with the seed's shortfalls as the detail: forms the target
        /// message had nothing for, or a target that did not read. Those go to the report and
        /// the task results, not onto the unit; the forms concerned are simply empty.
        /// </summary>
        private void Record(string unitId, string key, ExpandOutcome outcome, bool restored, WriteResult written, ExpansionSeed seed,
            IReadOnlyList<string> notes = null)
        {
            Expanded++;
            if (restored) Reexpanded++;

            string detail = null;
            if (seed.TargetFailure != null)
            {
                detail = seed.TargetFailure;
            }
            else if (written.UnfilledPaths.Count > 0)
            {
                detail = "The target message had no translation for " + written.UnfilledPaths.Count + " of "
                    + written.Segments + " forms (" + string.Join(", ", written.UnfilledPaths) + "); those start empty.";
            }

            if (detail != null)
            {
                _warnings.Add(new ExpansionWarning(unitId, detail));
                Diagnostics.Write("  unit " + unitId + ": " + detail);
            }

            _outcomes.Add(new ExpandUnitOutcome(unitId, key, outcome, written.Segments, detail, notes));
        }

        // ---- pre-translated messages ----------------------------------------------------------

        /// <summary>The state of every target segment of a never-expanded unit, paths empty.</summary>
        private static List<SegmentState> TargetStates(IParagraphUnit unit)
        {
            var states = new List<SegmentState>();
            foreach (var segment in SegmentsOf(unit.Target))
            {
                states.Add(SegmentState.Capture(string.Empty, segment.Properties));
            }
            return states;
        }

        /// <summary>
        /// Whether a message stays as it is: the task is set to keep, and every state it holds
        /// is matched at the threshold or better. For a pre-translated whole message the states
        /// are its segments'; for a restored message they are the forms' states the restore
        /// recorded, so a message whose every form was matched stays restored (Paul, 13 October
        /// 2026). One below, or with no origin at all, or no states at all, and the message is
        /// expanded like any other.
        /// </summary>
        private bool ShouldKeep(List<SegmentState> states)
        {
            if (!_options.KeepTranslated || states.Count == 0) return false;
            foreach (var state in states)
            {
                if (!state.HasOrigin || state.EffectiveMatch < _options.KeepMinimumMatch) return false;
            }
            return true;
        }

        /// <summary>Leaves the unit as it stands, its context included, locking its target segments where the task says so.</summary>
        private void Keep(IParagraphUnit unit, string unitId, string key)
        {
            var segments = SegmentsOf(unit.Target);
            if (_options.LockKept)
            {
                foreach (var segment in segments)
                {
                    segment.Properties.IsLocked = true;
                }
            }

            Kept++;
            _outcomes.Add(new ExpandUnitOutcome(unitId, key, ExpandOutcome.Kept, segments.Count, null));
            Diagnostics.Write("  unit " + unitId + ": kept, already translated at " + _options.KeepMinimumMatch
                + "% or better" + (_options.LockKept ? ", locked" : string.Empty));
        }

        /// <summary>
        /// The state every form of a pre-translated message starts with: the message's own where
        /// its segments agree on it, else a draft with no origin, since one match figure cannot
        /// stand for several. The lock is not carried: a locked whole message is the keep rule's
        /// business, and a message being expanded is one the translator is to work on.
        /// </summary>
        private static SegmentState StateForAll(List<SegmentState> states)
        {
            if (states.Count == 0) return null;

            var first = states[0];
            var uniform = true;
            foreach (var state in states)
            {
                if (state.ConfirmationLevel != first.ConfirmationLevel || state.OriginType != first.OriginType
                    || state.OriginSystem != first.OriginSystem || state.MatchPercent != first.MatchPercent
                    || state.TextContextMatchLevel != first.TextContextMatchLevel)
                {
                    uniform = false;
                    break;
                }
            }

            return uniform
                ? new SegmentState(string.Empty, first.ConfirmationLevel, false, first.OriginType, first.OriginSystem,
                    first.MatchPercent, first.TextContextMatchLevel)
                : new SegmentState(string.Empty, ConfirmationLevel.Draft, false, null, null, 0, TextContextMatchLevel.None);
        }

        private static List<ISegment> SegmentsOf(IAbstractMarkupDataContainer container)
        {
            var segments = new List<ISegment>();
            if (container == null) return segments;
            for (var i = 0; i < container.Count; i++)
            {
                var segment = container[i] as ISegment;
                if (segment != null)
                {
                    segments.Add(segment);
                    continue;
                }

                if (container[i] is ILockedContent) continue;
                var nested = container[i] as IAbstractMarkupDataContainer;
                if (nested != null) segments.AddRange(SegmentsOf(nested));
            }
            return segments;
        }

        private static string Metadata(IContextInfo context, string key)
        {
            return context != null && context.MetaDataContainsKey(key) ? context.GetMetaData(key) : null;
        }

        private static int CountSegments(IAbstractMarkupDataContainer container)
        {
            var count = 0;
            for (var i = 0; i < container.Count; i++)
            {
                if (container[i] is ISegment) count++;
                else if (container[i] is IAbstractMarkupDataContainer nested) count += CountSegments(nested);
            }
            return count;
        }

        /// <summary>
        /// A message over the branch budget is laid out all the same, with every selector walked:
        /// the syntax is protected and each branch is a whole sentence, but no category is
        /// expanded, so the branches stay the source's and the target language may lack forms it
        /// needs. The unit says so in a comment and in the report. Design 5.7 passed such a
        /// message through; Project 41 (6 September 2026) showed a passed-through value garbled
        /// in translation like any plain text.
        /// </summary>
        private void LayOutWalked(IParagraphUnit unit, string unitId, string key, MessageClassification classification,
            ExpansionPlan rejected, string sourceLanguage, string targetLanguage, bool restored, ExpansionSeed seed)
        {
            var size = rejected.Segments.Count + " segments needed, " + rejected.MaxUnitsPerMessage + " allowed";
            var plan = _planner.PlanWalked(classification, sourceLanguage, new[] { targetLanguage }, _options,
                "over the branch budget: " + size);
            if (plan.ExceedsBudget)
            {
                var reason = "Expansion is over the branch budget (" + size
                    + ") and so are the source's own branches (" + plan.Segments.Count + "); passed through unexpanded.";
                Warn(unit, unitId, reason);
                _outcomes.Add(new ExpandUnitOutcome(unitId, key, ExpandOutcome.PassedThrough, 0, reason));
                return;
            }

            WriteResult written;
            if (!TryWrite(unit, unitId, key, plan, seed, out written)) return;
            Expanded++;
            if (restored) Reexpanded++;
            var detail = "Expansion is over the branch budget (" + size
                + "); the branches are the source's and " + targetLanguage + " may need forms the source does not have.";
            Warn(unit, unitId, detail, "ICU message laid out without category expansion: ");
            if (seed.TargetFailure != null)
            {
                detail += " " + seed.TargetFailure;
                _warnings.Add(new ExpansionWarning(unitId, seed.TargetFailure));
            }
            _outcomes.Add(new ExpandUnitOutcome(unitId, key, ExpandOutcome.Walked, written.Segments, detail));
        }

        private ExpansionWriter Writer
        {
            get
            {
                if (_writer == null)
                {
                    _writer = new ExpansionWriter(ItemFactory, PropertiesFactory, _options.LockPlaceholders,
                        _plurals.CldrVersion, _appVersion);
                }
                return _writer;
            }
        }

        /// <summary>
        /// Records why a unit was left alone, as a comment on the paragraph unit so the translator
        /// sees it against the untouched value, and in the list the task reports.
        /// </summary>
        private void Warn(IParagraphUnit unit, string unitId, string reason)
        {
            Warn(unit, unitId, reason, "ICU message passed through unexpanded: ");
        }

        /// <summary>The same, with the comment's opening words chosen by the caller: a walked layout is not a pass-through.</summary>
        private void Warn(IParagraphUnit unit, string unitId, string reason, string commentPrefix)
        {
            _warnings.Add(new ExpansionWarning(unitId, reason));
            Diagnostics.Write("  unit " + unitId + ": " + commentPrefix + reason);

            var comment = PropertiesFactory.CreateComment(
                commentPrefix + reason, Constants.CommentAuthor, Severity.Medium);
            comment.Date = DateTime.Now;
            comment.DateSpecified = true;

            if (unit.Properties.Comments == null)
            {
                unit.Properties.Comments = PropertiesFactory.CreateCommentProperties();
            }
            unit.Properties.Comments.Add(comment);
        }

        private static string LanguageTagOf(Sdl.Core.Globalization.Language language)
        {
            if (language == null) return null;
            if (language.CultureInfo != null && !string.IsNullOrEmpty(language.CultureInfo.Name))
            {
                return language.CultureInfo.Name;
            }
            return string.IsNullOrEmpty(language.IsoAbbreviation) ? null : language.IsoAbbreviation;
        }
    }
}
