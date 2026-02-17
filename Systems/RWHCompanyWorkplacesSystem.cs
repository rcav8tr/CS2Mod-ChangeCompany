using Colossal.IO.AssetDatabase;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Companies;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Tools;
using HarmonyLib;
using System;
using System.Reflection;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Scripting;
using static Game.Modding.ModManager;

namespace ChangeCompany
{
    /// <summary>
    /// Set the workplaces for a company that was updated by the Realistic Workplaces and Households (RWH) mod.
    /// </summary>
    public partial class RWHCompanyWorkplacesSystem : GameSystemBase
    {
        // This system.
        private static RWHCompanyWorkplacesSystem _thisSystem;

        // Data specific to each company workplaces system.
        private GameSystemBase _rwhWorkplaceUpdateSystem;
        private PropertyInfo   _propertyInfoDependency;
        private bool           _initialized;

        // The RealisticWorkplaceData component type from RWH.
        public Type RWHRealisticWorkplaceDataType { get; private set; }

        /// <summary>
        /// Initialize this system.
        /// </summary>
        [Preserve]
        protected override void OnCreate()
        { 
            Mod.log.Info($"{nameof(RWHCompanyWorkplacesSystem)}.{nameof(OnCreate)}");

            base.OnCreate();
            
            // Get this system.
            _thisSystem = this;
        }

