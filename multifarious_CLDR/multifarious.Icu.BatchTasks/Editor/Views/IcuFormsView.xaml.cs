using System.Windows.Controls;
using System.Windows.Input;
using multifarious.Icu.BatchTasks.Editor.ViewModels;
using Sdl.Desktop.IntegrationApi.Interfaces;

namespace multifarious.Icu.BatchTasks.Editor.Views
{
    /// <summary>
    /// The control of the ICU Forms view part. <see cref="IUIControl"/> is what the view part
    /// controller must hand Studio; Studio hosts the WPF control itself.
    /// </summary>
    public partial class IcuFormsView : UserControl, IUIControl
    {
        public IcuFormsView()
        {
            InitializeComponent();
        }

        public void Dispose()
        {
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
    }
}
