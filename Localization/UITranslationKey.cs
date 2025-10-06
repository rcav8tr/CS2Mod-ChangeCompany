namespace ChangeCompany
{
    // Define UI translation keys.
    // These must match the translation keys used in the UI.
    public class UITranslationKey
    {
        // Error text.
        public const string IncompatibleWithEconomyFixes                            = ModAssemblyInfo.Name + ".IncompatibleWithEconomyFixes";

        // Section text for Change Company.
        public const string ChangeCompany                                           = ModAssemblyInfo.Name + ".ChangeCompany";
        public const string ChangeThis                                              = ModAssemblyInfo.Name + ".ChangeThis";
        public const string ChangeAll                                               = ModAssemblyInfo.Name + ".ChangeAll";
        public const string Random                                                  = ModAssemblyInfo.Name + ".Random";
        public const string Remove                                                  = ModAssemblyInfo.Name + ".Remove";

        // Section text for Lock Company.
        public const string LockCompany                                             = ModAssemblyInfo.Name + ".LockCompany";
        public const string LockAll                                                 = ModAssemblyInfo.Name + ".LockAll";
        public const string UnlockAll                                               = ModAssemblyInfo.Name + ".UnlockAll";

        // Section tool tips for change company, one for each property type.
        private const string SectionTooltipPrefix = ".SectionTooltip";
        public const string SectionTooltipCommercial                                = ModAssemblyInfo.Name + SectionTooltipPrefix + "Commercial";
        public const string SectionTooltipIndustrial                                = ModAssemblyInfo.Name + SectionTooltipPrefix + "Industrial";
        public const string SectionTooltipOffice                                    = ModAssemblyInfo.Name + SectionTooltipPrefix + "Office";
        public const string SectionTooltipStorage                                   = ModAssemblyInfo.Name + SectionTooltipPrefix + "Storage";

        // Section tool tip for optional change all.
        public const string SectionTooltipChangeAll                                 = ModAssemblyInfo.Name + SectionTooltipPrefix + "ChangeAll";

        // Section tool tip for lock company.
        public const string SectionTooltipLockCompany                               = ModAssemblyInfo.Name + SectionTooltipPrefix + "LockCompany";

        // Production balance button and panel heading.
        public const string ProductionBalanceStatistics                             = ModAssemblyInfo.Name + ".ProductionBalanceStatistics";
        public const string ProductionBalanceDescription                            = ModAssemblyInfo.Name + ".ProductionBalanceDescription";
        public const string ProductionBalanceShortcut                               = ModAssemblyInfo.Name + ".ProductionBalanceShortcut";

        // Production balance industrial and office headings.
        public const string ProductionBalanceIndustrial                             = ModAssemblyInfo.Name + ".ProductionBalanceIndustrial";
        public const string ProductionBalanceOffice                                 = ModAssemblyInfo.Name + ".ProductionBalanceOffice";

        // Production balance info on panel.
        public const string ProductionBalanceInfoCompanies                          = ModAssemblyInfo.Name + ".ProductionBalanceInfoCompanies";
        public const string ProductionBalanceInfoCompaniesTooltip                   = ModAssemblyInfo.Name + ".ProductionBalanceInfoCompaniesTooltip";
        public const string ProductionBalanceInfoMinimumCompanies                   = ModAssemblyInfo.Name + ".ProductionBalanceInfoMinimumCompanies";
        public const string ProductionBalanceInfoMinimumCompaniesTooltip            = ModAssemblyInfo.Name + ".ProductionBalanceInfoMinimumCompaniesTooltip";
        public const string ProductionBalanceInfoStandardDeviation                  = ModAssemblyInfo.Name + ".ProductionBalanceInfoStandardDeviation";
        public const string ProductionBalanceInfoStandardDeviationTooltip           = ModAssemblyInfo.Name + ".ProductionBalanceInfoStandardDeviationTooltip";
        public const string ProductionBalanceInfoMinimumStandardDeviation           = ModAssemblyInfo.Name + ".ProductionBalanceInfoMinimumStandardDeviation";
        public const string ProductionBalanceInfoMinimumStandardDeviationTooltip    = ModAssemblyInfo.Name + ".ProductionBalanceInfoMinimumStandardDeviationTooltip";
        public const string ProductionBalanceInfoLastChangeDateTime                 = ModAssemblyInfo.Name + ".ProductionBalanceInfoLastChangeDateTime";
        public const string ProductionBalanceInfoLastChangeDateTimeTooltip          = ModAssemblyInfo.Name + ".ProductionBalanceInfoLastChangeDateTimeTooltip";
        public const string ProductionBalanceInfoLastChangeFromResource             = ModAssemblyInfo.Name + ".ProductionBalanceInfoLastChangeFromResource";
        public const string ProductionBalanceInfoLastChangeFromResourceTooltip      = ModAssemblyInfo.Name + ".ProductionBalanceInfoLastChangeFromResourceTooltip";
        public const string ProductionBalanceInfoLastChangeToResource               = ModAssemblyInfo.Name + ".ProductionBalanceInfoLastChangeToResource";
        public const string ProductionBalanceInfoLastChangeToResourceTooltip        = ModAssemblyInfo.Name + ".ProductionBalanceInfoLastChangeToResourceTooltip";
        public const string ProductionBalanceInfoNextCheckDateTime                  = ModAssemblyInfo.Name + ".ProductionBalanceInfoNextCheckDateTime";
        public const string ProductionBalanceInfoNextCheckDateTimeTooltip           = ModAssemblyInfo.Name + ".ProductionBalanceInfoNextCheckDateTimeTooltip";

        // Settings.
        public const string SettingTitle                                                    = "Options.SECTION" +       "[ChangeCompany.ChangeCompany.Mod]";
        
        public const string SettingGroupProductionBalance                                   = "Options.GROUP" +         "[ChangeCompany.ChangeCompany.Mod.ProductionBalance]";
        public const string SettingProductionBalanceGeneralDescription                      = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceGeneralDescription]";
        public const string SettingProductionBalanceEnabledIndustrialLabel                  = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceEnabledIndustrial]";
        public const string SettingProductionBalanceEnabledIndustrialDesc                   = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceEnabledIndustrial]";
        public const string SettingProductionBalanceEnabledOfficeLabel                      = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceEnabledOffice]";
        public const string SettingProductionBalanceEnabledOfficeDesc                       = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceEnabledOffice]";
        public const string SettingProductionBalanceCheckIntervalIndustrialLabel            = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceCheckIntervalIndustrial]";
        public const string SettingProductionBalanceCheckIntervalIndustrialDesc             = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceCheckIntervalIndustrial]";
        public const string SettingProductionBalanceCheckIntervalOfficeLabel                = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceCheckIntervalOffice]";
        public const string SettingProductionBalanceCheckIntervalOfficeDesc                 = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceCheckIntervalOffice]";
        public const string SettingProductionBalanceMinimumCompaniesIndustrialLabel         = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceMinimumCompaniesIndustrial]";
        public const string SettingProductionBalanceMinimumCompaniesIndustrialDesc          = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceMinimumCompaniesIndustrial]";
        public const string SettingProductionBalanceMinimumCompaniesOfficeLabel             = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceMinimumCompaniesOffice]";
        public const string SettingProductionBalanceMinimumCompaniesOfficeDesc              = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceMinimumCompaniesOffice]";
        public const string SettingProductionBalanceMinimumStandardDeviationIndustrialLabel = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceMinimumStandardDeviationIndustrial]";
        public const string SettingProductionBalanceMinimumStandardDeviationIndustrialDesc  = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceMinimumStandardDeviationIndustrial]";
        public const string SettingProductionBalanceMinimumStandardDeviationOfficeLabel     = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceMinimumStandardDeviationOffice]";
        public const string SettingProductionBalanceMinimumStandardDeviationOfficeDesc      = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceMinimumStandardDeviationOffice]";
        public const string SettingProductionBalanceMaximumCompanyProductionIndustrialLabel = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceMaximumCompanyProductionIndustrial]";
        public const string SettingProductionBalanceMaximumCompanyProductionIndustrialDesc  = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceMaximumCompanyProductionIndustrial]";
        public const string SettingProductionBalanceMaximumCompanyProductionOfficeLabel     = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceMaximumCompanyProductionOffice]";
        public const string SettingProductionBalanceMaximumCompanyProductionOfficeDesc      = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceMaximumCompanyProductionOffice]";
        public const string SettingProductionBalanceHideActivationButtonLabel               = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceHideActivationButton]";
        public const string SettingProductionBalanceHideActivationButtonDesc                = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceHideActivationButton]";
        public const string SettingProductionBalanceActivationKeyLabel                      = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceActivationKey]";
        public const string SettingProductionBalanceActivationKeyDesc                       = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceActivationKey]";
        public const string SettingProductionBalanceResetLabel                              = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceReset]";
        public const string SettingProductionBalanceResetDesc                               = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ProductionBalanceReset]";
        
        public const string SettingGroupLockCompany                                         = "Options.GROUP" +         "[ChangeCompany.ChangeCompany.Mod.LockCompany]";
        public const string SettingLockAfterChangeLabel                                     = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.LockAfterChange]";
        public const string SettingLockAfterChangeDesc                                      = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.LockAfterChange]";
        public const string SettingLockAllCompaniesLabel                                    = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.LockAllCompanies]";
        public const string SettingLockAllCompaniesDesc                                     = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.LockAllCompanies]";
        public const string SettingUnlockAllCompaniesLabel                                  = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.UnlockAllCompanies]";
        public const string SettingUnlockAllCompaniesDesc                                   = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.UnlockAllCompanies]";

        public const string SettingGroupAbout                                               = "Options.GROUP" +         "[ChangeCompany.ChangeCompany.Mod.About]";
        public const string SettingModVersionLabel                                          = "Options.OPTION" +        "[ChangeCompany.ChangeCompany.Mod.ModSettings.ModVersion]";
        public const string SettingModVersionDesc                                           = "Options.OPTION_DESCRIPTION[ChangeCompany.ChangeCompany.Mod.ModSettings.ModVersion]";

    }
}
