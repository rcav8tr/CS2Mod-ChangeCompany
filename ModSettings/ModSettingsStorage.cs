using Game.Settings;
using Game.UI;

namespace ChangeCompany
{
    /// <summary>
    /// The storage settings for this mod.
    /// </summary>
    public partial class ModSettings
    {
        // Define default and min storage amounts by company type.
        // These are hard-coded to match StorageLimitData.m_Limit for each company type.
        // Currently in the game, all companies of a type have the same default storage limits
        // except offices that produce Financials and Media have 500 instead of 20000.
        // This difference is ignored such that Financials and Media get the same default as other offices.
        // If the game changes any default, these need to be updated.
        public const int DefaultStorageCommercial =   8000;
        public const int DefaultStorageExtractor  = 100000;
        public const int DefaultStorageIndustrial = 100000;
        public const int DefaultStorageOffice     =  20000;
        public const int DefaultStorageWarehouse  =  20000;

        // Define minimum storage as the default.
        private const float MinStorageCommercial = DefaultStorageCommercial;
        private const float MinStorageExtractor  = DefaultStorageExtractor ;
        private const float MinStorageIndustrial = DefaultStorageIndustrial;
        private const float MinStorageOffice     = DefaultStorageOffice    ;
        private const float MinStorageWarehouse  = DefaultStorageWarehouse ;

        // Define maximum storage as a reasonable number above the minimum.
        private const float MaxStorageCommercial =  40000f;
        private const float MaxStorageExtractor  = 500000f;
        private const float MaxStorageIndustrial = 500000f;
        private const float MaxStorageOffice     = 100000f;
        private const float MaxStorageWarehouse  = 100000f;

        // Define storage step as a reasonable number between min and max.
        private const float StepStorageCommercial = 1000f;
        private const float StepStorageExtractor  = 5000f;
        private const float StepStorageIndustrial = 5000f;
        private const float StepStorageOffice     = 1000f;
        private const float StepStorageWarehouse  = 1000f;

        /// <summary>
        /// Set default values for storage settings.
        /// </summary>
        private void SetDefaultsStorage()
        {
            StorageOverridesEnabled = true;
            StorageCommercial = DefaultStorageCommercial;
            StorageExtractor  = DefaultStorageExtractor ;
            StorageIndustrial = DefaultStorageIndustrial;
            StorageOffice     = DefaultStorageOffice    ;
            StorageWarehouse  = DefaultStorageWarehouse ;
        }

        // General description for Storage.
        [SettingsUISection(TabStorage, "")]
        [SettingsUIMultilineText]
        public string StorageGeneralDescription => Translation.Get(UITranslationKey.SettingStorageGeneralDescription);

        // Whether or not to enable storage overrides in this mod.
        private bool _storageOverridesEnabled;
        [SettingsUISection(TabStorage, "")]
        public bool StorageOverridesEnabled
        {
            get { return _storageOverridesEnabled; }
            set
            {
                _storageOverridesEnabled = value;

                // Override storage or restore storage defaults.
                if (value)
                {
                    OverrideStorageIfLoaded();
                }
                else
                {
                    RestoreStorageDefaultsIfLoaded();
                }
            }
        }

        // Storage for commercial.
        private int _storageCommercial;
        [SettingsUISection(TabStorage, "")]
        [SettingsUISlider(min = MinStorageCommercial, max = MaxStorageCommercial, step = StepStorageCommercial, scalarMultiplier = 1f, unit = Unit.kWeight)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(StorageOverridesEnabled), true)]
        public int StorageCommercial
        {
            get { return _storageCommercial; }
            set { _storageCommercial = value; OverrideStorageIfLoaded(); }
        }

        // Storage for extractor.
        private int _storageExtractor;
        [SettingsUISection(TabStorage, "")]
        [SettingsUISlider(min = MinStorageExtractor, max = MaxStorageExtractor, step = StepStorageExtractor, scalarMultiplier = 1f, unit = Unit.kWeight)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(StorageOverridesEnabled), true)]
        public int StorageExtractor
        {
            get { return _storageExtractor; }
            set { _storageExtractor = value; OverrideStorageIfLoaded(); }
        }

        // Storage for industrial.
        private int _storageIndustrial;
        [SettingsUISection(TabStorage, "")]
        [SettingsUISlider(min = MinStorageIndustrial, max = MaxStorageIndustrial, step = StepStorageIndustrial, scalarMultiplier = 1f, unit = Unit.kWeight)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(StorageOverridesEnabled), true)]
        public int StorageIndustrial
        {
            get { return _storageIndustrial; }
            set { _storageIndustrial = value; OverrideStorageIfLoaded(); }
        }

        // Storage for office.
        private int _storageOffice;
        [SettingsUISection(TabStorage, "")]
        [SettingsUISlider(min = MinStorageOffice, max = MaxStorageOffice, step = StepStorageOffice, scalarMultiplier = 1f, unit = Unit.kWeight)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(StorageOverridesEnabled), true)]
        public int StorageOffice
        {
            get { return _storageOffice; }
            set { _storageOffice = value; OverrideStorageIfLoaded(); }
        }

        // Storage for warehouse.
        private int _storageWarehouse;
        [SettingsUISection(TabStorage, "")]
        [SettingsUISlider(min = MinStorageWarehouse, max = MaxStorageWarehouse, step = StepStorageWarehouse, scalarMultiplier = 1f, unit = Unit.kWeight)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(StorageOverridesEnabled), true)]
        public int StorageWarehouse
        {
            get { return _storageWarehouse; }
            set { _storageWarehouse = value; OverrideStorageIfLoaded(); }
        }

        // Button to reset storage settings.
        [SettingsUIButton()]
        [SettingsUISection(TabStorage, "")]
        public bool StorageReset
        {
            set { SetDefaultsStorage(); }
        }

        /// <summary>
        /// Override storage if settings are loaded.
        /// </summary>
        private void OverrideStorageIfLoaded()
        {
            // Settings must be loaded.
            // This prevents overriding storage while initial defaults are being set and while settings are loading.
            if (_loaded)
            {
                _overrideStorageSystem.OverrideStorage();
            }
        }

        /// <summary>
        /// Restore storage defaults if settings are loaded.
        /// </summary>
        private void RestoreStorageDefaultsIfLoaded()
        {
            // Settings must be loaded.
            // This prevents restoring storage while initial defaults are being set and while settings are loading.
            if (_loaded)
            {
                _overrideStorageSystem.RestoreDefaults();
            }
        }
    }
}
