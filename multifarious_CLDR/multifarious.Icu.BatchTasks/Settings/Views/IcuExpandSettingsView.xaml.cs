using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.Interfaces;
using multifarious.Icu.BatchTasks.Settings.ViewModels;

namespace multifarious.Icu.BatchTasks.Settings.Views
{
    /// <summary>
    /// The control for the expand task's settings page.
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

        /// <summary>The page refuses to save while the budget box holds something that is not a number in range.</summary>
        public bool ValidateChildren()
        {
            var viewModel = DataContext as IcuExpandSettingsViewModel;
            return viewModel == null || viewModel.IsValid;
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// Enter on a radio button selects it rather than travelling on to the dialog's default
        /// button and closing the page. Same behaviour as the sibling plugins' pages.
        /// </summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            var radio = Keyboard.FocusedElement as RadioButton;
            if (radio != null)
            {
                radio.IsChecked = true;
                e.Handled = true;
            }
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
