using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Prefabs;
using Game.Serialization;
using Game.Simulation;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;

namespace ChangeCompany
{
    /// <summary>
    /// A system to get production and consumption amounts from processing companies.
    /// This system is used in Resource Locator and Change Company mods.
    /// </summary>
    public partial class MyProcessingCompanySystem : GameSystemBase //, IDefaultSerializable, ISerializable, IPostDeserialize
    {
        // This system is a copy of decompiled ProcessingCompanySystem as of 1.4.2 except generally:
        //      System's logic is run on demand, not periodically in a system phase.
        //      Avoid creating and being subject to all other dependencies.
        //      Do all companies at once, ignoring update frame.
        //      Do not actually update anything for the company.
        //      Just get company production and consumption amounts.
        // Comments indicate changes from the game's version.

        // Define company production.
        private struct CompanyProduction
        {
            public Entity Company;
            public int Production;
        }

        [BurstCompile]
        private struct UpdateProcessingJob : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle m_EntityType;

            [ReadOnly]
            public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

            [ReadOnly]
            public ComponentTypeHandle<PrefabRef> m_PrefabType;

            [ReadOnly]
            public ComponentTypeHandle<PropertyRenter> m_PropertyType;

            [ReadOnly]
            public BufferTypeHandle<Employee> m_EmployeeType;

            [ReadOnly]
            public ComponentTypeHandle<ServiceAvailable> m_ServiceAvailableType;

            public BufferTypeHandle<Game.Economy.Resources> m_ResourceType;

            public ComponentTypeHandle<CompanyData> m_CompanyDataType;

            public ComponentTypeHandle<TaxPayer> m_TaxPayerType;

            [ReadOnly]
            public ComponentLookup<IndustrialProcessData> m_IndustrialProcessDatas;

            [ReadOnly]
            public ComponentLookup<ResourceData> m_ResourceDatas;

            [ReadOnly]
            public ComponentLookup<StorageLimitData> m_Limits;

            [ReadOnly]
            public ComponentLookup<Building> m_Buildings;

            [ReadOnly]
            public ComponentLookup<Citizen> m_Citizens;

            [ReadOnly]
            public ComponentLookup<OfficeProperty> m_OfficeProperties;

            [ReadOnly]
            public BufferLookup<SpecializationBonus> m_Specializations;

            [ReadOnly]
            public BufferLookup<CityModifier> m_CityModifiers;

            [ReadOnly]
            public ComponentLookup<ServiceAvailable> m_ServiceAvailables;

            [ReadOnly]
            public ComponentLookup<ServiceCompanyData> m_ServiceCompanyDatas;

            [NativeDisableParallelForRestriction]
            public BufferLookup<Efficiency> m_BuildingEfficiencies;

            [ReadOnly]
            public NativeArray<int> m_TaxRates;

            [ReadOnly]
            public ResourcePrefabs m_ResourcePrefabs;

            [ReadOnly]
            public DeliveryTruckSelectData m_DeliveryTruckSelectData;

            //public NativeArray<long> m_ProducedResources;

            //public NativeQueue<ProductionSpecializationSystem.ProducedResource>.ParallelWriter m_ProductionQueue;

            //public NativeQueue<CityProductionStatisticSystem.CompanyProcessingEvent>.ParallelWriter m_CountQueue;

            //[NativeDisableParallelForRestriction]
            //public NativeReference<int> m_OfficeResourceConsumptionAmount;

            //public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            public EconomyParameterData m_EconomyParameters;

            public RandomSeed m_RandomSeed;

            public Entity m_City;

            public uint m_UpdateFrameIndex;

            // Nested arrays to return production and consumption amounts to OnUpdate.
            // The outer array is one for each possible thread.
            // The inner array is one for each resource index.
            // Even though the outer array is read only, entries can still be updated in the inner array.
            [ReadOnly] public NativeArray<NativeArray<int>> m_ProductionAmounts;
            [ReadOnly] public NativeArray<NativeArray<int>> m_ConsumptionAmounts;

            // Nested arrays of lists to return company production to OnUpdate.
            // The outer array is one for each possible thread.
            // The inner array is one for each company processed on that thread.
            // Even though the outer array is read only, entries can still be updated in the inner list.
            [ReadOnly] public NativeArray<NativeList<CompanyProduction>> m_CompanyProductions;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Do all companies at once, ignoring update frame.
                //if (chunk.GetSharedComponent(m_UpdateFrameType).m_Index != m_UpdateFrameIndex)
                //{
                //    return;
                //}

                // Get thread index once.
                int threadIndex = Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndex;

