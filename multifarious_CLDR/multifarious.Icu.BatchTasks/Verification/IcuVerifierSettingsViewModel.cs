using System.Collections.Generic;
using System.Linq;
using multifarious.Icu.BatchTasks.Resources;
using multifarious.Icu.BatchTasks.Settings.ViewModels;

namespace multifarious.Icu.BatchTasks.Verification
{
    /// <summary>One severity the page offers, with its label.</summary>
    public sealed class SeverityChoice
    {
        public SeverityChoice(CheckSeverity severity, string label)
        {
            Severity = severity;
            Label = label;
        }

        public CheckSeverity Severity { get; }

        public string Label { get; }

        public override string ToString()
        {
            return Label;
        }
    }

    /// <summary>The verifier page: the Enabled tick box and one severity per check.</summary>
    public sealed class IcuVerifierSettingsViewModel : ObservableObject
    {
        private readonly IcuVerifierSettings _settings;
        private bool _enabled;
        private SeverityChoice _emptyForm;
        private SeverityChoice _placeholders;
        private SeverityChoice _typedPound;
        private SeverityChoice _invalid;

        public IcuVerifierSettingsViewModel(IcuVerifierSettings settings)
        {
            _settings = settings ?? new IcuVerifierSettings();
            Severities = new List<SeverityChoice>
            {
                new SeverityChoice(CheckSeverity.Error, UIStrings.Severity_Error),
                new SeverityChoice(CheckSeverity.Warning, UIStrings.Severity_Warning),
                new SeverityChoice(CheckSeverity.Note, UIStrings.Severity_Note),
                new SeverityChoice(CheckSeverity.Ignore, UIStrings.Severity_Ignore),
            };
            LoadFrom(_settings);
        }

        public IReadOnlyList<SeverityChoice> Severities { get; }

        public bool Enabled
        {
            get { return _enabled; }
            set { Set(ref _enabled, value); }
        }

        public SeverityChoice EmptyForm
        {
            get { return _emptyForm; }
            set { Set(ref _emptyForm, value ?? _emptyForm); }
        }

        public SeverityChoice Placeholders
        {
            get { return _placeholders; }
            set { Set(ref _placeholders, value ?? _placeholders); }
        }

        public SeverityChoice TypedPound
        {
            get { return _typedPound; }
            set { Set(ref _typedPound, value ?? _typedPound); }
        }

        public SeverityChoice Invalid
        {
            get { return _invalid; }
            set { Set(ref _invalid, value ?? _invalid); }
        }

        public IcuVerifierSettings Apply()
        {
            _settings.Enabled = _enabled;
            _settings.EmptyForm = _emptyForm.Severity;
            _settings.Placeholders = _placeholders.Severity;
            _settings.TypedPound = _typedPound.Severity;
            _settings.Invalid = _invalid.Severity;
            return _settings;
        }

        public IcuVerifierSettings ResetToDefaults()
        {
            _settings.Reset();
            LoadFrom(_settings);
            Raise(nameof(Enabled));
            Raise(nameof(EmptyForm));
            Raise(nameof(Placeholders));
            Raise(nameof(TypedPound));
            Raise(nameof(Invalid));
            return _settings;
        }

        private void LoadFrom(IcuVerifierSettings settings)
        {
            _enabled = settings.Enabled;
            _emptyForm = Choice(settings.EmptyForm);
            _placeholders = Choice(settings.Placeholders);
            _typedPound = Choice(settings.TypedPound);
            _invalid = Choice(settings.Invalid);
        }

        private SeverityChoice Choice(CheckSeverity severity)
        {
            return Severities.First(choice => choice.Severity == severity);
        }
    }
}
