using Colossal.Entities;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Economy;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Game.UI.InGame;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Scripting;
using OutsideConnection = Game.Objects.OutsideConnection;

namespace ChangeCompany
{
    /// <summary>
    /// A new section in the game's selected info view in the UI.
    /// </summary>
    public partial class ChangeCompanySection : InfoSectionBase
    {
        // Define special company indexes.
        // The number of company infos for a property is usually at most a few tens.
        // So using 1000 for these special company indexes should be safe.
        private const int SpecialCompanyIndexRandom = 1000;
        private const int SpecialCompanyIndexRemove = 1001;

        // Other systems.
        private ChangeCompanySystem _changeCompanySystem;

        // For each property prefab, maintain its corresponding CompanyInfos.
        // Because the company infos for a property prefab should not change during the game,
        // the company infos are obtained every time a game is about to be loaded.
        // This makes obtaining the company infos for a property prefab very fast during the game (simple lookup)
        // compared to computing the company infos when they are needed.
        // This extra speed comes at the expense of more memory used to store the data.
        // The property company infos has about 1360 records in total and each record has at least two entries in its CompanyInfos.
        private Dictionary<Entity, CompanyInfos> _propertyCompanyInfos;

        // C# to UI bindings.
        private ValueBinding<int> _bindingSelectedCompanyIndex;

        // Selected company index.
        private int _selectedCompanyIndex;

        // Section properties.
        private PropertyType _sectionPropertyPropertyType;
        private bool         _sectionPropertyHasCompany;
        private CompanyInfos _sectionPropertyCompanyInfos = new();

        // Company infos for industrial and office resources.
        private Dictionary<Resource, CompanyInfos> _companyInfosIndustrialOfficResource;

        // For all sections in the base game, the group is the class name, so do the same for this section.
        protected override string group => nameof(ChangeCompanySection);

        /// <summary>
        /// Initialize this system.
        /// </summary>
        [Preserve]
        protected override void OnCreate()
        {
            try
            {
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(OnCreate)}");

                base.OnCreate();

                // Other systems.
                _changeCompanySystem = World.GetOrCreateSystemManaged<ChangeCompanySystem>();

                // Add bindings for C# to UI.
                AddBinding(_bindingSelectedCompanyIndex = new ValueBinding<int>(ModAssemblyInfo.Name, "SelectedCompanyIndex", 0));

                // Add binding for game's change in selected entity.
                SelectedInfoUISystem selectedInfoUISystem = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
                selectedInfoUISystem.eventSelectionChanged =
                    (Action<Entity, Entity, float3>)Delegate.Combine(
                        selectedInfoUISystem.eventSelectionChanged,
                        (Action<Entity, Entity, float3>)SelectedEntityChanged);

                // Add bindings for UI to C#.
                AddBinding(new TriggerBinding<int>(ModAssemblyInfo.Name, "SelectedCompanyChanged", SelectedCompanyChanged));
                AddBinding(new TriggerBinding     (ModAssemblyInfo.Name, "ChangeThisClicked",      ChangeThisClicked     ));
                AddBinding(new TriggerBinding     (ModAssemblyInfo.Name, "ChangeAllClicked",       ChangeAllClicked      ));

                // Initialize section properties.
                Reset();
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Called when a game, main menu, editor, etc. is about to be loaded.
        /// </summary>
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            // Must be a game about to be loaded.
            if (mode != GameMode.Game)
            {
                return;
            }

            // At the time OnCreate is called, the IndustrialProcessData and BuildingPropetyData are not yet initialized.
            // IndustrialProcessData defines the resource output by a company.
            // BuildingPropetyData defines the resources allowed by a property.
            // So these initializations must be performed after OnCreate.
            try
            {
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(OnGamePreload)}");

                // Get possible company infos for commercial, industrial, office, and storage company prefabs.
                GetCompanyInfos(
                    out CompanyInfos companyInfosCommercial,
                    out CompanyInfos companyInfosIndustrial,
                    out CompanyInfos companyInfosOffice,
                    out CompanyInfos companyInfosStorage);

                // Get all property prefabs, identified by having BuildingPropertyData.
                // All the properties this mod cares about have BuildingPropertyData even though DumpPropertyPrefabs uses BuildingData.
                EntityQuery propertyPrefabsQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingPropertyData>());
                List<Entity> propertyPrefabs = propertyPrefabsQuery.ToEntityArray(Allocator.Temp).ToList();
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(OnGamePreload)} Property prefabs total = {propertyPrefabs.Count,4}");

