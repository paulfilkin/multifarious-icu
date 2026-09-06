using System;
using multifarious.Icu.Expansion;
using Sdl.Core.Settings;

namespace multifarious.Icu.BatchTasks.Settings
{
    /// <summary>
    /// The finalise task's settings, stored and defaulted exactly as <see cref="IcuExpandSettings"/>.
    /// </summary>
    public class IcuFinaliseSettings : SettingsGroup
    {
        private const string OnEmptyBranchId = "OnEmptyBranch";
        private const string OnPlaceholderMismatchId = "OnPlaceholderMismatch";

        public EmptyBranchBehaviour OnEmptyBranch
        {
            get { return Parse(GetSetting<string>(OnEmptyBranchId).Value, FinaliseOptions.Default.OnEmptyBranch); }
            set { GetSetting<string>(OnEmptyBranchId).Value = value.ToString(); }
        }

        public PlaceholderMismatchBehaviour OnPlaceholderMismatch
        {
            get { return Parse(GetSetting<string>(OnPlaceholderMismatchId).Value, FinaliseOptions.Default.OnPlaceholderMismatch); }
            set { GetSetting<string>(OnPlaceholderMismatchId).Value = value.ToString(); }
        }

        public FinaliseOptions ToOptions()
        {
            return new FinaliseOptions
            {
                OnEmptyBranch = OnEmptyBranch,
                OnPlaceholderMismatch = OnPlaceholderMismatch,
            };
        }

        protected override object GetDefaultValue(string settingId)
        {
            switch (settingId)
            {
                case OnEmptyBranchId: return FinaliseOptions.Default.OnEmptyBranch.ToString();
                case OnPlaceholderMismatchId: return FinaliseOptions.Default.OnPlaceholderMismatch.ToString();
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
