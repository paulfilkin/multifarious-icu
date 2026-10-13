using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.Interfaces;
using multifarious.Icu.BatchTasks.Resources;
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

        /// <summary>The page refuses to save while a number box holds something that is not a number in range.</summary>
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

        // ---- the grammar dictionary buttons ----------------------------------------------------
        // The common item dialogs (Microsoft.Win32 on WPF is the IFileDialog-based one), where a
        // path can be typed or pasted. The view model validates and holds the text; the page's
        // Save writes it into the settings group.

        private IcuExpandSettingsViewModel ViewModel
        {
            get { return DataContext as IcuExpandSettingsViewModel; }
        }

        private void OnImportDictionary(object sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel == null) return;

            var dialog = new OpenFileDialog
            {
                Title = UIStrings.Expand_GrammarImportTitle,
                Filter = UIStrings.Expand_GrammarFileFilter,
                CheckFileExists = true,
            };
            if (dialog.ShowDialog() != true) return;

            string text;
            try
            {
                text = File.ReadAllText(dialog.FileName, Encoding.UTF8);
            }
            catch (IOException exception)
            {
                MessageBox.Show(string.Format(CultureInfo.CurrentCulture, UIStrings.Expand_GrammarInvalid, exception.Message),
                    UIStrings.Expand_GrammarImportTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            catch (UnauthorizedAccessException exception)
            {
                MessageBox.Show(string.Format(CultureInfo.CurrentCulture, UIStrings.Expand_GrammarInvalid, exception.Message),
                    UIStrings.Expand_GrammarImportTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            viewModel.TryImport(Path.GetFileName(dialog.FileName), text);
        }

        private void OnExportDictionary(object sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel == null || !viewModel.HasDictionary) return;

            Save(UIStrings.Expand_GrammarExportTitle,
                viewModel.DictionaryName.Length > 0 ? viewModel.DictionaryName : Constants.GrammarTemplateFileName,
                viewModel.DictionaryText);
        }

        private void OnRemoveDictionary(object sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel != null) viewModel.RemoveDictionary();
        }

        private void OnSaveTemplate(object sender, RoutedEventArgs e)
        {
            Save(UIStrings.Expand_GrammarTemplateTitle, Constants.GrammarTemplateFileName, IcuExpandSettingsViewModel.TemplateText());
        }

        private static void Save(string title, string fileName, string text)
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = UIStrings.Expand_GrammarFileFilter,
                FileName = fileName,
                DefaultExt = ".json",
                AddExtension = true,
                OverwritePrompt = true,
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(dialog.FileName, text, new UTF8Encoding(false));
            }
            catch (IOException exception)
            {
                MessageBox.Show(exception.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (UnauthorizedAccessException exception)
            {
                MessageBox.Show(exception.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
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