                // Do each property prefab.
                _propertyCompanyInfos = new();
                foreach (Entity propertyPrefab in propertyPrefabs)
                {
                    // Use the allowed resources to determine the property prefab's type: commercial, industrial, office, or storage.
                    // Each m_AllowedSold, m_AllowedManufactured, and m_AllowedStored in BuildingPropertyData is a combination of Resource flags.
                    // Each property prefab will have resources for only one of the "m_Allowed*"
                    // (i.e. a property cannot have sold+manufactured, sold+stored, manufactured+stored, or all 3).
                    // Then populate the property company infos from among the possible company infos for that property type.
                    BuildingPropertyData buildingPropertyData = EntityManager.GetComponentData<BuildingPropertyData>(propertyPrefab);
                    if (buildingPropertyData.m_AllowedSold != Resource.NoResource)
                    {
                        // A property prefab that allows sold resources is commercial.
                        PopulatePropertyCompanyInfos(propertyPrefab, buildingPropertyData.m_AllowedSold, companyInfosCommercial);
                    }
                    else if (buildingPropertyData.m_AllowedManufactured != Resource.NoResource)
                    {
                        // A property prefab that allows manufactured resources is industrial or office.
                        // Identify industrial vs office property prefabs by the resource.
                        if (!EconomyUtils.IsOfficeResource(buildingPropertyData.m_AllowedManufactured))
                        {
                            PopulatePropertyCompanyInfos(propertyPrefab, buildingPropertyData.m_AllowedManufactured, companyInfosIndustrial);
                        }
                        else
                        {
                            PopulatePropertyCompanyInfos(propertyPrefab, buildingPropertyData.m_AllowedManufactured, companyInfosOffice);
                        }
                    }
                    else if (buildingPropertyData.m_AllowedStored != Resource.NoResource)
                    {
                        // A property prefab that allows stored resources is storage.
                        PopulatePropertyCompanyInfos(propertyPrefab, buildingPropertyData.m_AllowedStored, companyInfosStorage);
                    }
                    else
                    {
                        // Property prefab has no allowed resources for sold, manufactured, or stored.
                        // This is all the residential property prefabs that are not mixed.
                        // Skip this property prefab.
                    }
                }

                // Info logging.
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(OnGamePreload)} Property prefabs found = {_propertyCompanyInfos.Count,4}");

#if DEBUG
                // Dump property prefabs.
                //DumpPropertyPrefabs();

                // Dump resource data to log file for analysis of resource prefab properties.
                //ComponentLookup<ResourceData> componentLookupResourceData = CheckedStateRef.GetComponentLookup<ResourceData>(true);
                //componentLookupResourceData.Update(ref CheckedStateRef);
                //ResourceSystem resourceSystem = base.World.GetOrCreateSystemManaged<ResourceSystem>();
                //ResourcePrefabs resourcePrefabs = resourceSystem.GetPrefabs();
                //ResourceIterator iterator = ResourceIterator.GetIterator();
                //while (iterator.Next())
                //{
                //    ResourceData resourceData = componentLookupResourceData[resourcePrefabs[iterator.resource]];
                //    Mod.log.Info($"{iterator.resource}\t{resourceData.m_IsProduceable}\t{resourceData.m_IsTradable}\t{resourceData.m_IsMaterial}\t{resourceData.m_IsLeisure}\t{resourceData.m_Price.x}\t{resourceData.m_Price.y}");
                //}
                //ResourceData resourceData1 = componentLookupResourceData[resourcePrefabs[Resource.NoResource]];
                //Mod.log.Info($"{Resource.NoResource}\t{resourceData1.m_IsProduceable}\t{resourceData1.m_IsTradable}\t{resourceData1.m_IsMaterial}\t{resourceData1.m_IsLeisure}\t{resourceData1.m_Price.x}\t{resourceData1.m_Price.y}");
                //resourceData1 = componentLookupResourceData[resourcePrefabs[Resource.Last]];
                //Mod.log.Info($"{Resource.Last}\t{resourceData1.m_IsProduceable}\t{resourceData1.m_IsTradable}\t{resourceData1.m_IsMaterial}\t{resourceData1.m_IsLeisure}\t{resourceData1.m_Price.x}\t{resourceData1.m_Price.y}");
