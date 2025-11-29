using Game.Economy;
using Game.Simulation;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace ChangeCompany
{
    /// <summary>
    /// Production amounts, production counts, and surplus amounts for the whole city.
    /// This system is used in Resource Locator and Change Company mods.
    /// </summary>
    public static class ProductionSurplus
    {
        // Get resource count only once.
        private static readonly int ResourceCount = EconomyUtils.ResourceCount;

        // Other systems.
        private static MyProcessingCompanySystem     _myProcessingCompanySystem;
        private static MyExtractorCompanySystem      _myExtractorCompanySystem;
        private static CityProductionStatisticSystem _cityProductionStatisticSystem;

        /// <summary>
        /// Initialize things needed for this class.
        /// </summary>
        public static void Initialize()
        {
            Mod.log.Info($"{nameof(ProductionSurplus)}.{nameof(Initialize)}");

            // Create and get this mod's systems that get company data.
            // These systems do not run periodically, so they only need to be created, not activated.
            World defaultWorld = World.DefaultGameObjectInjectionWorld;
            _myProcessingCompanySystem = defaultWorld.GetOrCreateSystemManaged<MyProcessingCompanySystem>();
            _myExtractorCompanySystem  = defaultWorld.GetOrCreateSystemManaged<MyExtractorCompanySystem>();

            // Get other systems.
            _cityProductionStatisticSystem = defaultWorld.GetOrCreateSystemManaged<CityProductionStatisticSystem>();
        }

        /// <summary>
        /// Get production and surplus amounts.
        /// </summary>
        public static bool GetAmounts(out int[] productionAmounts, out int[] surplusAmounts, out Dictionary<Entity, int> companyProductions)
        {
            // Initialize return values.
            productionAmounts = new int[ResourceCount];
            surplusAmounts    = new int[ResourceCount];

            // Get processing company production and consumption amounts.
            _myProcessingCompanySystem.GetAmounts(
                out int[] processingCompanyProductionAmounts,
                out int[] processingCompanyConsumptionAmounts,
                out companyProductions);

            // Get extractor company production amounts.
            _myExtractorCompanySystem.GetAmounts(out int[] extractorCompanyProductionAmounts);

            // Get city resource usage (aka final consumption) data.
            // Logic adapted from Game.UI.InGame.ProductionUISystem.WriteFinalConsumption().
            // Note that the city resource usage data updates in CityProductionStatisticSystem only once every 45 game minutes.
            // Therefore, the real-time company data obtained above is combined with this slower city resource usage data.
            // This will have to be good enough because it would be too complicated to duplicate
            // the 6-7 systems that provide the city resource usage data to try to get it more often.
            NativeArray<CityProductionStatisticSystem.CityResourceUsage> cityResourceUsages = _cityProductionStatisticSystem.GetCityResourceUsages();

            // Do each resource index.
            bool cityResourceUsageValid = false;
            for (int resourceIndex = 0; resourceIndex < ResourceCount; resourceIndex++)
            {
                // Compute and return production amount from processing and extractor companies.
                productionAmounts[resourceIndex] = processingCompanyProductionAmounts[resourceIndex] + extractorCompanyProductionAmounts [resourceIndex];

                // Compute consumption amount from processing companies and city resource usage.
                int consumptionAmount = processingCompanyConsumptionAmounts[resourceIndex];
                CityProductionStatisticSystem.CityResourceUsage cityResourceUsagesForResource = cityResourceUsages[resourceIndex];
                for (int i = 0; i < (int)CityProductionStatisticSystem.CityResourceUsage.Consumer.Count; i++)
                {
                    // Exclude import/export from consumption.
                    int cityResourceUsage = cityResourceUsagesForResource[i];
                    if (i != (int)CityProductionStatisticSystem.CityResourceUsage.Consumer.ImportExport)
                    {
                        consumptionAmount += cityResourceUsage;
                    }

                    // City resource usage is valid if any value is not zero, including import/export.
                    if (cityResourceUsage != 0)
                    {
                        cityResourceUsageValid = true;
                    }
                }

                // Compute and return surplus.
                // Deficit is a negative surplus.
                surplusAmounts[resourceIndex] = productionAmounts[resourceIndex] - consumptionAmount;
            }

            // If city resource usage is not valid, return zeroes for all amounts.
            if (!cityResourceUsageValid)
            {
                productionAmounts = new int[ResourceCount];
                surplusAmounts    = new int[ResourceCount];
            }

            // Return validity.
            return cityResourceUsageValid;
        }
    }
}
