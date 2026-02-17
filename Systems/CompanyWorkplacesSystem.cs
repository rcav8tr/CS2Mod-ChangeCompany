using Game;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Simulation;
using HarmonyLib;
using System;
using System.Reflection;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Scripting;
using ExtractorCompany  = Game.Companies.ExtractorCompany;
using ProcessingCompany = Game.Companies.ProcessingCompany;
using StorageCompany    = Game.Companies.StorageCompany;

namespace ChangeCompany
{
    // WorkProvider.m_MaxWorkers is set in the following game systems:
    //      CityServiceWorkplaceInitializeSystem
    //      PropertyProcessingSystem
    //      WorkProviderSystem
    //      CommercialAISystem
    //      ExtractorAISystem
    //      IndustrialAISystem

    // This mod does not care about CityServiceWorkplaceInitializeSystem because it is only for city service workplaces, not companies.
    // This mod does not care about PropertyProcessingSystem             because it is only for newly created companies, which will not have an override.
    // This mod does not care about WorkProviderSystem                   because it is only for non-companies.
    // That leaves only CommercialAISystem, ExtractorAISystem, and IndustrialAISystem to deal with.


    /// <summary>
    /// Set the workplaces for a commercial, extractor, and industrial company.
    /// </summary>
    public partial class CommercialWorkplacesSystem : CompanyWorkplacesSystem
    { 
        private static CommercialWorkplacesSystem _thisSystem;
        [Preserve] protected override void OnCreate() { _thisSystem = this; base.OnCreate<CommercialAISystem>(this, nameof(CompanyAISystemOnUpdatePostfix)); }
        [Preserve] private static void CompanyAISystemOnUpdatePostfix() { _thisSystem.CompanyAISystemOnUpdatePostfixImpl(); }
    }
    public partial class ExtractorWorkplacesSystem : CompanyWorkplacesSystem
    {
        private static ExtractorWorkplacesSystem _thisSystem;
        [Preserve] protected override void OnCreate() { _thisSystem = this; base.OnCreate<ExtractorAISystem>(this, nameof(CompanyAISystemOnUpdatePostfix)); }
        [Preserve] private static void CompanyAISystemOnUpdatePostfix() { _thisSystem.CompanyAISystemOnUpdatePostfixImpl(); }
    }
    public partial class IndustrialWorkplacesSystem : CompanyWorkplacesSystem
    {
        private static IndustrialWorkplacesSystem _thisSystem;
        [Preserve] protected override void OnCreate() { _thisSystem = this; base.OnCreate<IndustrialAISystem>(this, nameof(CompanyAISystemOnUpdatePostfix)); }
        [Preserve] private static void CompanyAISystemOnUpdatePostfix() { _thisSystem.CompanyAISystemOnUpdatePostfixImpl(); }
    }

    /// <summary>
    /// Set the workplaces for a company.
    /// </summary>
    public abstract partial class CompanyWorkplacesSystem : GameSystemBase
    {
        // The simulation system.
        private static SimulationSystem _simulationSystem;

        // Data specific to each company workplaces system.
        private EntityQuery    _companyQuery;
        private GameSystemBase _gameCompanyAISystem;
        private PropertyInfo   _propertyInfoDependency;
        private int            _updatesPerDay;
        private bool           _initialized;

