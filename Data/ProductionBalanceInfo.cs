using Colossal.Localization;
using Colossal.UI.Binding;
using Game.Economy;
using Game.SceneFlow;
using System.Globalization;

namespace ChangeCompany
{
    /// <summary>
    /// Information for production balance UI.
    /// </summary>
    public class ProductionBalanceInfo : IJsonWritable
    {
        // Whether info is for industrial or office.
        private bool        _isIndustrial;

        // The production balance info.
        public bool         InfoValid;
        public int          CompanyCount;
        public double       StandardDeviationPercent;

        public GameDateTime LastChangeDateTime = new();
        public Resource     LastChangeFromResource;
        public Resource     LastChangeToResource;

        public GameDateTime NextCheckDateTime = new();

        // Hold a reference to localization manager.
        private static readonly LocalizationManager _localizationManager = GameManager.instance.localizationManager;


        // Prevent public instantiation without parameters.
        private ProductionBalanceInfo() { }

        // Public instantiation must always specify industrial vs office.
        public ProductionBalanceInfo(bool isIndustrial)
        {
            _isIndustrial = isIndustrial;
        }

        /// <summary>
        /// Return a copy of this production balance info.
        /// </summary>
        public ProductionBalanceInfo Copy()
        {
            return new ProductionBalanceInfo()
            {
                _isIndustrial               = this._isIndustrial,

                InfoValid                   = this.InfoValid,
                CompanyCount                = this.CompanyCount,
                StandardDeviationPercent    = this.StandardDeviationPercent,

                LastChangeDateTime          = this.LastChangeDateTime.Copy(),
                LastChangeFromResource      = this.LastChangeFromResource,
                LastChangeToResource        = this.LastChangeToResource,

                NextCheckDateTime           = this.NextCheckDateTime.Copy(),
            };
        }

        /// <summary>
        /// Write production balance info to the UI.
        /// </summary>
        public void Write(IJsonWriter writer)
        {
            // Get current culture.
            CultureInfo currentCulture = new(_localizationManager.activeLocaleId);

            // Format company count.
            const string CompanyCountFormat = "N0";
            string formattedCompanyCount = InfoValid ? CompanyCount.ToString(CompanyCountFormat, currentCulture) : "";

            // Format minimum companies.
            string formattedMinimumCompanies = _isIndustrial ?
                Mod.ModSettings.ProductionBalanceMinimumCompaniesIndustrial.ToString(CompanyCountFormat, currentCulture) :
                Mod.ModSettings.ProductionBalanceMinimumCompaniesOffice    .ToString(CompanyCountFormat, currentCulture);

            // Format standard deviation percent.
            // All languages use same percent symbol.
            string standardDeviationPercentFormat = InfoValid && StandardDeviationPercent < 200d ? "N1" : "N0";
            string formattedStandardDeviation = InfoValid ?
                StandardDeviationPercent.ToString(standardDeviationPercentFormat, currentCulture) + " %" : "";

            // Format minimum standard deviation percent.
            // All languages use same percent symbol.
            string formattedMinimumStandardDeviation = (_isIndustrial ?
                Mod.ModSettings.ProductionBalanceMinimumStandardDeviationIndustrial.ToString(standardDeviationPercentFormat, currentCulture) :
                Mod.ModSettings.ProductionBalanceMinimumStandardDeviationOffice    .ToString(standardDeviationPercentFormat, currentCulture)) +
                " %";

            // Start type.
			writer.TypeBegin(ModAssemblyInfo.Name + ".ProductionBalanceInfo");

            // Write formatted company counts.
			writer.PropertyName("companyCount");
			writer.Write(formattedCompanyCount);
			writer.PropertyName("minimumCompanies");
			writer.Write(formattedMinimumCompanies);

            // Write formatted standard deviations.
			writer.PropertyName("standardDeviationPercent");
			writer.Write(formattedStandardDeviation);
			writer.PropertyName("minimumStandardDeviation");
			writer.Write(formattedMinimumStandardDeviation);

            // Write formatted last change data.
			writer.PropertyName("lastChangeDateTime");
			writer.Write(LastChangeDateTime.FormatForUI());
			writer.PropertyName("lastChangeFromResource");
			writer.Write(LastChangeFromResource.ToString());
			writer.PropertyName("lastChangeToResource");
			writer.Write(LastChangeToResource.ToString());

            // Write formatted next check date/time.
            writer.PropertyName("nextCheckDateTime");
            writer.Write(NextCheckDateTime.FormatForUI());

            // End type.
			writer.TypeEnd();
        }
    }
}
