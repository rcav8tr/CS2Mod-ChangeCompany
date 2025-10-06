using Colossal.Entities;
using Game;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Scripting;
using DeliveryTruck    = Game.Vehicles. DeliveryTruck;
using ExtractorCompany = Game.Companies.ExtractorCompany;
using Resources        = Game.Economy.  Resources;
using StorageCompany   = Game.Companies.StorageCompany;

namespace ChangeCompany
{
    /// <summary>
    /// Balance production on industrial and office companies based on surplus and deficit.
    /// </summary>
    public partial class ProductionBalanceSystem : GameSystemBase
    {
        /// <summary>
        /// Detail for a company.
        /// Specifically not named CompanyData to avoid confusion with the game's CompanyData component.
        /// </summary>
        private struct CompanyDetail
        {
            public Entity CompanyEntity;
            public Entity CompanyPrefab;
            public int    CompanyProduction;
            public Entity PropertyEntity;
            public Entity PropertyPrefab;
        }

        /// <summary>
        /// List of company details.
        /// </summary>
        private class CompanyDetails : List<CompanyDetail>
        {
            /// <summary>
            /// Compute average production.
            /// </summary>
            public double AverageProduction()
            {
                // Check for no companies.
                if (Count == 0)
                {
                    return 0d;
                }

                // Compute average.
                int totalProduction = 0;
                foreach (CompanyDetail companyDetail in this)
                {
                    totalProduction += companyDetail.CompanyProduction;
                }
                return (double)totalProduction / Count;
            }
        }

        /// <summary>
        /// Job to get the company details needed to do production balance.
        /// </summary>
        [BurstCompile]
        private partial struct GetCompanyDetailsJob : IJobChunk
        {
            // Entity type handle.
            [ReadOnly] public EntityTypeHandle                          EntityTypeHandle;

            // Component type handles.
            [ReadOnly] public ComponentTypeHandle<PrefabRef             > ComponentTypeHandlePrefabRef;
            [ReadOnly] public ComponentTypeHandle<PropertyRenter        > ComponentTypeHandlePropertyRenter;

            // Buffer lookups.
            [ReadOnly] public BufferLookup<Efficiency                   > BufferLookupEfficiency;
            [ReadOnly] public BufferLookup<Employee                     > BufferLookupEmployee;

            // Component lookups.
            [ReadOnly] public ComponentLookup<Citizen                   > ComponentLookupCitizen;
            [ReadOnly] public ComponentLookup<IndustrialProcessData     > ComponentLookupIndustrialProcessData;
            [ReadOnly] public ComponentLookup<PrefabRef                 > ComponentLookupPrefabRef;
            [ReadOnly] public ComponentLookup<ResourceData              > ComponentLookupResourceData;

            // Other data.
            [ReadOnly] public EconomyParameterData                      EconomyParameters;
            [ReadOnly] public ResourcePrefabs                           ResourcePrefabs;

            // Arrays of lists to return industrial and office company details to OnUpdate.
            // The outer array is one for each possible thread.
            // The inner list is one for each company detail created in that thread.
            // Even though the outer arrays are read only, entries can still be added to the inner lists.
            [ReadOnly] public NativeArray<NativeList<CompanyDetail>>    CompanyDetailsIndustrial;
            [ReadOnly] public NativeArray<NativeList<CompanyDetail>>    CompanyDetailsOffice;

            /// <summary>
            /// Job execution.
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Get arrays of query data from the chunk.
                NativeArray<Entity        > companyEntities        = chunk.GetNativeArray(EntityTypeHandle);
                NativeArray<PrefabRef     > companyPrefabRefs      = chunk.GetNativeArray(ref ComponentTypeHandlePrefabRef);
                NativeArray<PropertyRenter> companyPropertyRenters = chunk.GetNativeArray(ref ComponentTypeHandlePropertyRenter);

                // Do each company.
                for (int i = 0; i < companyEntities.Length; i++)
                {
                    // Get entries from the query arrays for this company.
                    Entity companyEntity  = companyEntities[i];
                    Entity companyPrefab  = companyPrefabRefs[i].m_Prefab;
                    Entity propertyEntity = companyPropertyRenters[i].m_Property;

                    // Get property prefab and company industrial process data.
                    if (ComponentLookupPrefabRef.TryGetComponent(propertyEntity, out PrefabRef propertyPrefabRef) &&
                        ComponentLookupIndustrialProcessData.TryGetComponent(companyPrefab, out IndustrialProcessData companyIndustrialProcessData))
                    {
                        // Create a new company detail to add.
                        CompanyDetail companyDetail = new()
                            {
                                CompanyEntity = companyEntity,
                                CompanyPrefab = companyPrefab,
                                CompanyProduction = GetCompanyProduction(companyEntity, companyIndustrialProcessData, propertyEntity),
                                PropertyEntity = propertyEntity,
                                PropertyPrefab = propertyPrefabRef.m_Prefab,
                            };

                        // Use company's output resource to differentiate between industrial and office.
                        // Add an entry of company detail for this thread.
                        // By having a separate entry for each thread, parallel threads will never access the same inner list at the same time.
                        Resource companyOutputResource = companyIndustrialProcessData.m_Output.m_Resource;
                        if (EconomyUtils.IsIndustrialResource(ComponentLookupResourceData[ResourcePrefabs[companyOutputResource]], false, false))
                        {
                            CompanyDetailsIndustrial[JobsUtility.ThreadIndex].Add(in companyDetail);
                        }
                        else if (EconomyUtils.IsOfficeResource(companyOutputResource))
                        {
                            CompanyDetailsOffice[JobsUtility.ThreadIndex].Add(in companyDetail);
                        }
                    }
                }
            }

