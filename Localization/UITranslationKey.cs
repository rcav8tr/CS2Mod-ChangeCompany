namespace ChangeCompany
{
    // Define UI translation keys.
    // These must match the translation keys used in the UI.
    public class UITranslationKey
    {
        // Error text.
        public const string IncompatibleWithEconomyFixes    = ModAssemblyInfo.Name + ".IncompatibleWithEconomyFixes";

        // Section text.
        public const string ChangeCompany                   = ModAssemblyInfo.Name + ".ChangeCompany";
        public const string ChangeThis                      = ModAssemblyInfo.Name + ".ChangeThis";
        public const string ChangeAll                       = ModAssemblyInfo.Name + ".ChangeAll";

        public const string LockCompany                     = ModAssemblyInfo.Name + ".LockCompany";
        public const string LockAll                         = ModAssemblyInfo.Name + ".LockAll";
        public const string UnlockAll                       = ModAssemblyInfo.Name + ".UnlockAll";

        public const string Random                          = ModAssemblyInfo.Name + ".Random";
        public const string Remove                          = ModAssemblyInfo.Name + ".Remove";

        // Section tool tips for change company, one for each property type.
        private const string SectionTooltipPrefix = ".SectionTooltip";
        public const string SectionTooltipCommercial        = ModAssemblyInfo.Name + SectionTooltipPrefix + "Commercial";
        public const string SectionTooltipIndustrial        = ModAssemblyInfo.Name + SectionTooltipPrefix + "Industrial";
        public const string SectionTooltipOffice            = ModAssemblyInfo.Name + SectionTooltipPrefix + "Office";
        public const string SectionTooltipStorage           = ModAssemblyInfo.Name + SectionTooltipPrefix + "Storage";

        // Section tool tip for optional change all.
        public const string SectionTooltipChangeAll         = ModAssemblyInfo.Name + SectionTooltipPrefix + "ChangeAll";

        // Section tool tip for lock company.
        public const string SectionTooltipLockCompany       = ModAssemblyInfo.Name + SectionTooltipPrefix + "LockCompany";

        // Settings.
        public const string SettingTitle                    = "Options.SECTION[ChangeCompany.ChangeCompany.Mod]";
        
        public const string SettingGroupLockCompany         = "Options.GROUP[ChangeCompany.ChangeCompany.Mod.LockCompany]";
        public const string SettingLockAfterChangeLabel     = "Options.OPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.LockAfterChange]";
        public const string SettingLockAfterChangeDesc      = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.LockAfterChange]";
        public const string SettingLockAllCompaniesLabel    = "Options.OPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.LockAllCompanies]";
        public const string SettingLockAllCompaniesDesc     = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.LockAllCompanies]";
        public const string SettingUnlockAllCompaniesLabel  = "Options.OPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.UnlockAllCompanies]";
        public const string SettingUnlockAllCompaniesDesc   = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.UnlockAllCompanies]";
        
        public const string SettingGroupAbout               = "Options.GROUP[ChangeCompany.ChangeCompany.Mod.About]";
        public const string SettingModVersionLabel          = "Options.OPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ModVersion]";
        public const string SettingModVersionDesc           = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ModVersion]";

    }
}
