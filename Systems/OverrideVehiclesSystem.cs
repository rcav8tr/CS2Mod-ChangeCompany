using Colossal.Serialization.Entities;
using Game;
using Game.Companies;
using Game.Prefabs;
using Game.SceneFlow;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;

namespace ChangeCompany
{
    /// <summary>
    /// System to override company vehicles.
    /// </summary>
    public partial class OverrideVehiclesSystem : GameSystemBase
    {
        // Whether or not to restore defaults.
        private bool _restoreDefaults;

        /// <summary>
        /// Initialize this system.
        /// </summary>
        [Preserve]
        protected override void OnCreate()
        {
            Mod.log.Info($"{nameof(OverrideVehiclesSystem)}.{nameof(OnCreate)}");
            base.OnCreate();

            // Initialize.
            _restoreDefaults = false;
            Enabled = false;
        }

        /// <summary>
        /// Called by the game when a GameMode is done being loaded.
        /// </summary>
        [Preserve]
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);

            // Skip if not in a game.
            if (mode != GameMode.Game)
            {
                return;
            }

            // Initialize overrides.
            _restoreDefaults = false;
            OverrideVehicles();
        }

        /// <summary>
        /// Called when the game needs this system to update itself.
        /// </summary>
        [Preserve]
        protected override void OnUpdate()
        {
            // Skip if not in a game.
            if (GameManager.instance.gameMode != GameMode.Game)
            {
                Enabled = false;
                return;
            }

            // Continue if vehicle overrides are enabled or if restoring defaults.
            if (!(Mod.ModSettings.VehiclesOverridesEnabled || _restoreDefaults))
            {
                Enabled = false;
                return;
            }

            // Define query to get commercial and industrial company prefabs that have TransportCompanyData.
            // There are other prefabs with TransportCompanyData, but only companies are needed.
            // TransportCompanyData holds the number of vehicles for the company.
            // Industrial includes extractor, industrial, office, and warehouse.
            EntityQuery query = SystemAPI.QueryBuilder()
                .WithAll<CommercialCompanyData, PrefabData, TransportCompanyData>()
                .AddAdditionalQuery()
                .WithAll<IndustrialCompanyData, PrefabData, TransportCompanyData>()
                .Build();

            // Define component lookups.
            ComponentLookup<CommercialCompanyData> componentLookupCommercialCompanyData = SystemAPI.GetComponentLookup<CommercialCompanyData>(true);
            ComponentLookup<ExtractorCompanyData > componentLookupExtractorCompanyData  = SystemAPI.GetComponentLookup<ExtractorCompanyData >(true);
            ComponentLookup<IndustrialCompanyData> componentLookupIndustrialCompanyData = SystemAPI.GetComponentLookup<IndustrialCompanyData>(true);
            ComponentLookup<StorageCompanyData   > componentLookupStorageCompanyData    = SystemAPI.GetComponentLookup<StorageCompanyData   >(true);
            ComponentLookup<IndustrialProcessData> componentLookupIndustrialProcessData = SystemAPI.GetComponentLookup<IndustrialProcessData>(true);
            
            // Do each company prefab.
            NativeArray<Entity              > companyPrefabs        = query.ToEntityArray                             (Allocator.Temp);
            NativeArray<TransportCompanyData> transportCompanyDatas = query.ToComponentDataArray<TransportCompanyData>(Allocator.Temp);
            for (int i = 0; i < companyPrefabs.Length; i++)
            {
                // Get company type.
                Entity companyPrefab = companyPrefabs[i];
                CompanyType companyType = CompanyUtilities.GetCompanyType(
                    companyPrefab,
                    in componentLookupCommercialCompanyData,
                    in componentLookupExtractorCompanyData,
                    in componentLookupIndustrialCompanyData,
                    in componentLookupStorageCompanyData,
                    in componentLookupIndustrialProcessData);

                // Company type must be valid.
                if (companyType == CompanyType.None)
                {
                    continue;
                }

                // Get new vehicle count depending on whether or not restoring defaults.
                int newVehicleCount;
                if (_restoreDefaults)
                {
                    // Get default vehicle count corresponding to the company type.
                    newVehicleCount = companyType switch
                    {
                        CompanyType.Commercial => ModSettings.DefaultVehiclesCommercial,
                        CompanyType.Extractor  => ModSettings.DefaultVehiclesExtractor,
                        CompanyType.Industrial => ModSettings.DefaultVehiclesIndustrial,
                        CompanyType.Office     => ModSettings.DefaultVehiclesOffice,
                        CompanyType.Warehouse  => ModSettings.DefaultVehiclesWarehouse,
                        _                      => 10,
                    };
                }
                else
                {
                    // Get vehicle count from the setting corresponding to the company type.
                    newVehicleCount = companyType switch
                    {
                        CompanyType.Commercial => Mod.ModSettings.VehiclesCommercial,
                        CompanyType.Extractor  => Mod.ModSettings.VehiclesExtractor,
                        CompanyType.Industrial => Mod.ModSettings.VehiclesIndustrial,
                        CompanyType.Office     => Mod.ModSettings.VehiclesOffice,
                        CompanyType.Warehouse  => Mod.ModSettings.VehiclesWarehouse,
                        _                      => 10,
                    };
                }

                // Update the transport company data.
                // This will be executed only while Options are displayed, and hence simulation is not running.
                // Therefore, performance and Unity sync points are not a concern.
                // Therefore, make the changes directly on EntityManager and do not use an EntityCommandBuffer or job.
                // Do not need to add the Updated component when setting component data on a prefab.
                TransportCompanyData transportCompanyData = transportCompanyDatas[i];
                transportCompanyData.m_MaxTransports = newVehicleCount;
                EntityManager.SetComponentData(companyPrefab, transportCompanyData);
            }

            // Done.
            _restoreDefaults = false;
            Enabled = false;
        }

        /// <summary>
        /// Enable this system to override vehicles during next OnUpdate.
        /// </summary>
        public void OverrideVehicles()
        {
            Enabled = true;
        }

        /// <summary>
        /// Set flag to restore defaults for all company types during next OnUpdate.
        /// </summary>
        public void RestoreDefaults()
        {
            _restoreDefaults = true;
            Enabled = true;
        }
    }
}
