using System.Windows.Controls;
using System.Windows.Input;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.Interfaces;

namespace multifarious.Icu.BatchTasks.Settings.Views
{
    /// <summary>The control for the finalise task's settings page; see <see cref="IcuExpandSettingsView"/>.</summary>
    public partial class IcuFinaliseSettingsView : UserControl, IUISettingsControl, ISettingsAware<IcuFinaliseSettings>
    {
        public IcuFinaliseSettingsView()
        {
            InitializeComponent();
        }

        public IcuFinaliseSettings Settings { get; set; }

        public bool ValidateChildren()
        {
            return true;
        }

        public void Dispose()
        {
        }

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
