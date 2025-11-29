using Colossal.Entities;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Agents;
using Game.Buildings;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Modding;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.UI.InGame;
using System;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;
using ExtractorCompany  = Game.Companies.ExtractorCompany;
using OutsideConnection = Game.Objects.OutsideConnection;
using ProcessingCompany = Game.Companies.ProcessingCompany;

namespace ChangeCompany
{
    /// <summary>
    /// A new section in the game's building info display in the UI.
    /// </summary>
    public partial class LockCompanySection : InfoSectionBase
    {
        // Other systems.
        private EndFrameBarrier         _endFrameBarrier;
        private CompanyMoveAwaySystem   _companyMoveAwaySystem;

        // C# to UI bindings.
        private ValueBinding<bool> _bindingCompanyLocked;

        // Selected company entity.
        private Entity _selectedCompanyEntity;

        // Company queries.
        private EntityQuery _companyQueryNotLocked;
        private EntityQuery _companyQueryLocked;

        // For all sections in the base game, the group is the class name, so do the same for this section.
        protected override string group => nameof(LockCompanySection);

        /// <summary>
        /// Initialize this system.
        /// </summary>
        [Preserve]
        protected override void OnCreate()
        {
            try
            {
                Mod.log.Info($"{nameof(LockCompanySection)}.{nameof(OnCreate)}");

                base.OnCreate();

                // Other systems.
                _endFrameBarrier       = World.GetOrCreateSystemManaged<EndFrameBarrier      >();
                _companyMoveAwaySystem = World.GetOrCreateSystemManaged<CompanyMoveAwaySystem>();

                // Get the field for the company move away query on the CompanyMoveAwaySystem.
                FieldInfo companyQueryField = typeof(CompanyMoveAwaySystem).GetField("m_CompanyQuery", BindingFlags.Instance | BindingFlags.NonPublic);
                if (companyQueryField == null)
                {
                    Mod.log.Error($"[{ModAssemblyInfo.Title}] Unable to get field [CompanyMoveAwaySystem.m_CompanyQuery].");
                    return;
                }

                // Set the company move away query on the CompanyMoveAwaySystem.
                // This company query is identical to the game's company move away query, except locked companies are excluded.
                // Excluding locked companies on the company query is what prevents the companies from moving away.
                // Note:
                //      Even though the query in the system is changed, the old query is still used for the system's RequireAnyForUpdate.
                //      Because this new query excludes some companies that would have been processed by the old query,
                //      the CompanyMoveAwaySystem.OnUpdate might run when this new query has nothing to update.
                //      All of this is okay because in a city of any size, the RequireAnyForUpdate will find at least one company to check.
                //      Therefore, the RequireAnyForUpdate prevents OnUpdate from running only on a city with no companies.
                //      Furthermore, this new query is the one that OnUpdate actually uses to check for companies to move away.
                companyQueryField.SetValue(_companyMoveAwaySystem, GetEntityQuery(
                    ComponentType.ReadOnly<ProcessingCompany    >(),
                    ComponentType.ReadOnly<PropertyRenter       >(),
                    ComponentType.ReadOnly<WorkProvider         >(),
                    ComponentType.ReadOnly<Resources            >(),
                    ComponentType.ReadOnly<PrefabRef            >(),
                    ComponentType.Exclude<ExtractorCompany      >(),
                    ComponentType.Exclude<MovingAway            >(),
                    ComponentType.Exclude<Deleted               >(),
                    ComponentType.Exclude<Temp                  >(),
                    ComponentType.Exclude<CompanyLocked         >() ));

                // Add bindings for C# to UI.
                // Need to use a binding for company locked status instead of OnWriteProperties because
                // OnWriteProperties is executed only while the simulation is running
                // and it is desired to update the company locked status even when paused.
                AddBinding(_bindingCompanyLocked = new ValueBinding<bool>(ModAssemblyInfo.Name, "CompanyLocked", false));

                // Add bindings for UI to C#.
                AddBinding(new TriggerBinding      (ModAssemblyInfo.Name, "ToggleCompanyLockedClicked",     ToggleCompanyLockedClicked    ));
                AddBinding(new TriggerBinding<bool>(ModAssemblyInfo.Name, "AllCompaniesLikeCurrentClicked", AllCompaniesLikeCurrentClicked));

                // Query to get companies that are not currently locked.
                _companyQueryNotLocked = GetEntityQuery(
                    ComponentType.ReadOnly<CompanyData      >(),
                    ComponentType.ReadOnly<PrefabRef        >(),
                    ComponentType.ReadOnly<PropertyRenter   >(),
                    ComponentType.Exclude<CompanyLocked     >(),
                    ComponentType.Exclude<Deleted           >(),
                    ComponentType.Exclude<Temp              >(),
                    ComponentType.Exclude<ExtractorCompany  >());

                // Query to get companies that are currently locked.
                _companyQueryLocked = GetEntityQuery(
                    ComponentType.ReadOnly<CompanyData      >(),
                    ComponentType.ReadOnly<PrefabRef        >(),
                    ComponentType.ReadOnly<PropertyRenter   >(),
                    ComponentType.ReadOnly<CompanyLocked    >(),
                    ComponentType.Exclude<Deleted           >(),
                    ComponentType.Exclude<Temp              >(),
                    ComponentType.Exclude<ExtractorCompany  >());
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

            // Check if incompatible Economy Fixes (aka Better Economy) mod is enabled.
            foreach (string modName in ModManager.GetModsEnabled())
            {
                if (modName.StartsWith("BetterEconomy"))
                {
                    Mod.log.Error(Translation.Get(UITranslationKey.IncompatibleWithEconomyFixes));
                    return;
                }
            }

            // Lock or unlock all companies.
            LockOrUnlockAllCompanies();
        }

        /// <summary>
        /// Called by the game when the game wants to reset section properties.
        /// </summary>
        [Preserve]
        protected override void Reset()
        {
            // Nothing to do here because there are no section properties, but implementation is required.
        }

        /// <summary>
        /// Called by the game when the game wants to know if the section should be visible.
        /// </summary>
        [Preserve]
        protected override void OnUpdate()
        {
            // If all companies are locked, then section is not visible.
            if (Mod.ModSettings.LockAllCompanies)
            {
                visible = false;
                return;
            }

            // If property has any of these components, then section is not visible.
            if (EntityManager.HasComponent<ExtractorProperty    >(selectedEntity) ||
                EntityManager.HasComponent<Abandoned            >(selectedEntity) ||
                EntityManager.HasComponent<Condemned            >(selectedEntity) ||
                EntityManager.HasComponent<Deleted              >(selectedEntity) ||
                EntityManager.HasComponent<Temp                 >(selectedEntity) ||
                EntityManager.HasComponent<Destroyed            >(selectedEntity) ||
                EntityManager.HasComponent<OutsideConnection    >(selectedEntity) ||
                EntityManager.HasComponent<UnderConstruction    >(selectedEntity))
            {
                visible = false;
                return;
            }

            // If there is no company currently on the property, then section is not visible.
            // Cannot lock a company that is not present.
            // Also save the selected company for later use.
            if (!CompanyUtilities.TryGetCompanyAtProperty(EntityManager, selectedEntity, selectedPrefab, out _selectedCompanyEntity))
            {
                visible = false;
                return;
            }

            // If the company does not have ProcessingCompany, then section is not visible.
            // The game logic in CompanyMoveAwaySystem.CheckMoveAwayJob acts only on companies with the ProcessingCompany component.
            // Because companies without ProcessingCompany cannot move away, there is no need for the lock section.
            // Note that storage companies do not have ProcessingCompany.
            if (!EntityManager.HasComponent<ProcessingCompany>(_selectedCompanyEntity))
            {
                visible = false;
                return;
            }

            // Section is visible.
            visible = true;

            // Update company locked status in UI.
            _bindingCompanyLocked.Update(EntityManager.HasComponent<CompanyLocked>(_selectedCompanyEntity));
        }

        /// <summary>
        /// Called by the game for a visible section when the game wants to process the section properties that will be sent to the UI.
        /// </summary>
        [Preserve]
        protected override void OnProcess()
        {
            // Nothing to do here because there are no section properties, but implementation is required.
        }

        /// <summary>
        /// Called by the game for a visible section when the game wants to write the previously processed section properties to the UI.
        /// </summary>
        [Preserve]
        public override void OnWriteProperties(IJsonWriter writer)
        {
            // Nothing to do here because there are no section properties, but implementation is required.
        }

        /// <summary>
        /// Handle click on button for toggle company locked.
        /// </summary>
        private void ToggleCompanyLockedClicked()
        {
            // Process the request to lock or unlock a company.
            // This logic will be executed only occasionally based on a user action.
            // This logic acts on only one company.
            // There is only one operation performed on the company.
            // Therefore, performance is not critical.
            // Because performance is not critical, a job is not used.
            try
            {
                // Toggle the company locked status on the selected company.
                bool companyLocked = !EntityManager.HasComponent<CompanyLocked>(_selectedCompanyEntity);
                if (companyLocked)
                {
                    _endFrameBarrier.CreateCommandBuffer().AddComponent<CompanyLocked>(_selectedCompanyEntity);
                }
                else
                {
                    _endFrameBarrier.CreateCommandBuffer().RemoveComponent<CompanyLocked>(_selectedCompanyEntity);
                }

                // Send the new company locked status to the UI.
                _bindingCompanyLocked.Update(companyLocked);
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Handle click on one of the buttons for all companies like current.
        /// </summary>
        private void AllCompaniesLikeCurrentClicked(bool lockAll)
        {
            // Process the request for all companies like current.
            // This logic will be executed only occasionally based on a user action.
            // This logic acts on many companies, but there is potentially only one operation performed on each company.
            // In a city with about 5,000 companies, the logic below took about 1ms on the author's PC.
            // Therefore, performance is not critical.
            // Because performance is not critical, a job is not used.
            try
            {
                // Get the company prefab for the selected company.
                if (!EntityManager.TryGetComponent(_selectedCompanyEntity, out PrefabRef selectedCompanyPrefabRef))
                {
                    // This should never happen.
                    return;
                }
                Entity selectedCompanyPrefab = selectedCompanyPrefabRef.m_Prefab;

                // Get the company query to use.
                // When   locking companies, get companies currently not locked.
                // When unlocking companies, get companies currently     locked.
                EntityQuery companyQuery = lockAll ? _companyQueryNotLocked : _companyQueryLocked;

                // Check each company.
                EntityCommandBuffer entityCommandbuffer = _endFrameBarrier.CreateCommandBuffer();
                foreach (Entity companyToCheckEntity in companyQuery.ToEntityArray(Allocator.Temp))
                {
                    // Determine if the company to check prefab is the same as the selected company prefab
                    // (i.e. "like this" or like the selected company).
                    if (EntityManager.TryGetComponent(companyToCheckEntity, out PrefabRef companyToCheckPrefabRef) &&
                        companyToCheckPrefabRef.m_Prefab == selectedCompanyPrefab)
                    {
                        // Property must valid on PropertyRenter of company.
                        if (EntityManager.TryGetComponent(companyToCheckEntity, out PropertyRenter propertyRenter) &&
                            propertyRenter.m_Property != Entity.Null)
                        {
                            // Add or remove the company locked component on the company to check.
                            if (lockAll)
                            {
                                entityCommandbuffer.AddComponent<CompanyLocked>(companyToCheckEntity);
                            }
                            else
                            {
                                entityCommandbuffer.RemoveComponent<CompanyLocked>(companyToCheckEntity);
                            }
                        }
                    }
                }
                
                // Update company locked status in UI.
                // The company on the selected property should have been handled by the logic above.
                _bindingCompanyLocked.Update(lockAll);
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Remove the lock on all individual companies.
        /// </summary>
        public void UnlockAllCompanies()
        {
            // Process the request for unlocking all companies.
            // This logic will be executed only occasionally based on a user action.
            // This logic acts on many companies, but there is only one operation performed on each company.
            // Therefore, performance is not critical.
            // Because performance is not critical, a job is not used.
            try
            {
                // Remove the locked component from every locked company.
                _endFrameBarrier.CreateCommandBuffer().RemoveComponent<CompanyLocked>(_companyQueryLocked, EntityQueryCaptureMode.AtPlayback);
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Lock or unlock all companies.
        /// </summary>
        public void LockOrUnlockAllCompanies()
        {
            // All companies are locked or unlocked by disabling or enabling respectively the entire company move away system.
            _companyMoveAwaySystem.Enabled = !Mod.ModSettings.LockAllCompanies;
        }
    }
}
