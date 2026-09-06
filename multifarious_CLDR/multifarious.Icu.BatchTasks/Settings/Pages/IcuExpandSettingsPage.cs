using System;
using System.Windows;
using System.Windows.Threading;
using multifarious.Icu.BatchTasks.Settings.ViewModels;
using multifarious.Icu.BatchTasks.Settings.Views;
using Sdl.Core.Settings;
using Sdl.Desktop.IntegrationApi;

namespace multifarious.Icu.BatchTasks.Settings.Pages
{
    /// <summary>
    /// The expand task's settings page, bound to the task by RequiresSettings. Studio supplies
    /// the settings bundle as DataSource and shows the page under the task's name in the batch
    /// task and project settings trees. The view model is attached when the view has loaded,
    /// on the pattern of the Multilingual XML file type's batch task pages; the base class
    /// begins the edit and Save or Cancel ends it.
    /// </summary>
    public class IcuExpandSettingsPage : DefaultSettingsPage<IcuExpandSettingsView, IcuExpandSettings>
    {
        private IcuExpandSettingsView _view;
        private IcuExpandSettingsViewModel _viewModel;
        private IcuExpandSettings _settings;

        public override object GetControl()
        {
            if (_view != null) return _view;

            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
            {
                _settings = ((ISettingsBundle)DataSource).GetSettingsGroup<IcuExpandSettings>();
                _view = base.GetControl() as IcuExpandSettingsView;
                if (_view != null)
                {
                    _viewModel = new IcuExpandSettingsViewModel(_settings);
                    _view.Loaded += OnViewLoaded;
                }
            }));

            return _view;
        }

        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            _view.DataContext = _viewModel;
        }

        public override void Save()
        {
            if (_viewModel != null) _settings = _viewModel.Apply();
            base.Save();
        }

        public override void ResetToDefaults()
        {
            if (_viewModel != null) _settings = _viewModel.ResetToDefaults();
        }

        public override void Dispose()
        {
            if (_view != null)
            {
                _view.Loaded -= OnViewLoaded;
            }

            base.Dispose();
        }
    }
}
