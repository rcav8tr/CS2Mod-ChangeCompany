using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Objects;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Tools;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace ChangeCompany
{
    /// <summary>
    /// System to override company storage.
    /// </summary>
    public partial class OverrideStorageSystem : GameSystemBase
    {
        // Resource prefabs.
        ResourcePrefabs _resourcePrefabs;

        /// <summary>
        /// Initialize this system.
        /// </summary>
        [Preserve]
        protected override void OnCreate()
        {
            Mod.log.Info($"{nameof(OverrideStorageSystem)}.{nameof(OnCreate)}");
            base.OnCreate();

            // Get resource prefabs.
			_resourcePrefabs = World.GetOrCreateSystemManaged<ResourceSystem>().GetPrefabs();

            // Initialize.
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

#if DEBUG
            // ShowStorage();
#endif

            // Initialize overrides.
            OverrideStorage();
        }

#if DEBUG
        private struct StorageCompany : IComparable<StorageCompany>
        {
            public CompanyType CompanyType;
            public int StorageLimit;
            public Entity Prefab;
            public string PrefabName;

            public int CompareTo(StorageCompany other)
            {
                if (this.CompanyType == other.CompanyType)
                {
                    if (this.StorageLimit == other.StorageLimit)
                    {
                        return this.PrefabName.CompareTo(other.PrefabName);
                    }
                    return this.StorageLimit.CompareTo(other.StorageLimit);
                }
                return this.CompanyType.CompareTo(other.CompanyType);
            }
        }
        private List<StorageCompany> _storageCompanies = new();

        private void ShowStorage()
        {
            _storageCompanies.Clear();

            // Define component lookups.
            ComponentLookup<CommercialCompanyData> componentLookupCommercialCompanyData = SystemAPI.GetComponentLookup<CommercialCompanyData>(true);
            ComponentLookup<ExtractorCompanyData > componentLookupExtractorCompanyData  = SystemAPI.GetComponentLookup<ExtractorCompanyData >(true);
            ComponentLookup<IndustrialCompanyData> componentLookupIndustrialCompanyData = SystemAPI.GetComponentLookup<IndustrialCompanyData>(true);
            ComponentLookup<StorageCompanyData   > componentLookupStorageCompanyData    = SystemAPI.GetComponentLookup<StorageCompanyData   >(true);
            ComponentLookup<IndustrialProcessData> componentLookupIndustrialProcessData = SystemAPI.GetComponentLookup<IndustrialProcessData>(true);

            PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            EntityQuery query = SystemAPI.QueryBuilder()
                .WithAll<StorageLimitData, PrefabData>()
                .WithNone<Deleted>()
                .Build();
            NativeArray<Entity> prefabs = query.ToEntityArray(Allocator.Temp);
            NativeArray<StorageLimitData> storageLimitDatas = query.ToComponentDataArray<StorageLimitData>(Allocator.Temp);
            for (int i = 0; i < prefabs.Length; i++)
            {
                // Get company type.
                Entity prefab = prefabs[i];
                CompanyType companyType = CompanyUtilities.GetCompanyType(
                    prefab,
                    in componentLookupCommercialCompanyData,
                    in componentLookupExtractorCompanyData,
                    in componentLookupIndustrialCompanyData,
                    in componentLookupStorageCompanyData,
                    in componentLookupIndustrialProcessData);
                if (companyType != CompanyType.None)
                {
                    _storageCompanies.Add(new() {
                        CompanyType = companyType,
                        StorageLimit = storageLimitDatas[i].m_Limit,
                        Prefab = prefab,
                        PrefabName = prefabSystem.GetPrefabName(prefab)});
                }
            }
            _storageCompanies.Sort();
            foreach (StorageCompany storageCompany in _storageCompanies)
            {
                Mod.log.Info($"{storageCompany.CompanyType.ToString().Substring(0,3)} {storageCompany.Prefab} {storageCompany.StorageLimit,6} {storageCompany.PrefabName}");
            }
        }
#endif

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

            // Define query to get commercial and industrial company prefabs that have StorageLimitData.
            // There are other prefabs with StorageLimitData, but only companies are needed.
            // StorageLimitData holds the storage limit for the company.
            // Industrial includes extractor, industrial, office, and warehouse.
            EntityQuery query = SystemAPI.QueryBuilder()
                .WithAll<CommercialCompanyData, PrefabData, StorageLimitData>()
                .AddAdditionalQuery()
                .WithAll<IndustrialCompanyData, PrefabData, StorageLimitData>()
                .Build();

            // Define component lookups.
            ComponentLookup<CommercialCompanyData> componentLookupCommercialCompanyData = SystemAPI.GetComponentLookup<CommercialCompanyData>(true);
            ComponentLookup<ExtractorCompanyData > componentLookupExtractorCompanyData  = SystemAPI.GetComponentLookup<ExtractorCompanyData >(true);
            ComponentLookup<IndustrialCompanyData> componentLookupIndustrialCompanyData = SystemAPI.GetComponentLookup<IndustrialCompanyData>(true);
            ComponentLookup<StorageCompanyData   > componentLookupStorageCompanyData    = SystemAPI.GetComponentLookup<StorageCompanyData   >(true);
            ComponentLookup<IndustrialProcessData> componentLookupIndustrialProcessData = SystemAPI.GetComponentLookup<IndustrialProcessData>(true);

            // Do each company prefab.
            NativeArray<Entity          > companyPrefabs    = query.ToEntityArray                         (Allocator.Temp);
            NativeArray<StorageLimitData> storageLimitDatas = query.ToComponentDataArray<StorageLimitData>(Allocator.Temp);
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
                if (companyType != CompanyType.None)
                {
                    // Get new storage limit from the setting corresponding to the company type.
                    int newStorageLimit = companyType switch
                    {
                        CompanyType.Commercial => Mod.ModSettings.StorageCommercial,
                        CompanyType.Extractor  => Mod.ModSettings.StorageExtractor,
                        CompanyType.Industrial => Mod.ModSettings.StorageIndustrial,
                        CompanyType.Office     => Mod.ModSettings.StorageOffice,
                        CompanyType.Warehouse  => Mod.ModSettings.StorageWarehouse,
                        _                      => 10000,
                    };

                    // Update the storage limit data.
                    // This will be executed only while Options are displayed, and hence simulation is not running.
                    // Therefore, performance and Unity sync points are not a concern.
                    // Therefore, make the changes directly on EntityManager and do not use an EntityCommandBuffer or job.
                    // Do not need to add the Updated component when setting component data on a prefab.
                    StorageLimitData storageLimitData = storageLimitDatas[i];
                    storageLimitData.m_Limit = newStorageLimit;
                    EntityManager.SetComponentData(companyPrefab, storageLimitData);
                }
            }

            // Update companies.
            // This depends on the storage limit data being updated immediately above.
            UpdateCompanies();

            // Done overriding.
            Enabled = false;
        }

        /// <summary>
        /// Update companies that have more stored than the new storage limit.
        /// </summary>
        private void UpdateCompanies()
        {
            // Define buffer lookups.
            BufferLookup<InstalledUpgrade> bufferLookupInstalledUpgrade = SystemAPI.GetBufferLookup<InstalledUpgrade>(true);
            BufferLookup<Resources       > bufferLookupResources        = SystemAPI.GetBufferLookup<Resources       >(true);

            // Define component lookups.
            ComponentLookup<Attached             > componentLookupAttached              = SystemAPI.GetComponentLookup<Attached             >(true);
            ComponentLookup<BuildingData         > componentLookupBuildingData          = SystemAPI.GetComponentLookup<BuildingData         >(true);
            ComponentLookup<IndustrialCompanyData> componentLookupIndustrialCompanyData = SystemAPI.GetComponentLookup<IndustrialCompanyData>(true);
            ComponentLookup<IndustrialProcessData> componentLookupIndustrialProcessData = SystemAPI.GetComponentLookup<IndustrialProcessData>(true);
            ComponentLookup<PrefabRef            > componentLookupPrefabRef             = SystemAPI.GetComponentLookup<PrefabRef            >(true);
            ComponentLookup<SpawnableBuildingData> componentLookupSpawnableBuildingData = SystemAPI.GetComponentLookup<SpawnableBuildingData>(true);
            ComponentLookup<StorageCompanyData   > componentLookupStorageCompanyData    = SystemAPI.GetComponentLookup<StorageCompanyData   >(true);
            ComponentLookup<StorageLimitData     > componentLookupStorageLimitData      = SystemAPI.GetComponentLookup<StorageLimitData     >(true);

            // Define query to get all companies.
            EntityQuery query = SystemAPI.QueryBuilder()
                .WithAll<CompanyData, PropertyRenter, PrefabRef, Resources>()
                .WithNone<Deleted, Temp>()
                .Build();

            // Do each company.
            NativeArray<Entity        > companies       = query.ToEntityArray                       (Allocator.Temp);
            NativeArray<PrefabRef     > prefabRefs      = query.ToComponentDataArray<PrefabRef     >(Allocator.Temp);
            NativeArray<PropertyRenter> propertyRenters = query.ToComponentDataArray<PropertyRenter>(Allocator.Temp);
            for (int i = 0; i < companies.Length; i++)
            {
                // Get entities from arrays.
                Entity companyEntity = companies[i];
                Entity companyPrefab = prefabRefs[i].m_Prefab;
                Entity property      = propertyRenters[i].m_Property;

                // Property must be valid.
                if (property == Entity.Null)
                {
                    // This is not an error, just skip the company.
                    continue;
                }

                // Get the company's resources buffer.
                if (bufferLookupResources.TryGetBuffer(companyEntity, out DynamicBuffer<Resources> companyResources) &&
                    companyResources.IsCreated)
                {
                    // Get the combination of input and output resources that have weight.
                    // Logic adapted from Game.UI.InGame.StorageSection.Visible().
                    Resource inputOutputResources = Resource.NoResource;
                    if (componentLookupIndustrialProcessData.TryGetComponent(companyPrefab, out IndustrialProcessData industrialProcessData))
                    {
                        if (EconomyUtils.GetWeight(EntityManager, industrialProcessData.m_Input1.m_Resource, _resourcePrefabs) > 0f)
                        {
                            inputOutputResources |= industrialProcessData.m_Input1.m_Resource;
                        }
                        if (EconomyUtils.GetWeight(EntityManager, industrialProcessData.m_Input2.m_Resource, _resourcePrefabs) > 0f)
                        {
                            inputOutputResources |= industrialProcessData.m_Input2.m_Resource;
                        }
                        if (EconomyUtils.GetWeight(EntityManager, industrialProcessData.m_Output.m_Resource, _resourcePrefabs) > 0f)
                        {
                            inputOutputResources |= industrialProcessData.m_Output.m_Resource;
                        }
                    }
                    else
                    {
                        // This should never happen.
                        continue;
                    }

                    // Compute the total input and output resources currently stored at the company.
                    int totalResources = 0;
                    foreach (Resources resource in companyResources)
                    {
                        if ((resource.m_Resource & inputOutputResources) != 0)
                        {
                            totalResources += resource.m_Amount;
                        }
                    }

                    // Get the company's storage limit.
                    int storageLimit = 0;
                    if (componentLookupStorageLimitData.TryGetComponent(companyPrefab, out StorageLimitData storageLimitData))
                    {
                        storageLimit = storageLimitData.m_Limit;
                    }

                    // Check if company's property is attached and parent has installed upgrades.
                    // Logic adapted from Game.UI.InGame.StorageSection.Visible().
                    if (componentLookupAttached.TryGetComponent(property, out Attached attached) &&
                        attached.m_Parent != Entity.Null &&
                        bufferLookupInstalledUpgrade.TryGetBuffer(attached.m_Parent, out DynamicBuffer<InstalledUpgrade> installedUpgrades) &&
                        installedUpgrades.IsCreated)
                    {
                        // Add in the storage limit of each installed upgrade prefab.
                        foreach (InstalledUpgrade installedUpgrade in installedUpgrades)
                        {
                            if (componentLookupPrefabRef.TryGetComponent(installedUpgrade.m_Upgrade, out PrefabRef upgradePrefabRef) &&
                                componentLookupStorageLimitData.TryGetComponent(upgradePrefabRef.m_Prefab, out StorageLimitData upgradeStorageLimitData))
                            {
                                storageLimit += upgradeStorageLimitData.m_Limit;
                            }
                        }
                    }

                    // Adjust storage limit for warehouse.
                    if (componentLookupIndustrialCompanyData.HasComponent(companyPrefab) &&
                        componentLookupStorageCompanyData.HasComponent(companyPrefab) &&
                        componentLookupPrefabRef.TryGetComponent(property, out PrefabRef propertyPrefabRef) &&
                        componentLookupSpawnableBuildingData.TryGetComponent(propertyPrefabRef.m_Prefab, out SpawnableBuildingData spawnableBuildingData) &&
                        componentLookupBuildingData.TryGetComponent(propertyPrefabRef.m_Prefab, out BuildingData buildingData))
                    {
                        // Actual storage limit for a warehouse is base storage limit multiplied by building level and lot size.
                        storageLimit *= spawnableBuildingData.m_Level * buildingData.m_LotSize.x * buildingData.m_LotSize.y;
                    }

                    // Check if company total resources are more than 95% of the storage limit.
                    float storageLimit95Percent = 0.95f * storageLimit;
                    if (totalResources > storageLimit95Percent)
                    {
                        // Reduce the company's input and output resources proportionally to get down to 95% of new storage limit.
                        float multiplier = storageLimit95Percent / totalResources;
                        for (int j = 0; j < companyResources.Length; j++)
                        {
                            Resources resource = companyResources[j];
                            if ((resource.m_Resource & inputOutputResources) != 0)
                            {
                                resource.m_Amount = math.max((int)(resource.m_Amount * multiplier), 1);
                                companyResources[j] = resource;
                            }
                        }

                        // Mark this company as updated.
                        // AddComponent does nothing if the entity already has the component.
                        EntityManager.AddComponent<Updated>(companyEntity);
                    }
                }
            }
        }

        /// <summary>
        /// Enable this system to override storage during next OnUpdate.
        /// </summary>
        public void OverrideStorage()
        {
            Enabled = true;
        }
    }
}
