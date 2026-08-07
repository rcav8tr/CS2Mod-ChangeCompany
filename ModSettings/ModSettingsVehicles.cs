using Game.Settings;
using Game.UI;

namespace ChangeCompany
{
    /// <summary>
    /// The vehicles settings for this mod.
    /// </summary>
    public partial class ModSettings
    {
        // Define default vehicles.
        // These are hard-coded to match TransportCompanyData.m_MaxTransports for each company type.
        // Currently in the game, all companies of a type have the same default number of vehicles.
        // If the game changes any default, these need to be updated.
        public const int DefaultVehiclesCommercial =  1;
        public const int DefaultVehiclesExtractor  =  5;
        public const int DefaultVehiclesIndustrial =  5;
        public const int DefaultVehiclesOffice     =  1;
        public const int DefaultVehiclesWarehouse  = 10;

        // Define minimum vehicles as the default.
        private const float MinVehiclesCommercial = DefaultVehiclesCommercial;
        private const float MinVehiclesExtractor  = DefaultVehiclesExtractor ;
        private const float MinVehiclesIndustrial = DefaultVehiclesIndustrial;
        private const float MinVehiclesOffice     = DefaultVehiclesOffice    ;
        private const float MinVehiclesWarehouse  = DefaultVehiclesWarehouse ;

        // Define maximum vehicles as a reasonable number above the minimum.
        private const float MaxVehiclesCommercial = 10f;
        private const float MaxVehiclesExtractor  = 25f;
        private const float MaxVehiclesIndustrial = 25f;
        private const float MaxVehiclesOffice     = 10f;
        private const float MaxVehiclesWarehouse  = 30f;

        /// <summary>
        /// Set default values for vehicles settings.
        /// </summary>
        private void SetDefaultsVehicles()
        {
            VehiclesUseATTOverrides = false;
            VehiclesCommercial = DefaultVehiclesCommercial;
            VehiclesExtractor  = DefaultVehiclesExtractor;
            VehiclesIndustrial = DefaultVehiclesIndustrial;
            VehiclesOffice     = DefaultVehiclesOffice;
            VehiclesWarehouse  = DefaultVehiclesWarehouse;
        }

        // General description for Vehicles.
        [SettingsUISection(TabVehicles, "")]
        [SettingsUIMultilineText]
        public string VehiclesGeneralDescription => Translation.Get(UITranslationKey.SettingVehiclesGeneralDescription);

        // Whether or not to use overrides from All Transit+Trucks mod if active.
        private bool _vehiclesUseATTOverrides;
        [SettingsUISection(TabVehicles, "")]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(AllTransitTrucksModActive), invert: true)]
        public bool VehiclesUseATTOverrides
        {
            get { return _vehiclesUseATTOverrides; }
            set
            {
                _vehiclesUseATTOverrides = value;

                // Check for value set or cleared.
                if (value)
                {
                    RestoreVehicleDefaultsIfLoaded();
                }
                else
                {
                    OverrideVehiclesIfLoaded();
                }
            }
        }

        /// <summary>
        /// Whether or not the All Transit+Trucks mod is active.
        /// </summary>
        private bool AllTransitTrucksModActive()
        {
            return _overrideVehiclesSystem.AllTransitTrucksModActive;
        }

        /// <summary>
        /// Whether or not All Transit+Trucks mod is active and being used to override vehicles.
        /// </summary>
        private bool AllTransitTrucksModActiveAndUsed()
        {
            return AllTransitTrucksModActive() && VehiclesUseATTOverrides;
        }

        // Vehicles for commercial.
        private int _vehiclesCommercial;
        [SettingsUISection(TabVehicles, "")]
        [SettingsUISlider(min = MinVehiclesCommercial, max = MaxVehiclesCommercial, step = 1f, scalarMultiplier = 1f, unit = Unit.kInteger)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(AllTransitTrucksModActiveAndUsed))]
        public int VehiclesCommercial
        {
            get { return _vehiclesCommercial; }
            set { _vehiclesCommercial = value; OverrideVehiclesIfLoaded(); }
        }

        // Vehicles for extractor.
        private int _vehiclesExtractor;
        [SettingsUISection(TabVehicles, "")]
        [SettingsUISlider(min = MinVehiclesExtractor, max = MaxVehiclesExtractor, step = 1f, scalarMultiplier = 1f, unit = Unit.kInteger)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(AllTransitTrucksModActiveAndUsed))]
        public int VehiclesExtractor
        {
            get { return _vehiclesExtractor; }
            set { _vehiclesExtractor = value; OverrideVehiclesIfLoaded(); }
        }

        // Vehicles for industrial.
        private int _vehiclesIndustrial;
        [SettingsUISection(TabVehicles, "")]
        [SettingsUISlider(min = MinVehiclesIndustrial, max = MaxVehiclesIndustrial, step = 1f, scalarMultiplier = 1f, unit = Unit.kInteger)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(AllTransitTrucksModActiveAndUsed))]
        public int VehiclesIndustrial
        {
            get { return _vehiclesIndustrial; }
            set { _vehiclesIndustrial = value; OverrideVehiclesIfLoaded(); }
        }

        // Vehicles for office.
        private int _vehiclesOffice;
        [SettingsUISection(TabVehicles, "")]
        [SettingsUISlider(min = MinVehiclesOffice, max = MaxVehiclesOffice, step = 1f, scalarMultiplier = 1f, unit = Unit.kInteger)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(AllTransitTrucksModActiveAndUsed))]
        public int VehiclesOffice
        {
            get { return _vehiclesOffice; }
            set { _vehiclesOffice = value; OverrideVehiclesIfLoaded(); }
        }

        // Vehicles for warehouse.
        private int _vehiclesWarehouse;
        [SettingsUISection(TabVehicles, "")]
        [SettingsUISlider(min = MinVehiclesWarehouse, max = MaxVehiclesWarehouse, step = 1f, scalarMultiplier = 1f, unit = Unit.kInteger)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(AllTransitTrucksModActiveAndUsed))]
        public int VehiclesWarehouse
        {
            get { return _vehiclesWarehouse; }
            set { _vehiclesWarehouse = value; OverrideVehiclesIfLoaded(); }
        }

        // Button to reset vehicle settings.
        [SettingsUIButton()]
        [SettingsUISection(TabVehicles, "")]
        public bool VehiclesReset
        {
            set { SetDefaultsVehicles(); }
        }

        /// <summary>
        /// Override vehicles if settings are loaded.
        /// </summary>
        private void OverrideVehiclesIfLoaded()
        {
            // Settings must be loaded.
            // This prevents overriding vehicles while initial defaults are being set and while settings are loading.
            if (_loaded)
            {
                _overrideVehiclesSystem.OverrideVehicles();
            }
        }

        /// <summary>
        /// Restore vehicle defaults if settings are loaded.
        /// </summary>
        private void RestoreVehicleDefaultsIfLoaded()
        {
            // Settings must be loaded.
            // This prevents setting vehicles while initial defaults are being set and while settings are loading.
            if (_loaded)
            {
                _overrideVehiclesSystem.RestoreDefaults();
            }
        }
    }
}
