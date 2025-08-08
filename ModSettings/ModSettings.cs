using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Settings;
using Unity.Entities;

namespace ChangeCompany
{
    /// <summary>
    /// The settings for this mod.
    /// </summary>
    [FileLocation(nameof(ChangeCompany))]
    [SettingsUIGroupOrder(GroupLockCompany, GroupAbout)]
    [SettingsUIShowGroupName(GroupLockCompany, GroupAbout)]
    public class ModSettings : ModSetting
    {
        // Group constants.
        public const string GroupLockCompany = "LockCompany";
        public const string GroupAbout       = "About";

        // Whether or not settings are loaded.
        private bool _loaded = false;

        // Other systems.
        LockCompanySection _lockCompanySection;

        // Constructor.
        public ModSettings(IMod mod) : base(mod)
        {
            Mod.log.Info($"{nameof(ModSettings)}.{nameof(ModSettings)}");

            // Other systems.
            _lockCompanySection = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<LockCompanySection>();

            // Set defaults.
            SetDefaults();
        }

        /// <summary>
        /// Set a default value for every setting that has a value that can change.
        /// </summary>
        public override void SetDefaults()
        {
            LockAfterChange  = false;
            LockAllCompanies = false;
        }

        /// <summary>
        /// Set loaded flag.
        /// </summary>
        public void Loaded()
        {
            _loaded = true;
        }

        // Whether or not to automatically lock a company after it is changed.
        [SettingsUISection(GroupLockCompany)]
        public bool LockAfterChange { get; set;}

        // Whether or not to lock all companies.
        private bool _lockAllCompanies = false;
        [SettingsUISection(GroupLockCompany)]
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
        [SettingsUISection(GroupLockCompany)]
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

        // Display mod version in settings.
        [SettingsUISection(GroupAbout)]
        public string ModVersion { get { return ModAssemblyInfo.Version; } }
    }
}
