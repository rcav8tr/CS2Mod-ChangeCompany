using Colossal.Entities;
using Game;
using Game.Agents;
using Game.Buildings;
using Game.Common;
using Game.Companies;
using Game.Net;
using Game.Notifications;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Triggers;
using Game.UI.InGame;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace ChangeCompany
{
    /// <summary>
    /// Change the company on a property.
    /// </summary>
    public partial class ChangeCompanySystem : GameSystemBase
    {
        // Other systems.
        private EndFrameBarrier         _endFrameBarrier;
        private IconCommandSystem       _iconCommandSystem;
        private PrefabSystem            _prefabSystem;
        private SelectedInfoUISystem    _selectedInfoUISystem;
        private TriggerSystem           _triggerSystem;

        // Entity archetypes.
        private EntityArchetype _rentersUpdatedEventArchetype;
        private EntityArchetype _pathTargetMovedEventArchetype;

        // Entity queries.
        private EntityQuery     _economyParameterDataQuery;
        private EntityQuery     _workProviderParameterDataQuery;

        // Data from ChangeCompanySection for changing the company on a property
        private readonly object _changeCompanyLock = new object();  // Used to lock the thread while writing or reading the change company data.
        private Entity          _changeCompanyNewCompanyPrefab;     // The prefab to use for the new company.
        private Entity          _changeCompanyPropertyEntity;       // The property entity to be changed.
        private Entity          _changeCompanyPropertyPrefab;       // The property prefab to be changed.
        private PropertyType    _changeCompanyPropertyType;         // The property type of the property to be changed.

        // Data for post-change initialization.
        private Entity _postChangePropertyEntity;

        // Miscellaneous.
        private bool _inGame;       // Whether or not application is in a game (i.e. as opposed to main menu, editor, etc.).
        private bool _initialized;  // Whether or not this system is initialized.

        /// <summary>
        /// Initialize this system.
        /// </summary>
        [Preserve]
        protected override void OnCreate()
        { 
            Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(OnCreate)}");

            base.OnCreate();
            
            try
            {
                // Get other systems.
                _endFrameBarrier        = World.GetOrCreateSystemManaged<EndFrameBarrier     >();
                _iconCommandSystem      = World.GetOrCreateSystemManaged<IconCommandSystem   >();
                _prefabSystem           = World.GetExistingSystemManaged<PrefabSystem        >();
                _selectedInfoUISystem   = World.GetExistingSystemManaged<SelectedInfoUISystem>();
                _triggerSystem          = World.GetOrCreateSystemManaged<TriggerSystem       >();

                // Initialize event archetypes.
                // Copied from PropertyProcessingSystem.
                _rentersUpdatedEventArchetype = base.EntityManager.CreateArchetype(
                    ComponentType.ReadWrite<Event>(),
                    ComponentType.ReadWrite<RentersUpdated>());
                _pathTargetMovedEventArchetype = base.EntityManager.CreateArchetype(
                    ComponentType.ReadWrite<Event>(),
                    ComponentType.ReadWrite<PathTargetMoved>());

                // Initialize EconomyParameterData entity query.
                // Query copied from PropertyProcessingSystem.
                _economyParameterDataQuery = GetEntityQuery(
                    ComponentType.ReadOnly<EconomyParameterData>());
            
                // Initialize WorkProviderParameterData entity query.
                // Query adapted from CompanyMoveAwaySystem.
                _workProviderParameterDataQuery = GetEntityQuery(new EntityQueryDesc
                {
                    All = new ComponentType[1] { ComponentType.ReadOnly<WorkProviderParameterData>() },
                    Options = EntityQueryOptions.IncludeSystems
                });

                // Initialize change company data.
                ResetChangeCompanyData();

                // Initialize post-change data.
                _postChangePropertyEntity = Entity.Null;

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
            Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(OnGameLoadingComplete)} mode={mode}");

            // If started a game, then initialize.
            if (mode == GameMode.Game)
            {
                // Reset change company data to prevent processing of any change company request from a previous game.
                ResetChangeCompanyData();

                // Reset post-change data.
                _postChangePropertyEntity = Entity.Null;

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
        /// Request this system to change the company on the property.
        /// </summary>
        public void ChangeCompany(Entity newCompanyPrefab, Entity propertyEntity, Entity propertyPrefab, PropertyType propertyType)
        {
            // Lock the thread while writing change company data.
            lock(_changeCompanyLock)
            {
                _changeCompanyNewCompanyPrefab = newCompanyPrefab;
                _changeCompanyPropertyEntity   = propertyEntity;
                _changeCompanyPropertyPrefab   = propertyPrefab;
                _changeCompanyPropertyType     = propertyType;
            }
        }
        
        /// <summary>
        /// Reset change company data.
        /// </summary>
        private void ResetChangeCompanyData()
        {
            // Lock thread while writing change company data.
            lock(_changeCompanyLock)
            {
                // Clear change company data to indicate there is no change company requested.
                _changeCompanyNewCompanyPrefab = Entity.Null;
                _changeCompanyPropertyEntity   = Entity.Null;
                _changeCompanyPropertyPrefab   = Entity.Null;
                _changeCompanyPropertyType     = PropertyType.None; 
            }
        }

        /// <summary>
        /// Called every frame, even when at the main menu.
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

            // Check if post-change initialization should be performed from the previous frame.
            CheckPostChangeInitialization();

            // Lock the thread while reading change company data.
            Entity newCompanyPrefab;
            Entity propertyEntity;
            Entity propertyPrefab;
            PropertyType propertyType;
            lock (_changeCompanyLock)
            {
                // Use property type to check if change company was requested.
                if (_changeCompanyPropertyType == PropertyType.None)
                {
                    // This is not an error.
                    // There is simply no pending change company request.
                    return;
                }

                // Get change company data.
                newCompanyPrefab = _changeCompanyNewCompanyPrefab;
                propertyEntity   = _changeCompanyPropertyEntity;
                propertyPrefab   = _changeCompanyPropertyPrefab;
                propertyType     = _changeCompanyPropertyType;
            }

            // Reset change company data to prevent processing this change company request again.
            ResetChangeCompanyData();

            // Info logging.
            Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(OnUpdate)} newCompanyPrefab={_prefabSystem.GetPrefabName(newCompanyPrefab)}");
            Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(OnUpdate)} propertyPrefab  ={_prefabSystem.GetPrefabName(propertyPrefab)}");
            Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(OnUpdate)} propertyType    ={propertyType}");

            // Process the change company request.
            // This logic will be executed only occasionally based on a user action.
            // This logic acts on only one property (building).
            // This logic acts on only the current company (if any) and the newly created company.
            // There are not many operations being performed on the property and the two companies.
            // Therefore, performance is not critical.
            // Because performance is not critical, a job is not used.
            try
            {
                // Create an entity command buffer on the EndFrameBarrier system.
                EntityCommandBuffer entityCommandbuffer = _endFrameBarrier.CreateCommandBuffer();

                // Move away the current company of the property.
                MoveAwayCompany(propertyEntity, entityCommandbuffer);
            
                // Create a new company and assign it to the property.
                CreateCompany(newCompanyPrefab, propertyEntity, propertyPrefab, propertyType, entityCommandbuffer);

                // Update selected info in UI.
                _selectedInfoUISystem.RequestUpdate();
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Move away the current company of the property.
        /// </summary>
        private void MoveAwayCompany(Entity propertyEntity, EntityCommandBuffer entityCommandbuffer)
        {
            // The logic below is similar to as if the game moved away the company,
            // except that the property is not put back on the market.
            // Logic adapted from CompanyMoveAwaySystem.CheckMoveAwayJob and CompanyMoveAwaySystem.MovingAwayJob.

            // Get company to move away, if any.
            if (!TryGetCompanyAtProperty(propertyEntity, out Entity companyToMoveAway))
            {
                // This is not an error.
                // It simply means there is no current company to move away.
                Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(MoveAwayCompany)} company to move away=none");
                return;
            }

            // Info logging.
            if (EntityManager.TryGetComponent(companyToMoveAway, out PrefabRef prefabRef))
            {
                Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(MoveAwayCompany)} company to move away={_prefabSystem.GetPrefabName(prefabRef.m_Prefab)}");
            }

            // Add the MovingAway and Deleted components to the company.
            ComponentTypeSet componentsMovingAwayDeleted = new ComponentTypeSet(
                ComponentType.ReadOnly<MovingAway>(),
                ComponentType.ReadOnly<Deleted   >());
            entityCommandbuffer.AddComponent(companyToMoveAway, in componentsMovingAwayDeleted);

            // Remove work provider education notification icons from the property, if any.
            // The WorkProvider notification entity is the property entity.
            if (EntityManager.TryGetComponent(companyToMoveAway, out WorkProvider workProvider))
            {
                WorkProviderParameterData workProviderParameterData = _workProviderParameterDataQuery.GetSingleton<WorkProviderParameterData>();
                IconCommandBuffer iconCommandBuffer = _iconCommandSystem.CreateCommandBuffer();
                if (workProvider.m_EducatedNotificationEntity != Entity.Null)
                {
                    iconCommandBuffer.Remove(workProvider.m_EducatedNotificationEntity, workProviderParameterData.m_EducatedNotificationPrefab);
                }
                if (workProvider.m_UneducatedNotificationEntity != Entity.Null)
                {
                    iconCommandBuffer.Remove(workProvider.m_UneducatedNotificationEntity, workProviderParameterData.m_UneducatedNotificationPrefab);
                }
            }

            // The company is now marked as moving away and deleted.
            // Let the normal game logic complete the removal of the company.
            // The Deleted component on the company will prevent the company
            // from being used for anything before it is destroyed.
        }

        /// <summary>
        /// Create a new company and assign it to the property.
        /// </summary>
        private void CreateCompany(
            Entity newCompanyPrefab,
            Entity propertyEntity,
            Entity propertyPrefab,
            PropertyType propertyType,
            EntityCommandBuffer entityCommandbuffer)
        {
            // Spawn a new company using the ArchetypeData of the company prefab.
            // Logic adapted from CommercialSpawnSystem.SpawnCompanyJob and IndustrialSpawnSystem.CheckSpawnJob.
            ArchetypeData newCompanyArchetypeData = EntityManager.GetComponentData<ArchetypeData>(newCompanyPrefab);
            Entity newCompanyEntity = entityCommandbuffer.CreateEntity(newCompanyArchetypeData.m_Archetype);

            // IMPORTANT:  The new company entity is only temporary (i.e. not realized) until the entity command buffer is played back.
            // Therefore, operations can be performed on the new company entity only using the entity command buffer.
            
            // Set the prefab on the new company.
            // All new companies already have a PrefabRef that can be set.
            // Logic adapted from CommercialSpawnSystem.SpawnCompanyJob and IndustrialSpawnSystem.CheckSpawnJob.
            entityCommandbuffer.SetComponent(newCompanyEntity, new PrefabRef { m_Prefab = newCompanyPrefab });

            // Disable PropertySeeker on the new company so the company cannot be added to a different property.
            // All new companies already have a PropertySeeker component.
            // Logic adapted from PropertyUtils.CompanyFindPropertyJob.SelectProperty().
            entityCommandbuffer.SetComponentEnabled<PropertySeeker>(newCompanyEntity, false);

            // The game normally calls PropertyUtils.CompanyFindPropertyJob to find a property for a company.
            // The logic in PropertyUtils.CompanyFindPropertyJob.SelectProperty() queues a new RentAction.
            // PropertyProcessingSystem.PropertyRentJob handles the queued RentAction entries
            // to perform some of the initialization of the new company on the property.
            // However, PropertyProcessingSystem runs only in GameSimulation phase.
            // It is highly desired for the user to be able to change a company while the simulation is paused.
            // Therefore, instead of queueing a new RentAction and waiting for the simulation to run,
            // this mod will initialize the company using the same logic that would have been performed by
            // PropertyProcessingSystem.PropertyRentJob if the RentAction had been queued.
            InitializeCompany(
                newCompanyEntity,
                newCompanyPrefab,
                propertyEntity,
                propertyPrefab,
                propertyType,
                entityCommandbuffer);

            // Some company initialization logic is performed by CompanyInitializeSystem.InitializeCompanyJob.
            // InitializeCompanyJob runs in Modification5 phase on companies with Created component.
            // Because all newly created companies have Created component and because Modification5 phase always runs,
            // InitializeCompanyJob will complete the initialization of this mod's newly created company.
            // Therefore, none of the logic from InitializeCompanyJob needs to be performed here.
        }

        /// <summary>
        /// Initialize a new company on the property.
        /// Logic adapted from PropertyProcessingSystem.PropertyRentJob.
        /// Logic not related to initializing a new company is excluded.
        /// </summary>
        private void InitializeCompany(
            Entity newCompanyEntity,
            Entity newCompanyPrefab,
            Entity propertyEntity,
            Entity propertyPrefab,
            PropertyType propertyType,
            EntityCommandBuffer entityCommandbuffer)
        {
            // Get SpawnableBuildingData for the property prefab.
            if (!EntityManager.TryGetComponent(propertyPrefab, out SpawnableBuildingData spawnableBuildingData))
            {
                // This should never happen.
                PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
                Mod.log.Warn($"SpawnableBuildingData not found on selected property prefab [{propertyPrefab}] [{prefabSystem.GetPrefabName(propertyPrefab)}].");
                return;
            }
                
            // Get BuildingPropertyData for the property prefab.
            if (!EntityManager.TryGetComponent(propertyPrefab, out BuildingPropertyData buildingPropertyData))
            {
                // This should never happen.
                PrefabSystem prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
                Mod.log.Warn($"BuildingPropertyData not found on selected property prefab [{propertyPrefab}] [{prefabSystem.GetPrefabName(propertyPrefab)}].");
                return;
            }

            // Add PropertyRenter to the new company.
            int rentPricePerRenter = GetRentPricePerRenter(propertyEntity, propertyPrefab, spawnableBuildingData, buildingPropertyData);
            entityCommandbuffer.AddComponent(newCompanyEntity, new PropertyRenter
            {
                m_Property = propertyEntity,
                m_Rent = rentPricePerRenter
            });

            // Add the new company to the property's Renter buffer.
            entityCommandbuffer.AppendToBuffer(propertyEntity, new Renter { m_Renter = newCompanyEntity });

            // Set the initial max workers on the new company.
            // The CommercialAISystem and IndustrialAISystem may adjust the max workers up or down during simulation.
            // Storage companies do not have workers.
            if (propertyType != PropertyType.Storage)
            {
                int companyMaxFittingWorkers = GetCompanyMaxFittingWorkers(
                    newCompanyPrefab, propertyPrefab, propertyType, spawnableBuildingData, buildingPropertyData);
                int maxWorkers = math.max(1, 2 * companyMaxFittingWorkers / 3);
                entityCommandbuffer.SetComponent(newCompanyEntity, new WorkProvider { m_MaxWorkers = maxWorkers });
            }

            // Remove PropertyOnMarket from the property.
            if (EntityManager.HasComponent<PropertyOnMarket>(propertyEntity))
            {
                entityCommandbuffer.RemoveComponent<PropertyOnMarket>(propertyEntity);
            }

            // Create a RentersUpdated event on the property.
            Entity rentersUpdatedEventArchetypeEntity = entityCommandbuffer.CreateEntity(_rentersUpdatedEventArchetype);
            entityCommandbuffer.SetComponent(rentersUpdatedEventArchetypeEntity, new RentersUpdated(propertyEntity));

            // Do post-change initialization.
            _postChangePropertyEntity = propertyEntity;
        }

        /// <summary>
        /// Get rent price per renter.
        /// Logic adapted from PropertyProcessingSystem.PutPropertyOnMarketJob.
        /// </summary>
        private int GetRentPricePerRenter(
            Entity propertyEntity,
            Entity propertyPrefab,
            SpawnableBuildingData spawnableBuildingData,
            BuildingPropertyData buildingPropertyData)
        {
            // Get building lot size.
            int lotSize = 4;
            if (EntityManager.TryGetComponent(propertyPrefab, out BuildingData buildingData))
            {
                lotSize = buildingData.m_LotSize.x * buildingData.m_LotSize.y;
            }
            else
            {
                // This should never happen.
                Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(GetRentPricePerRenter)} unable to get lot size for property prefab={_prefabSystem.GetPrefabName(propertyPrefab)}");
            }

            // Get land value.
            float landValueBase = 0f;
            if (EntityManager.TryGetComponent(propertyEntity, out Building building) &&
                EntityManager.TryGetComponent(building.m_RoadEdge, out LandValue landValue))
            {
                landValueBase = landValue.m_LandValue;
            }
            else
            {
                // This should never happen.
                Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(GetRentPricePerRenter)} unable to get land value for property entity={propertyEntity}");
            }

            // Get area type.
            Game.Zones.AreaType areaType = Game.Zones.AreaType.None;
            if (EntityManager.TryGetComponent(spawnableBuildingData.m_ZonePrefab, out ZoneData zoneData))
            {
                areaType = zoneData.m_AreaType;
            }
            else
            {
                // This should never happen.
                Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(GetRentPricePerRenter)} unable to get area type");
            }

            // Get economy parameter data.
            EconomyParameterData economyParameterData = _economyParameterDataQuery.GetSingleton<EconomyParameterData>();

            // Compute and return rent price per renter.
            return PropertyUtils.GetRentPricePerRenter(
                buildingPropertyData, spawnableBuildingData.m_Level, lotSize, landValueBase, areaType, ref economyParameterData);
        }

        /// <summary>
        /// Get company max fitting workers.
        /// Logic adapted from CompanyUtils.GetCompanyMaxFittingWorkers.
        /// </summary>
        private int GetCompanyMaxFittingWorkers(
            Entity newCompanyPrefab,
            Entity propertyPrefab,
            PropertyType propertyType,
            SpawnableBuildingData spawnableBuildingData,
            BuildingPropertyData buildingPropertyData)
        {
            // Check for building data.
            if (EntityManager.TryGetComponent(propertyPrefab, out BuildingData buildingData))
            {
                // Get max fitting workers for commercial.
                if (propertyType == PropertyType.Commercial)
                {
                    if (EntityManager.TryGetComponent(newCompanyPrefab, out ServiceCompanyData serviceCompanyData))
                    {
                        return CompanyUtils.GetCommercialMaxFittingWorkers(
                            buildingData, buildingPropertyData, spawnableBuildingData.m_Level, serviceCompanyData);
                    }
                    else
                    {
                        // This should never happen.
                        Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(GetCompanyMaxFittingWorkers)} unable to get service company data for company prefab={_prefabSystem.GetPrefabName(newCompanyPrefab)}");
                        return 1;
                    }
                }

                // Get max fitting workers for industrial, which includes office.
                if (EntityManager.TryGetComponent(newCompanyPrefab, out IndustrialProcessData industrialProcessData))
                {
                    return CompanyUtils.GetIndustrialAndOfficeFittingWorkers(
                        buildingData, buildingPropertyData, spawnableBuildingData.m_Level, industrialProcessData);
                }
                else
                {
                    // This should never happen.
                    Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(GetCompanyMaxFittingWorkers)} unable to get industrial process data for company prefab={_prefabSystem.GetPrefabName(newCompanyPrefab)}");
                    return 1;
                }
            }
            else
            {
                // This should never happen.
                Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(GetCompanyMaxFittingWorkers)} unable to get building data for property prefab={_prefabSystem.GetPrefabName(propertyPrefab)}");
                return 1;
            }
        }

        /// <summary>
        /// Check if post-change initialization should be performed.
        /// </summary>
        private void CheckPostChangeInitialization()
        {
            // Check if a property entity was set for post-change initialization.
            if (_postChangePropertyEntity == Entity.Null)
            {
                return;
            }

            // Perform initializations that require the realized new company entity.
            // At the time this mod performs the logic of PropertyProcessingSystem.PropertyRentJob in InitializeCompany,
            // the company entity has not yet been realized due to the use of the entity command buffer.

            // Get the company entity at the property, which should be there by now.
            if (TryGetCompanyAtProperty(_postChangePropertyEntity, out Entity companyEntity))
            {
                // Get the new company prefab.
                if (EntityManager.TryGetComponent(companyEntity, out PrefabRef companyPrefabRef))
                {
                    // Enqueue a BrandRented TriggerAction on the new company.
                    // The BrandRented TriggerAction causes a Chirper message to be displayed
                    // when certain company types are assigned to a property.
                    NativeQueue<TriggerAction> triggerQueue = _triggerSystem.CreateActionBuffer();
                    triggerQueue.Enqueue(new TriggerAction
                    {
                        m_PrimaryTarget = companyEntity,
                        m_SecondaryTarget = _postChangePropertyEntity,
                        m_TriggerPrefab = companyPrefabRef.m_Prefab,
                        m_TriggerType = TriggerType.BrandRented
                    });
                }

                // Create a new PathTargetMoved event on the new company.
                // Don't know what PathTargetMoved event does, but create it now.
                if (_pathTargetMovedEventArchetype.Valid)
                {
                    EntityCommandBuffer entityCommandbuffer = _endFrameBarrier.CreateCommandBuffer();
                    Entity pathTargetMovedEventArchetypeEntity = entityCommandbuffer.CreateEntity(_pathTargetMovedEventArchetype);
                    entityCommandbuffer.SetComponent(pathTargetMovedEventArchetypeEntity,
                        new PathTargetMoved(companyEntity, default(float3), default(float3)));
                }
            }

            // Do not perform post-change initialization on this property again.
            _postChangePropertyEntity = Entity.Null;
        }

        /// <summary>
        /// Get the company, if any, at a property.
        /// </summary>
        public bool TryGetCompanyAtProperty(Entity propertyEntity, out Entity company)
        {
            // Property must have a Renter buffer.
            if (EntityManager.TryGetBuffer(propertyEntity, true, out DynamicBuffer<Renter> renters))
            {
                // Find the renter that is a company (i.e. not a household in a mixed residential building).
                for (int i = 0; i < renters.Length; i++)
                {
                    // Companies have CompanyData component.
                    if (EntityManager.HasComponent<CompanyData>(renters[i].m_Renter))
                    {
                        // Return the one and only company.
                        company = renters[i].m_Renter;
                        return true;
                    }
                }
            }

            // No company found.
            company = Entity.Null;
            return false;
        }
    }
}