#endif
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Get possible company infos for commercial, industrial, office, and storage company prefabs.
        /// </summary>
        private void GetCompanyInfos(
            out CompanyInfos companyInfosCommercial,
            out CompanyInfos companyInfosIndustrial,
            out CompanyInfos companyInfosOffice,
            out CompanyInfos companyInfosStorage)
        {
            // Create new empty company infos.
            companyInfosCommercial = new();
            companyInfosIndustrial = new();
            companyInfosOffice     = new();
            companyInfosStorage    = new();

            try
            {
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(GetCompanyInfos)}");

                // Get component lookup for industrial process data.
                ComponentLookup<IndustrialProcessData> componentLookupIndustrialProcessData = CheckedStateRef.GetComponentLookup<IndustrialProcessData>();  
                componentLookupIndustrialProcessData.Update(ref CheckedStateRef);

                // Get company prefabs for commercial.
                // Query copied from CommercialSpawnSystem.
                EntityQuery companyPrefabQueryCommercial = GetEntityQuery(
                    ComponentType.ReadOnly<ArchetypeData        >(),
                    ComponentType.ReadOnly<CommercialCompanyData>(),
                    ComponentType.ReadOnly<IndustrialProcessData>());
                List<Entity> companyPrefabsCommercial = companyPrefabQueryCommercial.ToEntityArray(Allocator.Temp).ToList();

                // Get company prefabs for industrial, which includes office.
                // Query copied from IndustrialSpawnSystem except exclude extractors.
                EntityQuery companyPrefabQueryIndustrialOffice = GetEntityQuery(
                    ComponentType.ReadOnly<ArchetypeData        >(),
                    ComponentType.ReadOnly<IndustrialCompanyData>(),
                    ComponentType.ReadOnly<IndustrialProcessData>(),
                    ComponentType.Exclude<StorageCompanyData    >(),
                    ComponentType.Exclude<ExtractorCompanyData  >());
                List<Entity> companyPrefabsIndustrialOffice = companyPrefabQueryIndustrialOffice.ToEntityArray(Allocator.Temp).ToList();

                // Determine which company prefabs are industrial vs office.
                List<Entity> companyPrefabsIndustrial = new();
                List<Entity> companyPrefabsOffice     = new();
                foreach (Entity companyPrefabIndustrialOffice in companyPrefabsIndustrialOffice)
                {
                    // Identify industrial vs office company prefabs by the output resource.
                    // The only other way that was found to identify office is by the company prefab name starting with "Office".
                    Resource outputResource = componentLookupIndustrialProcessData[companyPrefabIndustrialOffice].m_Output.m_Resource;
                    if (!EconomyUtils.IsOfficeResource(outputResource))
                    {
                        companyPrefabsIndustrial.Add(companyPrefabIndustrialOffice);
                    }
                    else
                    {
                        companyPrefabsOffice.Add(companyPrefabIndustrialOffice);
                    }
                }

                // Get company prefabs for storage.
                // Query copied from IndustrialSpawnSystem.
                EntityQuery companyPrefabQueryStorage = GetEntityQuery(
                    ComponentType.ReadOnly<ArchetypeData        >(),
                    ComponentType.ReadOnly<StorageCompanyData   >(),
                    ComponentType.ReadOnly<IndustrialProcessData>());
                List<Entity> companyPrefabsStorage = companyPrefabQueryStorage.ToEntityArray(Allocator.Temp).ToList();

                // Create new company infos from the company prefabs.
                companyInfosCommercial = new(companyPrefabsCommercial, componentLookupIndustrialProcessData);
                companyInfosIndustrial = new(companyPrefabsIndustrial, componentLookupIndustrialProcessData);
                companyInfosOffice     = new(companyPrefabsOffice,     componentLookupIndustrialProcessData);
                companyInfosStorage    = new(companyPrefabsStorage,    componentLookupIndustrialProcessData);

                // Info logging.
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(GetCompanyInfos)} Company infos for Commercial = {companyInfosCommercial.Count,3}");
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(GetCompanyInfos)} Company infos for Industrial = {companyInfosIndustrial.Count,3}");
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(GetCompanyInfos)} Company infos for Office     = {companyInfosOffice    .Count,3}");
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(GetCompanyInfos)} Company infos for Storage    = {companyInfosStorage   .Count,3}");

                // Get the industrial and office company infos by output resource.
                // Some output resources have more than one company info.
                _companyInfosIndustrialOfficResource = new();
                foreach (CompanyInfo companyInfo in companyInfosIndustrial)
                {
                    if (!_companyInfosIndustrialOfficResource.ContainsKey(companyInfo.ResourceOutput))
                    {
                        _companyInfosIndustrialOfficResource.Add(companyInfo.ResourceOutput, new CompanyInfos());
                    }
                    _companyInfosIndustrialOfficResource[companyInfo.ResourceOutput].Add(companyInfo);
                }
                foreach (CompanyInfo companyInfo in companyInfosOffice)
                {
                    if (!_companyInfosIndustrialOfficResource.ContainsKey(companyInfo.ResourceOutput))
                    {
                        _companyInfosIndustrialOfficResource.Add(companyInfo.ResourceOutput, new CompanyInfos());
                    }
                    _companyInfosIndustrialOfficResource[companyInfo.ResourceOutput].Add(companyInfo);
                }

