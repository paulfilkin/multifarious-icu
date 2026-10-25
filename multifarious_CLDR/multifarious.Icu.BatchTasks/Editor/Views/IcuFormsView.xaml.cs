using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using multifarious.Icu.BatchTasks.Editor.ViewModels;
using multifarious.Icu.BatchTasks.Services;
using Sdl.Desktop.IntegrationApi.Interfaces;

namespace multifarious.Icu.BatchTasks.Editor.Views
{
    /// <summary>
    /// The control of the ICU Forms view part. <see cref="IUIControl"/> is what the view part
    /// controller must hand Studio; Studio hosts the WPF control itself.
    /// </summary>
    public partial class IcuFormsView : UserControl, IUIControl
    {
        private IcuFormsViewModel _viewModel;
        private readonly List<IcuFormRowViewModel> _watchedRows = new List<IcuFormRowViewModel>();
        private bool _scrollPending;

        public IcuFormsView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        public void Dispose()
        {
            Watch(null);
        }

        /// <summary>A click on a row takes the editor to the row's segment.</summary>
        private void OnRowClick(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as IcuFormsViewModel;
            var row = (sender as System.Windows.FrameworkElement)?.DataContext as IcuFormRowViewModel;
            if (viewModel != null && row != null) viewModel.ActivateRow(row);
        }

        /// <summary>Ctrl and the mouse wheel zoom the window, as they do the editor; a plain wheel scrolls it.</summary>
        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;

            var viewModel = DataContext as IcuFormsViewModel;
            if (viewModel == null) return;

            if (e.Delta > 0) viewModel.ZoomIn();
            else if (e.Delta < 0) viewModel.ZoomOut();
            e.Handled = true;
        }

        // ---- keeping the active row in view -----------------------------------------------
        //
        // A message with more forms than the window shows, a walked list at a hundred rows or
        // an Italian gender over a plural, leaves the active row out of sight as the translator
        // moves through the segments, so the hints cannot be followed. The view watches the
        // rows for the active one and, once it would fall outside the visible rows, centres
        // it; a row already in view is left where it is, so the translator's own scrolling is
        // not fought. Rows arrive one at a time after Rows.Clear() on every refresh, with the
        // active flag set before the add, so the collection change is the signal; a later
        // change of IsActive on an existing row is watched too.

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Watch(e.NewValue as IcuFormsViewModel);
        }

        private void Watch(IcuFormsViewModel viewModel)
        {
            if (_viewModel != null) _viewModel.Rows.CollectionChanged -= OnRowsChanged;
            foreach (var row in _watchedRows) row.PropertyChanged -= OnRowPropertyChanged;
            _watchedRows.Clear();

            _viewModel = viewModel;
            if (_viewModel == null) return;

            _viewModel.Rows.CollectionChanged += OnRowsChanged;
            foreach (var row in _viewModel.Rows) WatchRow(row);
            ScheduleScrollToActiveRow();
        }

        private void WatchRow(IcuFormRowViewModel row)
        {
            row.PropertyChanged += OnRowPropertyChanged;
            _watchedRows.Add(row);
        }

        private void OnRowsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var row in _watchedRows) row.PropertyChanged -= OnRowPropertyChanged;
                _watchedRows.Clear();
            }

            if (e.OldItems != null)
            {
                foreach (IcuFormRowViewModel row in e.OldItems)
                {
                    row.PropertyChanged -= OnRowPropertyChanged;
                    _watchedRows.Remove(row);
                }
            }

            if (e.NewItems != null)
            {
                foreach (IcuFormRowViewModel row in e.NewItems)
                {
                    WatchRow(row);
                    if (row.IsActive) ScheduleScrollToActiveRow();
                }
            }
        }

        private void OnRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var row = sender as IcuFormRowViewModel;
            if (row != null && row.IsActive && e.PropertyName == nameof(IcuFormRowViewModel.IsActive))
            {
                ScheduleScrollToActiveRow();
            }
        }

        /// <summary>
        /// One scroll per refresh, after layout, when the row's container exists and has its
        /// height; the rows are added one by one and each add would otherwise ask again.
        /// </summary>
        private void ScheduleScrollToActiveRow()
        {
            if (_scrollPending) return;
            _scrollPending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ScrollToActiveRow));
        }

        private void ScrollToActiveRow()
        {
            _scrollPending = false;
            try
            {
                var viewModel = _viewModel;
                if (viewModel == null || !viewModel.HasMessage) return;

                IcuFormRowViewModel active = null;
                foreach (var row in viewModel.Rows)
                {
                    if (row.IsActive) { active = row; break; }
                }
                if (active == null) return;

                RowsControl.UpdateLayout();
                var container = RowsControl.ItemContainerGenerator.ContainerFromItem(active) as FrameworkElement;
                if (container == null || !container.IsDescendantOf(RowsScroller)) return;

                // The container's place in the viewport: negative above it, past ViewportHeight below.
                var top = container.TransformToAncestor(RowsScroller).Transform(new Point(0, 0)).Y;
                var height = container.ActualHeight;
                var viewport = RowsScroller.ViewportHeight;
                if (top >= 0 && top + height <= viewport) return;

                var offset = RowsScroller.VerticalOffset + top - (viewport - height) / 2;
                RowsScroller.ScrollToVerticalOffset(Math.Max(0, offset));
            }
            catch (Exception exception)
            {
                Diagnostics.Write("forms: scroll to the active row failed: " + exception);
            }
        }
    }
}
