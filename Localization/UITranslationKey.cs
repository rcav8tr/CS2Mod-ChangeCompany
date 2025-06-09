namespace ChangeCompany
{
    // Define UI translation keys.
    // These must match the translation keys used in the UI.
    public class UITranslationKey
    {
        // Section heading and button.
        public const string ChangeCompany               = ModAssemblyInfo.Name + ".ChangeCompany";
        public const string ChangeNow                   = ModAssemblyInfo.Name + ".ChangeNow";

        // Section tool tips, one for each property type.
        private const string SectionTooltipPrefix = ".SectionTooltip";
        public const string SectionTooltipCommercial    = ModAssemblyInfo.Name + SectionTooltipPrefix + "Commercial";
        public const string SectionTooltipIndustrial    = ModAssemblyInfo.Name + SectionTooltipPrefix + "Industrial";
        public const string SectionTooltipOffice        = ModAssemblyInfo.Name + SectionTooltipPrefix + "Office";
        public const string SectionTooltipStorage       = ModAssemblyInfo.Name + SectionTooltipPrefix + "Storage";
    }
}
