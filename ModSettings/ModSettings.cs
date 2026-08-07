using Colossal.IO.AssetDatabase;
using Game;
using Game.Input;
using Game.Modding;
using Game.SceneFlow;
using Game.Settings;
using Unity.Entities;

namespace ChangeCompany
{
    /// <summary>
    /// The settings for this mod.
    /// </summary>
    [FileLocation("ModsSettings/" + nameof(ChangeCompany) + "/" + nameof(ChangeCompany))]
	[SettingsUITabOrder(TabProductionBalance, TabLock, TabWorkplaces, TabStorage, TabVehicles, TabAbout)]
    [SettingsUIKeyboardAction(ProductionBalanceActivationKeyActionName, modifierOptions: ModifierOptions.Allow)]
    public partial class ModSettings : ModSetting
    {
        // Tab constants.
        private const string TabProductionBalance = "ProductionBalance";
        private const string TabLock              = "Lock";
        private const string TabWorkplaces        = "Workplaces";
        private const string TabStorage           = "Storage";
        private const string TabVehicles          = "Vehicles";
        private const string TabAbout             = "About";

        // Whether or not settings are loaded.
        private bool _loaded = false;

        // Systems.
        private readonly ProductionBalanceSystem   _productionBalanceSystem;
        private readonly ProductionBalanceUISystem _productionBalanceUISystem;
        private readonly OverrideStorageSystem     _overrideStorageSystem;
        private readonly OverrideVehiclesSystem    _overrideVehiclesSystem;

        // Sections.
        private readonly LockCompanySection        _lockCompanySection;
        private readonly CompanyWorkplacesSection  _companyWorkplacesSection;

        // Constructor.
        public ModSettings(IMod mod) : base(mod)
        {
            Mod.log.Info($"{nameof(ModSettings)}.{nameof(ModSettings)}");

            // Get systems.
            World defaultWorld = World.DefaultGameObjectInjectionWorld;
            _productionBalanceSystem   = defaultWorld.GetOrCreateSystemManaged<ProductionBalanceSystem  >();
            _productionBalanceUISystem = defaultWorld.GetOrCreateSystemManaged<ProductionBalanceUISystem>();
            _overrideStorageSystem     = defaultWorld.GetOrCreateSystemManaged<OverrideStorageSystem    >();
            _overrideVehiclesSystem    = defaultWorld.GetOrCreateSystemManaged<OverrideVehiclesSystem   >();

            // Get sections.
            _lockCompanySection        = defaultWorld.GetOrCreateSystemManaged<LockCompanySection      >();
            _companyWorkplacesSection  = defaultWorld.GetOrCreateSystemManaged<CompanyWorkplacesSection>();

            // Set defaults.
            SetDefaults();
        }

        /// <summary>
        /// Set defaults.
        /// </summary>
        public override void SetDefaults()
        {
            SetDefaultsProductionBalance();
            SetDefaultsLockCompany();
            SetDefaultsWorkplaces();
            SetDefaultsStorage();
            SetDefaultsVehicles();
        }

        /// <summary>
        /// Set loaded flag.
        /// </summary>
        public void Loaded()
        {
            _loaded = true;
        }

        /// <summary>
        /// Get whether or not app is in a game.
        /// </summary>
        private bool InGame()
        {
            return GameManager.instance.gameMode == GameMode.Game;
        }

        // Display mod version in settings.
        [SettingsUISection(TabAbout, "")]
        public string ModVersion => ModAssemblyInfo.Version;
    }
}