            /// <summary>
            /// Get company production.
            /// </summary>
            private int GetCompanyProduction(Entity companyEntity, IndustrialProcessData companyIndustrialProcessData, Entity propertyEntity)
            {
                // Logic adapted from CountCompanyDataSystem.CountCompanyDataJob for computing Production.

                // Get company's employee buffer.
                if (BufferLookupEmployee.TryGetBuffer(companyEntity, out DynamicBuffer<Employee> companyEmployees))
                {
                    // Compute building efficiency.
				    float buildingEfficiency = 1f;
				    if (BufferLookupEfficiency.TryGetBuffer(propertyEntity, out DynamicBuffer<Efficiency> bufferEfficiency))
				    {
					    buildingEfficiency = BuildingUtils.GetEfficiency(bufferEfficiency);
				    }

                    // Compute and return the company production.
                    return EconomyUtils.GetCompanyProductionPerDay(
                        buildingEfficiency,
                        true,
                        companyEmployees,
                        companyIndustrialProcessData,
                        ResourcePrefabs,
                        ref ComponentLookupResourceData,
                        ref ComponentLookupCitizen,
                        ref EconomyParameters);
                }

                // This should never happen because a company should always have an employee buffer.
                return 0;
            }
        }

        /// <summary>
        /// Surplus amount for a resource.
        /// Deficit is a negative surplus.
        /// </summary>
        private class ResourceSurplus : IComparable<ResourceSurplus>
        {
            public Resource Resource;
            public int Surplus;
            public int Production;

            // For sorting by surplus amount.
            public int CompareTo(ResourceSurplus other)
            {
                return Surplus.CompareTo(other.Surplus);
            }
        }

        /// <summary>
        /// List of resource surpluses.
        /// </summary>
        private class ResourceSurpluses : List<ResourceSurplus>
        {
            // Compute standard deviation of resource surpluses.
            public double StandardDeviation()
            {
                // Check for no resources.
                if (Count == 0)
                {
                    return 0d;
                }
                
                // Compute average.
                int total = 0;
                for (int i = 0; i < Count; i++)  
                {
                    total += this[i].Surplus;
                }
                double average = (double)total / Count;

                // Compute standard deviaton.
                double sumOfSquaredDifferences = 0d;
                for (int i = 0; i < Count; i++)
                {
                    double difference = this[i].Surplus - average;
                    sumOfSquaredDifferences += difference * difference;
                } 
                return Math.Sqrt(sumOfSquaredDifferences / Count);
            }
        }

        /// <summary>
        /// Surplus amount for a company info.
        /// </summary>
        private class CompanyInfoSurplus : IComparable<CompanyInfoSurplus>
        {
            public CompanyInfo CompanyInfo;
            public int Surplus;

            // For sorting by surplus amount.
            public int CompareTo(CompanyInfoSurplus other)
            {
                return Surplus.CompareTo(other.Surplus);
            }
        }

        /// <summary>
        /// Total worth for a company detail.
        /// </summary>
        private class CompanyTotalWorth : IComparable<CompanyTotalWorth>
        {
            public CompanyDetail CompanyDetail;
            public int TotalWorth;

            // For sorting by total worth.
            public int CompareTo(CompanyTotalWorth other)
            {
                return TotalWorth.CompareTo(other.TotalWorth);
            }
        }

        // Other systems.
        private ChangeCompanySection    _changeCompanySection;
        private ChangeCompanySystem     _changeCompanySystem;
        private TimeSystem              _timeSystem;
        private ResourceSystem          _resourceSystem;
        private CountCompanyDataSystem  _countCompanyDataSystem;    // Update interval = 16, Update offset = 1
        private IndustrialDemandSystem  _industrialDemandSystem;    // Update interval = 16, Update offset = 7
        private CommercialDemandSystem  _commercialDemandSystem;    // Update interval = 16, Update offset = 4

        // Define resources for extraction, industrial, and office.
        private List<Resource> _resourcesExtraction;
        private List<Resource> _resourcesIndustrial;
        private List<Resource> _resourcesOffice;

        // Entity queries.
        private EntityQuery _queryCompanies;
        private EntityQuery _queryEconomyParameterData;

        // Arrays of lists to hold company details.
        // The outer array is one for each possible thread.
        // The inner list is one for each company detail created in that thread.
        NativeArray<NativeList<CompanyDetail>> _companyDetailsJobIndustrial;
        NativeArray<NativeList<CompanyDetail>> _companyDetailsJobOffice;

        // Flags to enable OnUpdate to run.
        private bool _inGame;           // Whether or not application is in a game (i.e. as opposed to main menu, editor, etc.).
        private bool _initialized;      // Whether or not this system is initialized.
        private int _onUpdateCounter;

        // Data and lookups for computing information about a company.
        private ResourcePrefabs                 _resourcePrefabs;
        private BufferLookup<LayoutElement>     _bufferLookupLayoutElement;
        private ComponentLookup<DeliveryTruck>  _componentLookupDeliveryTruck;
        private ComponentLookup<ResourceData>   _componentLookupResourceData;

        // Production balance info.
        private ProductionBalanceInfo _productionBalanceInfoIndustrial = new(true);
        private ProductionBalanceInfo _productionBalanceInfoOffice     = new(false);
        private static readonly object _productionBalanceInfoLockIndustrial = new();
        private static readonly object _productionBalanceInfoLockOffice     = new();

        // Game date/time to do production balance next check.
        private readonly GameDateTime _productionBalanceNextCheckIndustrial = new();
        private readonly GameDateTime _productionBalanceNextCheckOffice     = new();
        private static readonly object _productionBalanceNextCheckLockIndustrial = new();
        private static readonly object _productionBalanceNextCheckLockOffice     = new();

