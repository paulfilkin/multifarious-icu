using System.Windows.Controls;
using System.Windows.Input;

namespace multifarious.Icu.BatchTasks.Settings.Views
{
    /// <summary>
    /// The Options tab of the Expand ICU page, bound to <see cref="ViewModels.IcuExpandSettingsViewModel"/>.
    /// Hosted by <see cref="IcuExpandSettingsView"/> inside the wizards and by the ICU Settings
    /// dialog as a tab of its own.
    /// </summary>
    public partial class IcuExpandOptionsView : UserControl
    {
        public IcuExpandOptionsView()
        {
            InitializeComponent();
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
}