#if DEBUG
                // Dump company infos.
                //Mod.log.Info("Company infos commercial:");
                //DumpCompanyInfos(companyInfosCommercial);
                //Mod.log.Info("Company infos industrial:");
                //DumpCompanyInfos(companyInfosIndustrial);
                //Mod.log.Info("Company infos office:");
                //DumpCompanyInfos(companyInfosOffice);
                //Mod.log.Info("Company infos storage:");
                //DumpCompanyInfos(companyInfosStorage);
#endif
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Get company infos for an industrial or office resource.
        /// </summary>
        public CompanyInfos GetCompanyInfosForResource(Resource resource)
        {
            if (_companyInfosIndustrialOfficResource.ContainsKey(resource))
            {
                return _companyInfosIndustrialOfficResource[resource];
            }
            return new CompanyInfos();
        }

#if DEBUG
        /// <summary>
        /// Dump company infos to log file for analysis of output/input1/input2 resources and the components present on the company prefab.
        /// </summary>
        private void DumpCompanyInfos(CompanyInfos companyInfos)
        {
            // Do each company info.
            PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            foreach (CompanyInfo companyInfo in companyInfos)
            {
                // Get the company prefab.
                Entity companyPrefab = companyInfo.CompanyPrefab;

                // Get the IndustrialProcessData of the company prefab.
                if (!EntityManager.TryGetComponent(companyPrefab, out IndustrialProcessData industrialProcessData))
                {
                    Mod.log.Info($"Unable to find IndustrialProcessData for company prefab [{prefabSystem.GetPrefabName(companyPrefab)}].");
                    continue;
                }
                Mod.log.Info($"{companyPrefab} {prefabSystem.GetPrefabName(companyPrefab)} out={industrialProcessData.m_Output.m_Resource,-15} in1={industrialProcessData.m_Input1.m_Resource,-15} in2={industrialProcessData.m_Input2.m_Resource,-15}");

                // Get the ArchetypeData of the company prefab.
                if (!EntityManager.HasComponent<ArchetypeData>(companyPrefab))
                {
                    Mod.log.Info($"Unable to find ArchetypeData for company prefab [{prefabSystem.GetPrefabName(companyPrefab)}].");
                    continue;
                }
                ArchetypeData archetypeData = EntityManager.GetComponentData<ArchetypeData>(companyPrefab);

                // Construct and log a sorted list of the component types in the archetype.
                List<string> components = new();
                foreach (ComponentType componentType in archetypeData.m_Archetype.GetComponentTypes())
                {
                    components.Add(componentType.GetManagedType().ToString());
                }
                components.Sort(Comparer<string>.Default);
                foreach (string component in components)
                {
                    Mod.log.Info(component);
                }
            }
        }
#endif

        /// <summary>
        /// Populate a property company info.
        /// </summary>
        private void PopulatePropertyCompanyInfos(Entity propertyPrefab, Resource allowedResourcesFlags, CompanyInfos possibleCompanyInfos)
        {
            // From the allowed resources flags, create a list of resources that this property prefab allows.
            // Add only resources from the resource order.
            List<Resource> resourcesAllowedByPropertyPrefab = new();
            foreach (Resource resourceToCheck in CompanyInfos.ResourceOrder)
            {
                if ((allowedResourcesFlags & resourceToCheck) != 0)
                {
                    resourcesAllowedByPropertyPrefab.Add(resourceToCheck);
                }
            }

            // From the possible company infos to use,
            // use only the company infos where the output resource is one of the resources allowed by the property prefab.
            // Note that there are companies that produce Petrochemicals, Beverages, ConvenienceFood, and Textiles resources
            // from different input resources (e.g. Beverages can be produced from Grain and from Vegetables).
            // These are included separately in the possible company infos so the user can change between them.
            CompanyInfos companyInfosForPropertyPrefab = new();
            foreach (CompanyInfo possibleCompanyInfo in possibleCompanyInfos)
            {
                if (resourcesAllowedByPropertyPrefab.Contains(possibleCompanyInfo.ResourceOutput))
                {
                    companyInfosForPropertyPrefab.Add(possibleCompanyInfo);
                }
            }
            
            // There must be at least 1 company info to save the property prefab with its company info(s).
            // If there no company info for the property prefab, then
            // there is no reason to display the Change Company section.
            if (companyInfosForPropertyPrefab.Count >= 1)
            {
                _propertyCompanyInfos.Add(propertyPrefab, companyInfosForPropertyPrefab);
            }
        }

