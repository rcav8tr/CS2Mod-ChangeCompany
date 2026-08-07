using Game.Settings;

namespace ChangeCompany
{
    /// <summary>
    /// The lock company settings for this mod.
    /// </summary>
    public partial class ModSettings
    {
        /// <summary>
        /// Set default values for lock company settings.
        /// </summary>
        private void SetDefaultsLockCompany()
        {
            LockAfterChange  = false;
            LockAllCompanies = false;
        }

        // General description for Lock.
        [SettingsUISection(TabLock, "")]
        [SettingsUIMultilineText]
        public string LockGeneralDescription => Translation.Get(UITranslationKey.SettingLockGeneralDescription);

        // Whether or not to automatically lock a company after the company is changed.
        [SettingsUISection(TabLock, "")]
        public bool LockAfterChange { get; set;}

        // Whether or not to lock all companies.
        private bool _lockAllCompanies = false;
        [SettingsUISection(TabLock, "")]
        public bool LockAllCompanies
        {
            get
            {
                return _lockAllCompanies;
            }
            set
            {
                _lockAllCompanies = value;

                // Settings must be loaded.
                // This prevents locking/unlocking while initial defaults are being set and while settings are loading.
                if (_loaded)
                {
                    // Lock or unlock all companies.
                    _lockCompanySection.LockOrUnlockAllCompanies();
                }
            }
        }

        // Button to unlock all companies.
        // Hide the button if NOT in a game.
        [SettingsUIButton()]
        [SettingsUISection(TabLock, "")]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(InGame), invert: true)]
        public bool UnlockAllCompanies
        {
            set
            {
                _lockCompanySection.UnlockAllCompanies();
            }
        }
    }
}