        /// <summary>
        /// Initialize this system.
        /// </summary>
        [Preserve]
        protected void OnCreate<TGameCompanyAISystem>(CompanyWorkplacesSystem derivedSystem, string postfixMethodName) where TGameCompanyAISystem : GameSystemBase
        { 
            try
            {
                Mod.log.Info($"{derivedSystem.GetType().Name}.{nameof(OnCreate)}");

                base.OnCreate();
                
                // Get simulation system.
                _simulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>(); 

                // Set company query.
                // Each query is copied from the corresponding game company AI system but with WorkplacesOverride added.
                if (typeof(TGameCompanyAISystem) == typeof(CommercialAISystem))
                {
			        _companyQuery = GetEntityQuery(
                        ComponentType.ReadOnly<WorkplacesOverride>(),
                        ComponentType.ReadWrite<WorkProvider>(),

                        ComponentType.ReadOnly<ProcessingCompany>(),
                        ComponentType.ReadOnly<UpdateFrame>(),
                        ComponentType.ReadOnly<PrefabRef>(),
                        ComponentType.ReadOnly<Resources>(),

                        ComponentType.ReadOnly<ServiceAvailable>(),
                        ComponentType.ReadOnly<TradeCost>(),

                        ComponentType.Exclude<Created>(),
                        ComponentType.Exclude<Deleted>());
                }
                else if (typeof(TGameCompanyAISystem) == typeof(ExtractorAISystem))
                {
			        _companyQuery = GetEntityQuery(
                        ComponentType.ReadOnly<WorkplacesOverride>(),
                        ComponentType.ReadWrite<WorkProvider>(),

                        ComponentType.ReadOnly<ProcessingCompany>(),
                        ComponentType.ReadOnly<UpdateFrame>(),
                        ComponentType.ReadOnly<PrefabRef>(),
                        ComponentType.ReadOnly<Resources>(),

                        ComponentType.ReadOnly<ExtractorCompany>(),
                        ComponentType.Exclude<ServiceAvailable>(),

                        ComponentType.Exclude<Created>(),
                        ComponentType.Exclude<Deleted>());  // Exclude Deleted even though query in ExtractorAISystem does not exclude Deleted.
                }
                else if (typeof(TGameCompanyAISystem) == typeof(IndustrialAISystem))
                {
			        _companyQuery = GetEntityQuery(
                        ComponentType.ReadOnly<WorkplacesOverride>(),
                        ComponentType.ReadWrite<WorkProvider>(),

                        ComponentType.ReadOnly<ProcessingCompany>(),
                        ComponentType.ReadOnly<UpdateFrame>(),
                        ComponentType.ReadOnly<PrefabRef>(),
                        ComponentType.ReadOnly<Resources>(),

                        ComponentType.ReadOnly<BuyingCompany>(),
                        ComponentType.ReadOnly<CompanyStatisticData>(),
                        ComponentType.Exclude<ServiceAvailable>(),
                        ComponentType.Exclude<ExtractorCompany>(),
                        ComponentType.Exclude<StorageCompany>(),

                        ComponentType.Exclude<Created>(),
                        ComponentType.Exclude<Deleted>());
                }
                else
                {
                    return;
                }

                // Get the game company AI system.
                _gameCompanyAISystem = World.GetOrCreateSystemManaged<TGameCompanyAISystem>();

                // Get property info for the Dependency property of the game company AI system.
                _propertyInfoDependency = typeof(TGameCompanyAISystem).GetProperty("Dependency", BindingFlags.Instance | BindingFlags.NonPublic);

                // Get updates per day from the game company AI system.
                if      (typeof(TGameCompanyAISystem) == typeof(CommercialAISystem)) { _updatesPerDay = CommercialAISystem.kUpdatesPerDay; }
                else if (typeof(TGameCompanyAISystem) == typeof(ExtractorAISystem )) { _updatesPerDay = ExtractorAISystem .kUpdatesPerDay; }
                else if (typeof(TGameCompanyAISystem) == typeof(IndustrialAISystem)) { _updatesPerDay = IndustrialAISystem.kUpdatesPerDay; }
                else { return; }

                // Create a postfix patch on the game company AI system OnUpdate.
                const string originalMethodName = "OnUpdate";
                MethodInfo originalMethod = typeof(TGameCompanyAISystem).GetMethod(originalMethodName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (originalMethod == null)
                {
                    Mod.log.Error($"Unable to find patch original method {typeof(TGameCompanyAISystem)}.{originalMethodName}.");
                    return;
                }
                MethodInfo postfixMethod = derivedSystem.GetType().GetMethod(postfixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
                if (postfixMethod == null)
                {
                    Mod.log.Error($"Unable to find patch postfix method {derivedSystem.GetType().Name}.{postfixMethodName}.");
                    return;
                }
                new Harmony(Mod.HarmonyID).Patch(originalMethod, null, new HarmonyMethod(postfixMethod));

                // Initialized.
                _initialized = true;
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Never called because system is never activated to run in a system update phase.
        /// </summary>
        [Preserve]
        protected override void OnUpdate()
        {
            // System is never activated, so OnUpdate never executes.
            // But implementation is required.
            // Logic is in CompanyAISystemOnUpdatePostfixImpl.
        }

        /// <summary>
        /// Job to override the workplaces for companies with an override.
        /// </summary>
        [BurstCompile]
        private struct OverrideWorkplacesJob : IJobChunk
        {
            // Parameters.
			public uint UpdateFrameIndex;
			[ReadOnly] public SharedComponentTypeHandle<UpdateFrame       > ComponentTypeHandleUpdateFrame;
            [ReadOnly] public       ComponentTypeHandle<WorkplacesOverride> ComponentTypeHandleWorkplacesOverride;
                       public       ComponentTypeHandle<WorkProvider      > ComponentTypeHandleWorkProvider;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Check for correct frame index.
				if (chunk.GetSharedComponent(ComponentTypeHandleUpdateFrame).m_Index != UpdateFrameIndex)
				{
					return;
				}

                // Do each override.
                NativeArray<WorkplacesOverride> workplacesOverrides = chunk.GetNativeArray(ref ComponentTypeHandleWorkplacesOverride);
                NativeArray<WorkProvider      > workProviders       = chunk.GetNativeArray(ref ComponentTypeHandleWorkProvider);
                for (int i = 0; i < workplacesOverrides.Length; i++)
                {
                    // Check if current workplaces are different than override.
                    WorkplacesOverride workplacesOverride = workplacesOverrides[i];
                    WorkProvider workProvider = workProviders[i];
                    if (workProvider.m_MaxWorkers != workplacesOverride.Value)
                    {
                        // Workplaces are different (i.e. changed by company AI system that ran just before this).
                        // Set workplaces to override value.
                        // No need for an entity command buffer because WorkProvider.m_MaxWorkers
                        // can be updated directly in this job, just like in the game company AI systems.
                        workProvider.m_MaxWorkers = workplacesOverride.Value;
                        workProviders[i] = workProvider;
                    }
                }
            }
        }

        /// <summary>
        /// Postfix patch method implementation for the game company AI system OnUpdate().
        /// </summary>
        protected void CompanyAISystemOnUpdatePostfixImpl()
        {
            // Skip if not initialized or not in a game.
            if (!_initialized || GameManager.instance.gameMode != GameMode.Game)
            {
                return;
            }

            // Get update frame.
            // All of the company AI systems use 16 for the group count parameter.
			uint updateFrame = SimulationUtils.GetUpdateFrame(_simulationSystem.frameIndex, _updatesPerDay, 16);

            // Create the job.
            OverrideWorkplacesJob overrideWorkplacesJob = new()
            {
                UpdateFrameIndex = updateFrame,
                ComponentTypeHandleUpdateFrame        = SystemAPI.GetSharedComponentTypeHandle<UpdateFrame>(),
                ComponentTypeHandleWorkplacesOverride = SystemAPI.GetComponentTypeHandle<WorkplacesOverride>(true),
                ComponentTypeHandleWorkProvider       = SystemAPI.GetComponentTypeHandle<WorkProvider>(false),
            };

            // Schedule this job to run in parallel.
            // Make this job dependent on the game company AI system job.
            JobHandle gameCompanyAISystemJob = (JobHandle)_propertyInfoDependency.GetValue(_gameCompanyAISystem);
            base.Dependency = JobChunkExtensions.ScheduleParallel(overrideWorkplacesJob, _companyQuery, JobHandle.CombineDependencies(base.Dependency, gameCompanyAISystemJob));

            // Complete() will force the game company AI system job to immediately execute and then immediately execute this job.
            // This prevents any other game systems from reading WorkProvider.n_MaxWorkers
            // after the game company AI system updated the value but before this job has a chance to overwrite the value.
            base.Dependency.Complete();
        }
    }
}
