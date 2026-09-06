using Sdl.Core.Settings;
using Sdl.Verification.Api;

namespace multifarious.Icu.BatchTasks.Verification
{
    /// <summary>
    /// The verifier's page under Verification in the project settings, on the pattern of
    /// Studio's tag verifier page: Studio hands the settings bundle in as DataSource, the page
    /// shows a WPF control over the group, and Save writes the control's state back. The
    /// verifier names this page through GetSettingsPageExtensionIds, which is what puts it in
    /// the tree.
    /// </summary>
    [GlobalVerifierSettingsPage(Id = Constants.VerifierSettingsPageId, Name = "VerifierPage_Name", Description = "VerifierPage_Description")]
    public class IcuVerifierSettingsPage : AbstractSettingsPage
    {
        private IcuVerifierSettingsView _view;
        private IcuVerifierSettingsViewModel _viewModel;
        private IcuVerifierSettings _settings;

        public override object GetControl()
        {
            if (_view == null)
            {
                _settings = ((ISettingsBundle)DataSource).GetSettingsGroup<IcuVerifierSettings>();
                _settings.BeginEdit();
                _viewModel = new IcuVerifierSettingsViewModel(_settings);
                _view = new IcuVerifierSettingsView { DataContext = _viewModel };
            }

            return _view;
        }

        public override void Save()
        {
            if (_viewModel != null) _viewModel.Apply();
            if (_settings != null) _settings.EndEdit();
            base.Save();
        }

        public override void Cancel()
        {
            if (_settings != null) _settings.CancelEdit();
            base.Cancel();
        }

        public override void ResetToDefaults()
        {
            if (_viewModel != null) _viewModel.ResetToDefaults();
        }

        public override void Dispose()
        {
            _view = null;
            _viewModel = null;
            _settings = null;
        }
    }
}
