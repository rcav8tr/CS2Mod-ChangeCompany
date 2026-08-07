using Game.Settings;

namespace ChangeCompany
{
    /// <summary>
    /// The workplaces settings for this mod.
    /// </summary>
    public partial class ModSettings
    {
        /// <summary>
        /// Set default values for workplaces settings.
        /// </summary>
        private void SetDefaultsWorkplaces()
        {
            KeepWorkplacesOverrideAfterChange = false;
            WorkplacesOverrideValue = 30;
        }

        // General description for Workplaces.
        [SettingsUISection(TabWorkplaces, "")]
        [SettingsUIMultilineText]
        public string WorkplacesGeneralDescription => Translation.Get(UITranslationKey.SettingWorkplacesGeneralDescription);

        // Whether or not to keep a company workplaces overrride after the company is changed.
        [SettingsUISection(TabWorkplaces, "")]
        public bool KeepWorkplacesOverrideAfterChange { get; set;}

        // Button to remove all company workplace overrides.
        // Hide the button if NOT in a game.
        [SettingsUIButton()]
        [SettingsUISection(TabWorkplaces, "")]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(InGame), invert: true)]
        public bool CompanyWorkplacesRemoveAllOverrides
        {
            set { _companyWorkplacesSection.RemoveAllWorkplacesOverrides(); }
        }

        // Company workplaces override value.
        [SettingsUIHidden]
        public int WorkplacesOverrideValue { get; set; }
    }
}