#if DEBUG
        // Property category for prefab dump.
        private enum PropertyCategory
        {
            Res,
            ResMix,
            Comm,
            Ext,
            Ind,
            Off,
            Stor,
            Other,
        }

        // Property data for prefab dump.
        private class PropertyData
        {
            public Entity PropertyPrefab { get; set; }
            public PropertyCategory Category { get; set; }
            public string Zone { get; set; }
            public bool Signature { get; set; }
            public string Name { get; set; }
            public List<Resource> Resources { get; set; }

            public PropertyData(EntityManager entityManager, Entity propertyPrefab)
            {
                PropertyPrefab = propertyPrefab;

                // Get category and resources based on BuildingPropertyData.
                PrefabSystem prefabSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
                if (entityManager.TryGetComponent(propertyPrefab, out BuildingPropertyData buildingPropertyData))
                {
                    // Check if prefab has more than one of Sold, Manufactured, and Stored.
                    int count = 0;
                    if (buildingPropertyData.m_AllowedSold         != Resource.NoResource) { count++; }
                    if (buildingPropertyData.m_AllowedManufactured != Resource.NoResource) { count++; }
                    if (buildingPropertyData.m_AllowedStored       != Resource.NoResource) { count++; }
                    if (count > 1)
                    {
                        // This should never happen.
                        Mod.log.Warn($"Property prefab [{prefabSystem.GetPrefabName(propertyPrefab)}] has more than one of Sold, Manufactured, Stored.");
                    }

                    // Check for residential mixed and commercial.
                    if (buildingPropertyData.m_AllowedSold != Resource.NoResource)
                    {
                        if (HasResidential(entityManager, propertyPrefab))
                        {
                            Category = PropertyCategory.ResMix;
                        }
                        else
                        {
                            Category = PropertyCategory.Comm;
                        }
                        Resources = GetResourcesAllowed(buildingPropertyData.m_AllowedSold);
                    }
                    // Check for industrial and office.
                    else if (buildingPropertyData.m_AllowedManufactured != Resource.NoResource)
                    {
                        if (EconomyUtils.IsExtractorResource(buildingPropertyData.m_AllowedManufactured))
                        {
                            Category = PropertyCategory.Ext;
                        }
                        else if (!EconomyUtils.IsOfficeResource(buildingPropertyData.m_AllowedManufactured))
                        {
                            Category = PropertyCategory.Ind;
                        }
                        else
                        {
                            Category = PropertyCategory.Off;
                        }
                        Resources = GetResourcesAllowed(buildingPropertyData.m_AllowedManufactured);
                    }
                    // Check for storage.
                    else if (buildingPropertyData.m_AllowedStored != Resource.NoResource)
                    {
                        Category = PropertyCategory.Stor;
                        Resources = GetResourcesAllowed(buildingPropertyData.m_AllowedStored);
                    }
                    else
                    {
                        // Property prefab has no allowed resources for sold, manufactured, or stored.
                        // Check for residential vs other.
                        if (HasResidential(entityManager, propertyPrefab))
                        {
                            Category = PropertyCategory.Res;
                        }
                        else
                        {
                            Category = PropertyCategory.Other;
                        }
                        Resources = new List<Resource>();
                    }
                }
                else
                {
                    // Property prefab has no BuildingPropertyData.
                    // Check for residential vs other.
                    if (HasResidential(entityManager, propertyPrefab))
                    {
                        Category = PropertyCategory.Res;
                    }
                    else
                    {
                        Category = PropertyCategory.Other;
                    }
                    Resources = new List<Resource>();
                }

                // Get signature status.
                Signature = entityManager.HasComponent<SignatureBuildingData>(propertyPrefab);

                // Get zone as string.
                Zone = "";
                if (entityManager.TryGetComponent(propertyPrefab, out SpawnableBuildingData spawnableBuildingData))
                {
                    Zone = prefabSystem.GetPrefabName(spawnableBuildingData.m_ZonePrefab);
                }

                // Get prefab name.
                Name = prefabSystem.GetPrefabName(propertyPrefab);
            }

            /// <summary>
            /// Get whether or not the property prefab has residential.
            /// </summary>
            private bool HasResidential(EntityManager entityManager, Entity propertyPrefab)
            {
                if (entityManager.TryGetComponent(propertyPrefab, out ObjectData objectData))
                {
                    return objectData.m_Archetype.GetComponentTypes().Contains(typeof(ResidentialProperty));
                }
                return false;
            }

            /// <summary>
            /// Convert resources allowed flags to a list of resources.
            /// </summary>
            private List<Resource> GetResourcesAllowed(Resource allowedResourcesFlags)
            {
                List<Resource> resourcesAllowed = new();
                foreach (Resource resourceToCheck in CompanyInfos.ResourceOrder)
                {
                    if ((allowedResourcesFlags & resourceToCheck) != 0)
                    {
                        resourcesAllowed.Add(resourceToCheck);
                    }
                }
                return resourcesAllowed;
            }
        }

        /// <summary>
        /// Dump property prefabs.
        /// </summary>
        private void DumpPropertyPrefabs()
        {
            // Get all property prefabs, identified by having BuildingData.
            EntityQuery propertyPrefabsQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingData>());
            List<Entity> propertyPrefabs = propertyPrefabsQuery.ToEntityArray(Allocator.Temp).ToList();

            // Do each property prefab.
            List<PropertyData> propertyDatas = new();
            foreach (Entity propertyPrefab in propertyPrefabs)
            {
                propertyDatas.Add(new PropertyData(EntityManager, propertyPrefab));
            }

            // Use nested loops to sort property datas by category and then by zone and then by signature and then by prefab name.
            for (int i = 0; i < propertyDatas.Count - 1; i++)
            {
                PropertyCategory category1 = propertyDatas[i].Category;
                string zone1 = propertyDatas[i].Zone;
                bool signature1 = propertyDatas[i].Signature;
                string name1 = propertyDatas[i].Name;
                for (int j = i + 1; j < propertyDatas.Count; j++)
                {
                    PropertyCategory category2 = propertyDatas[j].Category;
                    string zone2 = propertyDatas[j].Zone;
                    bool signature2 = propertyDatas[j].Signature;
                    string name2 = propertyDatas[j].Name;
                    if (
                        (category1 >  category2) ||
                        (category1 == category2 && zone1.CompareTo(zone2) >  0) ||
                        (category1 == category2 && zone1.CompareTo(zone2) == 0 && signature1 && !signature2) ||
                        (category1 == category2 && zone1.CompareTo(zone2) == 0 && signature1 ==  signature2 && name1.CompareTo(name2) > 0)
                       )
                    {
                        (propertyDatas[i], propertyDatas[j]) = (propertyDatas[j], propertyDatas[i]);
                        category1  = category2;
                        zone1      = zone2;
                        signature1 = signature2;
                        name1      = name2;
                    }
                }
            }

            // Log headings.
            Mod.log.Info("Property prefabs:");
            string line = "Category\tZone\tSignature\tPrefab Name\tCount";
            foreach (Resource resource in CompanyInfos.ResourceOrder)
            {
                line += "\t" + resource.ToString();
            }
            Mod.log.Info(line);

            // Do each property data.
            foreach (PropertyData propertyData in propertyDatas)
            {
                // Include data.
                line =
                    propertyData.Category.ToString() +
                    "\t" + propertyData.Zone +
                    "\t" + (propertyData.Signature ? "X" : "") +
                    "\t" + propertyData.Name;

                // Include resources, if any.
                List<Resource> resources = propertyData.Resources;
                if (resources.Count > 0)
                {
                    line += $"\t{resources.Count}";
                    foreach (Resource resource in CompanyInfos.ResourceOrder)
                    {
                        line += (resources.Contains(resource) ? "\tX" : "\t");
                    }
                }

                // Log the property data.
                Mod.log.Info(line);
            }
        }
