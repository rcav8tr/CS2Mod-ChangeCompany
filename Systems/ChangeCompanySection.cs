using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Buildings;
using Game.Common;
using Game.Economy;
using Game.Prefabs;
using Game.UI.InGame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace ChangeCompany
{
    /// <summary>
    /// A new section in the game's selected info view in the UI.
    /// </summary>
    public partial class ChangeCompanySection : InfoSectionBase
    {
        // Other systems.
        private ChangeCompanySystem _changeCompanySystem;

        // For each property prefab, maintain its corresponding CompanyInfos.
        // Because the company infos for a property prefab should not change during the game,
        // the company infos are obtained once when this system is created.
        // This makes obtaining the company infos for a property prefab very fast during the game (simple lookup)
        // compared to computing the company infos when they are needed.
        // This extra speed comes at the expense of more memory used to store the data.
        // The property company infos has about 1360 records in total and each record has at least two entries in its CompanyInfos.
        private Dictionary<Entity, CompanyInfos> _propertyCompanyInfos;

        // Define resources manufactured by office.
        private const Resource ResourcesOffice = Resource.Software | Resource.Telecom | Resource.Financial | Resource.Media;

        // C# to UI bindings.
        private ValueBinding<int> _bindingSelectedCompanyIndex;

        // Selected company index.
        private int _selectedCompanyIndex;

        // Section properties.
        private PropertyType _sectionPropertyPropertyType;
        private CompanyInfos _sectionPropertyCompanyInfos = new CompanyInfos();

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

                // Initialize property company infos.
                InitializePropertyCompanyInfos();

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
                AddBinding(new TriggerBinding     (ModAssemblyInfo.Name, "ChangeNowClicked",       ChangeNowClicked      ));

                // Use reflection to add this section to the list of middle sections from SelectedInfoUISystem.
                // By adding right after the game's CompanySection, this section will be displayed right after CompanySection.
                FieldInfo fieldInfoMiddleSections = typeof(SelectedInfoUISystem).GetField("m_MiddleSections", BindingFlags.Instance | BindingFlags.NonPublic);
                List<ISectionSource> middleSections = (List<ISectionSource>)fieldInfoMiddleSections.GetValue(selectedInfoUISystem);
                bool found = false;
                for (int i = 0; i < middleSections.Count; i++)
                {
                    if (middleSections[i].GetType() == typeof(CompanySection))
                    {
                        middleSections.Insert(i + 1, this);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Mod.log.Error($"Change Company unable to find CompanySection in middle sections of SelectedInfoUISystem.");
                }

                // Initialize section properties.
                Reset();

#if DEBUG
                // Dump resource data to log file for analysis of resource prefab properties.
                //ComponentLookup<ResourceData> componentLookupResourceData = CheckedStateRef.GetComponentLookup<ResourceData>(true);
                //componentLookupResourceData.Update(ref CheckedStateRef);
                //ResourceSystem resourceSystem = base.World.GetOrCreateSystemManaged<ResourceSystem>();
                //ResourcePrefabs resourcePrefabs = resourceSystem.GetPrefabs();
                //ResourceIterator iterator = ResourceIterator.GetIterator();
                //while (iterator.Next())
                //{
                //    ResourceData resourceData = componentLookupResourceData[resourcePrefabs[iterator.resource]];
                //    Mod.log.Info($"{iterator.resource,15}\t{resourceData.m_IsProduceable}\t{resourceData.m_IsTradable}\t{resourceData.m_IsMaterial}\t{resourceData.m_IsLeisure}\t{resourceData.m_Price.x}\t{resourceData.m_Price.y}");
                //}
#endif
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Initialize property company infos.
        /// </summary>
        private void InitializePropertyCompanyInfos()
        {
            try
            {
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(InitializePropertyCompanyInfos)}");

                // Get possible company infos for commercial, industrial, office, and storage company prefabs.
                GetCompanyInfos(
                    out CompanyInfos companyInfosCommercial,
                    out CompanyInfos companyInfosIndustrial,
                    out CompanyInfos companyInfosOffice,
                    out CompanyInfos companyInfosStorage);

                // Get all property prefabs, identified by having BuildingPropertyData.
                EntityQuery propertyPrefabsQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingPropertyData>());
                List<Entity> propertyPrefabs = propertyPrefabsQuery.ToEntityArray(Allocator.Temp).ToList();
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(InitializePropertyCompanyInfos)} property prefabs total = {propertyPrefabs.Count,4}");

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
                        // A property prefab that allows sold resources means it is commercial.
                        PopulatePropertyCompanyInfos(propertyPrefab, buildingPropertyData.m_AllowedSold, companyInfosCommercial);
                    }
                    else if (buildingPropertyData.m_AllowedManufactured != Resource.NoResource)
                    {
                        // A property prefab that allows manufactured resources means it is industrial or office.
                        // Identify industrial vs office property prefabs by the resource.
                        if ((buildingPropertyData.m_AllowedManufactured & ResourcesOffice) == Resource.NoResource)
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
                        // A property prefab that allows stored resources means it is storage.
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
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(InitializePropertyCompanyInfos)} property prefabs found = {_propertyCompanyInfos.Count,4}");

#if DEBUG
                // Dump property prefabs.
                //DumpPropertyPrefabs();
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
                    ComponentType.ReadOnly<ArchetypeData>(),
                    ComponentType.ReadOnly<CommercialCompanyData>(),
                    ComponentType.ReadOnly<IndustrialProcessData>());
                List<Entity> companyPrefabsCommercial = companyPrefabQueryCommercial.ToEntityArray(Allocator.Temp).ToList();

                // Get company prefabs for industrial, which includes office.
                // Query copied from IndustrialSpawnSystem except exclude extractors.
                List<Entity> companyPrefabsIndustrial = new List<Entity>();
                List<Entity> companyPrefabsOffice     = new List<Entity>();
                EntityQuery companyPrefabQueryIndustrial = GetEntityQuery(
                    ComponentType.ReadOnly<ArchetypeData>(),
                    ComponentType.ReadOnly<IndustrialCompanyData>(),
                    ComponentType.ReadOnly<IndustrialProcessData>(),
                    ComponentType.Exclude<StorageCompanyData>(),
                    ComponentType.Exclude<ExtractorCompanyData>());
                List<Entity> companyPrefabsIndustrialOffice = companyPrefabQueryIndustrial.ToEntityArray(Allocator.Temp).ToList();
                foreach (Entity companyPrefabIndustrialOffice in companyPrefabsIndustrialOffice)
                {
                    // Identify industrial vs office company prefabs by the output resource.
                    // The only other way that was found to identify office is by the company prefab name starting with "Office".
                    Resource outputResource = componentLookupIndustrialProcessData[companyPrefabIndustrialOffice].m_Output.m_Resource;
                    if ((outputResource & ResourcesOffice) == Resource.NoResource)
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
                    ComponentType.ReadOnly<ArchetypeData>(),
                    ComponentType.ReadOnly<StorageCompanyData>(),
                    ComponentType.ReadOnly<IndustrialProcessData>());
                List<Entity> companyPrefabsStorage = companyPrefabQueryStorage.ToEntityArray(Allocator.Temp).ToList();

                // Create new company infos from the company prefabs.
                companyInfosCommercial = new CompanyInfos(companyPrefabsCommercial, componentLookupIndustrialProcessData);
                companyInfosIndustrial = new CompanyInfos(companyPrefabsIndustrial, componentLookupIndustrialProcessData);
                companyInfosOffice     = new CompanyInfos(companyPrefabsOffice,     componentLookupIndustrialProcessData);
                companyInfosStorage    = new CompanyInfos(companyPrefabsStorage,    componentLookupIndustrialProcessData);

                // Info logging.
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(GetCompanyInfos)} company infos for Commercial = {companyInfosCommercial.Count,3}");
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(GetCompanyInfos)} company infos for Industrial = {companyInfosIndustrial.Count,3}");
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(GetCompanyInfos)} company infos for Office     = {companyInfosOffice    .Count,3}");
                Mod.log.Info($"{nameof(ChangeCompanySection)}.{nameof(GetCompanyInfos)} company infos for Storage    = {companyInfosStorage   .Count,3}");

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
                    Mod.log.Info($"{companyPrefab} Unable to find IndustrialProcessData for company prefab");
                    continue;
                }
                Mod.log.Info($"{companyPrefab} {prefabSystem.GetPrefabName(companyPrefab)} out={industrialProcessData.m_Output.m_Resource,-15} in1={industrialProcessData.m_Input1.m_Resource,-15} in2={industrialProcessData.m_Input2.m_Resource,-15}");

                // Get the ArchetypeData of the company prefab.
                if (!EntityManager.HasComponent<ArchetypeData>(companyPrefab))
                {
                    Mod.log.Info($"{companyPrefab} Unable to find ArchetypeData for company prefab");
                    continue;
                }
                ArchetypeData archetypeData = EntityManager.GetComponentData<ArchetypeData>(companyPrefab);

                // Construct and log a sorted list of the component types in the archetype.
                List<string> components = new List<string>();
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
            List<Resource> resourcesAllowedByPropertyPrefab = new List<Resource>();
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
            // These are included separately in the company infos so the user can change between them.
            CompanyInfos companyInfosForPropertyPrefab = new();
            foreach (CompanyInfo possibleCompanyInfo in possibleCompanyInfos)
            {
                if (resourcesAllowedByPropertyPrefab.Contains(possibleCompanyInfo.ResourceOutput))
                {
                    companyInfosForPropertyPrefab.Add(possibleCompanyInfo);
                }
            }

            // There must be at least 2 company infos to save the property prefab with it company infos.
            // If there is only one company info for the property prefab, then
            // there is no reason to display the Change Company section because changing the company
            // will not result in any change in the resource sold, manufactured, or stored.
            if (resourcesAllowedByPropertyPrefab.Count >= 2)
            {
                _propertyCompanyInfos.Add(propertyPrefab, companyInfosForPropertyPrefab);
            }
        }

