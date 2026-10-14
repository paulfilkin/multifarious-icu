using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using multifarious.Icu.BatchTasks.Resources;
using multifarious.Icu.BatchTasks.Settings.ViewModels;
using Sdl.Core.Settings;

namespace multifarious.Icu.BatchTasks.Settings.Views
{
    /// <summary>
    /// The ICU Settings dialog the Projects view opens: the Expand ICU options, the grammar
    /// dictionary and the Restore ICU options over a project's settings bundle, three tabs. The
    /// view models hold every edit until Apply, so Cancel leaves the bundle as it was; OK
    /// applies both groups and the caller updates the project. Owned by Studio's main window so
    /// it is modal to it.
    /// </summary>
    public partial class IcuSettingsWindow : Window
    {
        private readonly IcuExpandSettingsViewModel _expand;
        private readonly IcuRestoreSettingsViewModel _restore;

        public IcuSettingsWindow(string projectName, ISettingsBundle bundle)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));

            InitializeComponent();
            Title = string.Format(CultureInfo.CurrentCulture, UIStrings.Settings_WindowTitle, projectName ?? string.Empty);

            _expand = new IcuExpandSettingsViewModel(bundle.GetSettingsGroup<IcuExpandSettings>());
            _restore = new IcuRestoreSettingsViewModel(bundle.GetSettingsGroup<IcuRestoreSettings>());
            ExpandView.DataContext = _expand;
            GrammarView.DataContext = _expand.Grammar;
            RestoreView.DataContext = _restore;

            var owner = Process.GetCurrentProcess().MainWindowHandle;
            if (owner != IntPtr.Zero)
            {
                new WindowInteropHelper(this) { Owner = owner };
            }
        }

        /// <summary>OK applies both groups into the bundle; an invalid number box or a dictionary edit that does not validate keeps the dialog open, its message showing.</summary>
        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (!_expand.IsValid)
            {
                (_expand.Grammar.IsValid ? (UIElement)ExpandView : GrammarView).Focus();
                return;
            }

            _expand.Apply();
            _restore.Apply();
            DialogResult = true;
        }

        /// <summary>
        /// Every setting on the three tabs back to its default, the dictionary file included,
        /// as the wizard page's own Reset to defaults does. The groups are reset at once, as
        /// there; OK then writes the project, Cancel leaves the project file as it was.
        /// </summary>
        private void OnResetDefaults(object sender, RoutedEventArgs e)
        {
            _expand.ResetToDefaults();
            _restore.ResetToDefaults();
        }
    }
}
