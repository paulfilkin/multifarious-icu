using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using multifarious.Icu.BatchTasks.Resources;
using multifarious.Icu.BatchTasks.Settings.ViewModels;

namespace multifarious.Icu.BatchTasks.Settings.Views
{
    /// <summary>
    /// The Grammar dictionary tab, bound to <see cref="GrammarDictionaryViewModel"/>. Hosted by
    /// <see cref="IcuExpandSettingsView"/> inside the wizards and by the ICU Settings dialog as
    /// a tab of its own. The file dialogs are the common item dialogs (Microsoft.Win32 on WPF is
    /// the IFileDialog-based one), where a path can be typed or pasted.
    /// </summary>
    public partial class IcuGrammarSettingsView : UserControl
    {
        public IcuGrammarSettingsView()
        {
            InitializeComponent();
        }

        private GrammarDictionaryViewModel ViewModel
        {
            get { return DataContext as GrammarDictionaryViewModel; }
        }

        // ---- the file ----------------------------------------------------------------------------

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
                Warn(UIStrings.Expand_GrammarImportTitle, string.Format(CultureInfo.CurrentCulture, UIStrings.Expand_GrammarInvalid, exception.Message));
                return;
            }
            catch (UnauthorizedAccessException exception)
            {
                Warn(UIStrings.Expand_GrammarImportTitle, string.Format(CultureInfo.CurrentCulture, UIStrings.Expand_GrammarInvalid, exception.Message));
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
            Save(UIStrings.Expand_GrammarTemplateTitle, Constants.GrammarTemplateFileName, GrammarDictionaryViewModel.TemplateText());
        }

        // ---- the tables ----------------------------------------------------------------------------

        private void OnResetLanguage(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.DataContext as GrammarLanguageRow;
            if (row != null) row.Reset();
        }

        private void OnAddLanguage(object sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel != null) viewModel.AddLanguage();
        }

        private void OnRemoveArgument(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.DataContext as GrammarArgumentRow;
            if (row != null) row.Remove();
        }

        private void OnAddArgument(object sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel != null) viewModel.AddArgument();
        }

        // ---- helpers ----------------------------------------------------------------------------

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
                Warn(title, exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                Warn(title, exception.Message);
            }
        }

        private static void Warn(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
