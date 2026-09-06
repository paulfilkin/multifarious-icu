using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using multifarious.Icu.BatchTasks.Resources;
using multifarious.Icu.BatchTasks.Services;
using multifarious.Icu.BatchTasks.Settings.ViewModels;
using Sdl.FileTypeSupport.Framework.BilingualApi;

namespace multifarious.Icu.BatchTasks.Editor.ViewModels
{
    /// <summary>One row of the ICU Forms window.</summary>
    public sealed class IcuFormRowViewModel : ObservableObject
    {
        private bool _isActive;
        private bool _isMatched;

        public IcuFormRowViewModel(IcuFormRow row)
        {
            Row = row;
            // A nested path shows every branch on the way down, "female / one", so the rows of a
            // select over a plural do not read as the same four forms three times over.
            var components = row.Path.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
            Form = components.Length > 1
                ? string.Join(" / ", components.Select(c => c.Substring(c.IndexOf(':') + 1)))
                : row.Category.Length > 0 ? row.Category : string.Empty;
            Counts = row.Counts.Count == 0
                ? string.Empty
                : string.Join(", ", row.Counts) + (row.FractionalOnly ? " (" + UIStrings.Forms_FractionalOnly + ")" : string.Empty);
            Source = row.SourceRendered;
            Target = row.TargetEmpty ? UIStrings.Forms_EmptyTarget : row.TargetRendered;
            Warning = row.PlaceholderWarning == null
                ? null
                : string.Format(CultureInfo.CurrentCulture, UIStrings.Forms_PlaceholderWarning, row.PlaceholderWarning);
        }

        public IcuFormRow Row { get; }

        public string Form { get; }

        public string Path { get { return Row.Path; } }

        public string Counts { get; }

        public string Source { get; }

        public string Target { get; }

        public bool TargetEmpty { get { return Row.TargetEmpty; } }

        /// <summary>The expansion's comment: the form, its counts and any grammar hint.</summary>
        public string Comment { get { return Row.Comment; } }

        public string Warning { get; }

        public bool HasWarning { get { return Warning != null; } }

        /// <summary>The row of the segment the translator is on.</summary>
        public bool IsActive
        {
            get { return _isActive; }
            set { Set(ref _isActive, value); }
        }

        /// <summary>The row a tried count lands on.</summary>
        public bool IsMatched
        {
            get { return _isMatched; }
            set { Set(ref _isMatched, value); }
        }
    }

    /// <summary>
    /// The ICU Forms window: the message the active segment belongs to, one row per form, a
    /// count box, and the reassembled target message with its parse result. When there is no
    /// message to show, an explanation with a title, what the window is for, and what to do:
    /// no document, a file type the plugin does not handle, a supported file the expand task
    /// has not run on, or an ordinary segment. Fed by the controller from the editor's document
    /// model; everything here is plain state, so the tests drive it with paragraph units and
    /// no editor.
    /// </summary>
    public sealed class IcuFormsViewModel : ObservableObject
    {
        private readonly IcuFormsReader _reader;
        private IcuFormsModel _model;
        private string _key = string.Empty;
        private string _summary = string.Empty;
        private string _emptyTitle = string.Empty;
        private string _emptyBody = string.Empty;
        private string _emptyNote = string.Empty;
        private string _countText = string.Empty;
        private string _countResult = string.Empty;
        private string _projection = string.Empty;
        private string _parseStatus = string.Empty;
        private bool _parses;
        private readonly ZoomState _zoom;

        public IcuFormsViewModel(IcuFormsReader reader = null, ZoomState zoom = null)
        {
            _reader = reader ?? new IcuFormsReader();
            _zoom = zoom ?? new ZoomState();
            Rows = new ObservableCollection<IcuFormRowViewModel>();
            ZoomInCommand = new RelayCommand(_ => ZoomIn(), _ => _zoom.CanZoomIn);
            ZoomOutCommand = new RelayCommand(_ => ZoomOut(), _ => _zoom.CanZoomOut);
            ZoomResetCommand = new RelayCommand(_ => ZoomReset());
            ShowNoDocument();
        }

        public ObservableCollection<IcuFormRowViewModel> Rows { get; }

        // ---- zoom ---------------------------------------------------------------------------

        /// <summary>The scale applied to the whole window's content: 1.0 is 100%.</summary>
        public double Zoom { get { return _zoom.Factor; } }

        public string ZoomPercent { get { return string.Format(CultureInfo.CurrentCulture, "{0:0}%", _zoom.Factor * 100); } }

        public ICommand ZoomInCommand { get; }

        public ICommand ZoomOutCommand { get; }

        public ICommand ZoomResetCommand { get; }

        public void ZoomIn()
        {
            _zoom.ZoomIn();
            RaiseZoom();
        }

        public void ZoomOut()
        {
            _zoom.ZoomOut();
            RaiseZoom();
        }

        public void ZoomReset()
        {
            _zoom.Reset();
            RaiseZoom();
        }

        private void RaiseZoom()
        {
            Raise(nameof(Zoom));
            Raise(nameof(ZoomPercent));
        }

        public bool HasMessage { get { return _model != null; } }

        public string Key
        {
            get { return _key; }
            private set { Set(ref _key, value); }
        }

        public string Summary
        {
            get { return _summary; }
            private set { Set(ref _summary, value); }
        }

