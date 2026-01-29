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
using Game.Tools;
using Game.Triggers;
using Game.UI.InGame;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using AreaType = Game.Zones.AreaType;

namespace ChangeCompany
{
    /// <summary>
    /// Change the company on a property.
    /// </summary>
    public partial class ChangeCompanySystem : GameSystemBase
    {
        // Other systems.
        private ChangeCompanySection    _changeCompanySection;
        private EndFrameBarrier         _endFrameBarrier;
        private IconCommandSystem       _iconCommandSystem;
        private PrefabSystem            _prefabSystem;
        private SelectedInfoUISystem    _selectedInfoUISystem;
        private TriggerSystem           _triggerSystem;

        // Entity archetypes.
        private EntityArchetype _rentersUpdatedEventArchetype;
        private EntityArchetype _pathTargetMovedEventArchetype;

        // Entity queries for companies.
        private EntityQuery _companyQueryCommercial;
        private EntityQuery _companyQueryIndustrial;

        // Data from other systems for changing or removing a company.
        private List<ChangeCompanyData> _changeCompanyDatas;
        private readonly object _changeCompanyDatasLock = new();

        // Data for post-change initialization.
        private List<Entity> _postChangePropertyEntities;

        // Random seed.
        private RandomSeed _randomSeed;

        // Company changed notification icon.
        private Entity _companyChangedNotificationIcon;

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
                _changeCompanySection   = World.GetOrCreateSystemManaged<ChangeCompanySection>();
                _endFrameBarrier        = World.GetOrCreateSystemManaged<EndFrameBarrier     >();
                _iconCommandSystem      = World.GetOrCreateSystemManaged<IconCommandSystem   >();
                _prefabSystem           = World.GetOrCreateSystemManaged<PrefabSystem        >();
                _selectedInfoUISystem   = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
                _triggerSystem          = World.GetOrCreateSystemManaged<TriggerSystem       >();

                // Initialize event archetypes.
                // Copied from PropertyProcessingSystem.
                _rentersUpdatedEventArchetype = base.EntityManager.CreateArchetype(
                    ComponentType.ReadWrite<Event>(),
                    ComponentType.ReadWrite<RentersUpdated>());
                _pathTargetMovedEventArchetype = base.EntityManager.CreateArchetype(
                    ComponentType.ReadWrite<Event>(),
                    ComponentType.ReadWrite<PathTargetMoved>());

                // Queries to get commercial or industrial/office/storage companies.
                // A separate query is used for each to reduce the number of companies to check.
                _companyQueryCommercial = GetEntityQuery(
                    ComponentType.ReadOnly<CompanyData      >(),
                    ComponentType.ReadOnly<PrefabRef        >(),
                    ComponentType.ReadOnly<CommercialCompany>(),
                    ComponentType.ReadOnly<PropertyRenter   >(),
                    ComponentType.Exclude<MovingAway        >(),
                    ComponentType.Exclude<Deleted           >(),
                    ComponentType.Exclude<Temp              >());
                _companyQueryIndustrial = GetEntityQuery(
                    ComponentType.ReadOnly<CompanyData      >(),
                    ComponentType.ReadOnly<PrefabRef        >(),
                    ComponentType.ReadOnly<IndustrialCompany>(),
                    ComponentType.ReadOnly<PropertyRenter   >(),
                    ComponentType.Exclude<MovingAway        >(),
                    ComponentType.Exclude<Deleted           >(),
                    ComponentType.Exclude<Temp              >());

                // Initialize change company datas.
                _changeCompanyDatas = new();

                // Initialize post-change data.
                _postChangePropertyEntities = new();

