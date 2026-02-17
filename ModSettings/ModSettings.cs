using Colossal.IO.AssetDatabase;
using Game;
using Game.Input;
using Game.Modding;
using Game.SceneFlow;
using Game.Settings;
using Game.UI;
using Unity.Entities;

namespace ChangeCompany
{
    /// <summary>
    /// The settings for this mod.
    /// </summary>
    [FileLocation(nameof(ChangeCompany))]
	[SettingsUITabOrder(TabProductionBalance, TabLockCompany, TabCompanyWorkplaces, TabAbout)]
    [SettingsUIKeyboardAction(ProductionBalanceActivationKeyActionName, modifierOptions: ModifierOptions.Allow)]
    public class ModSettings : ModSetting
    {
        // Tab constants.
        private const string TabProductionBalance = "ProductionBalance";
        private const string TabLockCompany       = "LockCompany";
        private const string TabCompanyWorkplaces = "CompanyWorkplaces";
        private const string TabAbout             = "About";

        // Production balance activation key action name.
        public const string ProductionBalanceActivationKeyActionName = "ProductionBalanceActivationKey";

        // Whether or not settings are loaded.
        private bool _loaded = false;

        // Other systems.
        private readonly ProductionBalanceSystem   _productionBalanceSystem;
        private readonly ProductionBalanceUISystem _productionBalanceUISystem;
        private readonly LockCompanySection        _lockCompanySection;
        private readonly CompanyWorkplacesSection  _companyWorkplacesSection;

        // Constructor.
        public ModSettings(IMod mod) : base(mod)
        {
            Mod.log.Info($"{nameof(ModSettings)}.{nameof(ModSettings)}");

            // Get other systems.
            World defaultWorld = World.DefaultGameObjectInjectionWorld;
            _productionBalanceSystem   = defaultWorld.GetOrCreateSystemManaged<ProductionBalanceSystem>();
            _productionBalanceUISystem = defaultWorld.GetOrCreateSystemManaged<ProductionBalanceUISystem>();
            _lockCompanySection        = defaultWorld.GetOrCreateSystemManaged<LockCompanySection>();
            _companyWorkplacesSection  = defaultWorld.GetOrCreateSystemManaged<CompanyWorkplacesSection>();

            // Set defaults.
            SetDefaults();
        }

        /// <summary>
        /// Set a default value for every setting that has a value that can change.
        /// </summary>
        public override void SetDefaults()
        {
            SetDefaultsProductionBalance();
            LockAfterChange  = false;
            LockAllCompanies = false;
            KeepWorkplacesOverrideAfterChange = false;
            WorkplacesOverrideValue = 30;
        }

        /// <summary>
        /// Set default values for production balance settings.
        /// </summary>
        private void SetDefaultsProductionBalance()
        {
            ProductionBalanceEnabledIndustrial                  = false;
            ProductionBalanceEnabledOffice                      = false;
            
            ProductionBalanceCheckIntervalIndustrial            = 10;
            ProductionBalanceCheckIntervalOffice                = 10;
            
            ProductionBalanceMinimumCompaniesIndustrial         = 50;
            ProductionBalanceMinimumCompaniesOffice             = 15;
            
            ProductionBalanceMinimumStandardDeviationIndustrial = 80;
            ProductionBalanceMinimumStandardDeviationOffice     = 80;
            
            ProductionBalanceMaximumCompanyProductionIndustrial = 50;
            ProductionBalanceMaximumCompanyProductionOffice     = 50;

            ProductionBalanceHideActivationButton               = false;
            ResetKeyBindings();
            
            ProductionBalancePanelVisible                       = false;
            ProductionBalancePanelPositionX                     = 175;
            ProductionBalancePanelPositionY                     = 55;
        }

        /// <summary>
        /// Set loaded flag.
        /// </summary>
        public void Loaded()
        {
            _loaded = true;
        }

        // General description for production balance.
        [SettingsUISection(TabProductionBalance, "")]
        [SettingsUIMultilineText]
        public string ProductionBalanceGeneralDescription => Translation.Get(UITranslationKey.SettingProductionBalanceGeneralDescription);

