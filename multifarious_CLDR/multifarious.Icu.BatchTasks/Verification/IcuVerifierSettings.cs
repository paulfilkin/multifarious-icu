using System;
using Sdl.Core.Settings;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.BatchTasks.Verification
{
    /// <summary>How one of the verifier's checks is reported; Ignore drops it.</summary>
    public enum CheckSeverity
    {
        Ignore,
        Note,
        Warning,
        Error
    }

    /// <summary>
    /// The ICU verifier's settings, stored under the class name as Studio keys a settings group,
    /// which is also the verifier's settings id. "Enabled" is the flag Studio's verification
    /// framework reads for every verifier; the four severities are this plugin's own.
    /// </summary>
    public class IcuVerifierSettings : SettingsGroup
    {
        private const string EnabledId = "Enabled";
        private const string EmptyFormId = "EmptyFormSeverity";
        private const string PlaceholdersId = "PlaceholdersSeverity";
        private const string TypedPoundId = "TypedPoundSeverity";
        private const string InvalidId = "InvalidSeverity";

        public bool Enabled
        {
            get { return GetSetting<bool>(EnabledId).Value; }
            set { GetSetting<bool>(EnabledId).Value = value; }
        }

        /// <summary>A form the target language needs with no translation.</summary>
        public CheckSeverity EmptyForm
        {
            get { return Parse(GetSetting<string>(EmptyFormId).Value, CheckSeverity.Warning); }
            set { GetSetting<string>(EmptyFormId).Value = value.ToString(); }
        }

        /// <summary>Protected placeholders in the target that differ from the source's.</summary>
        public CheckSeverity Placeholders
        {
            get { return Parse(GetSetting<string>(PlaceholdersId).Value, CheckSeverity.Error); }
            set { GetSetting<string>(PlaceholdersId).Value = value.ToString(); }
        }

        /// <summary>A '#' typed as text where the source uses the protected count marker.</summary>
        public CheckSeverity TypedPound
        {
            get { return Parse(GetSetting<string>(TypedPoundId).Value, CheckSeverity.Warning); }
            set { GetSetting<string>(TypedPoundId).Value = value.ToString(); }
        }

        /// <summary>A reassembled message that will not parse as ICU.</summary>
        public CheckSeverity Invalid
        {
            get { return Parse(GetSetting<string>(InvalidId).Value, CheckSeverity.Error); }
            set { GetSetting<string>(InvalidId).Value = value.ToString(); }
        }

        /// <summary>The framework's level for a severity; null for Ignore.</summary>
        public static ErrorLevel? LevelOf(CheckSeverity severity)
        {
            switch (severity)
            {
                case CheckSeverity.Error: return ErrorLevel.Error;
                case CheckSeverity.Warning: return ErrorLevel.Warning;
                case CheckSeverity.Note: return ErrorLevel.Note;
                default: return null;
            }
        }

        protected override object GetDefaultValue(string settingId)
        {
            switch (settingId)
            {
                case EnabledId: return true;
                case EmptyFormId: return CheckSeverity.Warning.ToString();
                case PlaceholdersId: return CheckSeverity.Error.ToString();
                case TypedPoundId: return CheckSeverity.Warning.ToString();
                case InvalidId: return CheckSeverity.Error.ToString();
            }

            return base.GetDefaultValue(settingId);
        }

        private static CheckSeverity Parse(string value, CheckSeverity fallback)
        {
            CheckSeverity parsed;
            return value != null && Enum.TryParse(value, true, out parsed) && Enum.IsDefined(typeof(CheckSeverity), parsed)
                ? parsed
                : fallback;
        }
    }
}
