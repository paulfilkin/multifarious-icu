using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Resources;
using multifarious.Icu.BatchTasks.Resources;
using multifarious.Icu.BatchTasks.Services;
using Sdl.Core.Settings;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;
using Sdl.Verification.Api;

namespace multifarious.Icu.BatchTasks.Verification
{
    /// <summary>
    /// The ICU verifier: what F8 and the Messages window say about an expanded message. Runs
    /// over every paragraph unit this plugin expanded, or over the one segment Studio names
    /// when it verifies as the translator confirms, and reports per target segment: a form
    /// with no translation, placeholders that differ from the source, a '#' typed as text, and
    /// a reassembled message that will not parse. On the pattern of Studio's own tag verifier:
    /// a bilingual file type component that is also the global verifier Studio lists.
    ///
    /// Its page under Verification holds the Enabled flag and a severity per check; the
    /// severities come from the settings bundle Studio shares with every verifier.
    /// </summary>
    [GlobalVerifier(Constants.VerifierId, "Verifier_Name", "Verifier_Description")]
    public class IcuVerifier : AbstractBilingualFileTypeComponent, IBilingualVerifier, ISharedObjectsAware, IGlobalVerifier
    {
        private static readonly ResourceManager PluginResources =
            new ResourceManager("multifarious.Icu.BatchTasks.PluginResources", typeof(Constants).Assembly);

        private readonly IcuFormsReader _reader = new IcuFormsReader();
        private SegmentId _currentSegmentId;
        private bool _enabled = true;
        private CheckSeverity _emptyForm = CheckSeverity.Warning;
        private CheckSeverity _placeholders = CheckSeverity.Error;
        private CheckSeverity _typedPound = CheckSeverity.Warning;
        private CheckSeverity _invalid = CheckSeverity.Error;
        private string _targetLanguage;

        // ---- IGlobalVerifier ----------------------------------------------------------------

        public string Name { get { return PluginResources.GetString("Verifier_Name"); } }

        public string Description { get { return PluginResources.GetString("Verifier_Description"); } }

        public Icon Icon { get { return PluginResources.GetObject("IcuForms_Icon") as Icon; } }

        public string SettingsId { get { return Constants.VerifierSettingsId; } }

        public string HelpTopic { get { return string.Empty; } }

        public IList<string> GetSettingsPageExtensionIds()
        {
            return new List<string> { Constants.VerifierSettingsPageId };
        }

        // ---- ISharedObjectsAware ------------------------------------------------------------

        public void SetSharedObjects(ISharedObjects sharedObjects)
        {
            if (sharedObjects == null) return;

            _currentSegmentId = sharedObjects.GetSharedObject<SegmentId>("CurrentlyVerifyingSegmentId");

            var bundle = sharedObjects.GetSharedObject<ISettingsBundle>("SettingsBundle");
            if (bundle != null)
            {
                var settings = bundle.GetSettingsGroup<IcuVerifierSettings>();
                _enabled = settings.Enabled;
                _emptyForm = settings.EmptyForm;
                _placeholders = settings.Placeholders;
                _typedPound = settings.TypedPound;
                _invalid = settings.Invalid;
            }

            Diagnostics.Write("verifier: shared objects, segment=" + (_currentSegmentId.Id ?? "<all>")
                + " bundle=" + (bundle != null) + " enabled=" + _enabled);
        }

        // ---- IBilingualContentHandler -------------------------------------------------------

        public void Initialize(IDocumentProperties documentInfo)
        {
            Diagnostics.Write("verifier: initialise, reporter=" + (MessageReporter != null));
        }

        public void SetFileProperties(IFileProperties fileInfo)
        {
            var conversion = fileInfo == null ? null : fileInfo.FileConversionProperties;
            var language = conversion == null ? null : conversion.TargetLanguage;
            _targetLanguage = language == null ? null
                : language.CultureInfo != null ? language.CultureInfo.Name : language.IsoAbbreviation;
            Diagnostics.Write("verifier: file " + (conversion != null && conversion.OriginalFilePath != null ? conversion.OriginalFilePath : "<?>")
                + " target=" + (_targetLanguage ?? "<null>"));
        }

