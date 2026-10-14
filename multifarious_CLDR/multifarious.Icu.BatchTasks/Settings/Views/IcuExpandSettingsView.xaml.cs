using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.Interfaces;
using multifarious.Icu.BatchTasks.Settings.ViewModels;

namespace multifarious.Icu.BatchTasks.Settings.Views
{
    /// <summary>
    /// The control for the expand task's settings page: the Options and Grammar dictionary tabs.
    ///
    /// <see cref="IUISettingsControl"/> and <see cref="ISettingsAware{TSettings}"/> are what
    /// <see cref="DefaultSettingsPage{TControl,TSettings}"/> constrains its control to; Studio
    /// hosts the WPF control itself, as the Multilingual XML file type's batch task pages show.
    /// </summary>
    public partial class IcuExpandSettingsView : UserControl, IUISettingsControl, ISettingsAware<IcuExpandSettings>
    {
        public IcuExpandSettingsView()
        {
            InitializeComponent();
        }

        public IcuExpandSettings Settings { get; set; }

        /// <summary>The page refuses to save while a number box holds something that is not a number in range, or the dictionary as edited does not validate.</summary>
        public bool ValidateChildren()
        {
            var viewModel = DataContext as IcuExpandSettingsViewModel;
            return viewModel == null || viewModel.IsValid;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>Collapsed when true, visible when false: shows a validation message only while the input is wrong.</summary>
    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool && (bool)value ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
