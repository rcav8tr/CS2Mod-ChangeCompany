using Colossal.IO.AssetDatabase;
using Colossal.Serialization.Entities;
using Game;
using Game.Companies;
using Game.Prefabs;
using Game.SceneFlow;
using HarmonyLib;
using System;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;
using static Game.Modding.ModManager;

namespace ChangeCompany
{
    /// <summary>
    /// System to override company vehicles.
    /// </summary>
    public partial class OverrideVehiclesSystem : GameSystemBase
    {
        // Systems.
        private static OverrideVehiclesSystem _thisSystem;

        // Info for the All Transit+Trucks mod.
        private ComponentSystemBase _allTransitTrucksIndustrySystem;
        public bool AllTransitTrucksModActive => _allTransitTrucksIndustrySystem != null;
        private bool _restoreDefaults;

        /// <summary>
        /// Initialize this system.
        /// </summary>
        [Preserve]
        protected override void OnCreate()
        {
            Mod.log.Info($"{nameof(OverrideVehiclesSystem)}.{nameof(OnCreate)}");
            base.OnCreate();

            // Save this system.
            _thisSystem = this;

            // Initialize.
            _restoreDefaults = false;
            Enabled = false;
        }
        
        /// <summary>
        /// Called by the game when the world is ready.
        /// </summary>
        [Preserve]
        protected override void OnWorldReady()
        {
            try
            {
                Mod.log.Info($"{nameof(OverrideVehiclesSystem)}.{nameof(OnWorldReady)}");
                base.OnWorldReady();

                // Try to get All Transit+Trucks (AT+T) mod IndustrySystem.
                _allTransitTrucksIndustrySystem = GetAllTransitTrucksIndustrySystem();
                if (_allTransitTrucksIndustrySystem != null)
                {
                    // Create a postfix patch on AT+T mod IndustrySystem.OnUpdate().
                    const string originalMethodName = "OnUpdate";
                    MethodInfo originalMethod = _allTransitTrucksIndustrySystem.GetType().GetMethod(originalMethodName, BindingFlags.Instance | BindingFlags.NonPublic);
                    if (originalMethod == null)
                    {
                        Mod.log.Error($"Unable to find patch original method {_allTransitTrucksIndustrySystem.GetType()}.{originalMethodName}.");
                        return;
                    }
                    const string postfixMethodName = nameof(ATTIndustrySystemOnUpdatePostfix);
                    MethodInfo postfixMethod = typeof(OverrideVehiclesSystem).GetMethod(postfixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
                    if (postfixMethod == null)
                    {
                        Mod.log.Error($"Unable to find patch postfix method {nameof(OverrideVehiclesSystem)}.{postfixMethodName}.");
                        return;
                    }
                    new Harmony(Mod.HarmonyID).Patch(originalMethod, null, new HarmonyMethod(postfixMethod));
                }
                else
                {
                    // AT+T mod is not active.
                    // Make sure option to use it is turned off.
                    Mod.ModSettings.VehiclesUseATTOverrides = false;
                }
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Get the All Transit+Trucks (AT+T) mod IndustrySystem, if active.
        /// </summary>
        private ComponentSystemBase GetAllTransitTrucksIndustrySystem()
        {
            // Find the AT+T mod.
            ExecutableAsset[] modAssets = ExecutableAsset.GetModAssets();
            foreach (ExecutableAsset executableAsset in modAssets)
            {
                // The name of the AT+T mod is "AllTransitTrucks".
                ModInfo modInfo = new(executableAsset);
                if (modInfo.isValid && modInfo.asset != null && modInfo.asset.name == "AllTransitTrucks")
                {
                    // Found the AT+T mod.
                    // Find IndustrySystem in the AT+T mod.
                    // The namespace in the mod is "PublicWorksPlus".
                    Type assemblyType = modInfo.asset.assembly.GetType("PublicWorksPlus.IndustrySystem");
                    if (assemblyType != null)
                    {
                        Mod.log.Info("All Transit+Trucks mod found.");
                        return World.GetOrCreateSystemManaged(assemblyType);
                    }

                    // Warn for AT+T mod found but system not found.
                    Mod.log.Warn("All Transit+Trucks mod found but unable to find IndustrySystem in the mod.");
                    return null;
                }
            }

            // AT+T mod not found.
            // This is not an error, the AT+T mod is simply not active.
            Mod.log.Info("All Transit+Trucks mod not found.");
            return null;
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
        /// Postfix patch method for AT+T IndustrySystem.OnUpdate().
        /// </summary>
        [Preserve]
        private static void ATTIndustrySystemOnUpdatePostfix()
        {
            // Call the postfix patch method implementation for the game's instance of this system.
            _thisSystem.ATTIndustrySystemOnUpdatePostfixImpl();
        }

        /// <summary>
        /// Postfix patch method implementation for AT+T IndustrySystem.OnUpdate().
        /// </summary>
        private void ATTIndustrySystemOnUpdatePostfixImpl()
        {
            // Skip if not in a game.
            if (GameManager.instance.gameMode != GameMode.Game)
            {
                return;
            }

            // Override vehicles.
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

                // Check if prefab value needs to be updated.
                TransportCompanyData transportCompanyData = transportCompanyDatas[i];
                if (transportCompanyData.m_MaxTransports != newVehicleCount)
                {
                    // Update prefab value if:   AT+T mod is not active   OR   AT+T mod is active and not being used   OR   restoring defaults.
                    if (!AllTransitTrucksModActive || (AllTransitTrucksModActive && !Mod.ModSettings.VehiclesUseATTOverrides) || _restoreDefaults)
                    {
                        // Update the transport company data.
                        // This will be executed only while Options are displayed, and hence simulation is not running.
                        // Therefore, performance and Unity sync points are not a concern.
                        // Therefore, make the changes directly on EntityManager and do not use an EntityCommandBuffer or job.
                        // Do not need to add the Updated component when setting component data on a prefab.
                        transportCompanyData.m_MaxTransports = newVehicleCount;
                        EntityManager.SetComponentData(companyPrefab, transportCompanyData);
                    }
                }
            }

            // Check if defaults were restored.
            if (_restoreDefaults)
            {
                // Do not restore defaults again.
                _restoreDefaults = false;

                // Execute the All Transit+Trucks mod IndustrySystem in the next frame
                // to let it overwrite, if needed, the default values just restored here.
                // The Harmony patch will re-enable this system to run again in the next frame after AT+T mod runs.
                _allTransitTrucksIndustrySystem.Enabled = true;
            }

            // Done overriding.
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
            OverrideVehicles();
        }
    }
}