        public void FileComplete()
        {
        }

        public void Complete()
        {
        }

        public void ProcessParagraphUnit(IParagraphUnit paragraphUnit)
        {
            if (!_enabled || paragraphUnit == null || paragraphUnit.IsStructure || MessageReporter == null) return;
            if (!ResourceKey.IsExpanded(paragraphUnit)) return;

            // A failure here must be seen, in the diagnostics and in the results, not swallowed.
            try
            {
                Verify(paragraphUnit);
            }
            catch (Exception ex)
            {
                Diagnostics.Write("verifier: FAILED on unit " + paragraphUnit.Properties.ParagraphUnitId.Id
                    + ": " + ex.GetType().Name + ": " + ex.Message);
                MessageReporter.ReportMessage(this, UIStrings.Verifier_Origin, ErrorLevel.Error,
                    ex.GetType().Name + ": " + ex.Message, paragraphUnit.Properties.ParagraphUnitId.Id);
            }
        }

        private void Verify(IParagraphUnit paragraphUnit)
        {
            var model = _reader.Read(paragraphUnit, _targetLanguage);
            if (model == null) return;
            Diagnostics.Write("verifier: unit " + paragraphUnit.Properties.ParagraphUnitId.Id
                + " rows=" + model.Rows.Count + " parses=" + model.TargetParses);

            var targets = TargetSegments(paragraphUnit);
            var onlySegment = _currentSegmentId.Id;
            ISegment reportedInvalidOn = null;

            for (var index = 0; index < model.Rows.Count && index < targets.Count; index++)
            {
                var row = model.Rows[index];
                var target = targets[index];
                if (onlySegment != null && target.Properties.Id.Id != onlySegment) continue;

                if (row.TargetEmpty)
                {
                    Report(_emptyForm, string.Format(CultureInfo.CurrentCulture, UIStrings.Verifier_EmptyForm, FormName(row)), target);
                }

                if (row.PlaceholderWarning != null)
                {
                    Report(_placeholders, string.Format(CultureInfo.CurrentCulture, UIStrings.Verifier_Placeholders, row.PlaceholderWarning), target);
                }

                if (row.TypedPound)
                {
                    Report(_typedPound, UIStrings.Verifier_TypedPound, target);
                }

                if (reportedInvalidOn == null) reportedInvalidOn = target;
            }

            // The whole message fails to parse: said once, on the segment being verified or the
            // first one, since the layout has no single owner of the syntax between segments.
            if (!model.TargetParses && reportedInvalidOn != null)
            {
                Report(_invalid, string.Format(CultureInfo.CurrentCulture, UIStrings.Verifier_Invalid, model.ParseError), reportedInvalidOn);
            }
        }

        private void Report(CheckSeverity severity, string message, ISegment target)
        {
            var level = IcuVerifierSettings.LevelOf(severity);
            if (level == null) return;

            var location = new TextLocation(target);
            Diagnostics.Write("verifier: report " + level.Value + " segment " + target.Properties.Id.Id + ": " + message);
            MessageReporter.ReportMessage(this, UIStrings.Verifier_Origin, level.Value, message, location, location);
        }

        private static string FormName(IcuFormRow row)
        {
            return row.Path.Length > 0 ? row.Path : row.Category;
        }

        private static List<ISegment> TargetSegments(IParagraphUnit unit)
        {
            var segments = new List<ISegment>();
            Collect(unit.Target, segments);
            return segments;
        }

        private static void Collect(IAbstractMarkupDataContainer container, List<ISegment> into)
        {
            if (container == null) return;
            for (var i = 0; i < container.Count; i++)
            {
                var segment = container[i] as ISegment;
                if (segment != null)
                {
                    into.Add(segment);
                    continue;
                }

                var nested = container[i] as IAbstractMarkupDataContainer;
                if (nested != null) Collect(nested, into);
            }
        }
    }
}