        // Whether or not production balance is enabled, for industrial.
        private bool _productionBalanceEnabledIndustrial;
        [SettingsUISection(TabProductionBalance, "")]
        public bool ProductionBalanceEnabledIndustrial
        {
            get { return _productionBalanceEnabledIndustrial; }
            set { _productionBalanceEnabledIndustrial = value; SetProductionBalanceNextCheckIndustrial(); }
        }

        // Whether or not production balance is enabled, for office.
        private bool _productionBalanceEnabledOffice;
        [SettingsUISection(TabProductionBalance, "")]
        public bool ProductionBalanceEnabledOffice
        {
            get { return _productionBalanceEnabledOffice; }
            set { _productionBalanceEnabledOffice = value; SetProductionBalanceNextCheckOffice(); }
        }

        // Interval in minutes between production balance checks, for industrial.
        private const float MinInterval = 1f;
        private const float MaxInterval = 60f;
        private int _productionBalanceCheckIntervalIndustrial;
        [SettingsUISection(TabProductionBalance, "")]
        [SettingsUISlider(min = MinInterval, max = MaxInterval, step = 1f, scalarMultiplier = 1f, unit = Unit.kInteger)]
        public int ProductionBalanceCheckIntervalIndustrial
        {
            get { return _productionBalanceCheckIntervalIndustrial; }
            set { _productionBalanceCheckIntervalIndustrial = value; SetProductionBalanceNextCheckIndustrial(); }
        }

        // Interval in minutes between production balance checks, for office.
        private int _productionBalanceCheckIntervalOffice;
        [SettingsUISection(TabProductionBalance, "")]
        [SettingsUISlider(min = MinInterval, max = MaxInterval, step = 1f, scalarMultiplier = 1f, unit = Unit.kInteger)]
        public int ProductionBalanceCheckIntervalOffice
        {
            get { return _productionBalanceCheckIntervalOffice; }
            set { _productionBalanceCheckIntervalOffice = value; SetProductionBalanceNextCheckOffice(); }
        }

        // Minimum number of companies to allow production balance, for industrial.
        [SettingsUISection(TabProductionBalance, "")]
        [SettingsUISlider(min = 20f, max = 100f, step = 1f, scalarMultiplier = 1f, unit = Unit.kInteger)]
        public int ProductionBalanceMinimumCompaniesIndustrial { get; set; }

        // Minimum number of companies to allow production balance, for office.
        [SettingsUISection(TabProductionBalance, "")]
        [SettingsUISlider(min = 5f, max = 50f, step = 1f, scalarMultiplier = 1f, unit = Unit.kInteger)]
        public int ProductionBalanceMinimumCompaniesOffice { get; set; }

        // Minimum standard deviation percent of surpluses to allow production balance, for industrial.
        private const float MinStdDev = 10f;
        private const float MaxStdDev = 150f;
        [SettingsUISection(TabProductionBalance, "")]
        [SettingsUISlider(min = MinStdDev, max = MaxStdDev, step = 1f, scalarMultiplier = 1f, unit = Unit.kPercentage)]
        public int ProductionBalanceMinimumStandardDeviationIndustrial { get; set; }

        // Minimum standard deviation percent of surpluses to allow production balance, for office.
        [SettingsUISection(TabProductionBalance, "")]
        [SettingsUISlider(min = MinStdDev, max = MaxStdDev, step = 1f, scalarMultiplier = 1f, unit = Unit.kPercentage)]
        public int ProductionBalanceMinimumStandardDeviationOffice { get; set; }

        // Maximum production of the company as a percent of the city's production to allow production balance, for industrial.
        private const float MinMaxProd = 20f;
        private const float MaxMaxProd = 100f;
        [SettingsUISection(TabProductionBalance, "")]
        [SettingsUISlider(min = MinMaxProd, max = MaxMaxProd, step = 1f, scalarMultiplier = 1f, unit = Unit.kPercentage)]
        public int ProductionBalanceMaximumCompanyProductionIndustrial { get; set; }

        // Maximum production of the company as a percent of the city's production to allow production balance, for office.
        [SettingsUISection(TabProductionBalance, "")]
        [SettingsUISlider(min = MinMaxProd, max = MaxMaxProd, step = 1f, scalarMultiplier = 1f, unit = Unit.kPercentage)]
        public int ProductionBalanceMaximumCompanyProductionOffice { get; set; }

