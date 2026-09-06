using System.Windows.Controls;
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
    }
}
