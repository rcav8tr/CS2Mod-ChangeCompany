using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Buildings;
using Game.Common;
using Game.Companies;
using Game.Prefabs;
using Game.UI.InGame;
using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;

namespace ChangeCompany
{
    /// <summary>
    /// A new section in the game's selected info view in the UI.
    /// </summary>
    public partial class CompanyWorkplacesSection : InfoSectionBase
    {
        // Other systems.
        private ChangeCompanySystem        _changeCompanySystem;
        private RWHCompanyWorkplacesSystem _rwhCompanyWorkplacesSystem;
        private SelectedInfoUISystem       _selectedInfoUISystem;

        // Selected company.
        private Entity _selectedCompanyEntity;

        // C# to UI bindings.
        private ValueBinding<bool> _bindingWorkplacesOverrideValid;
        private ValueBinding<int > _bindingWorkplacesOverrideValue;

        // Section properties are for the company on the selected property.
        private bool _sectionPropertyWorkplacesOverridden;
        private int  _sectionPropertyWorkplacesOverrideValue;

        // For all sections in the base game, the group is the class name, so do the same for this section.
        protected override string group => nameof(CompanyWorkplacesSection);

        /// <summary>
        /// Initialize this system.
        /// </summary>
        [Preserve]
        protected override void OnCreate()
        {
            try
            {
                Mod.log.Info($"{nameof(CompanyWorkplacesSection)}.{nameof(OnCreate)}");

                base.OnCreate();

                // Other systems.
                _changeCompanySystem        = World.GetOrCreateSystemManaged<ChangeCompanySystem       >();
                _rwhCompanyWorkplacesSystem = World.GetOrCreateSystemManaged<RWHCompanyWorkplacesSystem>();
                _selectedInfoUISystem       = World.GetOrCreateSystemManaged<SelectedInfoUISystem      >();

                // Add bindings for C# to UI.
                AddBinding(_bindingWorkplacesOverrideValid = new ValueBinding<bool>(ModAssemblyInfo.Name, "WorkplacesOverrideValid", true));
                AddBinding(_bindingWorkplacesOverrideValue = new ValueBinding<int >(ModAssemblyInfo.Name, "WorkplacesOverrideValue", 0));

                // Add bindings for UI to C#.
                AddBinding(new TriggerBinding<bool>(ModAssemblyInfo.Name, "WorkplacesOverrideValidChanged", WorkplacesOverrideValidChanged));
                AddBinding(new TriggerBinding<int >(ModAssemblyInfo.Name, "WorkplacesOverrideValueChanged", WorkplacesOverrideValueChanged));
                AddBinding(new TriggerBinding      (ModAssemblyInfo.Name, "WorkplacesApplyClicked",         WorkplacesApplyClicked));
                AddBinding(new TriggerBinding      (ModAssemblyInfo.Name, "WorkplacesResetClicked",         WorkplacesResetClicked));
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Called by the game when the world is ready.
        /// </summary>
        [Preserve]
        protected override void OnWorldReady()
        {
            try
            {
                Mod.log.Info($"{nameof(CompanyWorkplacesSection)}.{nameof(OnWorldReady)}");

                base.OnWorldReady();

                // Initialize workplaces override value.
                // Cannot reference Mod.ModSettings in OnCreate because Mod.ModSettings are not available at that time.
                _bindingWorkplacesOverrideValue.Update(Mod.ModSettings.WorkplacesOverrideValue);
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Called by the game when the game wants to reset section properties.
        /// </summary>
        [Preserve]
        protected override void Reset()
        {
            // Clear section properties.
            _sectionPropertyWorkplacesOverridden = false;
            _sectionPropertyWorkplacesOverrideValue = 0;
        }

        /// <summary>
        /// Called by the game when the game wants to know if the section should be visible.
        /// </summary>
        [Preserve]
        protected override void OnUpdate()
        {
            // Section is visible if the selected entity has a company with PropertyRenter and WorkProvider.
            visible =
                CompanyUtilities.TryGetCompanyAtProperty(EntityManager, selectedEntity, selectedPrefab, out _selectedCompanyEntity) &&
                EntityManager.HasComponent<PropertyRenter>(_selectedCompanyEntity) &&
                EntityManager.HasComponent<WorkProvider  >(_selectedCompanyEntity);
        }

        /// <summary>
        /// Called by the game for a visible section when the game wants to process the section properties that will be sent to the UI.
        /// </summary>
        [Preserve]
        protected override void OnProcess()
        {
            // Check if there is an existing override.
            _sectionPropertyWorkplacesOverridden = EntityManager.TryGetComponent(_selectedCompanyEntity, out WorkplacesOverride workplacesOverride);
            if (_sectionPropertyWorkplacesOverridden)
            {
                // Get current override value from the override.
                _sectionPropertyWorkplacesOverrideValue = workplacesOverride.Value;
            }
        }

        /// <summary>
        /// Called by the game for a visible section when the game wants to write the previously processed section properties to the UI.
        /// </summary>
        [Preserve]
        public override void OnWriteProperties(IJsonWriter writer)
        {
            // Write the section properties.
            writer.PropertyName("workplacesOverridden");
            writer.Write(_sectionPropertyWorkplacesOverridden);
            writer.PropertyName("workplacesOverrideValue");
            writer.Write(_sectionPropertyWorkplacesOverrideValue);
        }

        /// <summary>
        /// Handle change in workplaces override valid status.
        /// </summary>
        private void WorkplacesOverrideValidChanged(bool valid)
        {
            // Immediately send the valid status back to the UI.
            // This is done because the number input determines the valid status,
            // but the company workplaces component also needs the valid status.
            _bindingWorkplacesOverrideValid.Update(valid);
        }

        /// <summary>
        /// Handle change in workplaces override value.
        /// </summary>
        private void WorkplacesOverrideValueChanged(int newValue)
        {
            // Save the new value to settings.
            Mod.ModSettings.WorkplacesOverrideValue = newValue;

            // Immediately send the override value back to the UI.
            _bindingWorkplacesOverrideValue.Update(newValue);
        }

        /// <summary>
        /// Handle click on the workplaces override Apply button.
        /// </summary>
        private void WorkplacesApplyClicked()
        {
            // The logic below causes a Unity sync point.
            // This sync point is acceptable because it happens infrequently as a result of user action.

            // Construct a new override.
            WorkplacesOverride workplacesOverride = new()
            {
                // Use the override value last saved to settings.
                Value = Mod.ModSettings.WorkplacesOverrideValue,
            };

            // Update an existing override or add a new override to the company.
            if (EntityManager.HasComponent<WorkplacesOverride>(_selectedCompanyEntity))
            {
                EntityManager.SetComponentData(_selectedCompanyEntity, workplacesOverride);
            }
            else
            {
                EntityManager.AddComponentData(_selectedCompanyEntity, workplacesOverride);
            }

            // Immediately perform the override.
            if (EntityManager.TryGetComponent(_selectedCompanyEntity, out WorkProvider workProvider))
            {
                workProvider.m_MaxWorkers = workplacesOverride.Value;
                EntityManager.SetComponentData(_selectedCompanyEntity, workProvider);
            }

            // Update the section so the new override is displayed.
            _selectedInfoUISystem.SetDirty();
        }

        /// <summary>
        /// Handle click on the workplaces override Reset button.
        /// </summary>
        private void WorkplacesResetClicked()
        {
            // Remove the workplaces override from the selected company.
            RemoveSingleWorkplacesOverride(_selectedCompanyEntity);

            // Update the section.
            _selectedInfoUISystem.SetDirty();
        }

        /// <summary>
        /// Remove the workplaces override from the company.
        /// </summary>
        private void RemoveSingleWorkplacesOverride(Entity companyEntity)
        {
            // The logic below causes a Unity sync point.
            // This sync point is acceptable because it happens infrequently as a result of user action.

            // Remove existing override from the company.
            EntityManager.RemoveComponent<WorkplacesOverride>(companyEntity);

            // Initialize workplaces same as if company was first assigned to property.
            // If Realistic Workplaces and Households mod is present, it will overwrite WorkProvider.m_MaxWorkers within 1 frame.
            if (EntityManager.TryGetComponent(companyEntity, out WorkProvider workProvider) &&
                EntityManager.TryGetComponent(companyEntity, out PrefabRef companyPrefabRef) &&
                EntityManager.TryGetComponent(companyEntity, out PropertyRenter propertyRenter) &&
                EntityManager.TryGetComponent(propertyRenter.m_Property, out PrefabRef propertyPrefabRef))
            {
                int initialWorkers = _changeCompanySystem.GetCompanyInitialWorkplaces(propertyRenter.m_Property, propertyPrefabRef.m_Prefab, companyPrefabRef.m_Prefab);
                workProvider.m_MaxWorkers = initialWorkers;
                EntityManager.SetComponentData(companyEntity, workProvider);
            }

            // If got the RealisticWorkplaceData type from RWH mod and the company has the RealisticWorkplaceData component,
            // remove the RealisticWorkplaceData component from the company so RWH recomputes workplaces.
            if (_rwhCompanyWorkplacesSystem.RWHRealisticWorkplaceDataType != null &&
                EntityManager.HasComponent(companyEntity, _rwhCompanyWorkplacesSystem.RWHRealisticWorkplaceDataType))
            {
                EntityManager.RemoveComponent(companyEntity, _rwhCompanyWorkplacesSystem.RWHRealisticWorkplaceDataType);
            }
        }

        /// <summary>
        /// Remove workplaces override from all companies.
        /// </summary>
        public void RemoveAllWorkplacesOverrides()
        {
            // Get companies with a workplaces override.
            EntityQuery queryWorkplacesOverride = SystemAPI.QueryBuilder()
                .WithAll<WorkplacesOverride, WorkProvider>()
                .WithNone<Deleted>()
                .Build();

            // Remove the workplaces override from each company.
            NativeArray<Entity> workplacesOverridesEntities = queryWorkplacesOverride.ToEntityArray(Allocator.Temp);
            foreach (Entity companyEntity in workplacesOverridesEntities)
            {
                RemoveSingleWorkplacesOverride(companyEntity);
            }

            // Update the section in case it is displayed.
            _selectedInfoUISystem.SetDirty();
        }
    }
}
