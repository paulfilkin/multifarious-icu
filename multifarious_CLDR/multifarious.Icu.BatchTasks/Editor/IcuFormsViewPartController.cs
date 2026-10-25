using System;
using System.Linq;
using multifarious.Icu.BatchTasks.Editor.ViewModels;
using multifarious.Icu.BatchTasks.Editor.Views;
using multifarious.Icu.BatchTasks.Services;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.Extensions;
using Sdl.Desktop.IntegrationApi.Interfaces;
using Sdl.TranslationStudioAutomation.IntegrationApi;

namespace multifarious.Icu.BatchTasks.Editor
{
    /// <summary>
    /// The ICU Forms window: docked under the editor, following the active segment, showing
    /// every form of the message that segment belongs to. On Localyzer Connect's view part
    /// pattern, with two differences: the paragraph unit comes from the editor's document
    /// model rather than the file on disk, and the content follows typing as well as the
    /// active segment.
    /// </summary>
    [ViewPart(
        Id = Constants.FormsViewPartId,
        Name = "ViewPart_Forms_Name",
        Description = "ViewPart_Forms_Description",
        Icon = "IcuForms_Icon")]
    [ViewPartLayout(Dock = DockType.Bottom, LocationByType = typeof(EditorController))]
    public class IcuFormsViewPartController : AbstractViewPartController
    {
        private IcuFormsView _view;
        private IcuFormsViewModel _viewModel;
        private EditorController _editor;
        private IStudioDocument _document;
        private bool _documentSupported;
        private bool _documentExpanded;
        private bool _documentRestored;

        protected override IUIControl GetContentControl()
        {
            if (_view == null)
            {
                _viewModel = new IcuFormsViewModel();
                _viewModel.RowActivated += OnRowActivated;
                _view = new IcuFormsView { DataContext = _viewModel };
            }

            return _view;
        }

        /// <summary>
        /// A click on a row moves the editor to that segment, with the focus, so the window
        /// navigates the document as well as following it. The editor then raises
        /// ActiveSegmentChanged and the window refreshes as for any other move.
        /// </summary>
        private void OnRowActivated(object sender, RowActivatedEventArgs e)
        {
            if (_document == null) return;
            try
            {
                var moved = _document.SetActiveSegmentPair(e.ParagraphUnitId, e.SegmentId, true);
                Diagnostics.Write("forms: row clicked, segment " + e.SegmentId + " of unit " + e.ParagraphUnitId + (moved ? "" : " (not found)"));
            }
            catch (Exception exception)
            {
                Diagnostics.Write("forms: navigation failed: " + exception);
            }
        }

        protected override void Initialize()
        {
            try
            {
                _editor = SdlTradosStudio.Application.GetController<EditorController>();
                if (_editor == null) return;

                _editor.ActiveDocumentChanged += OnActiveDocumentChanged;
                Attach(_editor.ActiveDocument);
            }
            catch (Exception exception)
            {
                Diagnostics.Write("forms: initialise failed: " + exception);
            }
        }

        private void OnActiveDocumentChanged(object sender, DocumentEventArgs e)
        {
            Attach(_editor != null ? _editor.ActiveDocument : null);
        }

        /// <summary>Follows one document at a time; the previous one's events are let go.</summary>
        private void Attach(IStudioDocument document)
        {
            if (_document != null)
            {
                _document.ActiveSegmentChanged -= OnSegmentOrContentChanged;
                _document.ContentChanged -= OnContentChanged;
                _document.SegmentTranslated -= OnSegmentOrContentChanged;
                _document.ActiveFileChanged -= OnSegmentOrContentChanged;
            }

            _document = document;

            if (_document != null)
            {
                _document.ActiveSegmentChanged += OnSegmentOrContentChanged;
                _document.ContentChanged += OnContentChanged;
                _document.SegmentTranslated += OnSegmentOrContentChanged;
                _document.ActiveFileChanged += OnSegmentOrContentChanged;
                Survey(_document);
            }

            Refresh();
        }

