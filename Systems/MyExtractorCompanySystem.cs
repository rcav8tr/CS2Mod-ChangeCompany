using System.Runtime.CompilerServices;
using System.Threading;
using Colossal;
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
//using Colossal.PSI.Common;
using Game;
using Game.Achievements;
using Game.Agents;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Routes;
using Game.Simulation;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace ChangeCompany
{
    /// <summary>
    /// A system to get production amounts from extractors.
    /// This system is used in Resource Locator and Change Company mods.
    /// </summary>
    public partial class MyExtractorCompanySystem : GameSystemBase
    {
        // This system is a copy of decompiled ExtractorCompanySystem as of 1.4.2 except generally:
        //      System's logic is run on demand, not periodically in a system phase.
        //      Avoid creating and being subject to all other dependencies.
        //      Do all companies at once, ignoring update frame.
        //      Do not actually update anything for the company.
        //      Just get company production amounts.
        // Comments indicate changes from the game's version.

        [BurstCompile]
        private struct ExtractorJob : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle m_EntityType;

            [ReadOnly]
            public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

            public BufferTypeHandle<Resources> m_CompanyResourceType;

            [ReadOnly]
            public BufferTypeHandle<Employee> m_EmployeeType;

            [ReadOnly]
            public ComponentTypeHandle<PropertyRenter> m_PropertyType;

            public ComponentTypeHandle<CompanyStatisticData> m_CompanyStatisticType;

            public ComponentTypeHandle<TaxPayer> m_TaxPayerType;

            [ReadOnly]
            public ComponentLookup<IndustrialProcessData> m_IndustrialProcessDatas;

            [ReadOnly]
            public ComponentLookup<StorageLimitData> m_StorageLimitDatas;

            [NativeDisableParallelForRestriction]
            public BufferLookup<Efficiency> m_BuildingEfficiencies;

            [ReadOnly]
            public ComponentLookup<WorkplaceData> m_WorkplaceDatas;

            [ReadOnly]
            public ComponentLookup<SpawnableBuildingData> m_SpawnableDatas;

            [ReadOnly]
            public ComponentLookup<Attached> m_Attached;

            [ReadOnly]
            public BufferLookup<Game.Areas.SubArea> m_SubAreas;

            [ReadOnly]
            public BufferLookup<SubRoute> m_SubRouteBufs;

            //[ReadOnly]
            //public BufferLookup<Game.Prefabs.SubNet> m_SubNetsBufs;

            [ReadOnly]
            public BufferLookup<RouteWaypoint> m_RouteWaypointBufs;

            [ReadOnly]
            public ComponentLookup<Connected> m_Connecteds;

            [ReadOnly]
            public ComponentLookup<Route> m_RouteData;

            [ReadOnly]
            public ComponentLookup<Owner> m_Owners;

            [ReadOnly]
            public ComponentLookup<ExtractorFacilityData> m_ExtractorFacilityDatas;

            [ReadOnly]
            public BufferLookup<InstalledUpgrade> m_InstalledUpgrades;

            [ReadOnly]
            public BufferLookup<CityModifier> m_CityModifiers;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<Extractor> m_ExtractorAreas;

            [ReadOnly]
            public ComponentLookup<Geometry> m_GeometryData;

            [ReadOnly]
            public ComponentLookup<PrefabRef> m_Prefabs;

            [ReadOnly]
            public ComponentLookup<ExtractorAreaData> m_ExtractorAreaDatas;

            [ReadOnly]
            public ComponentLookup<Citizen> m_Citizens;

            [ReadOnly]
            public ComponentLookup<Game.Buildings.ServiceUpgrade> m_ServiceUpgradeData;

            [ReadOnly]
            public ComponentLookup<Edge> m_Edges;

            [ReadOnly]
            public ComponentLookup<PlaceableObjectData> m_PlaceableObjectDatas;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<Game.Net.ResourceConnection> m_ResourceConnectionData;

            [ReadOnly]
            public BufferLookup<Game.Net.SubNet> m_SubNets;

            public EconomyParameterData m_EconomyParameters;

            public ExtractorParameterData m_ExtractorParameters;

            [ReadOnly]
            public NativeArray<int> m_TaxRates;

            [ReadOnly]
            public ResourcePrefabs m_ResourcePrefabs;

            [ReadOnly]
            public ComponentLookup<ResourceData> m_ResourceDatas;

            public RandomSeed m_RandomSeed;

            //public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            public uint m_UpdateFrameIndex;

            //public NativeArray<long> m_ProducedResources;

            //[ReadOnly]
            //public bool m_ShouldCheckOffshoreOilProduce;

            //[ReadOnly]
            //public bool m_ShouldCheckProducedFish;

            //public NativeCounter.Concurrent m_OffshoreOilProduceCounter;

            //public NativeCounter.Concurrent m_ProducedFishCounter;

            //public NativeQueue<ProductionSpecializationSystem.ProducedResource>.ParallelWriter m_ProductionQueue;

            //public NativeQueue<CityProductionStatisticSystem.CompanyProcessingEvent>.ParallelWriter m_ProductionChainQueue;

            [ReadOnly]
            public Entity m_City;

            [ReadOnly]
            public DeliveryTruckSelectData m_DeliveryTruckSelectData;

            // Nested arrays to return production amounts to OnUpdate.
            // The outer array is one for each possible thread.
            // The inner array is one for each resource index.
            // Even though the outer array is read only, entries can still be updated in the inner array.
            [ReadOnly] public NativeArray<NativeArray<int>> m_ProductionAmounts;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Do all companies at once, ignoring update frame.
                //if (chunk.GetSharedComponent(m_UpdateFrameType).m_Index != m_UpdateFrameIndex)
                //{
                //    return;
                //}

                // Get thread index once.
                int threadIndex = Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndex;

                Random random = m_RandomSeed.GetRandom(unfilteredChunkIndex);
                NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
                BufferAccessor<Resources> bufferAccessor = chunk.GetBufferAccessor(ref m_CompanyResourceType);
                BufferAccessor<Employee> bufferAccessor2 = chunk.GetBufferAccessor(ref m_EmployeeType);
                NativeArray<PropertyRenter> nativeArray2 = chunk.GetNativeArray(ref m_PropertyType);
                NativeArray<TaxPayer> nativeArray3 = chunk.GetNativeArray(ref m_TaxPayerType);
                DynamicBuffer<CityModifier> modifiers = m_CityModifiers[m_City];
                NativeArray<CompanyStatisticData> nativeArray4 = chunk.GetNativeArray(ref m_CompanyStatisticType);
                for (int i = 0; i < chunk.Count; i++)
                {
                    Entity entity = nativeArray[i];
                    Entity property = nativeArray2[i].m_Property;
                    DynamicBuffer<Resources> resources = bufferAccessor[i];
                    Entity prefab = m_Prefabs[entity].m_Prefab;
                    Entity prefab2 = m_Prefabs[property].m_Prefab;
                    IndustrialProcessData processData = m_IndustrialProcessDatas[prefab];
                    StorageLimitData storageLimitData = m_StorageLimitDatas[prefab];
                    if (m_Attached.HasComponent(property) && m_InstalledUpgrades.HasBuffer(m_Attached[property].m_Parent) && UpgradeUtils.TryGetCombinedComponent(m_Attached[property].m_Parent, out var data, ref m_Prefabs, ref m_StorageLimitDatas, ref m_InstalledUpgrades))
                    {
                        storageLimitData.m_Limit += data.m_Limit;
                    }
                    _ = m_WorkplaceDatas[prefab];
                    _ = m_SpawnableDatas[prefab2];
                    int totalStorageUsed = EconomyUtils.GetTotalStorageUsed(resources);
                    int num = storageLimitData.m_Limit - totalStorageUsed;
                    if (!m_Attached.HasComponent(property))
                    {
                        continue;
                    }
                    Entity parent = m_Attached[property].m_Parent;
                    float concentration;
                    float size;
                    bool bestConcentration = GetBestConcentration(processData.m_Output.m_Resource, parent, ref m_SubAreas, ref m_InstalledUpgrades, ref m_ExtractorAreas, ref m_GeometryData, ref m_Prefabs, ref m_ExtractorAreaDatas, m_ExtractorParameters, m_ResourcePrefabs, ref m_ResourceDatas, out concentration, out size);
                    float buildingEfficiency = 1f;
                    if (m_BuildingEfficiencies.TryGetBuffer(property, out var bufferData))
                    {
                        //if (processData.m_Output.m_Resource == Resource.Fish)
                        //{
                        //    float value = 100f;
                        //    CityUtils.ApplyModifier(ref value, modifiers, CityModifierType.IndustrialFishHubEfficiency);
                        //    BuildingUtils.SetEfficiencyFactor(bufferData, EfficiencyFactor.CityModifierFishHub, value / 100f);
                        //}
                        //BuildingUtils.SetEfficiencyFactor(bufferData, EfficiencyFactor.NaturalResources, concentration);
                        buildingEfficiency = BuildingUtils.GetEfficiency(bufferData);
                    }
                    if (!bestConcentration)
                    {
                        continue;
                    }
                    int companyProductionPerDay = EconomyUtils.GetCompanyProductionPerDay(buildingEfficiency, isIndustrial: true, bufferAccessor2[i], processData, m_ResourcePrefabs, ref m_ResourceDatas, ref m_Citizens, ref m_EconomyParameters);
                    float y = 1f * (float)companyProductionPerDay / (float)EconomyUtils.kCompanyUpdatesPerDay;
                    y = math.min(num, y);
                    float num2 = 0f;
                    bool requireNaturalResource = m_ResourceDatas[m_ResourcePrefabs[processData.m_Output.m_Resource]].m_RequireNaturalResource;
                    if (m_SubAreas.TryGetBuffer(parent, out var bufferData2))
                    {
                        for (int j = 0; j < bufferData2.Length; j++)
                        {
                            num2 += ProcessArea(bufferData2[j].m_Area, y, concentration, size, requireNaturalResource);
                        }
                    }
                    if (m_InstalledUpgrades.TryGetBuffer(parent, out var bufferData3))
                    {
                        for (int k = 0; k < bufferData3.Length; k++)
                        {
                            if (BuildingUtils.CheckOption(bufferData3[k], BuildingOption.Inactive))
                            {
                                continue;
                            }
                            Entity stopObject = Entity.Null;
                            DynamicBuffer<Game.Areas.SubArea> bufferData4;
                            if (m_Prefabs.TryGetComponent(bufferData3[k].m_Upgrade, out var componentData) && m_ExtractorFacilityDatas.TryGetComponent(componentData.m_Prefab, out var componentData2))
                            {
                                if (((componentData2.m_Requirements & ExtractorRequirementFlags.RouteConnect) != ExtractorRequirementFlags.None && !CheckHaveValidRoute(parent, bufferData3[k].m_Upgrade, out stopObject)) || ((componentData2.m_Requirements & ExtractorRequirementFlags.NetConnect) != ExtractorRequirementFlags.None && (!FindResourceConnectionNode(bufferData3[k].m_Upgrade, out stopObject, out var connected) || !connected)))
                                {
                                    continue;
                                }
                            }
                            else if (m_SubAreas.TryGetBuffer(bufferData3[k].m_Upgrade, out bufferData4))
                            {
                                bool flag = false;
                                for (int l = 0; l < bufferData4.Length; l++)
                                {
                                    if (m_Prefabs.TryGetComponent(bufferData4[l].m_Area, out var componentData3) && m_ExtractorAreaDatas.TryGetComponent(componentData3.m_Prefab, out var componentData4) && componentData4.m_MapFeature == MapFeature.Fish)
                                    {
                                        flag = true;
                                        break;
                                    }
                                }
                                if (flag && !CheckHaveValidRoute(parent, bufferData3[k].m_Upgrade, out var _))
                                {
                                    continue;
                                }
                            }
                            if (m_SubAreas.TryGetBuffer(bufferData3[k].m_Upgrade, out bufferData2))
                            {
                                float num3 = 0f;
                                for (int m = 0; m < bufferData2.Length; m++)
                                {
                                    num3 += ProcessArea(bufferData2[m].m_Area, y, concentration, size, requireNaturalResource);
                                }
                                num2 += num3;
                                //if (m_ResourceConnectionData.TryGetComponent(stopObject, out var componentData5))
                                //{
                                //    componentData5.m_Flow.y |= MathUtils.RoundToIntRandom(ref random, num3) << 1;
                                //    m_ResourceConnectionData[stopObject] = componentData5;
                                //}
                            }
                        }
                    }
                    // Round normally, not randomly, to prevent value from changing while simulation is paused.
                    //int num4 = math.min(num, MathUtils.RoundToIntRandom(ref random, num2));
                    int num4 = math.min(num, (int)System.Math.Round(num2));
                    ResourceStack output = processData.m_Output;
                    //int industrialTaxRate = TaxSystem.GetIndustrialTaxRate(output.m_Resource, m_TaxRates);
                    //if (num4 > 0)
                    //{
                    //    ref TaxPayer reference = ref nativeArray3.ElementAt(i);
                    //    int num5 = EconomyUtils.GetCompanyProfitPerDay(buildingEfficiency, isIndustrial: true, bufferAccessor2[i], processData, m_ResourcePrefabs, ref m_ResourceDatas, ref m_Citizens, ref m_EconomyParameters) / EconomyUtils.kCompanyUpdatesPerDay;
                    //    if (num5 > 0)
                    //    {
                    //        reference.m_AverageTaxRate = (int)math.round(math.lerp(reference.m_AverageTaxRate, industrialTaxRate, (float)num5 / (float)(num5 + reference.m_UntaxedIncome)));
                    //        reference.m_UntaxedIncome += num5;
                    //    }
                    //}
                    //CompanyStatisticData value2 = nativeArray4[i];
                    //value2.m_LastUpdateProduce = num4 * EconomyUtils.kCompanyUpdatesPerDay;
                    //nativeArray4[i] = value2;
                    //AddProducedResource(output.m_Resource, num4);
                    //if (m_ShouldCheckOffshoreOilProduce && output.m_Resource == Resource.Oil && m_PlaceableObjectDatas.HasComponent(prefab2) && (m_PlaceableObjectDatas[prefab2].m_Flags & Game.Objects.PlacementFlags.Shoreline) != Game.Objects.PlacementFlags.None)
                    //{
                    //    m_OffshoreOilProduceCounter.Increment(num4);
                    //}
                    //else if (m_ShouldCheckProducedFish && output.m_Resource == Resource.Fish)
                    //{
                    //    m_ProducedFishCounter.Increment(num4);
                    //}
                    //int num6 = EconomyUtils.AddResources(output.m_Resource, num4, resources);
                    //m_ProductionChainQueue.Enqueue(new CityProductionStatisticSystem.CompanyProcessingEvent
                    //{
                    //    m_Consume1 = processData.m_Input1.m_Resource,
                    //    m_Consume2 = processData.m_Input2.m_Resource,
                    //    m_Produce = processData.m_Output.m_Resource,
                    //    m_Consume2Amount = 0,
                    //    m_Consume1Amount = 0,
                    //    m_ProduceAmount = num4
                    //});
                    //if (num < 100 || num6 > 3 * storageLimitData.m_Limit / 4)
                    //{
                    //    m_DeliveryTruckSelectData.GetCapacityRange(output.m_Resource, out var _, out var max);
                    //    m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, new ResourceExporter
                    //    {
                    //        m_Resource = output.m_Resource,
                    //        m_Amount = math.min(max, storageLimitData.m_Limit / 2)
                    //    });
                    //}

                    // Accumulate the production amounts.
                    ProductionConsumptionUtils.AddValue(in m_ProductionAmounts, threadIndex, output.m_Resource, num4 * EconomyUtils.kCompanyUpdatesPerDay);
                }
            }

            private bool FindResourceConnectionNode(Entity ownerEntity, out Entity node, out bool connected)
            {
                if (m_SubNets.TryGetBuffer(ownerEntity, out var bufferData))
                {
                    for (int i = 0; i < bufferData.Length; i++)
                    {
                        Game.Net.SubNet subNet = bufferData[i];
                        if (!m_ServiceUpgradeData.HasComponent(subNet.m_SubNet) && !m_Edges.HasComponent(subNet.m_SubNet) && m_ResourceConnectionData.TryGetComponent(subNet.m_SubNet, out var componentData))
                        {
                            node = subNet.m_SubNet;
                            connected = (componentData.m_Flow.y & 1) != 0;
                            return true;
                        }
                    }
                }
                node = Entity.Null;
                connected = false;
                return false;
            }

            private bool CheckHaveValidRoute(Entity placeholderEntity, Entity upgradeEntity, out Entity stopObject)
            {
                bool flag = false;
                bool flag2 = false;
                stopObject = Entity.Null;
                if (m_SubRouteBufs.TryGetBuffer(placeholderEntity, out var bufferData))
                {
                    for (int i = 0; i < bufferData.Length; i++)
                    {
                        flag = false;
                        flag2 = false;
                        if (m_RouteData.TryGetComponent(bufferData[i].m_Route, out var componentData) && m_RouteWaypointBufs.TryGetBuffer(bufferData[i].m_Route, out var bufferData2) && !RouteUtils.CheckOption(componentData, RouteOption.Inactive))
                        {
                            for (int j = 0; j < bufferData2.Length; j++)
                            {
                                Entity waypoint = bufferData2[j].m_Waypoint;
                                if (m_Connecteds.HasComponent(waypoint) && m_Owners.HasComponent(m_Connecteds[waypoint].m_Connected))
                                {
                                    if (m_Owners[m_Connecteds[waypoint].m_Connected].m_Owner == upgradeEntity)
                                    {
                                        flag = true;
                                    }
                                    else if (m_Edges.HasComponent(m_Owners[m_Connecteds[waypoint].m_Connected].m_Owner))
                                    {
                                        stopObject = m_Connecteds[waypoint].m_Connected;
                                        flag2 = true;
                                    }
                                }
                            }
                        }
                        if (flag && flag2)
                        {
                            break;
                        }
                    }
                }
                return flag && flag2;
            }

            //private unsafe void AddProducedResource(Resource resource, int amount)
            //{
            //    if (resource != Resource.NoResource)
            //    {
            //        long* unsafePtr = (long*)m_ProducedResources.GetUnsafePtr();
            //        unsafePtr += EconomyUtils.GetResourceIndex(resource);
            //        Interlocked.Add(ref *unsafePtr, amount);
            //        m_ProductionQueue.Enqueue(new ProductionSpecializationSystem.ProducedResource
            //        {
            //            m_Resource = resource,
            //            m_Amount = amount
            //        });
            //    }
            //}

            private float ProcessArea(Entity area, float totalProduced, float totalConcentration, float totalSize, bool requireNaturalResources)
            {
                if (!m_ExtractorAreas.TryGetComponent(area, out var componentData) || !m_GeometryData.TryGetComponent(area, out var componentData2))
                {
                    return 0f;
                }
                float num = totalProduced * componentData2.m_SurfaceArea / math.max(1f, totalConcentration * totalSize);
                float num2 = 1f;
                Entity prefab = m_Prefabs[area].m_Prefab;
                ExtractorAreaData componentData3;
                bool flag = m_ExtractorAreaDatas.TryGetComponent(prefab, out componentData3);
                if (flag)
                {
                    num2 = componentData3.m_WorkAmountFactor;
                }
                if (requireNaturalResources && flag && componentData3.m_RequireNaturalResource)
                {
                    float upperBound = componentData.m_ResourceAmount - componentData.m_ExtractedAmount;
                    float effectiveConcentration = GetEffectiveConcentration(m_ExtractorParameters, componentData3.m_MapFeature, componentData.m_MaxConcentration);
                    effectiveConcentration = math.min(1f, effectiveConcentration);
                    num = math.clamp(num * effectiveConcentration, 0f, upperBound);
                }
                //float num3 = GetExtractionMultiplier(area) * num;
                //componentData.m_ExtractedAmount += num3;
                //componentData.m_TotalExtracted += num3;
                //componentData.m_WorkAmount += num * num2;
                //m_ExtractorAreas[area] = componentData;
                return num;
            }

            //private float GetExtractionMultiplier(Entity subArea)
            //{
            //    float result = 1f;
            //    if (m_Prefabs.TryGetComponent(subArea, out var componentData) && m_ExtractorAreaDatas.TryGetComponent(componentData, out var componentData2))
            //    {
            //        result = componentData2.m_MapFeature switch
            //        {
            //            MapFeature.FertileLand => m_ExtractorParameters.m_FertilityConsumption, 
            //            MapFeature.Fish => m_ExtractorParameters.m_FishConsumption, 
            //            MapFeature.Forest => m_ExtractorParameters.m_ForestConsumption, 
            //            _ => 1f, 
            //        };
            //    }
            //    return result;
            //}

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
            }
        }

        private struct TypeHandle
        {
            [ReadOnly]
            public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

            public BufferTypeHandle<Resources> __Game_Economy_Resources_RW_BufferTypeHandle;

            public ComponentLookup<Extractor> __Game_Areas_Extractor_RW_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<Geometry> __Game_Areas_Geometry_RO_ComponentLookup;

            public ComponentTypeHandle<CompanyStatisticData> __Game_Companies_CompanyStatisticData_RW_ComponentTypeHandle;

            [ReadOnly]
            public BufferTypeHandle<Employee> __Game_Companies_Employee_RO_BufferTypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<PropertyRenter> __Game_Buildings_PropertyRenter_RO_ComponentTypeHandle;

            public SharedComponentTypeHandle<UpdateFrame> __Game_Simulation_UpdateFrame_SharedComponentTypeHandle;

            public ComponentTypeHandle<TaxPayer> __Game_Agents_TaxPayer_RW_ComponentTypeHandle;

            [ReadOnly]
            public ComponentLookup<IndustrialProcessData> __Game_Prefabs_IndustrialProcessData_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<StorageLimitData> __Game_Companies_StorageLimitData_RO_ComponentLookup;

            public BufferLookup<Efficiency> __Game_Buildings_Efficiency_RW_BufferLookup;

            [ReadOnly]
            public ComponentLookup<WorkplaceData> __Game_Prefabs_WorkplaceData_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<SpawnableBuildingData> __Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<Attached> __Game_Objects_Attached_RO_ComponentLookup;

            [ReadOnly]
            public BufferLookup<Game.Areas.SubArea> __Game_Areas_SubArea_RO_BufferLookup;

            [ReadOnly]
            public BufferLookup<InstalledUpgrade> __Game_Buildings_InstalledUpgrade_RO_BufferLookup;

            [ReadOnly]
            public BufferLookup<CityModifier> __Game_City_CityModifier_RO_BufferLookup;

            [ReadOnly]
            public ComponentLookup<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<ExtractorAreaData> __Game_Prefabs_ExtractorAreaData_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<Citizen> __Game_Citizens_Citizen_RO_ComponentLookup;

            [ReadOnly]
            public BufferLookup<SubRoute> __Game_Routes_SubRoute_RO_BufferLookup;

            [ReadOnly]
            public BufferLookup<RouteWaypoint> __Game_Routes_RouteWaypoint_RO_BufferLookup;

            [ReadOnly]
            public ComponentLookup<Connected> __Game_Routes_Connected_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<Route> __Game_Routes_Route_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<Owner> __Game_Common_Owner_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<ExtractorFacilityData> __Game_Prefabs_ExtractorFacilityData_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<Game.Buildings.ServiceUpgrade> __Game_Buildings_ServiceUpgrade_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<Edge> __Game_Net_Edge_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<PlaceableObjectData> __Game_Prefabs_PlaceableObjectData_RO_ComponentLookup;

            public ComponentLookup<Game.Net.ResourceConnection> __Game_Net_ResourceConnection_RW_ComponentLookup;

            [ReadOnly]
            public BufferLookup<Game.Net.SubNet> __Game_Net_SubNet_RO_BufferLookup;

            [ReadOnly]
            public ComponentLookup<ResourceData> __Game_Prefabs_ResourceData_RO_ComponentLookup;

            [MethodImpl((MethodImplOptions)0x100 /*AggressiveInlining*/)]
            public void __AssignHandles(ref SystemState state)
            {
                __Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
                __Game_Economy_Resources_RW_BufferTypeHandle = state.GetBufferTypeHandle<Resources>();
                __Game_Areas_Extractor_RW_ComponentLookup = state.GetComponentLookup<Extractor>();
                __Game_Areas_Geometry_RO_ComponentLookup = state.GetComponentLookup<Geometry>(isReadOnly: true);
                __Game_Companies_CompanyStatisticData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<CompanyStatisticData>();
                __Game_Companies_Employee_RO_BufferTypeHandle = state.GetBufferTypeHandle<Employee>(isReadOnly: true);
                __Game_Buildings_PropertyRenter_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PropertyRenter>(isReadOnly: true);
                __Game_Simulation_UpdateFrame_SharedComponentTypeHandle = state.GetSharedComponentTypeHandle<UpdateFrame>();
                __Game_Agents_TaxPayer_RW_ComponentTypeHandle = state.GetComponentTypeHandle<TaxPayer>();
                __Game_Prefabs_IndustrialProcessData_RO_ComponentLookup = state.GetComponentLookup<IndustrialProcessData>(isReadOnly: true);
                __Game_Companies_StorageLimitData_RO_ComponentLookup = state.GetComponentLookup<StorageLimitData>(isReadOnly: true);
                __Game_Buildings_Efficiency_RW_BufferLookup = state.GetBufferLookup<Efficiency>();
                __Game_Prefabs_WorkplaceData_RO_ComponentLookup = state.GetComponentLookup<WorkplaceData>(isReadOnly: true);
                __Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup = state.GetComponentLookup<SpawnableBuildingData>(isReadOnly: true);
                __Game_Objects_Attached_RO_ComponentLookup = state.GetComponentLookup<Attached>(isReadOnly: true);
                __Game_Areas_SubArea_RO_BufferLookup = state.GetBufferLookup<Game.Areas.SubArea>(isReadOnly: true);
                __Game_Buildings_InstalledUpgrade_RO_BufferLookup = state.GetBufferLookup<InstalledUpgrade>(isReadOnly: true);
                __Game_City_CityModifier_RO_BufferLookup = state.GetBufferLookup<CityModifier>(isReadOnly: true);
                __Game_Prefabs_PrefabRef_RO_ComponentLookup = state.GetComponentLookup<PrefabRef>(isReadOnly: true);
                __Game_Prefabs_ExtractorAreaData_RO_ComponentLookup = state.GetComponentLookup<ExtractorAreaData>(isReadOnly: true);
                __Game_Citizens_Citizen_RO_ComponentLookup = state.GetComponentLookup<Citizen>(isReadOnly: true);
                __Game_Routes_SubRoute_RO_BufferLookup = state.GetBufferLookup<SubRoute>(isReadOnly: true);
                __Game_Routes_RouteWaypoint_RO_BufferLookup = state.GetBufferLookup<RouteWaypoint>(isReadOnly: true);
                __Game_Routes_Connected_RO_ComponentLookup = state.GetComponentLookup<Connected>(isReadOnly: true);
                __Game_Routes_Route_RO_ComponentLookup = state.GetComponentLookup<Route>(isReadOnly: true);
                __Game_Common_Owner_RO_ComponentLookup = state.GetComponentLookup<Owner>(isReadOnly: true);
                __Game_Prefabs_ExtractorFacilityData_RO_ComponentLookup = state.GetComponentLookup<ExtractorFacilityData>(isReadOnly: true);
                __Game_Buildings_ServiceUpgrade_RO_ComponentLookup = state.GetComponentLookup<Game.Buildings.ServiceUpgrade>(isReadOnly: true);
                __Game_Net_Edge_RO_ComponentLookup = state.GetComponentLookup<Edge>(isReadOnly: true);
                __Game_Prefabs_PlaceableObjectData_RO_ComponentLookup = state.GetComponentLookup<PlaceableObjectData>(isReadOnly: true);
                __Game_Net_ResourceConnection_RW_ComponentLookup = state.GetComponentLookup<Game.Net.ResourceConnection>();
                __Game_Net_SubNet_RO_BufferLookup = state.GetBufferLookup<Game.Net.SubNet>(isReadOnly: true);
                __Game_Prefabs_ResourceData_RO_ComponentLookup = state.GetComponentLookup<ResourceData>(isReadOnly: true);
            }
        }

        private SimulationSystem m_SimulationSystem;

        private EndFrameBarrier m_EndFrameBarrier;

        private TaxSystem m_TaxSystem;

        private ResourceSystem m_ResourceSystem;

        private VehicleCapacitySystem m_VehicleCapacitySystem;

        private ProcessingCompanySystem m_ProcessingCompanySystem;

        private ProductionSpecializationSystem m_ProductionSpecializationSystem;

        private AchievementTriggerSystem m_AchievementTriggerSystem;

        private CitySystem m_CitySystem;

        private CityProductionStatisticSystem m_CityProductionStatisticSystem;

        private EntityQuery m_CompanyGroup;

        private TypeHandle __TypeHandle;

        private EntityQuery __query_1012523228_0;

        private EntityQuery __query_1012523228_1;

        // Nested arrays to hold production amounts.
        // The outer array is one for each possible thread.
        // The inner array is one for each resource index.
        private NativeArray<NativeArray<int>> _productionAmounts;

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 262144 / (EconomyUtils.kCompanyUpdatesPerDay * 16);
        }

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
            m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_TaxSystem = base.World.GetOrCreateSystemManaged<TaxSystem>();
            m_ResourceSystem = base.World.GetOrCreateSystemManaged<ResourceSystem>();
            m_VehicleCapacitySystem = base.World.GetOrCreateSystemManaged<VehicleCapacitySystem>();
            m_ProcessingCompanySystem = base.World.GetOrCreateSystemManaged<ProcessingCompanySystem>();
            m_ProductionSpecializationSystem = base.World.GetOrCreateSystemManaged<ProductionSpecializationSystem>();
            m_AchievementTriggerSystem = base.World.GetOrCreateSystemManaged<AchievementTriggerSystem>();
            m_CitySystem = base.World.GetExistingSystemManaged<CitySystem>();
            m_CityProductionStatisticSystem = base.World.GetOrCreateSystemManaged<CityProductionStatisticSystem>();
            m_CompanyGroup = GetEntityQuery(ComponentType.ReadOnly<CompanyStatisticData>(), ComponentType.ReadOnly<Game.Companies.ExtractorCompany>(), ComponentType.ReadOnly<PropertyRenter>(), ComponentType.ReadWrite<Resources>(), ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadOnly<WorkProvider>(), ComponentType.ReadOnly<UpdateFrame>(), ComponentType.ReadWrite<CompanyData>(), ComponentType.ReadWrite<Employee>());
            RequireForUpdate(m_CompanyGroup);
            RequireForUpdate<EconomyParameterData>();
            RequireForUpdate<ExtractorParameterData>();

            // Create arrays for production amounts.
            _productionAmounts = ProductionConsumptionUtils.CreateArrays();
        }

        /// <summary>
        /// Dispose native collections.
        /// </summary>
        [Preserve]
        protected override void OnDestroy()
        {
            ProductionConsumptionUtils.DisposeArrays(in _productionAmounts);
            base.OnDestroy();
        }

        public static MapFeature GetRequiredMapFeature(Resource output, Entity lotPrefab, ResourcePrefabs resourcePrefabs, ComponentLookup<ResourceData> resourceDatas, ComponentLookup<ExtractorAreaData> extractorAreaDatas)
        {
            if (resourceDatas.TryGetComponent(resourcePrefabs[output], out var componentData) && componentData.m_RequireNaturalResource && extractorAreaDatas.TryGetComponent(lotPrefab, out var componentData2) && componentData2.m_RequireNaturalResource)
            {
                return componentData2.m_MapFeature;
            }
            return MapFeature.None;
        }

        public static bool GetBestConcentration(Resource resource, Entity mainBuilding, ref BufferLookup<Game.Areas.SubArea> subAreas, ref BufferLookup<InstalledUpgrade> installedUpgrades, ref ComponentLookup<Extractor> extractors, ref ComponentLookup<Geometry> geometries, ref ComponentLookup<PrefabRef> prefabs, ref ComponentLookup<ExtractorAreaData> extractorDatas, ExtractorParameterData extractorParameters, ResourcePrefabs resourcePrefabs, ref ComponentLookup<ResourceData> resourceDatas, out float concentration, out float size)
        {
            concentration = 0f;
            size = 0f;
            ResourceData componentData;
            bool requireNaturalResource = resourceDatas.TryGetComponent(resourcePrefabs[resource], out componentData) && componentData.m_RequireNaturalResource;
            if (subAreas.TryGetBuffer(mainBuilding, out var bufferData))
            {
                GetBestConcentration(bufferData, ref extractors, ref geometries, ref prefabs, ref extractorDatas, extractorParameters, requireNaturalResource, ref concentration, ref size);
            }
            if (installedUpgrades.TryGetBuffer(mainBuilding, out var bufferData2))
            {
                for (int i = 0; i < bufferData2.Length; i++)
                {
                    if (subAreas.TryGetBuffer(bufferData2[i].m_Upgrade, out bufferData))
                    {
                        GetBestConcentration(bufferData, ref extractors, ref geometries, ref prefabs, ref extractorDatas, extractorParameters, requireNaturalResource, ref concentration, ref size);
                    }
                }
            }
            concentration = math.min(1f, concentration / math.max(1f, size));
            return concentration > 0f;
        }

        private static void GetBestConcentration(DynamicBuffer<Game.Areas.SubArea> subAreas, ref ComponentLookup<Extractor> extractors, ref ComponentLookup<Geometry> geometries, ref ComponentLookup<PrefabRef> prefabs, ref ComponentLookup<ExtractorAreaData> extractorDatas, ExtractorParameterData extractorParameters, bool requireNaturalResource, ref float concentration, ref float size)
        {
            for (int i = 0; i < subAreas.Length; i++)
            {
                Entity area = subAreas[i].m_Area;
                if (extractors.TryGetComponent(area, out var componentData) && geometries.TryGetComponent(area, out var componentData2) && prefabs.TryGetComponent(area, out var componentData3) && extractorDatas.TryGetComponent(componentData3.m_Prefab, out var componentData4))
                {
                    if (requireNaturalResource && componentData4.m_RequireNaturalResource)
                    {
                        float effectiveConcentration = GetEffectiveConcentration(extractorParameters, componentData4.m_MapFeature, componentData.m_MaxConcentration);
                        effectiveConcentration = math.min(1f, effectiveConcentration);
                        concentration += effectiveConcentration * componentData2.m_SurfaceArea;
                        size += componentData2.m_SurfaceArea;
                    }
                    else
                    {
                        concentration += componentData2.m_SurfaceArea;
                        size += componentData2.m_SurfaceArea;
                    }
                }
            }
        }

        public static float GetEffectiveConcentration(ExtractorParameterData extractorParameters, MapFeature feature, float concentration)
        {
            return math.min(1f, concentration / feature switch
            {
                MapFeature.Oil => extractorParameters.m_FullOil, 
                MapFeature.FertileLand => extractorParameters.m_FullFertility, 
                MapFeature.Fish => extractorParameters.m_FullFish, 
                MapFeature.Ore => extractorParameters.m_FullOre, 
                _ => 1f, 
            });
        }

        [Preserve]
        protected override void OnUpdate()
        {
            // System is never activated, so OnUpdate never executes.
            // But implementation is required.
            // Logic moved to GetAmounts.
        }

        /// <summary>
        /// Get extractor company production amounts.
        /// </summary>
        public void GetAmounts(out int[] productionAmounts)
        {
            // Logic copied from OnUpdate.

            // Iinitialize production amounts.
            ProductionConsumptionUtils.InitializeArrays(in _productionAmounts);

            uint updateFrame = SimulationUtils.GetUpdateFrame(m_SimulationSystem.frameIndex, EconomyUtils.kCompanyUpdatesPerDay, 16);
            //IAchievement achievement;
            //IAchievement achievement2;
            //JobHandle dependencies;
            //JobHandle deps;
            //JobHandle deps2;
            ExtractorJob jobData = new ExtractorJob
            {
                m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref __TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef),
                m_CompanyResourceType = InternalCompilerInterface.GetBufferTypeHandle(ref __TypeHandle.__Game_Economy_Resources_RW_BufferTypeHandle, ref base.CheckedStateRef),
                m_ExtractorAreas = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Areas_Extractor_RW_ComponentLookup, ref base.CheckedStateRef),
                m_GeometryData = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Areas_Geometry_RO_ComponentLookup, ref base.CheckedStateRef),
                m_CompanyStatisticType = InternalCompilerInterface.GetComponentTypeHandle(ref __TypeHandle.__Game_Companies_CompanyStatisticData_RW_ComponentTypeHandle, ref base.CheckedStateRef),
                m_EmployeeType = InternalCompilerInterface.GetBufferTypeHandle(ref __TypeHandle.__Game_Companies_Employee_RO_BufferTypeHandle, ref base.CheckedStateRef),
                m_PropertyType = InternalCompilerInterface.GetComponentTypeHandle(ref __TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentTypeHandle, ref base.CheckedStateRef),
                m_UpdateFrameType = InternalCompilerInterface.GetSharedComponentTypeHandle(ref __TypeHandle.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle, ref base.CheckedStateRef),
                m_TaxPayerType = InternalCompilerInterface.GetComponentTypeHandle(ref __TypeHandle.__Game_Agents_TaxPayer_RW_ComponentTypeHandle, ref base.CheckedStateRef),
                m_IndustrialProcessDatas = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Prefabs_IndustrialProcessData_RO_ComponentLookup, ref base.CheckedStateRef),
                m_StorageLimitDatas = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Companies_StorageLimitData_RO_ComponentLookup, ref base.CheckedStateRef),
                m_BuildingEfficiencies = InternalCompilerInterface.GetBufferLookup(ref __TypeHandle.__Game_Buildings_Efficiency_RW_BufferLookup, ref base.CheckedStateRef),
                m_WorkplaceDatas = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Prefabs_WorkplaceData_RO_ComponentLookup, ref base.CheckedStateRef),
                m_SpawnableDatas = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup, ref base.CheckedStateRef),
                m_Attached = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Objects_Attached_RO_ComponentLookup, ref base.CheckedStateRef),
                m_SubAreas = InternalCompilerInterface.GetBufferLookup(ref __TypeHandle.__Game_Areas_SubArea_RO_BufferLookup, ref base.CheckedStateRef),
                m_InstalledUpgrades = InternalCompilerInterface.GetBufferLookup(ref __TypeHandle.__Game_Buildings_InstalledUpgrade_RO_BufferLookup, ref base.CheckedStateRef),
                m_CityModifiers = InternalCompilerInterface.GetBufferLookup(ref __TypeHandle.__Game_City_CityModifier_RO_BufferLookup, ref base.CheckedStateRef),
                m_Prefabs = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef),
                m_ExtractorAreaDatas = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Prefabs_ExtractorAreaData_RO_ComponentLookup, ref base.CheckedStateRef),
                m_Citizens = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Citizens_Citizen_RO_ComponentLookup, ref base.CheckedStateRef),
                m_SubRouteBufs = InternalCompilerInterface.GetBufferLookup(ref __TypeHandle.__Game_Routes_SubRoute_RO_BufferLookup, ref base.CheckedStateRef),
                m_RouteWaypointBufs = InternalCompilerInterface.GetBufferLookup(ref __TypeHandle.__Game_Routes_RouteWaypoint_RO_BufferLookup, ref base.CheckedStateRef),
                m_Connecteds = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Routes_Connected_RO_ComponentLookup, ref base.CheckedStateRef),
                m_RouteData = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Routes_Route_RO_ComponentLookup, ref base.CheckedStateRef),
                m_Owners = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Common_Owner_RO_ComponentLookup, ref base.CheckedStateRef),
                m_ExtractorFacilityDatas = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Prefabs_ExtractorFacilityData_RO_ComponentLookup, ref base.CheckedStateRef),
                m_ServiceUpgradeData = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Buildings_ServiceUpgrade_RO_ComponentLookup, ref base.CheckedStateRef),
                m_Edges = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Net_Edge_RO_ComponentLookup, ref base.CheckedStateRef),
                m_PlaceableObjectDatas = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Prefabs_PlaceableObjectData_RO_ComponentLookup, ref base.CheckedStateRef),
                m_ResourceConnectionData = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Net_ResourceConnection_RW_ComponentLookup, ref base.CheckedStateRef),
                m_SubNets = InternalCompilerInterface.GetBufferLookup(ref __TypeHandle.__Game_Net_SubNet_RO_BufferLookup, ref base.CheckedStateRef),
                m_ResourcePrefabs = m_ResourceSystem.GetPrefabs(),
                m_ResourceDatas = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup, ref base.CheckedStateRef),
                m_TaxRates = m_TaxSystem.GetTaxRates(),
                m_EconomyParameters = __query_1012523228_0.GetSingleton<EconomyParameterData>(),
                m_ExtractorParameters = __query_1012523228_1.GetSingleton<ExtractorParameterData>(),
                //m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
                m_City = m_CitySystem.City,
                m_RandomSeed = RandomSeed.Next(),
                m_UpdateFrameIndex = updateFrame,
                //m_ShouldCheckOffshoreOilProduce = (PlatformManager.instance.achievementsEnabled && PlatformManager.instance.GetAchievement(Game.Achievements.Achievements.ADifferentPlatformer, out achievement) && !achievement.achieved),
                //m_ShouldCheckProducedFish = (PlatformManager.instance.achievementsEnabled && PlatformManager.instance.GetAchievement(Game.Achievements.Achievements.HowMuchIsTheFish, out achievement2) && !achievement2.achieved),
                //m_OffshoreOilProduceCounter = m_AchievementTriggerSystem.m_OffshoreOilProduceCounter.ToConcurrent(),
                //m_ProducedFishCounter = m_AchievementTriggerSystem.m_ProducedFishCounter.ToConcurrent(),
                //m_ProducedResources = m_ProcessingCompanySystem.GetProducedResourcesArray(out dependencies),
                //m_ProductionQueue = m_ProductionSpecializationSystem.GetQueue(out deps).AsParallelWriter(),
                //m_ProductionChainQueue = m_CityProductionStatisticSystem.GetConsumptionQueue(out deps2).AsParallelWriter(),
                m_DeliveryTruckSelectData = m_VehicleCapacitySystem.GetDeliveryTruckSelectData(),

                // Pass production amounts.
                m_ProductionAmounts = _productionAmounts,
            };
            //base.Dependency = JobChunkExtensions.ScheduleParallel(jobData, m_CompanyGroup, JobUtils.CombineDependencies(dependencies, deps, base.Dependency, deps2));
            base.Dependency = JobChunkExtensions.ScheduleParallel(jobData, m_CompanyGroup, base.Dependency);
            //m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
            //m_TaxSystem.AddReader(base.Dependency);
            //m_CityProductionStatisticSystem.AddChainWriter(base.Dependency);

            // Wait for the job to complete before accessing the amounts.
            base.Dependency.Complete();

            // Consolidate and return production amounts from the job.
            ProductionConsumptionUtils.ConsolidateValues(in _productionAmounts, out productionAmounts);
        }

        [MethodImpl((MethodImplOptions)0x100 /*AggressiveInlining*/)]
        private void __AssignQueries(ref SystemState state)
        {
            EntityQueryBuilder entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
            EntityQueryBuilder entityQueryBuilder2 = entityQueryBuilder.WithAll<EconomyParameterData>();
            entityQueryBuilder2 = entityQueryBuilder2.WithOptions(EntityQueryOptions.IncludeSystems);
            __query_1012523228_0 = entityQueryBuilder2.Build(ref state);
            entityQueryBuilder.Reset();
            entityQueryBuilder2 = entityQueryBuilder.WithAll<ExtractorParameterData>();
            entityQueryBuilder2 = entityQueryBuilder2.WithOptions(EntityQueryOptions.IncludeSystems);
            __query_1012523228_1 = entityQueryBuilder2.Build(ref state);
            entityQueryBuilder.Reset();
            entityQueryBuilder.Dispose();
        }

        protected override void OnCreateForCompiler()
        {
            base.OnCreateForCompiler();
            __AssignQueries(ref base.CheckedStateRef);
            __TypeHandle.__AssignHandles(ref base.CheckedStateRef);
        }

        [Preserve]
        public MyExtractorCompanySystem()
        {
        }
    }
}