        /// <summary>
        /// Set update interval same as game systems from which data is obtained.
        /// </summary>
        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 16;
        }

        /// <summary>
        /// Set update offset to be after game systems from which data is obtained.
        /// </summary>
        public override int GetUpdateOffset(SystemUpdatePhase phase)
        {
            return 10;
        }

        /// <summary>
        /// Initialize this system.
        /// </summary>
        [Preserve]
        protected override void OnCreate()
        { 
            Mod.log.Info($"{nameof(ProductionBalanceSystem)}.{nameof(OnCreate)}");

            base.OnCreate();
            
            try
            {
                // Get other systems.
                _changeCompanySection   = World.GetOrCreateSystemManaged<ChangeCompanySection   >();
                _changeCompanySystem    = World.GetOrCreateSystemManaged<ChangeCompanySystem    >();
                _timeSystem             = World.GetOrCreateSystemManaged<TimeSystem             >();
                _resourceSystem         = World.GetOrCreateSystemManaged<ResourceSystem         >();
                _countCompanyDataSystem = World.GetOrCreateSystemManaged<CountCompanyDataSystem >();
                _industrialDemandSystem = World.GetOrCreateSystemManaged<IndustrialDemandSystem >();
                _commercialDemandSystem = World.GetOrCreateSystemManaged<CommercialDemandSystem >();

                // Define extraction, industrial, and office resources.
                // This system does not care about the resource order.
                _resourcesExtraction = new();
                _resourcesIndustrial = new();
                _resourcesOffice     = new();
		        _componentLookupResourceData = SystemAPI.GetComponentLookup<ResourceData>(true);
                _resourcePrefabs = _resourceSystem.GetPrefabs();
			    ResourceIterator resourceIterator = ResourceIterator.GetIterator();
			    while (resourceIterator.Next())
			    {
                    Resource resource = resourceIterator.resource;
                    if (EconomyUtils.IsExtractorResource(resource))
                    {
                        _resourcesExtraction.Add(resource);
                    }
                    if (EconomyUtils.IsIndustrialResource(_componentLookupResourceData[_resourcePrefabs[resource]], false, false))
                    {
                        _resourcesIndustrial.Add(resource);
                    }
                    if (EconomyUtils.IsOfficeResource(resource))
                    {
                        _resourcesOffice.Add(resource);
                    }
			    }

                // Query to get industrial and office companies.
                // There is no way to distinguish between industrial and office companies based only on the components they have.
                _queryCompanies = GetEntityQuery(
                    ComponentType.ReadOnly<CompanyData      >(),
                    ComponentType.ReadOnly<PrefabRef        >(),
                    ComponentType.ReadOnly<IndustrialCompany>(),    
                    ComponentType.ReadOnly<PropertyRenter   >(),    // Company must be on a property.
                    ComponentType.Exclude<ExtractorCompany  >(),    // Exclude extractor companies.
                    ComponentType.Exclude<StorageCompany    >(),    // Exclude storage companies.
                    ComponentType.Exclude<MovingAway        >(),    // Exclude companies already moving away.
                    ComponentType.Exclude<Deleted           >(),
                    ComponentType.Exclude<Temp              >());

                // Query to get economy parameter data.
                _queryEconomyParameterData = GetEntityQuery(ComponentType.ReadOnly<EconomyParameterData>());

                // Create arrays of lists to hold industrial and office company detail.
                // Arrays and lists are persistent so they do not need to be created and expanded each time the production balance check runs.
                int threadCount = JobsUtility.ThreadIndexCount;
                _companyDetailsJobIndustrial = new(threadCount, Allocator.Persistent);
                _companyDetailsJobOffice     = new(threadCount, Allocator.Persistent);
                for (int i = 0; i < threadCount; i++)
                {
                    // Each thread array entry is a list to hold company details.
                    _companyDetailsJobIndustrial[i] = new(32, Allocator.Persistent);
                    _companyDetailsJobOffice    [i] = new(32, Allocator.Persistent);
                }

                // Initialize miscellaneous.
                _inGame = false;
                _initialized = true;
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Deinitialize this system.
        /// </summary>
        [Preserve]
        protected override void OnDestroy()
        {
            // Dispose persistent native collections.
            DisposeCompanyDetails(_companyDetailsJobIndustrial);
            DisposeCompanyDetails(_companyDetailsJobOffice);

            base.OnDestroy();
        }

        /// <summary>
        /// Dispose of a company details array of lists.
        /// </summary>
        private void DisposeCompanyDetails(in NativeArray<NativeList<CompanyDetail>> companyDetailsJob)
        {
            if (companyDetailsJob != null)
            {
                // Dispose each inner list.
                foreach (NativeList<CompanyDetail> companyDetailsList in companyDetailsJob)
                {
                    companyDetailsList.Dispose();
                }

                // Dispose the outer array.
                companyDetailsJob.Dispose();
            }
        }

        /// <summary>
        /// Called by the game when a GameMode is about to be loaded.
        /// </summary>
        [Preserve]
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            // Not in a game.
            _inGame = false;
        }

        /// <summary>
        /// Called by the game when a GameMode is done being loaded.
        /// </summary>
        [Preserve]
        protected override void OnGameLoadingComplete(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);

            // If started a game, then initialize.
            if (mode == GameMode.Game)
            {
                // Initialize production balance infos.
                _productionBalanceInfoIndustrial = new(true);
                _productionBalanceInfoOffice     = new(false);

                // Initialize production balance next check.
                SetProductionBalanceNextCheckIndustrial();
                SetProductionBalanceNextCheckOffice();

                // Initialize OnUpdate counter.
                _onUpdateCounter = 0;

                // In a game.  Set this last because this allows OnUpdate to run and everything else should be initialized before then.
                _inGame = true;
            }
            else
            {
                // Not in a game.
                _inGame = false;
            }
        }

        /// <summary>
        /// Called by the game while simulation is running.
        /// Timing is based on update interval and update offset.
        /// </summary>
        [Preserve]
        protected override void OnUpdate()
        {
            // Must be initialized.
            if (!_initialized)
            {
                return;
            }

            // Must be in a game.
            if (!_inGame)
            {
                return;
            }

            try
            {
                // Delay running this mod's OnUpdate.
                // This delay allows game systems to initialize data that this mod depends on.
                // Without the delay, initial game data is bad.
                const int FirstRunCount = 3;
                _onUpdateCounter++;
                if (_onUpdateCounter < FirstRunCount)
                {
                    return;
                }

                // Initialize production balance next check, but only once after initial delay.
                if (_onUpdateCounter == FirstRunCount)
                {
                    SetProductionBalanceNextCheckIndustrial();
                    SetProductionBalanceNextCheckOffice();
                    return;
                }

                // Get company details for industrial and office.
                GetCompanyDetails(
                    out CompanyDetails companyDetailsIndustrial,
                    out CompanyDetails companyDetailsOffice);

                // Get resource surpluses for industrial, office, and all.
                GetResourceSurpluses(
                    out ResourceSurpluses resourceSurplusesIndustrial,
                    out ResourceSurpluses resourceSurplusesOffice,
                    out ResourceSurpluses resourceSurplusesAll);

                // Get data and lookups needed to compute company total worth.
                _resourcePrefabs              = _resourceSystem.GetPrefabs();
		        _bufferLookupLayoutElement    = SystemAPI.GetBufferLookup   <LayoutElement>(true);
		        _componentLookupDeliveryTruck = SystemAPI.GetComponentLookup<DeliveryTruck>(true);
		        _componentLookupResourceData  = SystemAPI.GetComponentLookup<ResourceData >(true);

                // Get current game date/time.
                GameDateTime currentGameDateTime = GetCurrentGameDateTime();

                // Perform production balance check for industrial.
                ProductionBalanceCheck(
                    Mod.ModSettings.ProductionBalanceEnabledIndustrial,
                    Mod.ModSettings.ProductionBalanceCheckIntervalIndustrial,
                    Mod.ModSettings.ProductionBalanceMinimumCompaniesIndustrial,
                    Mod.ModSettings.ProductionBalanceMinimumStandardDeviationIndustrial,
                    Mod.ModSettings.ProductionBalanceMaximumCompanyProductionIndustrial,
                    currentGameDateTime,
                    _productionBalanceNextCheckIndustrial,
                    _productionBalanceNextCheckLockIndustrial,
                    resourceSurplusesIndustrial,
                    resourceSurplusesAll,
                    companyDetailsIndustrial,
                    _productionBalanceInfoIndustrial,
                    _productionBalanceInfoLockIndustrial);

                // Perform production balance check for office.
                ProductionBalanceCheck(
                    Mod.ModSettings.ProductionBalanceEnabledOffice,
                    Mod.ModSettings.ProductionBalanceCheckIntervalOffice,
                    Mod.ModSettings.ProductionBalanceMinimumCompaniesOffice,
                    Mod.ModSettings.ProductionBalanceMinimumStandardDeviationOffice,
                    Mod.ModSettings.ProductionBalanceMaximumCompanyProductionOffice,
                    currentGameDateTime,
                    _productionBalanceNextCheckOffice,
                    _productionBalanceNextCheckLockOffice,
                    resourceSurplusesOffice,
                    resourceSurplusesAll,
                    companyDetailsOffice,
                    _productionBalanceInfoOffice,
                    _productionBalanceInfoLockOffice);
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Get details for industrial and office companies.
        /// </summary>
        private void GetCompanyDetails(out CompanyDetails companyDetailsIndustrial, out CompanyDetails companyDetailsOffice)
        {
            // Clear company detail lists.
            // When a NativeList is cleared, capacity remains the same.
            // So once increased, the capacity never decreases, as desired
            // so list capacity does not need to be expanded each time production balance check runs.
            foreach (NativeList<CompanyDetail> companyDetailsList in _companyDetailsJobIndustrial)
            {
                companyDetailsList.Clear();
            }
            foreach (NativeList<CompanyDetail> companyDetailsList in _companyDetailsJobOffice)
            {
                companyDetailsList.Clear();
            }

            // Create the job to get company details.
            GetCompanyDetailsJob getCompanyDetailsJob = new()
            {
                EntityTypeHandle                        = SystemAPI.GetEntityTypeHandle(),
                    
                ComponentTypeHandlePrefabRef            = SystemAPI.GetComponentTypeHandle  <PrefabRef              >(true),
                ComponentTypeHandlePropertyRenter       = SystemAPI.GetComponentTypeHandle  <PropertyRenter         >(true),

                BufferLookupEfficiency                  = SystemAPI.GetBufferLookup         <Efficiency             >(true),
                BufferLookupEmployee                    = SystemAPI.GetBufferLookup         <Employee               >(true),

                ComponentLookupCitizen                  = SystemAPI.GetComponentLookup      <Citizen                >(true),
                ComponentLookupIndustrialProcessData    = SystemAPI.GetComponentLookup      <IndustrialProcessData  >(true),
                ComponentLookupPrefabRef                = SystemAPI.GetComponentLookup      <PrefabRef              >(true),
                ComponentLookupResourceData             = SystemAPI.GetComponentLookup      <ResourceData           >(true),

                EconomyParameters                       = _queryEconomyParameterData.GetSingleton<EconomyParameterData>(),
                ResourcePrefabs                         = _resourceSystem.GetPrefabs(),

                CompanyDetailsIndustrial                = _companyDetailsJobIndustrial,
                CompanyDetailsOffice                    = _companyDetailsJobOffice,
            };

            // Schedule the job to get company details.
            // Execute the job in parallel (i.e. job uses multiple threads, if available).
            // Parallel threads execute much faster than a single thread.
            JobHandle jobHandleGetCompanyDetails = JobChunkExtensions.ScheduleParallel(getCompanyDetailsJob, _queryCompanies, base.Dependency);

            // Prevent the company details job from running again until it is complete.
            base.Dependency = jobHandleGetCompanyDetails;

            // Wait for the company details job to complete before accessing the company details.
            jobHandleGetCompanyDetails.Complete();

            // Consolidate and return company details from the job.
            companyDetailsIndustrial = ConsolidateCompanyDetailsFromJob(in _companyDetailsJobIndustrial);
            companyDetailsOffice     = ConsolidateCompanyDetailsFromJob(in _companyDetailsJobOffice);
        }

        /// <summary>
        /// Consolidate company details from the company details that were obtained form the job.
        /// </summary>
        private CompanyDetails ConsolidateCompanyDetailsFromJob(in NativeArray<NativeList<CompanyDetail>> companyDetailsJob)
        {
            // Do each thread entry in the outer array.
            CompanyDetails consolidatedCompanyDetails = new();
            foreach (NativeList<CompanyDetail> companyDetailsJobList in companyDetailsJob)
            {
                // Do each company detail entry in the inner list.
                foreach (CompanyDetail companyDetailJob in companyDetailsJobList)
                {
                    // The company must be able to change.
                    // Need to check this here because class ChangeCompanySection cannot be referenced
                    // in the burst compiled job that gets the company details.
                    if (_changeCompanySection.CompanyCanChange(companyDetailJob.PropertyEntity, companyDetailJob.PropertyPrefab))
                    {
                        // Include this company detail from the job.
                        consolidatedCompanyDetails.Add(companyDetailJob);
                    }
                }
            }
            return consolidatedCompanyDetails;
        }

        /// <summary>
        /// Get surplus amounts by resource for industrial, office, and all.
        /// </summary>
        private void GetResourceSurpluses(
            out ResourceSurpluses resourceSurplusesIndustrial,
            out ResourceSurpluses resourceSurplusesOffice,
            out ResourceSurpluses resourceSurplusesAll)
        {
            // Initialize returns.
            resourceSurplusesIndustrial = new();
            resourceSurplusesOffice     = new();
            resourceSurplusesAll        = new();

            // Get production and consumption data.
            // Logic adapted from Game.UI.InGame.ProductionUISystem.UpdateCache().
		    NativeArray<int> productionData        = _countCompanyDataSystem.GetProduction (out JobHandle jobHandleProductionData);
		    NativeArray<int> industrialConsumption = _industrialDemandSystem.GetConsumption(out JobHandle jobHandleIndustrialConsumption);
		    NativeArray<int> commercialConsumption = _commercialDemandSystem.GetConsumption(out JobHandle jobHandleCommercialConsumption);
		    JobHandle.CompleteAll(ref jobHandleProductionData, ref jobHandleIndustrialConsumption, ref jobHandleCommercialConsumption);

            // Get resource surpluses for industrial.
            foreach (Resource resource in _resourcesIndustrial)
            {
                resourceSurplusesIndustrial.Add(GetResourceSurplus(resource, in productionData, in industrialConsumption, in commercialConsumption));
            }

            // Get resource surpluses for office.
            foreach (Resource resource in _resourcesOffice)
            {
                resourceSurplusesOffice.Add(GetResourceSurplus(resource, in productionData, in industrialConsumption, in commercialConsumption));
            }

            // Get resource surpluses for All.
            // Extraction is included in All, even though extraction is not returned by itself.
            foreach (Resource resource in _resourcesExtraction)
            {
                resourceSurplusesAll.Add(GetResourceSurplus(resource, in productionData, in industrialConsumption, in commercialConsumption));
            }
            resourceSurplusesAll.AddRange(resourceSurplusesIndustrial);
            resourceSurplusesAll.AddRange(resourceSurplusesOffice);

            // Sort resource surpluses.
            resourceSurplusesIndustrial.Sort();
            resourceSurplusesOffice    .Sort();
            resourceSurplusesAll       .Sort();
        }

        /// <summary>
        /// Get the surplus amount for a resource.
        /// </summary>
        private ResourceSurplus GetResourceSurplus(
            Resource resource,
            in NativeArray<int> productionData,
            in NativeArray<int> industrialConsumption,
            in NativeArray<int> commercialConsumption)
        {
            // Compute production and consumption.
            // Logic adapted from Game.UI.InGame.ProductionUISystem.GetData().
            int resourceIndex = EconomyUtils.GetResourceIndex(resource);
            int production = productionData[resourceIndex];
            int consumption = commercialConsumption[resourceIndex] + industrialConsumption[resourceIndex];

            // Return the resource surplus and production amounts.
            // Surplus is production minus consumption.
            // Deficit is a negative surplus.
            return new() { Resource = resource, Surplus = production - consumption, Production = production };
        }

        /// <summary>
        /// Perform a production balance check.
        /// </summary>
        private void ProductionBalanceCheck(
            bool settingProductionBalanceEnabled,
            int settingProductionBalanceCheckInterval,
            int settingProductionBalanceMinimumCompanies,
            int settingProductionBalanceMinimumStandardDeviation,
            int settingProductionBalanceMaximumCompanyProduction,
            GameDateTime currentGameDateTime,
            GameDateTime productionBalanceNextCheck,
            object productionBalanceNextCheckLock,
            ResourceSurpluses resourceSurpluses,
            ResourceSurpluses resourceSurplusesAll,
            CompanyDetails companyDetails,
            ProductionBalanceInfo productionBalanceInfo,
            object productionBalanceInfoLock)
        {
            // Compute standard deviation of resource surpluses as a percent of average company production.
            double standardDeviationPercent = 0d;
            double averageProduction = companyDetails.AverageProduction();
            if (averageProduction > 0d)
            {
                standardDeviationPercent = 100d * resourceSurpluses.StandardDeviation() / averageProduction;
                const double MaxStandardDeviationPercent = 999999d;
                if (standardDeviationPercent > MaxStandardDeviationPercent)
                {
                    standardDeviationPercent = MaxStandardDeviationPercent;
                }
            }

            // Lock thread while writing production balance info.
            lock (productionBalanceInfoLock)
            {
                // Production balance info is computed and written even if production balance is disabled.
                // This allows the UI to display the info even when production balance is disabled.
                productionBalanceInfo.InfoValid = true;
                productionBalanceInfo.CompanyCount = companyDetails.Count;
                productionBalanceInfo.StandardDeviationPercent = standardDeviationPercent;
            }
            
            // Production balance must be enabled.
            if (settingProductionBalanceEnabled)
            {
                // Lock thread while reading next check date/time.
                bool isTimeToDoProductionBalance;
                lock (productionBalanceNextCheckLock)
                {
                    // Check if it is time to do the production balance.
                    isTimeToDoProductionBalance =
                        (currentGameDateTime.Hour  == productionBalanceNextCheck.Hour  && currentGameDateTime.Minute >= productionBalanceNextCheck.Minute) ||
                        (currentGameDateTime.Month == productionBalanceNextCheck.Month && currentGameDateTime.Hour   >  productionBalanceNextCheck.Hour  ) ||
                        (currentGameDateTime.Year  == productionBalanceNextCheck.Year  && currentGameDateTime.Month  >  productionBalanceNextCheck.Month ) ||
                        (                                                                 currentGameDateTime.Year   >  productionBalanceNextCheck.Year  );
                }
                if (isTimeToDoProductionBalance)
                {
                    // It is time to do production balance.

                    // Must meet minimum company count and meet minimum standard deviation percent.
                    if (companyDetails.Count     >= settingProductionBalanceMinimumCompanies &&
                        standardDeviationPercent >= settingProductionBalanceMinimumStandardDeviation)
                    {
                        // Now, finally, try to do a production balance.
                        DoProductionBalance(
                            settingProductionBalanceMaximumCompanyProduction,
                            currentGameDateTime,
                            resourceSurpluses,
                            resourceSurplusesAll,
                            companyDetails,
                            productionBalanceInfo,
                            productionBalanceInfoLock);
                    }

                    // Set production balance next check date/time even if did not try to do a production balance.
                    SetProductionBalanceNextCheck(
                        currentGameDateTime,
                        settingProductionBalanceEnabled,
                        settingProductionBalanceCheckInterval,
                        productionBalanceNextCheck,
                        productionBalanceNextCheckLock,
                        productionBalanceInfo,
                        productionBalanceInfoLock);
                }
            }
        }

        /// <summary>
        /// Do a production balance.
        /// </summary>
        private void DoProductionBalance(
            int settingProductionBalanceMaximumCompanyProduction,
            GameDateTime currentGameDateTime,
            ResourceSurpluses resourceSurpluses,
            ResourceSurpluses resourceSurplusesAll,
            CompanyDetails companyDetails,
            ProductionBalanceInfo productionBalanceInfo,
            object productionBalanceInfoLock)
        {
            // Do resource surpluses starting with the greatest surplus and continuing with decreasing surpluses.
            // The greatest surplus could be a deficit (i.e. a negative surplus).
            // These are the resources that are producing too much, i.e a "too much resource".
            // Do down to but not including the last resource surplus.
            for (int i = resourceSurpluses.Count - 1; i > 0; i--)
            {
                // Get the too much resource.
                ResourceSurplus resourceSurplusTooMuch = resourceSurpluses[i];
                Resource resourceTooMuch = resourceSurplusTooMuch.Resource;

                // The max allowed company production is a percent of the city production of the resource.
                // This prevents removing a company when its production accounts for a significant portion of city production.
                // This is especially important in small cities where only 1 or a few companies produce each resource.
                int maxAllowedCompanyProduction = resourceSurplusTooMuch.Production * settingProductionBalanceMaximumCompanyProduction / 100;

                // Get the company infos for the too much resource.
                CompanyInfos companyInfosTooMuch = _changeCompanySection.GetCompanyInfosForResource(resourceTooMuch);
                List<CompanyInfoSurplus> companyInfoSurpluses = new();
                if (companyInfosTooMuch.Count == 1)
                {
                    // There is only only one company info, use it.
                    // Surplus of input resources does not matter, just use zero.
                    companyInfoSurpluses.Add(new CompanyInfoSurplus
                    {
                        CompanyInfo = companyInfosTooMuch[0],
                        Surplus = 0
                    });
                }
                else
                {
                    // There is more than one company info.
                    // Get the surplus of input resources for each company info.
                    foreach (CompanyInfo companyInfoTooMuch in companyInfosTooMuch)
                    {
                        companyInfoSurpluses.Add(new CompanyInfoSurplus
                        {
                            CompanyInfo = companyInfoTooMuch,
                            Surplus = ComputeSurplusOfInputResources(companyInfoTooMuch, resourceSurplusesAll)
                        });
                    }

                    // Sort the company infos by the surplus of input resources.
                    companyInfoSurpluses.Sort();
                }

                // Do each company info starting with the lowest surplus of input resources.
                // This is the company to be replaced, so it is desired to keep the companies whose input resources have a higher surplus.
                for (int j = 0; j < companyInfoSurpluses.Count; j++)
                {
                    // Get the company prefab for this company info.
                    Entity companyPrefabTooMuch = companyInfoSurpluses[j].CompanyInfo.CompanyPrefab;

                    // Get current companies that have the too much company prefab.
                    CompanyDetails companyDetailsTooMuch = new();
                    foreach (CompanyDetail companyDetail in companyDetails)
                    {
                        // Current company prefab must match the too much company prefab.
                        if (companyDetail.CompanyPrefab == companyPrefabTooMuch)
                        {
                            companyDetailsTooMuch.Add(companyDetail);
                        }
                    }

                    // Check for no companies found.
                    if (companyDetailsTooMuch.Count == 0)
                    {
                        // Finding no companies is not an error.
                        // It simply means no companies match the too much prefab.
                        // Check the next company info.
                        continue;
                    }

                    // Do resource surpluses starting with the lowest surplus and continuing with increasing surpluses.
                    // The lowest surplus could be a deficit (i.e. a negative surplus).
                    // These are the resources that are producing not enough, i.e a "not enough resource".
                    // Do up to but not including the too much resource surplus.
                    for (int k = 0; k < i; k++)
                    {
                        // Get the not enough resource.
                        Resource resourceNotEnough = resourceSurpluses[k].Resource;

                        // Find a company that produces the too much resource and whose property allows the not enough resource.
                        if (TryFindCompanyTooMuch(companyDetailsTooMuch, maxAllowedCompanyProduction, resourceNotEnough, out CompanyDetail companyDetailTooMuch))
                        {
                            // Change the too much company to the not enough company.
                            ChangeCompany(companyDetailTooMuch, resourceNotEnough, resourceSurplusesAll);

                            // Lock thread while writing production balance info.
                            lock (productionBalanceInfoLock)
                            {
                                // Save the last change data to production balance info.
                                productionBalanceInfo.LastChangeDateTime     = currentGameDateTime.Copy();
                                productionBalanceInfo.LastChangeFromResource = resourceTooMuch;
                                productionBalanceInfo.LastChangeToResource   = resourceNotEnough;
                            }

                            // A production balance was performed.
                            // Stop trying to do a production balance.
                            return;
                        }
                    }
                }
            }

            // Execution getting here is not an error.
            // It simply means that a production balance could not be performed.
        }

        /// <summary>
        /// Find a company that produces the too much resource.
        /// </summary>
        private bool TryFindCompanyTooMuch(
            CompanyDetails companyDetailsTooMuch,
            int maxAllowedCompanyProduction,
            Resource resourceNotEnough,
            out CompanyDetail companyDetailFound)
        {
            // Get the too much companies that are valid.
            List<CompanyTotalWorth> validCompanies = new();
            foreach (CompanyDetail companyDetailTooMuch in companyDetailsTooMuch)
            {
                // Company production must be less than max allowed production.
                if (companyDetailTooMuch.CompanyProduction <= maxAllowedCompanyProduction)
                {
                    // Company property's prefab must allow the not enough resource to be manufactured.
                    // This will eliminate buildings that allow only one resource to be produced.
                    if (EntityManager.TryGetComponent(companyDetailTooMuch.PropertyPrefab, out BuildingPropertyData buildingPropertyData) &&
                        (buildingPropertyData.m_AllowedManufactured & resourceNotEnough) != 0)
                    {
                        // Save this valid company with its total worth.
                        validCompanies.Add(new CompanyTotalWorth
                        {
                            CompanyDetail = companyDetailTooMuch,
                            TotalWorth = GetCompanyTotalWorth(companyDetailTooMuch.CompanyEntity)
                        });
                    }
                }
            }

            // Check for no valid companies.
            if (validCompanies.Count == 0)
            {
                // Finding no valid companies is not an error.
                // It simply means no companies meet all the conditions.
                companyDetailFound = new();
                return false;
            }

            // Choose the valid too much company that is closest to bankruptcy (i.e. has lowest total worth).
            validCompanies.Sort();
            companyDetailFound = validCompanies[0].CompanyDetail;
            return true;
        }

        /// <summary>
        /// Get company total worth.
        /// </summary>
        private int GetCompanyTotalWorth(Entity companyEntity)
        {
            // Logic adapted from CompanyMoveAwaySystem.CheckMoveAwayJob.
            if (EntityManager.TryGetBuffer(companyEntity, true, out DynamicBuffer<Resources> resources))
            {
                if (EntityManager.TryGetBuffer(companyEntity, true, out DynamicBuffer<OwnedVehicle> ownedVehicles))
				{
					return EconomyUtils.GetCompanyTotalWorth(
                        resources, ownedVehicles, ref _bufferLookupLayoutElement, ref _componentLookupDeliveryTruck, _resourcePrefabs, ref _componentLookupResourceData);
				}
				else
				{
					return EconomyUtils.GetCompanyTotalWorth(
                        resources, _resourcePrefabs, ref _componentLookupResourceData);
				}
            }
            return 0;
        }

        /// <summary>
        /// Change the too much company to the not enough company.
        /// </summary>
        private void ChangeCompany(CompanyDetail companyDetailTooMuch, Resource resourceNotEnough, ResourceSurpluses resourceSurplusesAll)
        { 
            // Get the "not enough" company prefab based on the company infos for the not enough resource.
            Entity companyPrefabNotEnough = Entity.Null;
            CompanyInfos companyInfosNotEnough = _changeCompanySection.GetCompanyInfosForResource(resourceNotEnough);
            if (companyInfosNotEnough.Count == 1)
            {
                // Use the one company prefab.
                companyPrefabNotEnough = companyInfosNotEnough[0].CompanyPrefab;
            }
            else
            {
                // Get the company prefab that has the greatest surplus of input resources.
                // This is the resource of the new company, so it is desired to use more of the resource with the greatest surplus.
                int maxSurplus = int.MinValue;
                foreach (CompanyInfo companyInfo in companyInfosNotEnough)
                {
                    // Compute the surplus of input resources for this company info.
                    int surplus = ComputeSurplusOfInputResources(companyInfo, resourceSurplusesAll);

                    // Check for new max surplus.
                    if (surplus > maxSurplus)
                    {
                        // Use this company prefab.
                        companyPrefabNotEnough = companyInfo.CompanyPrefab;
                        maxSurplus = surplus;
                    }
                }
            }

            // Request the company change.
            if (companyPrefabNotEnough != Entity.Null)
            {
                _changeCompanySystem.ChangeCompany(new ChangeCompanyData
                {
                    RequestType      = RequestType.ChangeOneToSpecified,
                    NewCompanyPrefab = companyPrefabNotEnough,
                    PropertyEntity   = companyDetailTooMuch.PropertyEntity,
                    PropertyPrefab   = companyDetailTooMuch.PropertyPrefab,
                    PropertyType     = _changeCompanySection.GetPropertyType(companyDetailTooMuch.PropertyEntity)
                });
            }
        }

        /// <summary>
        /// Compute the surplus of the input resources of a company info.
        /// </summary>
        private int ComputeSurplusOfInputResources(CompanyInfo companyInfo, ResourceSurpluses resourceSurplusesAll)
        {
            // Get the input resources from the company info.
            Resource resourceInput1 = companyInfo.ResourceInput1;
            Resource resourceInput2 = companyInfo.ResourceInput2;

            // Accumulate the surplus from all resource surpluses that match the input resources.
            int surplus = 0;
            foreach (ResourceSurplus resourceSurplus in resourceSurplusesAll)
            {
                if (resourceSurplus.Resource == resourceInput1 || resourceSurplus.Resource == resourceInput2)
                {
                    surplus += resourceSurplus.Surplus;
                }
            }

            // Return the surplus.
            return surplus;
        }

        /// <summary>
        /// Set production balance next check date/time for industrial.
        /// </summary>
        public void SetProductionBalanceNextCheckIndustrial()
        {
            SetProductionBalanceNextCheck(
                GetCurrentGameDateTime(),
                Mod.ModSettings.ProductionBalanceEnabledIndustrial,
                Mod.ModSettings.ProductionBalanceCheckIntervalIndustrial,
                _productionBalanceNextCheckIndustrial,
                _productionBalanceNextCheckLockIndustrial,
                _productionBalanceInfoIndustrial,
                _productionBalanceInfoLockIndustrial);
        }

        /// <summary>
        /// Set production balance next check date/time for office.
        /// </summary>
        public void SetProductionBalanceNextCheckOffice()
        {
            SetProductionBalanceNextCheck(
                GetCurrentGameDateTime(),
                Mod.ModSettings.ProductionBalanceEnabledOffice,
                Mod.ModSettings.ProductionBalanceCheckIntervalOffice,
                _productionBalanceNextCheckOffice,
                _productionBalanceNextCheckLockOffice,
                _productionBalanceInfoOffice,
                _productionBalanceInfoLockOffice);
        }

        /// <summary>
        /// Set the date/time for a production balance next check.
        /// </summary>
        private void SetProductionBalanceNextCheck(
            GameDateTime currentGameDateTime,
            bool productionBalanceEnabled,
            int productionBalanceCheckInterval,
            GameDateTime productionBalanceNextCheck,
            object productionBalanceNextCheckLock,
            ProductionBalanceInfo productionBalanceInfo,
            object productionBalanceInfoLock)
        {
            // Lock thread while writing production balance next check date/time.
            lock (productionBalanceNextCheckLock)
            {
                // Check if in game and production balance is enabled.
                if (_inGame && productionBalanceEnabled)
                {
                    // Production balance is enabled.
                    // Production balance next check is performed at current game date/time plus check interval minutes.
                    // Use a copy to prevent changes to the original current game date/time.
                    productionBalanceNextCheck.CopyFrom(currentGameDateTime);
                    productionBalanceNextCheck.Minute += productionBalanceCheckInterval;

                    // Handle rollover of hour.
                    // Minute is from 0 to 59, so rollover is at 60.
                    while (productionBalanceNextCheck.Minute >= 60)
                    {
                        productionBalanceNextCheck.Minute -= 60;
                        productionBalanceNextCheck.Hour++;
                    }

                    // Handle rollover of month.
                    // Hour is from 0 to 23, so rollover is at 24.
                    while (productionBalanceNextCheck.Hour >= 24)
                    {
                        productionBalanceNextCheck.Hour -= 24;
                        productionBalanceNextCheck.Month++;
                    }

                    // Handle rollover of year.
                    // Month is from 1 to 12, so rollover is at 13.
                    while (productionBalanceNextCheck.Month >= 13)
                    {
                        productionBalanceNextCheck.Month -= 12;
                        productionBalanceNextCheck.Year++;
                    }
                }
                else
                {
                    // Not in game or production balance is disabled.
                    // Use no production balance next check date/time.
                    productionBalanceNextCheck = new();
                }

                // Lock thread while writing production balance info.
                lock (productionBalanceInfoLock)
                {
                    productionBalanceInfo.NextCheckDateTime = productionBalanceNextCheck.Copy();
                }
            }
        }

        /// <summary>
        /// Get the current game date/time values.
        /// </summary>
        private GameDateTime GetCurrentGameDateTime()
        {
            // Get the current game date/time from the time system.
            DateTime currentDateTime = _timeSystem.GetCurrentDateTime();

            // This mod uses only year, month, hour, and minute.
            // The game stores the month in DateTime.DayOfYear field.
            return new GameDateTime
            {
                Year   = currentDateTime.Year,
                Month  = currentDateTime.DayOfYear,     // 1 to 12
                Hour   = currentDateTime.Hour,          // 0 to 23
                Minute = currentDateTime.Minute,        // 0 to 59
            };
        }

        /// <summary>
        /// Get production balance infos.
        /// </summary>
        public void GetProductionBalanceInfos(
            out ProductionBalanceInfo productionBalanceInfoIndustrial,
            out ProductionBalanceInfo productionBalanceInfoOffice)
        {
            // Return copies of production balance info.
            // Copies are returned to prevent writing to these while the UI system is reading from them.

            // Lock thread while reading production balance info.
            lock (_productionBalanceInfoLockIndustrial)
            {
                productionBalanceInfoIndustrial = _productionBalanceInfoIndustrial.Copy();
            }
            lock (_productionBalanceInfoLockOffice)
            {
                productionBalanceInfoOffice = _productionBalanceInfoOffice.Copy();
            }
        }
    }
}