        /// <summary>
        /// What kind of file this is, decided once per document so the empty state can say
        /// something useful: a file type the tasks do not handle, a supported file the expand
        /// task has not run on, or one whose messages Restore has put back together. The scan
        /// stops once an expanded unit is found; a restored one is noted on the way, so a file
        /// with only restored units is not told to expand before translating.
        /// </summary>
        private void Survey(IStudioDocument document)
        {
            _documentSupported = false;
            _documentExpanded = false;
            _documentRestored = false;
            try
            {
                var file = document.ActiveFile;
                var fileTypeId = file != null ? file.FileTypeId : null;
                _documentSupported = Constants.DefaultFileTypeIds.Any(id =>
                    string.Equals(fileTypeId, id, StringComparison.OrdinalIgnoreCase));
                if (!_documentSupported)
                {
                    Diagnostics.Write("forms: file type " + (fileTypeId ?? "<null>") + " not supported");
                    return;
                }

                foreach (var pair in document.SegmentPairs)
                {
                    var unit = document.GetParentParagraphUnit(pair);
                    if (unit == null) continue;
                    if (ResourceKey.IsExpanded(unit))
                    {
                        _documentExpanded = true;
                        break;
                    }
                    if (ResourceKey.IsRestored(unit)) _documentRestored = true;
                }

                Diagnostics.Write("forms: document surveyed, expanded=" + _documentExpanded + " restored=" + _documentRestored);
            }
            catch (Exception exception)
            {
                Diagnostics.Write("forms: survey failed: " + exception);
                _documentSupported = true;
                _documentExpanded = true;
            }
        }

        private void OnSegmentOrContentChanged(object sender, EventArgs e)
        {
            Diagnostics.Write("forms: segment or translation changed");
            Refresh();
        }

        private void OnContentChanged(object sender, DocumentContentEventArgs e)
        {
            Diagnostics.Write("forms: content changed");
            Refresh();
        }

        protected override void Refresh()
        {
            GetContentControl();
            var view = _view;
            if (view == null) return;

            if (!view.Dispatcher.CheckAccess())
            {
                view.Dispatcher.BeginInvoke(new Action(Refresh));
                return;
            }

            try
            {
                var document = _document;
                if (document == null)
                {
                    _viewModel.ShowNoDocument();
                    return;
                }

                var file = document.ActiveFile;
                if (!_documentSupported)
                {
                    _viewModel.ShowUnsupported(file != null ? file.FileTypeId : string.Empty);
                    return;
                }

                if (!_documentExpanded && !_documentRestored)
                {
                    _viewModel.ShowNotExpanded();
                    return;
                }

                var pair = document.ActiveSegmentPair;
                if (pair == null)
                {
                    _viewModel.ShowNotIcu();
                    return;
                }

                var unit = document.GetParentParagraphUnit(pair);
                var language = file != null && file.Language != null ? file.Language : null;
                var tag = language != null && language.CultureInfo != null ? language.CultureInfo.Name : (language != null ? language.IsoAbbreviation : null);
                var name = language != null ? language.DisplayName : null;
                var segmentId = pair.Properties != null && pair.Properties.Id != null ? pair.Properties.Id.Id : null;

                // The pair's own target is the editor's live content; the paragraph unit's copy is
                // what was last committed, so the pair's wins for the active segment.
                _viewModel.Show(unit, segmentId, tag, name, pair.Target);
                Diagnostics.Write("forms: shown segment " + segmentId + " rows=" + _viewModel.Rows.Count
                    + " liveTarget=" + (pair.Target != null ? RawValueReconstruction.Reconstruct(pair.Target).RawValue : "<null>"));
            }
            catch (Exception exception)
            {
                Diagnostics.Write("forms: refresh failed: " + exception);
                _viewModel.ShowNotIcu();
            }
        }

        public override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_editor != null) _editor.ActiveDocumentChanged -= OnActiveDocumentChanged;
                if (_viewModel != null) _viewModel.RowActivated -= OnRowActivated;
                Attach(null);
            }

            base.Dispose(disposing);
        }
    }
}