#if DEBUG
        /// <summary>
        /// Dump property prefabs and resources allowed by each.
        /// </summary>
        private void DumpPropertyPrefabs()
        {
            // Get all property prefabs, identified by having BuildingPropertyData.
            EntityQuery propertyPrefabsQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingPropertyData>());
            List<Entity> propertyPrefabs = propertyPrefabsQuery.ToEntityArray(Allocator.Temp).ToList();

            // Use nested loops to sort property prefabs by name.
            PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            for (int i = 0; i < propertyPrefabs.Count - 1; i++)
            {
                string prefabName1 = prefabSystem.GetPrefabName(propertyPrefabs[i]);
                for (int j = i + 1; j < propertyPrefabs.Count; j++)
                {
                    string prefabName2 = prefabSystem.GetPrefabName(propertyPrefabs[j]);
                    if (prefabName1.CompareTo(prefabName2) > 0)
                    {
                        (propertyPrefabs[i], propertyPrefabs[j]) = (propertyPrefabs[j], propertyPrefabs[i]);
                        prefabName1 = prefabName2;
                    }
                }
            }

            // Create dictionaries of property prefabs and for each property prefab store a list of the resources allowed.
            Dictionary<Entity, List<Resource>> propertyPrefabsSold         = new();
            Dictionary<Entity, List<Resource>> propertyPrefabsManufactured = new();
            Dictionary<Entity, List<Resource>> propertyPrefabsStored       = new();

            // Do each property prefab.
            foreach (Entity propertyPrefab in propertyPrefabs)
            {
                BuildingPropertyData buildingPropertyData = EntityManager.GetComponentData<BuildingPropertyData>(propertyPrefab);
                if (buildingPropertyData.m_AllowedSold != Resource.NoResource)
                {
                    propertyPrefabsSold.Add(propertyPrefab, GetResourcesAllowed(buildingPropertyData.m_AllowedSold));
                }
                else if (buildingPropertyData.m_AllowedManufactured != Resource.NoResource)
                {
                    propertyPrefabsManufactured.Add(propertyPrefab, GetResourcesAllowed(buildingPropertyData.m_AllowedManufactured));
                }
                else if (buildingPropertyData.m_AllowedStored != Resource.NoResource)
                {
                    propertyPrefabsStored.Add(propertyPrefab, GetResourcesAllowed(buildingPropertyData.m_AllowedStored));
                }
                else
                {
                    // Property prefab has no allowed resources for sold, manufactured, or stored.
                    // Skip this property prefab.
                }
            }

            // Dump property prefabs.
            Mod.log.Info("Property prefabs for Sold:");
            DumpPropertyPrefabs(propertyPrefabsSold);
            Mod.log.Info("Property prefabs for Manufactured:");
            DumpPropertyPrefabs(propertyPrefabsManufactured);
            Mod.log.Info("Property prefabs for Stored:");
            DumpPropertyPrefabs(propertyPrefabsStored);
        }

        /// <summary>
        /// Convert resources allowed flags to a list of resources.
        /// </summary>
        private List<Resource> GetResourcesAllowed(Resource allowedResourcesFlags)
        {
            List<Resource> resourcesAllowed = new List<Resource>();
            foreach (Resource resourceToCheck in CompanyInfos.ResourceOrder)
            {
                if ((allowedResourcesFlags & resourceToCheck) != 0)
                {
                    resourcesAllowed.Add(resourceToCheck);
                }
            }
            return resourcesAllowed;
        }

        /// <summary>
        /// Dump property prefabs and resources allowed for each.
        /// </summary>
        private void DumpPropertyPrefabs(Dictionary<Entity, List<Resource>> resourcesAllowed)
        {
            // Dump the resources allowed with a heading.
            PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            string line = "Prefab Name\tCount";
            foreach (Resource resource in CompanyInfos.ResourceOrder)
            {
                line += "\t" + resource.ToString();
            }
            Mod.log.Info(line);
            foreach (Entity entity in resourcesAllowed.Keys)
            {
                List<Resource> resources = resourcesAllowed[entity];
                line = $"{prefabSystem.GetPrefabName(entity)}\t{resources.Count}";
                foreach (Resource resource in CompanyInfos.ResourceOrder)
                {
                    line += "\t" + (resources.Contains(resource) ? "X" : "");
                }
                Mod.log.Info(line);
            }
        }