        // Whether or not to hide the activation button.
        private bool _productionBalanceHideActivationButton;
        [SettingsUISection(TabProductionBalance, "")]
        public bool ProductionBalanceHideActivationButton
        {
            get { return _productionBalanceHideActivationButton; }
            set { _productionBalanceHideActivationButton = value; UpdateProductionBalanceUISettingsIfLoaded(); }
        }

        // Activation key binding.
        // Default is Ctrl+Shift+B.
        private ProxyBinding _productionBalanceActivationKey;
        [SettingsUIKeyboardBinding(BindingKeyboard.B, ProductionBalanceActivationKeyActionName, ctrl: true, shift: true)]
        [SettingsUISection(TabProductionBalance, "")]
        public ProxyBinding ProductionBalanceActivationKey
        {
            get { return _productionBalanceActivationKey; }
            set { _productionBalanceActivationKey = value; UpdateProductionBalanceUISettingsIfLoaded(); }
        }

        // Production balance panel visibility.
        [SettingsUIHidden]
        public bool ProductionBalancePanelVisible { get; set; }

        // Production balance panel position (in pixels).
        [SettingsUIHidden]
        public int ProductionBalancePanelPositionX { get; set; }
        [SettingsUIHidden]
        public int ProductionBalancePanelPositionY { get; set; }

        // Button to reset production balance settings.
        [SettingsUIButton()]
        [SettingsUISection(TabProductionBalance, "")]
        public bool ProductionBalanceReset
        {
            set { SetDefaultsProductionBalance(); UpdateProductionBalanceUISettingsIfLoaded(); }
        }

        /// <summary>
        /// Set production balance next check date/time for industrial.
        /// </summary>
        private void SetProductionBalanceNextCheckIndustrial()
        {
            // Settings must be loaded.
            // This prevents setting production balance next check date/time while initial defaults are being set and while settings are loading.
            if (_loaded)
            {
                _productionBalanceSystem.SetProductionBalanceNextCheckIndustrial();
            }
        }

        /// <summary>
        /// Set production balance next check date/time for office.
        /// </summary>
        private void SetProductionBalanceNextCheckOffice()
        {
            // Settings must be loaded.
            // This prevents setting production balance next check date/time while initial defaults are being set and while settings are loading.
            if (_loaded)
            {
                _productionBalanceSystem.SetProductionBalanceNextCheckOffice();
            }
        }

        /// <summary>
        /// Update production balance settings in UI if settings are loaded.
        /// </summary>
        private void UpdateProductionBalanceUISettingsIfLoaded()
        {
            // Settings must be loaded.
            // This prevents updating UI while initial defaults are being set and while settings are loading.
            if (_loaded)
            {
                _productionBalanceUISystem.UpdateProductionBalanceUISettings();
            }
        }


        // Whether or not to automatically lock a company after the company is changed.
        [SettingsUISection(TabLockCompany, "")]
        public bool LockAfterChange { get; set;}

        // Whether or not to lock all companies.
        private bool _lockAllCompanies = false;
        [SettingsUISection(TabLockCompany, "")]
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
        // Hide the button if not in a game.
        [SettingsUIButton()]
        [SettingsUISection(TabLockCompany, "")]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(NotInGame))]
        public bool UnlockAllCompanies
        {
            set
            {
                _lockCompanySection.UnlockAllCompanies();
            }
        }

        /// <summary>
        /// Get whether or not app is NOT in a game.
        /// </summary>
        private bool NotInGame()
        {
            return GameManager.instance.gameMode != GameMode.Game;
        }
        

        // Whether or not to keep a company workplaces overrride after the company is changed.
        [SettingsUISection(TabCompanyWorkplaces, "")]
        public bool KeepWorkplacesOverrideAfterChange { get; set;}

        // Button to remove all company workplace overrides.
        // Hide the button if not in a game.
        [SettingsUIButton()]
        [SettingsUISection(TabCompanyWorkplaces, "")]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(NotInGame))]
        public bool CompanyWorkplacesRemoveAllOverrides
        {
            set { _companyWorkplacesSection.RemoveAllWorkplacesOverrides(); }
        }

        // Company workplaces override value.
        [SettingsUIHidden]
        public int WorkplacesOverrideValue { get; set; }


        // Display mod version in settings.
        [SettingsUISection(TabAbout, "")]
        public string ModVersion { get { return ModAssemblyInfo.Version; } }
    }
}
