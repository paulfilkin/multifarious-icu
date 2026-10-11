using System;
using System.Windows;
using System.Windows.Threading;
using multifarious.Icu.BatchTasks.Settings.ViewModels;
using multifarious.Icu.BatchTasks.Settings.Views;
using Sdl.Core.Settings;
using Sdl.Desktop.IntegrationApi;

namespace multifarious.Icu.BatchTasks.Settings.Pages
{
    /// <summary>The restore task's settings page; see <see cref="IcuExpandSettingsPage"/>.</summary>
    public class IcuRestoreSettingsPage : DefaultSettingsPage<IcuRestoreSettingsView, IcuRestoreSettings>
    {
        private IcuRestoreSettingsView _view;
        private IcuRestoreSettingsViewModel _viewModel;
        private IcuRestoreSettings _settings;

        public override object GetControl()
        {
            if (_view != null) return _view;

            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
            {
                _settings = ((ISettingsBundle)DataSource).GetSettingsGroup<IcuRestoreSettings>();
                _view = base.GetControl() as IcuRestoreSettingsView;
                if (_view != null)
                {
                    _viewModel = new IcuRestoreSettingsViewModel(_settings);
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
