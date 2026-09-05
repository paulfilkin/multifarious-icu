using System;
using System.Collections.Generic;
using Icu.Cldr;
using multifarious.Icu.Expansion;
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

        private ExpansionWriter _writer;
        private string _fileSourceLanguage;
        private string _fileTargetLanguage;

        /// <param name="sourceLanguageTag">The project's source language, or null to take it from the file.</param>
        /// <param name="targetLanguageTag">The file's target language, or null to take it from the file.</param>
        public IcuExpandProcessor(string sourceLanguageTag, string targetLanguageTag, ExpansionOptions options,
            string appVersion)
        {
            _sourceLanguageTag = sourceLanguageTag;
            _targetLanguageTag = targetLanguageTag;
            _options = options ?? ExpansionOptions.Default;
            _appVersion = appVersion ?? string.Empty;
            _plurals = CldrPlurals.Default;
            _planner = new ExpansionPlanner(_plurals, _options.IncludeHints ? GrammaticalHints.Embedded : GrammaticalHints.None);
        }

        /// <summary>Translatable units seen.</summary>
        public int Units { get; private set; }

        /// <summary>Units rewritten.</summary>
        public int Expanded { get; private set; }

        /// <summary>Units skipped because this plugin had already expanded them.</summary>
        public int AlreadyExpanded { get; private set; }

        public IReadOnlyList<ExpansionWarning> Warnings { get { return _warnings; } }

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
            }

            base.ProcessParagraphUnit(paragraphUnit);
        }

        private void Expand(IParagraphUnit unit)
        {
            Units++;
            var unitId = unit.Properties.ParagraphUnitId.Id;

            if (ResourceKey.IsExpanded(unit))
            {
                AlreadyExpanded++;
                Diagnostics.Write("  unit " + unitId + ": already expanded, skipped");
                return;
            }

            var raw = RawValueReconstruction.Reconstruct(unit.Source).RawValue;
            if (raw.Length == 0) return;

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
                    return;

                case MessageKind.Protect:
                    Writer.Write(unit, _planner.PlanProtected(classification), ResourceKey.Of(unit));
                    Expanded++;
                    Diagnostics.Write("  unit " + unitId + ": arguments protected");
                    return;

                case MessageKind.Expand:
                    var targetLanguage = _targetLanguageTag ?? _fileTargetLanguage;
                    var sourceLanguage = _sourceLanguageTag ?? _fileSourceLanguage ?? "en";
                    if (string.IsNullOrEmpty(targetLanguage))
                    {
                        throw new InvalidOperationException(
                            "The target language is unknown: neither the task nor the file supplied one.");
                    }

                    var plan = _planner.Plan(classification, sourceLanguage, new[] { targetLanguage }, _options);
                    if (plan.ExceedsBudget)
                    {
                        Warn(unit, unitId, "Expansion would produce " + plan.Segments.Count
                            + " segments, over the budget of " + plan.MaxUnitsPerMessage
                            + "; passed through unexpanded.");
                        return;
                    }

                    Writer.Write(unit, plan, ResourceKey.Of(unit));
                    Expanded++;
                    Diagnostics.Write("  unit " + unitId + ": expanded to " + plan.Segments.Count
                        + " segments for " + targetLanguage);
                    return;
            }
        }

        private ExpansionWriter Writer
        {
            get
            {
                if (_writer == null)
                {
                    _writer = new ExpansionWriter(ItemFactory, PropertiesFactory, _options.TagConstruct,
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
            _warnings.Add(new ExpansionWarning(unitId, reason));
            Diagnostics.Write("  unit " + unitId + ": passed through: " + reason);

            var comment = PropertiesFactory.CreateComment(
                "ICU message passed through unexpanded: " + reason, Constants.CommentAuthor, Severity.Medium);
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