        /// <summary>The empty state's heading, such as "Not an ICU message".</summary>
        public string EmptyTitle
        {
            get { return _emptyTitle; }
            private set { Set(ref _emptyTitle, value); }
        }

        /// <summary>What the window is for; the same in every empty state.</summary>
        public string EmptyBody
        {
            get { return _emptyBody; }
            private set { Set(ref _emptyBody, value); }
        }

        /// <summary>What to do about it, specific to the state.</summary>
        public string EmptyNote
        {
            get { return _emptyNote; }
            private set { Set(ref _emptyNote, value); }
        }

        public bool ShowsCountBox { get { return _model != null && _model.CountArgument != null; } }

        public string CountText
        {
            get { return _countText; }
            set
            {
                if (Set(ref _countText, value ?? string.Empty)) ApplyCount();
            }
        }

        public string CountResult
        {
            get { return _countResult; }
            private set { Set(ref _countResult, value); }
        }

        public string Projection
        {
            get { return _projection; }
            private set { Set(ref _projection, value); }
        }

        public string ParseStatus
        {
            get { return _parseStatus; }
            private set { Set(ref _parseStatus, value); }
        }

        public bool Parses
        {
            get { return _parses; }
            private set { Set(ref _parses, value); }
        }

        /// <summary>
        /// Shows the unit the active segment belongs to, or the "not an ICU message" state where
        /// it is not one this plugin expanded. <paramref name="activeTarget"/> is the editor's
        /// live copy of the active segment's target, which stands in for the unit's copy so
        /// typing shows.
        /// </summary>
        public void Show(IParagraphUnit unit, string activeSegmentId, string targetLanguageTag, string targetLanguageName, ISegment activeTarget = null)
        {
            var model = _reader.Read(unit, targetLanguageTag, activeTarget);
            if (model == null)
            {
                ShowNotIcu();
                return;
            }

            _model = model;
            Key = model.Key ?? string.Empty;
            Summary = string.Format(CultureInfo.CurrentCulture, UIStrings.Forms_Summary,
                string.IsNullOrEmpty(targetLanguageName) ? targetLanguageTag : targetLanguageName, model.Rows.Count);

            Rows.Clear();
            foreach (var row in model.Rows)
            {
                Rows.Add(new IcuFormRowViewModel(row) { IsActive = row.SegmentId == activeSegmentId });
            }

            Projection = model.TargetProjection;
            Parses = model.TargetParses;
            ParseStatus = model.TargetParses
                ? UIStrings.Forms_Parses
                : string.Format(CultureInfo.CurrentCulture, UIStrings.Forms_ParseError, model.ParseError);

            Raise(nameof(HasMessage));
            Raise(nameof(ShowsCountBox));
            ApplyCount();
        }

        public void ShowNoDocument()
        {
            ShowEmpty(UIStrings.Forms_NoDocumentTitle, UIStrings.Forms_NoDocument);
        }

        /// <summary>The open file is of a type the plugin does not handle; the state names it.</summary>
        public void ShowUnsupported(string fileTypeId)
        {
            // Some files carry no identifier at all (a DWG in Project 45 came through as ""), and
            // quoting an empty string reads as a fault in the window.
            ShowEmpty(UIStrings.Forms_UnsupportedTitle,
                string.IsNullOrWhiteSpace(fileTypeId)
                    ? UIStrings.Forms_UnsupportedUnknown
                    : string.Format(CultureInfo.CurrentCulture, UIStrings.Forms_UnsupportedWhy, fileTypeId));
        }

        /// <summary>A supported file on which the expand task has not run.</summary>
        public void ShowNotExpanded()
        {
            ShowEmpty(UIStrings.Forms_NotExpandedTitle, UIStrings.Forms_NotExpanded);
        }

        /// <summary>An ordinary segment in an expanded file.</summary>
        public void ShowNotIcu()
        {
            ShowEmpty(UIStrings.Forms_NotIcuTitle, UIStrings.Forms_NotIcu);
        }

        private void ShowEmpty(string title, string note)
        {
            _model = null;
            Key = string.Empty;
            Summary = string.Empty;
            Rows.Clear();
            Projection = string.Empty;
            ParseStatus = string.Empty;
            Parses = false;
            CountResult = string.Empty;
            EmptyTitle = title;
            EmptyBody = UIStrings.Forms_Purpose;
            EmptyNote = note;
            Raise(nameof(HasMessage));
            Raise(nameof(ShowsCountBox));
        }

        /// <summary>The rows a tried count lands on, for the tests.</summary>
        public IReadOnlyList<IcuFormRowViewModel> MatchedRows
        {
            get { return Rows.Where(row => row.IsMatched).ToList(); }
        }

        private void ApplyCount()
        {
            if (_model == null || string.IsNullOrWhiteSpace(_countText))
            {
                foreach (var row in Rows) row.IsMatched = false;
                CountResult = string.Empty;
                return;
            }

            var matched = _reader.RowsFor(_model, _countText);
            var paths = new HashSet<string>(matched.Select(row => row.Path));
            foreach (var row in Rows)
            {
                row.IsMatched = paths.Contains(row.Path);
            }

            CountResult = matched.Count == 0
                ? UIStrings.Forms_ResolvesNothing
                : string.Format(CultureInfo.CurrentCulture, UIStrings.Forms_ResolvesTo, matched[0].Category);
        }
    }
}
