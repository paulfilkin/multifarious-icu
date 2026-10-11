using System;
using multifarious.Icu.Expansion;
using Sdl.Core.Settings;

namespace multifarious.Icu.BatchTasks.Settings
{
    /// <summary>
    /// The restore task's settings, stored and defaulted exactly as <see cref="IcuExpandSettings"/>.
    /// </summary>
    public class IcuRestoreSettings : SettingsGroup
    {
        private const string OnEmptyBranchId = "OnEmptyBranch";
        private const string OnPlaceholderMismatchId = "OnPlaceholderMismatch";

        public EmptyBranchBehaviour OnEmptyBranch
        {
            get { return Parse(GetSetting<string>(OnEmptyBranchId).Value, RestoreOptions.Default.OnEmptyBranch); }
            set { GetSetting<string>(OnEmptyBranchId).Value = value.ToString(); }
        }

        public PlaceholderMismatchBehaviour OnPlaceholderMismatch
        {
            get { return Parse(GetSetting<string>(OnPlaceholderMismatchId).Value, RestoreOptions.Default.OnPlaceholderMismatch); }
            set { GetSetting<string>(OnPlaceholderMismatchId).Value = value.ToString(); }
        }

        public RestoreOptions ToOptions()
        {
            return new RestoreOptions
            {
                OnEmptyBranch = OnEmptyBranch,
                OnPlaceholderMismatch = OnPlaceholderMismatch,
            };
        }

        protected override object GetDefaultValue(string settingId)
        {
            switch (settingId)
            {
                case OnEmptyBranchId: return RestoreOptions.Default.OnEmptyBranch.ToString();
                case OnPlaceholderMismatchId: return RestoreOptions.Default.OnPlaceholderMismatch.ToString();
            }

            return base.GetDefaultValue(settingId);
        }

        private static T Parse<T>(string value, T fallback) where T : struct
        {
            T parsed;
            return value != null && Enum.TryParse(value, true, out parsed) && Enum.IsDefined(typeof(T), parsed)
                ? parsed
                : fallback;
        }
    }
}