        /// <summary>
        /// Called by the game when the world is ready.
        /// </summary>
        [Preserve]
        protected override void OnWorldReady()
        {
            try
            {
                Mod.log.Info($"{nameof(RWHCompanyWorkplacesSystem)}.{nameof(OnWorldReady)}");

                base.OnWorldReady();

                // Try to get RWH WorkplaceUpdateSystem and RealisticWorkplaceData.
                if (TryGetRWHWorkplaceUpdateSystem(out Type rwhWorkplaceUpdateSystemType, out Type rwhRealisticWorkplaceDataType))
                {
                    // Save the RealisticWorkplaceData type.
                    RWHRealisticWorkplaceDataType = rwhRealisticWorkplaceDataType;

                    // Finish initializing this system.
                    FinishInitialization(rwhWorkplaceUpdateSystemType);
                }
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Try to get the RWH WorkplaceUpdateSystem.
        /// </summary>
        private bool TryGetRWHWorkplaceUpdateSystem(out Type rwhWorkplaceUpdateSystemType, out Type rwhRealisticWorkplaceDataType)
        {
            // Initialize.
            rwhWorkplaceUpdateSystemType  = null;
            rwhRealisticWorkplaceDataType = null;

            // Check if Realistic Workplaces and Households (RWH) mod is active.
            ExecutableAsset[] modAssets = ExecutableAsset.GetModAssets();
            foreach (ExecutableAsset executableAsset in modAssets)
            {
                ModInfo modInfo = new(executableAsset);
                if (modInfo.isValid && modInfo.asset != null && modInfo.asset.name == "RWH")
                {
                    // Find WorkplaceUpdateSystem and RealisticWorkplaceData in RWH.
                    Type[] assemblyTypes = modInfo.asset.assembly.GetTypes();
                    foreach (Type assemblyType in assemblyTypes)
                    {
                        if (assemblyType.FullName == "RealisticWorkplacesAndHouseholds.Systems.WorkplaceUpdateSystem")
                        {
                            rwhWorkplaceUpdateSystemType = assemblyType;
                        }
                        if (assemblyType.FullName == "RealisticWorkplacesAndHouseholds.Components.RealisticWorkplaceData")
                        {
                            rwhRealisticWorkplaceDataType = assemblyType;
                        }
                    }

                    // Check results.
                    if (rwhWorkplaceUpdateSystemType == null)
                    {
                        Mod.log.Warn($"Found the RWH mod but not WorkplaceUpdateSystem in the mod.");
                        return false;
                    }
                    if (rwhRealisticWorkplaceDataType == null)
                    {
                        Mod.log.Warn($"Found the RWH mod but not RealisticWorkplaceData in the mod.");
                        return false;
                    }

                    // Found both.
                    Mod.log.Info("Realistic Workplaces and Households mod found.");
                    return true;
                }
            }

            // RWH mod not found.
            // This is not an error, the mod is simply not active.
            Mod.log.Info("Realistic Workplaces and Households mod not found.");
            return false;
        }

        /// <summary>
        /// Finish initialization after RWH WorkplaceUpdateSystem was found.
        /// </summary>
        private void FinishInitialization(Type rwhWorkplaceUpdateSystemType)
        {
            // Get the RWH WorkplaceUpdateSystem.
            _rwhWorkplaceUpdateSystem = (GameSystemBase)World.GetOrCreateSystemManaged(rwhWorkplaceUpdateSystemType);
            
            // Get property info for the Dependency property of the RWH WorkplaceUpdateSystem.
            _propertyInfoDependency = rwhWorkplaceUpdateSystemType.GetProperty("Dependency", BindingFlags.Instance | BindingFlags.NonPublic);

            // Create a postfix patch on RWH WorkplaceUpdateSystem.OnUpdate().
            const string originalMethodName = "OnUpdate";
            MethodInfo originalMethod = rwhWorkplaceUpdateSystemType.GetMethod(originalMethodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (originalMethod == null)
            {
                Mod.log.Error($"Unable to find patch original method {rwhWorkplaceUpdateSystemType.GetType().Name}.{originalMethodName}.");
                return;
            }
            const string postfixMethodName = nameof(RWHWorkplaceUpdateSystemOnUpdatePostfix);
            MethodInfo postfixMethod = typeof(RWHCompanyWorkplacesSystem).GetMethod(postfixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            if (postfixMethod == null)
            {
                Mod.log.Error($"Unable to find patch postfix method {nameof(RWHCompanyWorkplacesSystem)}.{postfixMethodName}.");
                return;
            }
            new Harmony(Mod.HarmonyID).Patch(originalMethod, null, new HarmonyMethod(postfixMethod));

            // Initialized.
            _initialized = true;
        }

        /// <summary>
        /// Never called because system is never activated to run in a system update phase.
        /// </summary>
        [Preserve]
        protected override void OnUpdate()
        {
            // System is never activated, so OnUpdate never executes.
            // But implementation is required.
            // Logic is in RWHWorkplaceUpdateSystemOnUpdatePostfixImpl.
        }

        /// <summary>
        /// Job to override the workplaces for companies with an override.
        /// </summary>
        [BurstCompile]
        private struct OverrideWorkplacesJob : IJobChunk
        {
            // Parameters.
            [ReadOnly] public ComponentTypeHandle<WorkplacesOverride> ComponentTypeHandleWorkplacesOverride;
                       public ComponentTypeHandle<WorkProvider      > ComponentTypeHandleWorkProvider;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
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
                        // Workplaces are different (i.e. changed by RWH WorkplaceUpdateSystem that ran just before this).
                        // Set workplaces to override value.
                        // No need for an entity command buffer because WorkProvider.m_MaxWorkers
                        // can be updated directly in this job, just like in RWH WorkplaceUpdateSystem.
                        workProvider.m_MaxWorkers = workplacesOverride.Value;
                        workProviders[i] = workProvider;
                    }
                }
            }
        }

        /// <summary>
        /// Postfix patch method for RWH WorkplaceUpdateSystem.OnUpdate().
        /// </summary>
        [Preserve]
        private static void RWHWorkplaceUpdateSystemOnUpdatePostfix()
        {
            // Call the postfix patch method implementation for the game's instance of this system.
            _thisSystem.RWHWorkplaceUpdateSystemOnUpdatePostfixImpl();
        }

        /// <summary>
        /// Postfix patch method implementation for RWH WorkplaceUpdateSystem.OnUpdate().
        /// </summary>
        private void RWHWorkplaceUpdateSystemOnUpdatePostfixImpl()
        {
            // Skip if not initialized or not in a game.
            if (!_initialized || GameManager.instance.gameMode != GameMode.Game)
            {
                return;
            }

            // Construct the query to get companies.
            // Query adapted from RWH WorkplaceUpdateSystem but with WorkplacesOverride added.
            EntityQuery companyQuery = SystemAPI.QueryBuilder()
                .WithAll<WorkplacesOverride>()
                .WithAll<WorkProvider, PropertyRenter, PrefabRef, CompanyData>()
                .WithNone<Deleted, Temp>()
                .Build();

            // Create the job.
            OverrideWorkplacesJob overrideWorkplacesJob = new()
            {
                ComponentTypeHandleWorkplacesOverride = SystemAPI.GetComponentTypeHandle<WorkplacesOverride>(true),
                ComponentTypeHandleWorkProvider = SystemAPI.GetComponentTypeHandle<WorkProvider>(false),
            };

            // Schedule this job to run in parallel.
            // Make this job dependent on the RWH WorkplaceUpdateSystem.UpdateWorkplaceJob.
            JobHandle rwhUpdateWorkplaceJob = (JobHandle)_propertyInfoDependency.GetValue(_rwhWorkplaceUpdateSystem);
            base.Dependency = JobChunkExtensions.ScheduleParallel(overrideWorkplacesJob, companyQuery, JobHandle.CombineDependencies(base.Dependency, rwhUpdateWorkplaceJob));

            // Complete() will force the RWH UpdateWorkplaceJob job to immediately execute and then immediately execute this job.
            // This prevents any other game systems from reading WorkProvider.n_MaxWorkers
            // after the RWH UpdateWorkplaceJob updated the value but before this job has a chance to overwrite the value.
            base.Dependency.Complete();
        }
    }
}