#endif

        /// <summary>
        /// Called when the game wants to reset section properties.
        /// </summary>
        [Preserve]
        protected override void Reset()
        {
            _sectionPropertyPropertyType = PropertyType.None;
            _sectionPropertyCompanyInfos = null;    // Do not Clear because that will clear the company infos saved for the property prefab.
        }

        /// <summary>
        /// Called when the game wants to know if the section should be visible.
        /// </summary>
        [Preserve]
        protected override void OnUpdate()
        {
            // If property type is invalid, then section is not visible.
            PropertyType propertyType = GetPropertyType();
            if (propertyType == PropertyType.None)
            {
                visible = false;
                return;
            }

            // If property prefab was not saved with company infos, then section is not visible.
            if (!_propertyCompanyInfos.ContainsKey(selectedPrefab))
            {
                visible = false;
                return;
            }
            
            // If property has any of these components, then section is not visible.
            // InfoSectionBase by default is not visible for Destroyed, OutsideConnection, UnderConstruction, and Upgrades.
            // So no need to explicitly check for those here.
            if (EntityManager.HasComponent<Signature           >(selectedEntity) ||
                EntityManager.HasComponent<ExtractorProperty   >(selectedEntity) ||
                EntityManager.HasComponent<Abandoned           >(selectedEntity) ||
                EntityManager.HasComponent<Condemned           >(selectedEntity) ||
                EntityManager.HasComponent<Deleted             >(selectedEntity))
            {
                visible = false;
                return;
            }

            // Section is visible.
            visible = true;
        }

        /// <summary>
        /// Called for a visible section when the game wants to process the section properties that will be sent to the UI.
        /// </summary>
        [Preserve]
        protected override void OnProcess()
        {
            // Get the property type for the section.
            _sectionPropertyPropertyType = GetPropertyType();

            // Get the company infos for the section.
            _sectionPropertyCompanyInfos = _propertyCompanyInfos[selectedPrefab];
        }

        /// <summary>
        /// Called for a visible section when the game wants to write the previously processed section properties to the UI.
        /// </summary>
        [Preserve]
        public override void OnWriteProperties(IJsonWriter writer)
        {
            // Write the section properties.
            writer.PropertyName("propertyType");
            writer.Write((int)_sectionPropertyPropertyType);
            _sectionPropertyCompanyInfos.Write(writer);
        }

        /// <summary>
        /// Handle change to selected entity.
        /// </summary>
        private void SelectedEntityChanged(Entity entity, Entity prefab, float3 position)
        {
            // The game calls OnUpdate (and OnProcess and OnWriteProperties, if needed) when the entity changes.
            // So no need to send the new section properties explicitly here.

            // Reset selected company index so first dropdown entry is selected.
            _selectedCompanyIndex = 0;
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
        /// Handle click on the Change Now button.
        /// </summary>
        private void ChangeNowClicked()
        {
            // Send the change company data to the ChangeCompanySystem.
            Entity newCompanyPrefab = _sectionPropertyCompanyInfos[_selectedCompanyIndex].CompanyPrefab;
            _changeCompanySystem.ChangeCompany(newCompanyPrefab, selectedEntity, selectedPrefab, _sectionPropertyPropertyType);
        }

        /// <summary>
        /// Get the property type of the selected property and prefab.
        /// </summary>
        private PropertyType GetPropertyType()
        {
            // Residential must be mixed to have commercial.
            if (EntityManager.HasComponent<ResidentialProperty>(selectedEntity))
            {
                if (EntityManager.TryGetComponent(selectedPrefab, out BuildingPropertyData buildingPropertyData))
                {
                    if (PropertyUtils.IsMixedBuilding(buildingPropertyData))
                    {
                        return PropertyType.Commercial;
                    }
                }

                // Residential that is not mixed is never any other property type.
                return PropertyType.None;
            }

            // Check for commercial.
            if (EntityManager.HasComponent<CommercialProperty>(selectedEntity))
            {
                return PropertyType.Commercial;
            }

            // Check for industrial, which includes office and storage.
            if (EntityManager.HasComponent<IndustrialProperty>(selectedEntity))
            {
                if (EntityManager.HasComponent<OfficeProperty >(selectedEntity)) { return PropertyType.Office;  }
                if (EntityManager.HasComponent<StorageProperty>(selectedEntity)) { return PropertyType.Storage; }
                return PropertyType.Industrial;
            }

            // No property type.
            return PropertyType.None;
        }
    }
}