#endif

        /// <summary>
        /// Called by the game when the game wants to reset section properties.
        /// </summary>
        [Preserve]
        protected override void Reset()
        {
            _sectionPropertyPropertyType = PropertyType.None;
            _sectionPropertyHasCompany   = false;
            _sectionPropertyCompanyInfos = null;    // Do not Clear because that will clear the possible company infos saved for the property prefab.
        }

        /// <summary>
        /// Called by the game when the game wants to know if the section should be visible.
        /// </summary>
        [Preserve]
        protected override void OnUpdate()
        {
            // Section is visible if the company on the selected property can change.
            visible = CompanyCanChange(selectedEntity, selectedPrefab);
        }

        /// <summary>
        /// Return whether or not the company on the property can change.
        /// </summary>
        public bool CompanyCanChange(Entity propertyEntity, Entity propertyPrefab)
        {
            // If property type is invalid, then company cannot change.
            if (GetPropertyType(propertyEntity) == PropertyType.None)
            {
                return false;
            }

            // If property prefab was not saved with its company infos, then company cannot change.
            if (!_propertyCompanyInfos.ContainsKey(propertyPrefab))
            {
                return false;
            }
            
            // If property has any of these components, then company cannot change.
            if (EntityManager.HasComponent<ExtractorProperty    >(propertyEntity) ||
                EntityManager.HasComponent<Abandoned            >(propertyEntity) ||
                EntityManager.HasComponent<Condemned            >(propertyEntity) ||
                EntityManager.HasComponent<Deleted              >(propertyEntity) ||
                EntityManager.HasComponent<Temp                 >(propertyEntity) ||
                EntityManager.HasComponent<Destroyed            >(propertyEntity) ||
                EntityManager.HasComponent<OutsideConnection    >(propertyEntity) ||
                EntityManager.HasComponent<UnderConstruction    >(propertyEntity))
            {
                return false;
            }

            // Company can change.
            return true;
        }

        /// <summary>
        /// Called by the game for a visible section when the game wants to process the section properties that will be sent to the UI.
        /// </summary>
        [Preserve]
        protected override void OnProcess()
        {
            // Set section properties for selected property.
            _sectionPropertyPropertyType = GetPropertyType(selectedEntity);
            _sectionPropertyHasCompany   = CompanyUtilities.TryGetCompanyAtProperty(EntityManager, selectedEntity, selectedPrefab, out Entity _);
            _sectionPropertyCompanyInfos = _propertyCompanyInfos[selectedPrefab];
        }

        /// <summary>
        /// Called by the game for a visible section when the game wants to write the previously processed section properties to the UI.
        /// </summary>
        [Preserve]
        public override void OnWriteProperties(IJsonWriter writer)
        {
            // Write the section properties.
            writer.PropertyName("propertyType");
            writer.Write((int)_sectionPropertyPropertyType);
            writer.PropertyName("hasCompany");
            writer.Write(_sectionPropertyHasCompany);

            // Write the company infos.
            // If property has a company, then include the special company info for remove company.
            _sectionPropertyCompanyInfos.Write(writer, _sectionPropertyHasCompany);
        }

        /// <summary>
        /// Handle change to selected entity.
        /// </summary>
        private void SelectedEntityChanged(Entity entity, Entity prefab, float3 position)
        {
            // The game calls OnUpdate (and OnProcess and OnWriteProperties, if needed) when the entity changes.
            // So no need to send the new section properties explicitly here.

            // Get the property's current company prefab, if any.
            _selectedCompanyIndex = 0;
            if (CompanyUtilities.TryGetCompanyAtProperty(EntityManager, selectedEntity, selectedPrefab, out Entity currentCompanyEntity) &&
                EntityManager.TryGetComponent(currentCompanyEntity, out PrefabRef currentCompanyPrefabRef))
            {
                Entity currentCompanyPrefab = currentCompanyPrefabRef.m_Prefab;

                // Find the company info that matches the current company prefab.
                if (_propertyCompanyInfos.ContainsKey(selectedPrefab))
                {
                    CompanyInfos companyInfos = _propertyCompanyInfos[selectedPrefab];
                    for (int i = 0; i < companyInfos.Count; i++)
                    {
                        if (companyInfos[i].CompanyPrefab == currentCompanyPrefab)
                        {
                            // Use the company info's index as the selected company index for the dropdown.
                            _selectedCompanyIndex = i;
                            break;
                        }
                    }
                }
            }

            // Send selected company index to UI.
            _bindingSelectedCompanyIndex.Update(_selectedCompanyIndex);
        }

        /// <summary>
        /// Handle change to selected company in the dropdown list.
        /// </summary>
        private void SelectedCompanyChanged(int selectedCompanyIndex)
        {
            // Save selected company index.
            _selectedCompanyIndex = selectedCompanyIndex;

            // Send selected company index back to the UI so the correct dropdown entry is highlighted.
            _bindingSelectedCompanyIndex.Update(_selectedCompanyIndex);
        }

        /// <summary>
        /// Handle click on the Change This button.
        /// </summary>
        private void ChangeThisClicked()
        {
            // Change or remove the current company.
            ChangeCompanyData changeCompanyData;
            if (_selectedCompanyIndex == SpecialCompanyIndexRandom)
            {
                // Change to a random company.
                changeCompanyData = BuildChangeCompanyData(RequestType.ChangeOneToRandom, Entity.Null);
            }
            else if (_selectedCompanyIndex == SpecialCompanyIndexRemove)
            {
                // Remove company.
                changeCompanyData = BuildChangeCompanyData(RequestType.RemoveOne, Entity.Null);
                _selectedCompanyIndex = 0;
                _bindingSelectedCompanyIndex.Update(_selectedCompanyIndex);
            }
            else
            {
                // Change to a specified company.
                Entity newCompanyPrefab = _sectionPropertyCompanyInfos[_selectedCompanyIndex].CompanyPrefab;
                changeCompanyData = BuildChangeCompanyData(RequestType.ChangeOneToSpecified, newCompanyPrefab);
            }

            // Send the change company data to the ChangeCompanySystem.
            _changeCompanySystem.ChangeCompany(changeCompanyData);
        }

        /// <summary>
        /// Handle click on the Change All button.
        /// </summary>
        private void ChangeAllClicked()
        {
            // Change or remove all companies like the current company.
            ChangeCompanyData changeCompanyData;
            if (_selectedCompanyIndex == SpecialCompanyIndexRandom)
            {
                // Change to a random company.
                changeCompanyData = BuildChangeCompanyData(RequestType.ChangeAllToRandom, Entity.Null);
            }
            else if (_selectedCompanyIndex == SpecialCompanyIndexRemove)
            {
                // Remove company.
                changeCompanyData = BuildChangeCompanyData(RequestType.RemoveAll, Entity.Null);
                _selectedCompanyIndex = 0;
                _bindingSelectedCompanyIndex.Update(_selectedCompanyIndex);
            }
            else
            {
                // Change to a specified company.
                Entity newCompanyPrefab = _sectionPropertyCompanyInfos[_selectedCompanyIndex].CompanyPrefab;
                changeCompanyData = BuildChangeCompanyData(RequestType.ChangeAllToSpecified, newCompanyPrefab);
            }

            // Send the change company data to the ChangeCompanySystem.
            _changeCompanySystem.ChangeCompany(changeCompanyData);
        }

        /// <summary>
        /// Build a new change company data.
        /// </summary>
        private ChangeCompanyData BuildChangeCompanyData(RequestType requestType, Entity newCompanyPrefab)
        {
            return new ChangeCompanyData
            {
                RequestType      = requestType,
                NewCompanyPrefab = newCompanyPrefab,
                CompanyInfos     = _sectionPropertyCompanyInfos,
                PropertyEntity   = selectedEntity,
                PropertyPrefab   = selectedPrefab,
                PropertyType     = _sectionPropertyPropertyType
            };
        }

        /// <summary>
        /// Get the property type of the property.
        /// </summary>
        public PropertyType GetPropertyType(Entity propertyEntity)
        {
            // For mixed residential:
            //      The base game and current DLCs/CCPs provide only residential mixed with commercial.
            //      A future DLC, CCP, or mod may provide residential mixed with industrial, office, or storage.
            //      In any case, a mixed residential property still has the *Property components below that identify the property type.

            // Check for commercial.
            if (EntityManager.HasComponent<CommercialProperty>(propertyEntity))
            {
                return PropertyType.Commercial;
            }

            // Check for industrial, which includes office and storage.
            if (EntityManager.HasComponent<IndustrialProperty>(propertyEntity))
            {
                if (EntityManager.HasComponent<OfficeProperty >(propertyEntity)) { return PropertyType.Office;  }
                if (EntityManager.HasComponent<StorageProperty>(propertyEntity)) { return PropertyType.Storage; }
                return PropertyType.Industrial;
            }

            // No property type.
            return PropertyType.None;
        }
    }
}
