using multifarious.Icu.Expansion;

namespace multifarious.Icu.BatchTasks.Settings.ViewModels
{
    /// <summary>The finalise page: two choices, on the pattern of the expand page.</summary>
    public sealed class IcuFinaliseSettingsViewModel : ObservableObject
    {
        private readonly IcuFinaliseSettings _settings;

        private EmptyBranchBehaviour _onEmptyBranch;
        private PlaceholderMismatchBehaviour _onPlaceholderMismatch;

        public IcuFinaliseSettingsViewModel(IcuFinaliseSettings settings)
        {
            _settings = settings ?? new IcuFinaliseSettings();
            LoadFrom(_settings);
        }

        public bool EmptyBranchUsesSource
        {
            get { return _onEmptyBranch == EmptyBranchBehaviour.UseSource; }
            set { if (value) SetEmptyBranch(EmptyBranchBehaviour.UseSource); }
        }

        public bool EmptyBranchFailsTask
        {
            get { return _onEmptyBranch == EmptyBranchBehaviour.FailTask; }
            set { if (value) SetEmptyBranch(EmptyBranchBehaviour.FailTask); }
        }

        public bool MismatchWarns
        {
            get { return _onPlaceholderMismatch == PlaceholderMismatchBehaviour.Warn; }
            set { if (value) SetMismatch(PlaceholderMismatchBehaviour.Warn); }
        }

        public bool MismatchFailsTask
        {
            get { return _onPlaceholderMismatch == PlaceholderMismatchBehaviour.FailTask; }
            set { if (value) SetMismatch(PlaceholderMismatchBehaviour.FailTask); }
        }

        public EmptyBranchBehaviour OnEmptyBranch
        {
            get { return _onEmptyBranch; }
        }

        public PlaceholderMismatchBehaviour OnPlaceholderMismatch
        {
            get { return _onPlaceholderMismatch; }
        }

        public IcuFinaliseSettings Apply()
        {
            _settings.OnEmptyBranch = _onEmptyBranch;
            _settings.OnPlaceholderMismatch = _onPlaceholderMismatch;
            return _settings;
        }

        public IcuFinaliseSettings ResetToDefaults()
        {
            _settings.Reset();
            LoadFrom(_settings);
            RaiseAll();
            return _settings;
        }

        private void SetEmptyBranch(EmptyBranchBehaviour behaviour)
        {
            if (_onEmptyBranch == behaviour) return;
            _onEmptyBranch = behaviour;
            Raise(nameof(EmptyBranchUsesSource));
            Raise(nameof(EmptyBranchFailsTask));
        }

        private void SetMismatch(PlaceholderMismatchBehaviour behaviour)
        {
            if (_onPlaceholderMismatch == behaviour) return;
            _onPlaceholderMismatch = behaviour;
            Raise(nameof(MismatchWarns));
            Raise(nameof(MismatchFailsTask));
        }

        private void LoadFrom(IcuFinaliseSettings settings)
        {
            _onEmptyBranch = settings.OnEmptyBranch;
            _onPlaceholderMismatch = settings.OnPlaceholderMismatch;
        }

        private void RaiseAll()
        {
            Raise(nameof(EmptyBranchUsesSource));
            Raise(nameof(EmptyBranchFailsTask));
            Raise(nameof(MismatchWarns));
            Raise(nameof(MismatchFailsTask));
        }
    }
}