                Unity.Mathematics.Random random = m_RandomSeed.GetRandom(unfilteredChunkIndex);
                DynamicBuffer<CityModifier> cityModifiers = m_CityModifiers[m_City];
                DynamicBuffer<SpecializationBonus> specializations = m_Specializations[m_City];
                NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
                NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref m_PrefabType);
                NativeArray<PropertyRenter> nativeArray3 = chunk.GetNativeArray(ref m_PropertyType);
                BufferAccessor<Game.Economy.Resources> bufferAccessor = chunk.GetBufferAccessor(ref m_ResourceType);
                BufferAccessor<Employee> bufferAccessor2 = chunk.GetBufferAccessor(ref m_EmployeeType);
                NativeArray<CompanyData> nativeArray4 = chunk.GetNativeArray(ref m_CompanyDataType);
                NativeArray<TaxPayer> nativeArray5 = chunk.GetNativeArray(ref m_TaxPayerType);
                bool flag = chunk.Has(ref m_ServiceAvailableType);
                for (int i = 0; i < chunk.Count; i++)
                {
                    Entity entity = nativeArray[i];
                    Entity prefab = nativeArray2[i].m_Prefab;
                    Entity property = nativeArray3[i].m_Property;
                    ServiceAvailable serviceAvailable = default(ServiceAvailable);
                    ServiceCompanyData serviceCompanyData = default(ServiceCompanyData);
                    if (m_ServiceAvailables.HasComponent(entity))
                    {
                        serviceAvailable = m_ServiceAvailables[entity];
                    }
                    if (m_ServiceCompanyDatas.HasComponent(prefab))
                    {
                        serviceCompanyData = m_ServiceCompanyDatas[prefab];
                    }
                    ref CompanyData reference = ref nativeArray4.ElementAt(i);
                    if (!m_Buildings.HasComponent(property))
                    {
                        continue;
                    }
                    bool flag2 = m_OfficeProperties.HasComponent(property);
                    DynamicBuffer<Game.Economy.Resources> resources = bufferAccessor[i];
                    IndustrialProcessData industrialProcessData = m_IndustrialProcessDatas[prefab];
                    StorageLimitData storageLimitData = m_Limits[prefab];
                    float buildingEfficiency = 1f;
                    bool flag3 = false;
                    if (m_BuildingEfficiencies.TryGetBuffer(property, out var bufferData))
                    {
                        //UpdateEfficiencyFactors(industrialProcessData, flag, bufferData, cityModifiers, specializations);
                        buildingEfficiency = BuildingUtils.GetEfficiencyExcludingFactor(bufferData, EfficiencyFactor.LackResources);
                        flag3 = true;
                    }
                    int companyProductionPerDay = EconomyUtils.GetCompanyProductionPerDay(buildingEfficiency, !flag, bufferAccessor2[i], industrialProcessData, m_ResourcePrefabs, ref m_ResourceDatas, ref m_Citizens, ref m_EconomyParameters, serviceAvailable, serviceCompanyData);
                    // Round normally, not randomly, to prevent value from changing while simulation is paused.
                    //int num = MathUtils.RoundToIntRandom(ref random, 1f * (float)companyProductionPerDay / (float)EconomyUtils.kCompanyUpdatesPerDay);
                    int num = (int)System.Math.Round((float)companyProductionPerDay / EconomyUtils.kCompanyUpdatesPerDay);
                    ResourceStack input = industrialProcessData.m_Input1;
                    ResourceStack input2 = industrialProcessData.m_Input2;
                    ResourceStack output = industrialProcessData.m_Output;
                    if (input.m_Resource == output.m_Resource && input2.m_Resource == Resource.NoResource && input.m_Amount == output.m_Amount)
                    {
                        continue;
                    }
                    float num2 = 1f;
                    float num3 = 1f;
                    int num4 = 0;
                    int num5 = 0;
                    int num6 = 0;
                    int num7 = 0;
                    if (input.m_Resource != Resource.NoResource && (float)input.m_Amount > 0f)
                    {
                        num6 = EconomyUtils.GetResources(input.m_Resource, resources);
                        num2 = (float)input.m_Amount * 1f / (float)output.m_Amount;
                        num = math.min(num, (int)((float)num6 / num2));
                    }
                    if (input2.m_Resource != Resource.NoResource && (float)input2.m_Amount > 0f)
                    {
                        num7 = EconomyUtils.GetResources(input2.m_Resource, resources);
                        num3 = (float)input2.m_Amount * 1f / (float)output.m_Amount;
                        num = math.min(num, (int)((float)num7 / num3));
                    }
                    if (flag3)
                    {
                        //BuildingUtils.SetEfficiencyFactor(bufferData, EfficiencyFactor.LackResources, (num != 0) ? 1 : 0);
                    }
                    int resources2;
                    if ((float)num > 0f)
                    {
                        int num8 = 0;
                        if (flag && EconomyUtils.GetResources(output.m_Resource, resources) > 5000)
                        {
                            continue;
                        }
                        if (input.m_Resource != Resource.NoResource)
                        {
                            // Round normally, not randomly, to prevent value from changing while simulation is paused.
                            //num4 = -MathUtils.RoundToIntRandom(ref reference.m_RandomSeed, (float)num * num2);
                            num4 = -(int)System.Math.Round(num * num2);
                            
                            // Compute new amount of input resource there would be at the company if consumed,
                            // but don't actualy change the resource amount at the company.
                            //int num9 = EconomyUtils.AddResources(input.m_Resource, num4, resources);
                            int num9 = GetNewResourceAmount(input.m_Resource, num4, resources);
                            num8 += ((EconomyUtils.GetWeight(input.m_Resource, m_ResourcePrefabs, ref m_ResourceDatas) > 0f) ? num9 : 0);
                        }
                        if (input2.m_Resource != Resource.NoResource)
                        {
                            // Round normally, not randomly, to prevent value from changing while simulation is paused.
                            //num5 = -MathUtils.RoundToIntRandom(ref reference.m_RandomSeed, (float)num * num3);
                            num5 = -(int)System.Math.Round(num * num3);
                            
                            // Compute new amount of input2 resource there would be at the company if consumed,
                            // but don't actualy change the resource amount at the company.
                            // int num10 = EconomyUtils.AddResources(input2.m_Resource, num5, resources);
                            int num10 = GetNewResourceAmount(input2.m_Resource, num5, resources);
                            num8 += ((EconomyUtils.GetWeight(input2.m_Resource, m_ResourcePrefabs, ref m_ResourceDatas) > 0f) ? num10 : 0);
                        }
                        int x = storageLimitData.m_Limit - num8;
                        if (EconomyUtils.IsResourceHasWeight(output.m_Resource, m_ResourcePrefabs, ref m_ResourceDatas))
                        {
                            num = math.min(x, num);
                        }
                        else
                        {
                            resources2 = EconomyUtils.GetResources(output.m_Resource, resources);
                            num = math.clamp(IndustrialAISystem.kMaxVirtualResourceStorage - resources2, 0, num);
                        }
                        //if (!flag && !flag2)
                        //{
                        //    Interlocked.Add(ref UnsafeUtility.AsRef<int>(m_OfficeResourceConsumptionAmount.GetUnsafePtr()), num);
                        //}
                        //resources2 = EconomyUtils.AddResources(output.m_Resource, num, resources);
                        //AddProducedResource(output.m_Resource, num);
                        //m_CountQueue.Enqueue(new CityProductionStatisticSystem.CompanyProcessingEvent
                        //{
                        //    m_Consume1 = input.m_Resource,
                        //    m_Consume1Amount = num4,
                        //    m_Consume2 = input2.m_Resource,
                        //    m_Consume2Amount = num5,
                        //    m_Produce = output.m_Resource,
                        //    m_ProduceAmount = num
                        //});

                        // Accumulate the production and consumption amounts.
                        // Convert consumption from negative to positive.
                        ProductionConsumptionUtils.AddValue(in m_ProductionAmounts,  threadIndex, output.m_Resource,  num  * EconomyUtils.kCompanyUpdatesPerDay);
                        ProductionConsumptionUtils.AddValue(in m_ConsumptionAmounts, threadIndex, input .m_Resource, -num4 * EconomyUtils.kCompanyUpdatesPerDay);
                        ProductionConsumptionUtils.AddValue(in m_ConsumptionAmounts, threadIndex, input2.m_Resource, -num5 * EconomyUtils.kCompanyUpdatesPerDay);
                    }
                    else
                    {
                        resources2 = EconomyUtils.GetResources(output.m_Resource, resources);
                    }

                    // Save production for this company, even if zero.
                    NativeList<CompanyProduction> companyProductionsForThread = m_CompanyProductions[threadIndex];
                    companyProductionsForThread.Add(new CompanyProduction { Company = entity, Production = num * EconomyUtils.kCompanyUpdatesPerDay });

                    //int num11 = EconomyUtils.GetCompanyProfitPerDay(buildingEfficiency, !flag, bufferAccessor2[i], industrialProcessData, m_ResourcePrefabs, ref m_ResourceDatas, ref m_Citizens, ref m_EconomyParameters, serviceAvailable, serviceCompanyData) / EconomyUtils.kCompanyUpdatesPerDay;
                    //TaxPayer value = nativeArray5[i];
                    //int num12 = (flag ? TaxSystem.GetCommercialTaxRate(output.m_Resource, m_TaxRates) : TaxSystem.GetIndustrialTaxRate(output.m_Resource, m_TaxRates));
                    //if (input.m_Resource != output.m_Resource && (float)num11 > 0f)
                    //{
                    //    if (num11 > 0)
                    //    {
                    //        value.m_AverageTaxRate = Mathf.RoundToInt(math.lerp(value.m_AverageTaxRate, num12, (float)num11 / (float)(num11 + value.m_UntaxedIncome)));
                    //    }
                    //    value.m_UntaxedIncome += num11;
                    //    nativeArray5[i] = value;
                    //}
                    //if (!flag && EconomyUtils.IsResourceHasWeight(output.m_Resource, m_ResourcePrefabs, ref m_ResourceDatas) && resources2 > 0)
                    //{
                    //    m_DeliveryTruckSelectData.TrySelectItem(ref random, output.m_Resource, resources2, out var item);
                    //    if ((float)item.m_Cost / (float)math.min(resources2, item.m_Capacity) < 0.03f)
                    //    {
                    //        _ = 100;
                    //        m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, new ResourceExporter
                    //        {
                    //            m_Resource = output.m_Resource,
                    //            m_Amount = math.max(0, math.min(item.m_Capacity, resources2))
                    //        });
                    //    }
                    //}
                }
            }

            /// <summary>
            /// Get the new resource amount in the company as if the resource amount was added to existing resource amount.
            /// </summary>
            private int GetNewResourceAmount(Resource resource, int amount, DynamicBuffer<Game.Economy.Resources> resources)
            {
                // Logic adapted from EconomyUtils.AddResource().
                for (int i = 0; i < resources.Length; i++)
                {
                    Game.Economy.Resources value = resources[i];
                    if (value.m_Resource == resource)
                    {
                        return (int)math.clamp((long)value.m_Amount + (long)amount, -2147483648L, 2147483647L);
                    }
                }
                return amount;
            }

            private void UpdateEfficiencyFactors(IndustrialProcessData process, bool isCommercial, DynamicBuffer<Efficiency> efficiencies, DynamicBuffer<CityModifier> cityModifiers, DynamicBuffer<SpecializationBonus> specializations)
            {
                if (IsOffice(process))
                {
                    float value = 100f;
                    if (!isCommercial)
                    {
                        CityUtils.ApplyModifier(ref value, cityModifiers, CityModifierType.OfficeEfficiency);
                    }
                    BuildingUtils.SetEfficiencyFactor(efficiencies, EfficiencyFactor.CityModifierOfficeEfficiency, value / 100f);
                }
                else if (!isCommercial)
                {
                    float value2 = 100f;
                    CityUtils.ApplyModifier(ref value2, cityModifiers, CityModifierType.IndustrialEfficiency);
                    BuildingUtils.SetEfficiencyFactor(efficiencies, EfficiencyFactor.CityModifierIndustrialEfficiency, value2 / 100f);
                }
                if (process.m_Input1.m_Resource == Resource.Fish || process.m_Input2.m_Resource == Resource.Fish)
                {
                    float value3 = 100f;
                    CityUtils.ApplyModifier(ref value3, cityModifiers, CityModifierType.IndustrialFishInputEfficiency);
                    BuildingUtils.SetEfficiencyFactor(efficiencies, EfficiencyFactor.CityModifierFishInput, value3 / 100f);
                }
                if (process.m_Output.m_Resource == Resource.Software)
                {
                    float value4 = 100f;
                    CityUtils.ApplyModifier(ref value4, cityModifiers, CityModifierType.OfficeSoftwareEfficiency);
                    BuildingUtils.SetEfficiencyFactor(efficiencies, EfficiencyFactor.CityModifierSoftware, value4 / 100f);
                }
                else if (process.m_Output.m_Resource == Resource.Electronics)
                {
                    float value5 = 100f;
                    CityUtils.ApplyModifier(ref value5, cityModifiers, CityModifierType.IndustrialElectronicsEfficiency);
                    BuildingUtils.SetEfficiencyFactor(efficiencies, EfficiencyFactor.CityModifierElectronics, value5 / 100f);
                }
                int resourceIndex = EconomyUtils.GetResourceIndex(process.m_Output.m_Resource);
                if (specializations.Length > resourceIndex)
                {
                    float efficiency = 1f + specializations[resourceIndex].GetBonus(m_EconomyParameters.m_MaxCitySpecializationBonus, m_EconomyParameters.m_ResourceProductionCoefficient);
                    BuildingUtils.SetEfficiencyFactor(efficiencies, EfficiencyFactor.SpecializationBonus, efficiency);
                }
            }

            private bool IsOffice(IndustrialProcessData process)
            {
                return !EconomyUtils.IsResourceHasWeight(process.m_Output.m_Resource, m_ResourcePrefabs, ref m_ResourceDatas);
            }

            private Resource GetRandomUpkeepResource(CompanyData companyData, Resource outputResource)
            {
                switch (companyData.m_RandomSeed.NextInt(4))
                {
                    case 0:
                        return Resource.Software;
                    case 1:
                        return Resource.Telecom;
                    case 2:
                        return Resource.Financial;
                    case 3:
                        if (EconomyUtils.IsResourceHasWeight(outputResource, m_ResourcePrefabs, ref m_ResourceDatas))
                        {
                            return Resource.Machinery;
                        }
                        if (!companyData.m_RandomSeed.NextBool())
                        {
                            return Resource.Furniture;
                        }
                        return Resource.Paper;
                    default:
                        return Resource.NoResource;
                }
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

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
            }
        }

        private struct TypeHandle
        {
            [ReadOnly]
            public ComponentLookup<ResourceData> __Game_Prefabs_ResourceData_RO_ComponentLookup;

            [ReadOnly]
            public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

            public SharedComponentTypeHandle<UpdateFrame> __Game_Simulation_UpdateFrame_SharedComponentTypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<PropertyRenter> __Game_Buildings_PropertyRenter_RO_ComponentTypeHandle;

            [ReadOnly]
            public BufferTypeHandle<Employee> __Game_Companies_Employee_RO_BufferTypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<ServiceAvailable> __Game_Companies_ServiceAvailable_RO_ComponentTypeHandle;

            public BufferTypeHandle<Game.Economy.Resources> __Game_Economy_Resources_RW_BufferTypeHandle;

            public ComponentTypeHandle<CompanyData> __Game_Companies_CompanyData_RW_ComponentTypeHandle;

            public ComponentTypeHandle<TaxPayer> __Game_Agents_TaxPayer_RW_ComponentTypeHandle;

            [ReadOnly]
            public ComponentLookup<IndustrialProcessData> __Game_Prefabs_IndustrialProcessData_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<StorageLimitData> __Game_Companies_StorageLimitData_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<Building> __Game_Buildings_Building_RO_ComponentLookup;

            [ReadOnly]
            public BufferLookup<SpecializationBonus> __Game_City_SpecializationBonus_RO_BufferLookup;

            [ReadOnly]
            public BufferLookup<CityModifier> __Game_City_CityModifier_RO_BufferLookup;

            [ReadOnly]
            public ComponentLookup<Citizen> __Game_Citizens_Citizen_RO_ComponentLookup;

            public BufferLookup<Efficiency> __Game_Buildings_Efficiency_RW_BufferLookup;

            [ReadOnly]
            public ComponentLookup<OfficeProperty> __Game_Buildings_OfficeProperty_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<ServiceAvailable> __Game_Companies_ServiceAvailable_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<ServiceCompanyData> __Game_Companies_ServiceCompanyData_RO_ComponentLookup;

            [MethodImpl((MethodImplOptions)0x100 /*AggressiveInlining*/)]
            public void __AssignHandles(ref SystemState state)
            {
                __Game_Prefabs_ResourceData_RO_ComponentLookup = state.GetComponentLookup<ResourceData>(isReadOnly: true);
                __Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
                __Game_Simulation_UpdateFrame_SharedComponentTypeHandle = state.GetSharedComponentTypeHandle<UpdateFrame>();
                __Game_Prefabs_PrefabRef_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PrefabRef>(isReadOnly: true);
                __Game_Buildings_PropertyRenter_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PropertyRenter>(isReadOnly: true);
                __Game_Companies_Employee_RO_BufferTypeHandle = state.GetBufferTypeHandle<Employee>(isReadOnly: true);
                __Game_Companies_ServiceAvailable_RO_ComponentTypeHandle = state.GetComponentTypeHandle<ServiceAvailable>(isReadOnly: true);
                __Game_Economy_Resources_RW_BufferTypeHandle = state.GetBufferTypeHandle<Game.Economy.Resources>();
                __Game_Companies_CompanyData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<CompanyData>();
                __Game_Agents_TaxPayer_RW_ComponentTypeHandle = state.GetComponentTypeHandle<TaxPayer>();
                __Game_Prefabs_IndustrialProcessData_RO_ComponentLookup = state.GetComponentLookup<IndustrialProcessData>(isReadOnly: true);
                __Game_Companies_StorageLimitData_RO_ComponentLookup = state.GetComponentLookup<StorageLimitData>(isReadOnly: true);
                __Game_Buildings_Building_RO_ComponentLookup = state.GetComponentLookup<Building>(isReadOnly: true);
                __Game_City_SpecializationBonus_RO_BufferLookup = state.GetBufferLookup<SpecializationBonus>(isReadOnly: true);
                __Game_City_CityModifier_RO_BufferLookup = state.GetBufferLookup<CityModifier>(isReadOnly: true);
                __Game_Citizens_Citizen_RO_ComponentLookup = state.GetComponentLookup<Citizen>(isReadOnly: true);
                __Game_Buildings_Efficiency_RW_BufferLookup = state.GetBufferLookup<Efficiency>();
                __Game_Buildings_OfficeProperty_RO_ComponentLookup = state.GetComponentLookup<OfficeProperty>(isReadOnly: true);
                __Game_Companies_ServiceAvailable_RO_ComponentLookup = state.GetComponentLookup<ServiceAvailable>(isReadOnly: true);
                __Game_Companies_ServiceCompanyData_RO_ComponentLookup = state.GetComponentLookup<ServiceCompanyData>(isReadOnly: true);
            }
        }

        public const int kMaxCommercialOutputResource = 5000;

        public const float kMaximumTransportUnitCost = 0.03f;

        private SimulationSystem m_SimulationSystem;

        private EndFrameBarrier m_EndFrameBarrier;

        private ResourceSystem m_ResourceSystem;

        private TaxSystem m_TaxSystem;

        private VehicleCapacitySystem m_VehicleCapacitySystem;

        private ProductionSpecializationSystem m_ProductionSpecializationSystem;

        private CitySystem m_CitySystem;

        private CityProductionStatisticSystem m_CityProductionStatisticSystem;

        private OfficeAISystem m_OfficeAISystem;

        private EntityQuery m_CompanyGroup;

        //private NativeArray<long> m_ProducedResources;

        //private JobHandle m_ProducedResourcesDeps;

        private TypeHandle __TypeHandle;

        private EntityQuery __query_1038562633_0;

        // Nested arrays to hold production and consumption amounts.
        // The outer array is one for each possible thread.
        // The inner array is one for each resource index.
        private NativeArray<NativeArray<int>> _productionAmounts;
        private NativeArray<NativeArray<int>> _consumptionAmounts;

        // Nested arrays of lists to return company production to OnUpdate.
        // The outer array is one for each possible thread.
        // The inner array is one for each company processed on that thread.
        // Even though the outer array is read only, entries can still be updated in the inner list.
        private NativeArray<NativeList<CompanyProduction>> _companyProductions;

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
            m_ResourceSystem = base.World.GetOrCreateSystemManaged<ResourceSystem>();
            m_TaxSystem = base.World.GetOrCreateSystemManaged<TaxSystem>();
            m_VehicleCapacitySystem = base.World.GetOrCreateSystemManaged<VehicleCapacitySystem>();
            m_ProductionSpecializationSystem = base.World.GetOrCreateSystemManaged<ProductionSpecializationSystem>();
            m_CitySystem = base.World.GetExistingSystemManaged<CitySystem>();
            m_CityProductionStatisticSystem = base.World.GetOrCreateSystemManaged<CityProductionStatisticSystem>();
            m_OfficeAISystem = base.World.GetOrCreateSystemManaged<OfficeAISystem>();
            m_CompanyGroup = GetEntityQuery(
                ComponentType.ReadWrite<Game.Companies.ProcessingCompany>(),
                ComponentType.ReadOnly<PropertyRenter>(),
                ComponentType.ReadWrite<Game.Economy.Resources>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<WorkProvider>(),
                ComponentType.ReadOnly<UpdateFrame>(),
                ComponentType.ReadWrite<Employee>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Game.Companies.ExtractorCompany>());
            RequireForUpdate(m_CompanyGroup);
            RequireForUpdate<EconomyParameterData>();
            //m_ProducedResources = new NativeArray<long>(EconomyUtils.ResourceCount, Allocator.Persistent);

            // Create arrays for production and consumption amounts.
            _productionAmounts  = ProductionConsumptionUtils.CreateArrays();
            _consumptionAmounts = ProductionConsumptionUtils.CreateArrays();

            // Create arrays for company productions.
            // Arrays and lists are persistent so they do not need to be created and expanded each time the production balance check runs.
            int threadCount = JobsUtility.ThreadIndexCount;
            _companyProductions = new(threadCount, Allocator.Persistent);
            for (int i = 0; i < threadCount; i++)
            {
                // Inner lists start with a default initial capacity.
                _companyProductions[i] = new(32, Allocator.Persistent);
            }
        }

        //public void PostDeserialize(Context context)
        //{
        //    if (!(context.version < Version.officeFix))
        //    {
        //        return;
        //    }
        //    ResourcePrefabs prefabs = m_ResourceSystem.GetPrefabs();
        //    ComponentLookup<ResourceData> componentLookup = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup, ref base.CheckedStateRef);
        //    NativeArray<Entity> nativeArray = m_CompanyGroup.ToEntityArray(Allocator.Temp);
        //    for (int i = 0; i < nativeArray.Length; i++)
        //    {
        //        Entity prefab = base.EntityManager.GetComponentData<PrefabRef>(nativeArray[i]).m_Prefab;
        //        IndustrialProcessData componentData = base.EntityManager.GetComponentData<IndustrialProcessData>(prefab);
        //        if (!base.EntityManager.HasComponent<ServiceAvailable>(nativeArray[i]) && componentLookup[prefabs[componentData.m_Output.m_Resource]].m_Weight == 0f)
        //        {
        //            DynamicBuffer<Game.Economy.Resources> buffer = base.EntityManager.GetBuffer<Game.Economy.Resources>(nativeArray[i]);
        //            if (EconomyUtils.GetResources(componentData.m_Output.m_Resource, buffer) >= 500)
        //            {
        //                EconomyUtils.AddResources(componentData.m_Output.m_Resource, -500, buffer);
        //            }
        //        }
        //    }
        //    nativeArray.Dispose();
        //}

        [Preserve]
        protected override void OnDestroy()
        {
            //m_ProducedResources.Dispose();
            ProductionConsumptionUtils.DisposeArrays(in _productionAmounts);
            ProductionConsumptionUtils.DisposeArrays(in _consumptionAmounts);
            foreach (NativeList<CompanyProduction> companyProductionList in _companyProductions)
            {
                companyProductionList.Dispose();
            }
            _companyProductions.Dispose();
            base.OnDestroy();
        }

        [Preserve]
        protected override void OnUpdate()
        {
            // System is never activated, so OnUpdate never executes.
            // But implementation is required.
            // Logic moved to GetAmounts.
        }

        /// <summary>
        /// Get processing company production and consumption amounts.
        /// </summary>
        public void GetAmounts(out int[] productionAmounts, out int[] consumptionAmounts, out Dictionary<Entity, int> companyProductions)
        {
            // Logic copied from OnUpdate.

            // Initialize production and consumption amounts.
            ProductionConsumptionUtils.InitializeArrays(in _productionAmounts);
            ProductionConsumptionUtils.InitializeArrays(in _consumptionAmounts);

            // Clear company productions.
            // When a NativeList is cleared, capacity remains the same.
            // So once increased, the capacity never decreases, as desired
            // so list capacity does not need to be expanded each time production balance check runs.
            foreach (NativeList<CompanyProduction> companyProduction in _companyProductions)
            {
                companyProduction.Clear();
            }

            uint updateFrame = SimulationUtils.GetUpdateFrame(m_SimulationSystem.frameIndex, EconomyUtils.kCompanyUpdatesPerDay, 16);
            //JobHandle deps;
            //JobHandle deps2;
            //JobHandle deps3;
            UpdateProcessingJob jobData = new UpdateProcessingJob
            {
                m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref __TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef),
                m_UpdateFrameType = InternalCompilerInterface.GetSharedComponentTypeHandle(ref __TypeHandle.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle, ref base.CheckedStateRef),
                m_PrefabType = InternalCompilerInterface.GetComponentTypeHandle(ref __TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle, ref base.CheckedStateRef),
                m_PropertyType = InternalCompilerInterface.GetComponentTypeHandle(ref __TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentTypeHandle, ref base.CheckedStateRef),
                m_EmployeeType = InternalCompilerInterface.GetBufferTypeHandle(ref __TypeHandle.__Game_Companies_Employee_RO_BufferTypeHandle, ref base.CheckedStateRef),
                m_ServiceAvailableType = InternalCompilerInterface.GetComponentTypeHandle(ref __TypeHandle.__Game_Companies_ServiceAvailable_RO_ComponentTypeHandle, ref base.CheckedStateRef),
                m_ResourceType = InternalCompilerInterface.GetBufferTypeHandle(ref __TypeHandle.__Game_Economy_Resources_RW_BufferTypeHandle, ref base.CheckedStateRef),
                m_CompanyDataType = InternalCompilerInterface.GetComponentTypeHandle(ref __TypeHandle.__Game_Companies_CompanyData_RW_ComponentTypeHandle, ref base.CheckedStateRef),
                m_TaxPayerType = InternalCompilerInterface.GetComponentTypeHandle(ref __TypeHandle.__Game_Agents_TaxPayer_RW_ComponentTypeHandle, ref base.CheckedStateRef),
                m_IndustrialProcessDatas = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Prefabs_IndustrialProcessData_RO_ComponentLookup, ref base.CheckedStateRef),
                m_ResourceDatas = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup, ref base.CheckedStateRef),
                m_Limits = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Companies_StorageLimitData_RO_ComponentLookup, ref base.CheckedStateRef),
                m_Buildings = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Buildings_Building_RO_ComponentLookup, ref base.CheckedStateRef),
                m_Specializations = InternalCompilerInterface.GetBufferLookup(ref __TypeHandle.__Game_City_SpecializationBonus_RO_BufferLookup, ref base.CheckedStateRef),
                m_CityModifiers = InternalCompilerInterface.GetBufferLookup(ref __TypeHandle.__Game_City_CityModifier_RO_BufferLookup, ref base.CheckedStateRef),
                m_Citizens = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Citizens_Citizen_RO_ComponentLookup, ref base.CheckedStateRef),
                m_BuildingEfficiencies = InternalCompilerInterface.GetBufferLookup(ref __TypeHandle.__Game_Buildings_Efficiency_RW_BufferLookup, ref base.CheckedStateRef),
                m_OfficeProperties = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Buildings_OfficeProperty_RO_ComponentLookup, ref base.CheckedStateRef),
                m_ServiceAvailables = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Companies_ServiceAvailable_RO_ComponentLookup, ref base.CheckedStateRef),
                m_ServiceCompanyDatas = InternalCompilerInterface.GetComponentLookup(ref __TypeHandle.__Game_Companies_ServiceCompanyData_RO_ComponentLookup, ref base.CheckedStateRef),
                m_TaxRates = m_TaxSystem.GetTaxRates(),
                m_ResourcePrefabs = m_ResourceSystem.GetPrefabs(),
                m_DeliveryTruckSelectData = m_VehicleCapacitySystem.GetDeliveryTruckSelectData(),
                //m_ProducedResources = m_ProducedResources,
                //m_ProductionQueue = m_ProductionSpecializationSystem.GetQueue(out deps).AsParallelWriter(),
                //m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
                //m_CountQueue = m_CityProductionStatisticSystem.GetConsumptionQueue(out deps2).AsParallelWriter(),
                //m_OfficeResourceConsumptionAmount = m_OfficeAISystem.GetIndustrialConsumptionAmount(out deps3),
                m_EconomyParameters = __query_1038562633_0.GetSingleton<EconomyParameterData>(),
                m_RandomSeed = RandomSeed.Next(),
                m_City = m_CitySystem.City,
                m_UpdateFrameIndex = updateFrame,

                // Pass production and consumption amounts.
                m_ProductionAmounts  = _productionAmounts,
                m_ConsumptionAmounts = _consumptionAmounts,
                m_CompanyProductions = _companyProductions,
            };
            //base.Dependency = JobChunkExtensions.ScheduleParallel(jobData, m_CompanyGroup, JobUtils.CombineDependencies(m_ProducedResourcesDeps, deps, deps2, deps3, base.Dependency));
            base.Dependency = JobChunkExtensions.ScheduleParallel(jobData, m_CompanyGroup, base.Dependency);
            //m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
            //m_ResourceSystem.AddPrefabsReader(base.Dependency);
            //m_OfficeAISystem.AddWriteConsumptionDeps(base.Dependency);
            //m_ProductionSpecializationSystem.AddQueueWriter(base.Dependency);
            //m_CityProductionStatisticSystem.AddChainWriter(base.Dependency);
            //m_TaxSystem.AddReader(base.Dependency);
            //m_ProducedResourcesDeps = default(JobHandle);

            // Wait for the job to complete before accessing the amounts.
            base.Dependency.Complete();

            // Consolidate and return production and consumption amounts from the job.
            ProductionConsumptionUtils.ConsolidateValues(in _productionAmounts,  out productionAmounts);
            ProductionConsumptionUtils.ConsolidateValues(in _consumptionAmounts, out consumptionAmounts);

            // Consolidate company productions.
            companyProductions = new();
            for (int i = 0; i < _companyProductions.Length; i++)
            {
                var companyProduction = _companyProductions[i];
                for (int j = 0; j < companyProduction.Length; j++)
                {
                    companyProductions.Add(companyProduction[j].Company, companyProduction[j].Production);
                }
            }
        }

        //public NativeArray<long> GetProducedResourcesArray(out JobHandle dependencies)
        //{
        //    dependencies = base.Dependency;
        //    return m_ProducedResources;
        //}

        //public void AddProducedResourcesReader(JobHandle handle)
        //{
        //    m_ProducedResourcesDeps = JobHandle.CombineDependencies(m_ProducedResourcesDeps, handle);
        //}

        //public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        //{
        //    byte value = (byte)m_ProducedResources.Length;
        //    writer.Write(value);
        //    for (int i = 0; i < m_ProducedResources.Length; i++)
        //    {
        //        long value2 = m_ProducedResources[i];
        //        writer.Write(value2);
        //    }
        //}

        //public void Deserialize<TReader>(TReader reader) where TReader : IReader
        //{
        //    reader.Read(out byte value);
        //    for (int i = 0; i < value; i++)
        //    {
        //        reader.Read(out long value2);
        //        if (i < m_ProducedResources.Length)
        //        {
        //            m_ProducedResources[i] = value2;
        //        }
        //    }
        //    for (int j = value; j < m_ProducedResources.Length; j++)
        //    {
        //        m_ProducedResources[j] = 0L;
        //    }
        //}

        //public void SetDefaults(Context context)
        //{
        //    for (int i = 0; i < m_ProducedResources.Length; i++)
        //    {
        //        m_ProducedResources[i] = 0L;
        //    }
        //}

        [MethodImpl((MethodImplOptions)0x100 /*AggressiveInlining*/)]
        private void __AssignQueries(ref SystemState state)
        {
            EntityQueryBuilder entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
            EntityQueryBuilder entityQueryBuilder2 = entityQueryBuilder.WithAll<EconomyParameterData>();
            entityQueryBuilder2 = entityQueryBuilder2.WithOptions(EntityQueryOptions.IncludeSystems);
            __query_1038562633_0 = entityQueryBuilder2.Build(ref state);
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
        public MyProcessingCompanySystem()
        {
        }
    }
}