                // Get a stream for the embedded file for the company changed notification icon.
                // The svg file is the original.
                // The png file was created from the svg file.
                // The dxt5 file was created from the png file.
                // Note that the png and dxt5 files are flipped vertically because
                // for unknown reasons the game flips the image when displayed.
                const string notificationIconFileName = ModAssemblyInfo.Name + ".Images.CompanyChanged.dxt5";
                using (Stream notificationIconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(notificationIconFileName))
                {
                    // Make sure stream was created.
                    if (notificationIconStream == null)
                    {
                        Mod.log.Error($"Notification icon file [{notificationIconFileName}] does not exist in the assembly.");
                        return;
                    }

                    // Read all bytes from the notification icon file.
                    byte[] bufferAll = new byte[notificationIconStream.Length];
                    notificationIconStream.Read(bufferAll, 0, bufferAll.Length);

                    // Remove the DDS header (i.e. first 128 bytes) to get the data only.
                    const int DDSHeaderLength = 128;
                    byte[] bufferDataOnly = new byte[notificationIconStream.Length - DDSHeaderLength];
                    for (int i = 0; i < bufferDataOnly.Length; i++)
                    {
                        bufferDataOnly[i] = bufferAll[i + DDSHeaderLength];
                    }

                    // Convert the data only to a DXT5 texture.
                    const string NotificationIconName = "CompanyChanged";
                    Texture2D textureDXT5 = new(128, 128, TextureFormat.DXT5, true);
                    textureDXT5.name = NotificationIconName;
                    textureDXT5.LoadRawTextureData(bufferDataOnly);
                    textureDXT5.Apply();

                    // Create the notification icon prefab with the DXT5 texture.
                    NotificationIconPrefab notificationIconPrefab = ScriptableObject.CreateInstance<NotificationIconPrefab>();
                    notificationIconPrefab.name                 = NotificationIconName;
                    notificationIconPrefab.m_Icon               = textureDXT5;
                    notificationIconPrefab.m_Description        = NotificationIconName;
                    notificationIconPrefab.m_TargetDescription  = "";
                    notificationIconPrefab.m_DisplaySize        = new(2f, 2f);
                    notificationIconPrefab.m_PulsateAmplitude   = new(0f, 0f);

                    // Add the notification icon prefab to the prefab system.
                    _prefabSystem.AddPrefab(notificationIconPrefab);
                    _companyChangedNotificationIcon = _prefabSystem.GetEntity(notificationIconPrefab);
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
                // Clear change company datas to prevent processing any change company request from a previous game.
                _changeCompanyDatas.Clear();

                // Reset post-change data.
                _postChangePropertyEntities.Clear();

                // Initialize random numbers.
                _randomSeed = RandomSeed.Next();

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
        /// Request this system to change or remove one or all companies.
        /// </summary>
        public void ChangeCompany(ChangeCompanyData changeCompanyData)
        {
            // Lock thread while reading and writing request queue.
            lock (_changeCompanyDatasLock)
            {
                // Check if property of this request is already in the request queue.
                Entity property = changeCompanyData.PropertyEntity;
                foreach (ChangeCompanyData changeCompanyDataInQueue in _changeCompanyDatas)
                {
                    if (changeCompanyDataInQueue.PropertyEntity == property)
                    {
                        // Property is already in the queue.
                        return;
                    }
                }

                // Property is not already in the queue.
                // Add the request to the queue.
                _changeCompanyDatas.Add(changeCompanyData);
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

            // Lock thread while reading and writing request queue.
            List<ChangeCompanyData> changeCompanyDatas = new();
            lock (_changeCompanyDatasLock)
            {
                // Quickly copy requests locally to spend as little time as possible with the thread locked.
                foreach (ChangeCompanyData changeCompanyData in _changeCompanyDatas)
                {
                    changeCompanyDatas.Add(changeCompanyData);
                }
                _changeCompanyDatas.Clear();
            }

            // Check if there are any change company requests.
            if (changeCompanyDatas.Count == 0)
            {
                // This is not an error.
                // There are simply no pending change company requests.
                return;
            }

            // Process each change company request from the local copy.
            foreach (ChangeCompanyData changeCompanyData in changeCompanyDatas)
            {
                try
                {
                    // Process the change company request according to its request type.
                    switch (changeCompanyData.RequestType)
                    {
                        case RequestType.ChangeOneToSpecified:
                            ChangeOneCompanyToSpecified(
                                changeCompanyData.NewCompanyPrefab,
                                changeCompanyData.PropertyEntity,
                                changeCompanyData.PropertyPrefab,
                                changeCompanyData.PropertyType);
                            break;

                        case RequestType.ChangeOneToRandom:
                            ChangeOneCompanyToRandom(
                                changeCompanyData.CompanyInfos,
                                changeCompanyData.PropertyEntity,
                                changeCompanyData.PropertyPrefab,
                                changeCompanyData.PropertyType);
                            break;

                        case RequestType.RemoveOne:
                            RemoveOneCompany(
                                changeCompanyData.PropertyEntity,
                                changeCompanyData.PropertyPrefab);
                            break;

                        case RequestType.ChangeAllToSpecified:
                        case RequestType.ChangeAllToRandom:
                        case RequestType.RemoveAll:
                            ChangeRemoveAllCompanies(
                                changeCompanyData.RequestType,
                                changeCompanyData.NewCompanyPrefab,
                                changeCompanyData.CompanyInfos,
                                changeCompanyData.PropertyEntity,
                                changeCompanyData.PropertyPrefab,
                                changeCompanyData.PropertyType);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Mod.log.Error(ex);
                }
            }

            // Update selected info in UI.
            _selectedInfoUISystem.RequestUpdate();
        }

        /// <summary>
        /// Change one company to the specified new company.
        /// </summary>
        private void ChangeOneCompanyToSpecified(Entity newCompanyPrefab, Entity propertyEntity, Entity propertyPrefab, PropertyType propertyType)
        {
            // Create an entity command buffer on the EndFrameBarrier system.
            EntityCommandBuffer entityCommandbuffer = _endFrameBarrier.CreateCommandBuffer();

            // Move away the current company of the property.
            bool companyWasLocked = MoveAwayCompany(propertyEntity, propertyPrefab, entityCommandbuffer);
            
            // Create a new company and assign it to the property.
            CreateCompany(newCompanyPrefab, propertyEntity, propertyPrefab, propertyType, companyWasLocked, entityCommandbuffer);

            // Display company changed notification icon on the property.
            DisplayCompanyChangedNotificationIcon(propertyEntity);
        }

        /// <summary>
        /// Change one company to a random new company.
        /// </summary>
        private void ChangeOneCompanyToRandom(CompanyInfos companyInfos, Entity propertyEntity, Entity propertyPrefab, PropertyType propertyType)
        {
            // Get a random new company prefab from among the company infos for the property.
            Unity.Mathematics.Random random = _randomSeed.GetRandom(DateTime.Now.Millisecond);
            int randomIndex = random.NextInt(companyInfos.Count);
            Entity newCompanyPrefab = companyInfos[randomIndex].CompanyPrefab;

            // Process as if requested to change one company to a specified new company.
            ChangeOneCompanyToSpecified(newCompanyPrefab, propertyEntity, propertyPrefab, propertyType);
        }

        /// <summary>
        /// Remove one company.
        /// </summary>
        private void RemoveOneCompany(Entity propertyEntity, Entity propertyPrefab)
        {
            // Create an entity command buffer on the EndFrameBarrier system.
            EntityCommandBuffer entityCommandbuffer = _endFrameBarrier.CreateCommandBuffer();

            // Move away the current company of the property.
            // When removing a company, do not care if the company was locked.
            MoveAwayCompany(propertyEntity, propertyPrefab, entityCommandbuffer);

            // If property is on the market, take property off the market.
            if (EntityManager.HasComponent<PropertyOnMarket>(propertyEntity))
            {
                entityCommandbuffer.RemoveComponent<PropertyOnMarket>(propertyEntity);
            }

            // Following logic is adapted from CompanyMoveAwaySystem.MovingAwayJob.
            // This logic completes what the game would have done to move away a company when
            // a new company is not being reassigned immediately like this mod does when changing the company.

            // Add PropertyToBeOnMarket component.
            // Let the normal game logic (PropertyProcessingSystem.PutPropertyOnMarketJob)
            // complete the work of putting the property on the market.
            // The normal game logic includes special handling to assign a company to a signature building.
            // The normal game logic runs only in GameSimulation phase.
            // It is acceptable to wait for the simulation to run before the property is placed on the market.
            entityCommandbuffer.AddComponent(propertyEntity, default(PropertyToBeOnMarket));

            // Create a RentersUpdated event on the property.
            CreateRentersUpdatedEvent(propertyEntity, entityCommandbuffer);

            // Display company changed notification icon on the property.
            DisplayCompanyChangedNotificationIcon(propertyEntity);
        }

        /// <summary>
        /// For all companies that are like the company on the property, do one of the following:
        ///     Change the company to the specified new company.
        ///     Change the company to a random new company.
        ///     Remove the company.
        /// </summary>
        private void ChangeRemoveAllCompanies(
            RequestType requestType,
            Entity newCompanyPrefab,
            CompanyInfos companyInfos,
            Entity propertyEntity,
            Entity propertyPrefab,
            PropertyType propertyType)
        {
            // Get the prefab for the current company on the property.
            if (CompanyUtilities.TryGetCompanyAtProperty(EntityManager, propertyEntity, propertyPrefab, out Entity companyEntity) &&
                EntityManager.TryGetComponent(companyEntity, out PrefabRef companyPrefabRef))
            {
                Entity currentCompanyPrefab = companyPrefabRef.m_Prefab;

                // Check each commercial or industrial/office/storage company.
                EntityQuery companyQuery = propertyType == PropertyType.Commercial ? _companyQueryCommercial : _companyQueryIndustrial;
                foreach (Entity companyToCheckEntity in companyQuery.ToEntityArray(Allocator.Temp))
                {
                    // Use the company prefab to determine if the company to check is "like" the current company.
                    if (EntityManager.TryGetComponent(companyToCheckEntity, out PrefabRef companyToCheckPrefabRef) &&
                        companyToCheckPrefabRef.m_Prefab == currentCompanyPrefab)
                    {
                        // Get property entity and property prefab of the company to check.
                        if (EntityManager.TryGetComponent(companyToCheckEntity, out PropertyRenter propertyRenter) &&
                            EntityManager.TryGetComponent(propertyRenter.m_Property, out PrefabRef propertyPrefabRef))
                        {
                            Entity propertyToCheckEntity = propertyRenter.m_Property;
                            Entity propertyToCheckPrefab = propertyPrefabRef.m_Prefab;

                            // Check for remove company versus change company.
                            if (requestType == RequestType.RemoveAll)
                            {
                                // It was already determined above that the company to check has a property.
                                // Therefore, the company to check can be removed from the property to check.
                                // Process as if requested to remove one company.
                                RemoveOneCompany(propertyToCheckEntity, propertyToCheckPrefab);
                            }
                            else
                            {
                                // Verify company can change on the property to check.
                                if (_changeCompanySection.CompanyCanChange(propertyToCheckEntity, propertyToCheckPrefab))
                                {
                                    // Check for specified versus random.
                                    if (requestType == RequestType.ChangeAllToSpecified)
                                    {
                                        // Process as if requested to change one company to a specified new company.
                                        ChangeOneCompanyToSpecified(newCompanyPrefab, propertyToCheckEntity, propertyToCheckPrefab, propertyType);
                                    }
                                    else
                                    {
                                        // Process as if requested to change one company to a random new company.
                                        ChangeOneCompanyToRandom(companyInfos, propertyToCheckEntity, propertyToCheckPrefab, propertyType);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Move away the current company of the property.
        /// </summary>
        private bool MoveAwayCompany(Entity propertyEntity, Entity propertyPrefab, EntityCommandBuffer entityCommandbuffer)
        {
            // The logic below is similar to as if the game moved away the company,
            // except that the property is not put back on the market.
            // Logic adapted from CompanyMoveAwaySystem.CheckMoveAwayJob and CompanyMoveAwaySystem.MovingAwayJob.

            // Get company to move away, if any.
            if (!CompanyUtilities.TryGetCompanyAtProperty(EntityManager, propertyEntity, propertyPrefab, out Entity companyToMoveAway))
            {
                // This is not an error.
                // It simply means there is no current company to move away.
                return false;
            }

            // Add the MovingAway and Deleted components to the company.
            ComponentTypeSet componentsMovingAwayDeleted = new(
                ComponentType.ReadOnly<MovingAway>(),
                ComponentType.ReadOnly<Deleted   >());
            entityCommandbuffer.AddComponent(companyToMoveAway, in componentsMovingAwayDeleted);

            // Remove work provider education notification icons from the property, if any.
            // The WorkProvider notification entity is the property entity.
            if (EntityManager.TryGetComponent(companyToMoveAway, out WorkProvider workProvider))
            {
                // Query adapted from CompanyMoveAwaySystem.
			    EntityQuery workProviderParameterDataQuery = SystemAPI.QueryBuilder()
                    .WithAll<WorkProviderParameterData>()
                    .WithOptions(EntityQueryOptions.IncludeSystems)
                    .Build();

                WorkProviderParameterData workProviderParameterData = workProviderParameterDataQuery.GetSingleton<WorkProviderParameterData>();
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

            // Return whether or not the company is locked.
            return EntityManager.HasComponent<CompanyLocked>(companyToMoveAway);
        }

        /// <summary>
        /// Create a new company and assign it to the property.
        /// </summary>
        private void CreateCompany(
            Entity newCompanyPrefab,
            Entity propertyEntity,
            Entity propertyPrefab,
            PropertyType propertyType,
            bool companyWasLocked,
            EntityCommandBuffer entityCommandbuffer)
        {
            // Spawn a new company using the ArchetypeData of the company prefab.
            // Logic adapted from CommercialSpawnSystem.SpawnCompanyJob and IndustrialSpawnSystem.CheckSpawnJob.
            ArchetypeData newCompanyArchetypeData = EntityManager.GetComponentData<ArchetypeData>(newCompanyPrefab);
            Entity newCompanyEntity = entityCommandbuffer.CreateEntity(newCompanyArchetypeData.m_Archetype);

            // IMPORTANT:  The new company entity is only temporary (i.e. not realized) until the entity command buffer is played back.
            // Therefore, operations can be performed on the new company entity only using the entity command buffer.
            // For operations that require the realized company entity, see CheckPostChangeInitialization.
            
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

            // If previous company was locked or option is set to lock after change, lock the new company.
            if (companyWasLocked || Mod.ModSettings.LockAfterChange)
            {
                entityCommandbuffer.AddComponent<CompanyLocked>(newCompanyEntity);
            }

            // Prevent property from being placed back on the market.
            // This handles the case where a company was removed by this mod, which adds PropertyToBeOnMarket,
            // and then the company is changed by this mod before the simulation has a chance to run
            // and actually place the property on the market.
            if (EntityManager.HasComponent<PropertyToBeOnMarket>(propertyEntity))
            {
                entityCommandbuffer.RemoveComponent<PropertyToBeOnMarket>(propertyEntity);
            }

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
                Mod.log.Warn($"{nameof(ChangeCompanySystem)}.{nameof(InitializeCompany)} SpawnableBuildingData not found on selected property prefab [{propertyPrefab}] [{_prefabSystem.GetPrefabName(propertyPrefab)}].");
                return;
            }
            
            // Get BuildingPropertyData for the property prefab.
            if (!EntityManager.TryGetComponent(propertyPrefab, out BuildingPropertyData buildingPropertyData))
            {
                // This should never happen.
                Mod.log.Warn($"{nameof(ChangeCompanySystem)}.{nameof(InitializeCompany)} BuildingPropertyData not found on selected property prefab [{propertyPrefab}] [{_prefabSystem.GetPrefabName(propertyPrefab)}].");
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
            CreateRentersUpdatedEvent(propertyEntity, entityCommandbuffer);

            // Do post-change initialization on this property.
            _postChangePropertyEntities.Add(propertyEntity);
        }

        /// <summary>
        /// Create a RentersUpdated event on the property.
        /// </summary>
        private void CreateRentersUpdatedEvent(Entity propertyEntity, EntityCommandBuffer entityCommandbuffer)
        {
            // The RentersUpdated event is processed by RemovedSystem.RentersUpdateJob.
            // RentersUpdateJob does things like remove the renter from the Renter buffer
            // and clear the high rent notification from the building.
            Entity rentersUpdatedEventArchetypeEntity = entityCommandbuffer.CreateEntity(_rentersUpdatedEventArchetype);
            entityCommandbuffer.SetComponent(rentersUpdatedEventArchetypeEntity, new RentersUpdated(propertyEntity));
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
                Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(GetRentPricePerRenter)} Unable to get lot size for property prefab [{_prefabSystem.GetPrefabName(propertyPrefab)}].");
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
                Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(GetRentPricePerRenter)} Unable to get land value for property entity [{propertyEntity}].");
            }

            // Get area type.
            AreaType areaType = AreaType.None;
            if (EntityManager.TryGetComponent(spawnableBuildingData.m_ZonePrefab, out ZoneData zoneData))
            {
                areaType = zoneData.m_AreaType;
            }
            else
            {
                // This should never happen.
                Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(GetRentPricePerRenter)} Unable to get area type.");
            }

            // Get economy parameter data.
            EconomyParameterData economyParameterData = SystemAPI.GetSingleton<EconomyParameterData>();

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
                        Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(GetCompanyMaxFittingWorkers)} Unable to get service company data for company prefab [{_prefabSystem.GetPrefabName(newCompanyPrefab)}].");
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
                    Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(GetCompanyMaxFittingWorkers)} Unable to get industrial process data for company prefab [{_prefabSystem.GetPrefabName(newCompanyPrefab)}]");
                    return 1;
                }
            }
            else
            {
                // This should never happen.
                Mod.log.Info($"{nameof(ChangeCompanySystem)}.{nameof(GetCompanyMaxFittingWorkers)} Unable to get building data for property prefab [{_prefabSystem.GetPrefabName(propertyPrefab)}]");
                return 1;
            }
        }

        /// <summary>
        /// Display the company changed notification icon on the property.
        /// </summary>
        private void DisplayCompanyChangedNotificationIcon(Entity propertyEntity)
        {
            // Logic adapted from Game.Simulation.BuildingUpkeepSystem.LevelupJob
            IconCommandBuffer iconCommandBuffer = _iconCommandSystem.CreateCommandBuffer();
            iconCommandBuffer.Add(
                propertyEntity,
                _companyChangedNotificationIcon,
                IconPriority.Info,
                IconClusterLayer.Transaction);
        }

        /// <summary>
        /// Check if post-change initialization should be performed.
        /// </summary>
        private void CheckPostChangeInitialization()
        {
            // Check if any property entities were set for post-change initialization.
            if (_postChangePropertyEntities.Count == 0)
            {
                return;
            }

            // Do each property entity that was set for post-change initialization.
            EntityCommandBuffer entityCommandbuffer = _endFrameBarrier.CreateCommandBuffer();
            foreach (Entity propertyEntity in _postChangePropertyEntities)
            {
                // Mark the property as updated so billboards are updated for the new brand.
                entityCommandbuffer.AddComponent<Updated>(propertyEntity);

                // Perform initializations that require the realized new company entity.
                // At the time this mod performs the logic of PropertyProcessingSystem.PropertyRentJob in InitializeCompany,
                // the company entity has not yet been realized due to the use of the entity command buffer.
                // This logic executes in the next frame, by which time the new company entity should be realized.

                // Get the company entity at the property.
                if (EntityManager.TryGetComponent(propertyEntity, out PrefabRef propertyPrefabRef) &&
                    CompanyUtilities.TryGetCompanyAtProperty(EntityManager, propertyEntity, propertyPrefabRef.m_Prefab, out Entity companyEntity))
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
                            m_SecondaryTarget = propertyEntity,
                            m_TriggerPrefab = companyPrefabRef.m_Prefab,
                            m_TriggerType = TriggerType.BrandRented
                        });
                    }

                    // Create a new PathTargetMoved event on the new company.
                    // Don't know what PathTargetMoved event does, but create it now.
                    if (_pathTargetMovedEventArchetype.Valid)
                    {
                        Entity pathTargetMovedEventArchetypeEntity = entityCommandbuffer.CreateEntity(_pathTargetMovedEventArchetype);
                        entityCommandbuffer.SetComponent(pathTargetMovedEventArchetypeEntity,
                            new PathTargetMoved(companyEntity, default(float3), default(float3)));
                    }
                }
            }

            // Do not perform post-change initialization on these properties again.
            _postChangePropertyEntities.Clear();
        }
    }
}
