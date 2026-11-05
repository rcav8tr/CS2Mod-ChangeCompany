using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Economy;
using Game.Simulation;
using Game.UI;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;

namespace ChangeCompany
{
    /// <summary>
    /// Production and surplus amounts by resource.
    /// This system is used in Resource Locator and Change Company mods.
    /// </summary>
    public partial class ProductionSurplusSystem : UISystemBase
    {
        // Define resource count from EconomyUtils.ResourceCount.
        // This system checks if the game has changed from this value.
        public const int ResourceCount = 41;

        // Define updates per day from CityProductionStatisticSystem.kUpdatesPerDay.
        // This system checks if the game has changed from this value.
        public const int CityProdStatisticUpdatesPerDay = 32;

        // Current amounts.
        private int[] _currentProductions;
        private int[] _currentSurpluses;
        private Dictionary<Entity, int> _companyProductions;
        private readonly object _dataLock = new();

        // Other systems.
        private MyProcessingCompanySystem     _myProcessingCompanySystem;
        private MyExtractorCompanySystem      _myExtractorCompanySystem;
        private CityResourceUsageSystem       _cityResourceUsageSystem;
        private CityProductionStatisticSystem _cityProductionStatisticSystem;
        private TimeSystem                    _timeSystem;

        // Resource history development mode info.
        private int _rowsCurrent;
        private string _rowLastAdded;
        private RawValueBinding _bindingResourceHistoryDevInfo;

        /// <summary>
        /// Set update interval.
        /// </summary>
        [Preserve]
        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            // For the Resource Locator mod,
            // this system is desired to run at least once every real time second because that is the refresh rate of the mod's UI.
            // However, there is no way to determine the amount of real time that a certain number of simulation ticks will take because
            // real time is dependent on things like selected sim speed, city population, PC hardware, etc.
            // If this system runs less than once per real time second,
            // then it is acceptable that the mod's UI will simply not have new values every second.

            // For the Production Balance feature in the Change Company mod,
            // this system needs to run at least once every game minute because
            // that is the highest frequency that the user can choose in settings.
            // There are 262144 simulation ticks per game day.
            // There are 262144 / (24 * 60) = 182.044 sim ticks per game minute.
            // Interval is the power of 2 less than that.
            return 128;
        }

        /// <summary>
        /// Set update offset same as CityProductionStatisticSystem.
        /// </summary>
        [Preserve]
        public override int GetUpdateOffset(SystemUpdatePhase phase)
        {
            return World.GetOrCreateSystemManaged<CityProductionStatisticSystem>().GetUpdateOffset(phase);
        }

        /// <summary>
        /// Initialize this system.
        /// </summary>
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            Mod.log.Info($"{nameof(ProductionSurplusSystem)}.{nameof(OnCreate)}");

            // Check if game changed its resource count.
            if (ResourceCount != EconomyUtils.ResourceCount)
            {
                Mod.log.Fatal($"Game changed its resource count from {ResourceCount} to {EconomyUtils.ResourceCount}.\n" +
                              $"Please quit game until the {ModAssemblyInfo.Title} mod is updated.");
                return;
            }

            // Check if game changed its CityProductionStatisticSystem updates per day.
            if (CityProdStatisticUpdatesPerDay != CityProductionStatisticSystem.kUpdatesPerDay)
            {
                Mod.log.Fatal($"Game changed its city production statistic system updates per day from {CityProdStatisticUpdatesPerDay} to {CityProductionStatisticSystem.kUpdatesPerDay}.\n" +
                              $"Please quit game until the {ModAssemblyInfo.Title} mod is updated.");
                return;
            }

            // Initialize current amounts.
            _currentProductions = new int[ResourceCount];
            _currentSurpluses   = new int[ResourceCount];
            _companyProductions = new();

            // Create and get this mod's systems that get company data.
            // These systems do not run periodically, so they only need to be created, not activated.
            _myProcessingCompanySystem = World.GetOrCreateSystemManaged<MyProcessingCompanySystem>();
            _myExtractorCompanySystem  = World.GetOrCreateSystemManaged<MyExtractorCompanySystem>();

            // Get other systems.
            _cityResourceUsageSystem       = World.GetOrCreateSystemManaged<CityResourceUsageSystem>();
            _cityProductionStatisticSystem = World.GetOrCreateSystemManaged<CityProductionStatisticSystem>();
            _timeSystem                    = World.GetOrCreateSystemManaged<TimeSystem>();

            // Add C# to UI binding for resource history dev info.
            AddBinding(_bindingResourceHistoryDevInfo = new RawValueBinding(ModAssemblyInfo.Name, "ResourceHistoryDevInfo", WriteResourceHistoryDevInfo));
            ResetResourceHistoryDevInfo();
        }

        /// <summary>
        /// Called when a game, main menu, editor, etc. is about to be loaded.
        /// </summary>
        [Preserve]
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            // Ensure there is no resource history singleton from a previous game.
            if (SystemAPI.TryGetSingletonEntity<ResourceHistoryData>(out Entity resourceHistoryEntity))
            {
                EntityManager.DestroyEntity(resourceHistoryEntity);
            }

            // Reset resource history dev info.
            ResetResourceHistoryDevInfo();
        }

        /// <summary>
        /// Reset resource history dev info.
        /// </summary>
        private void ResetResourceHistoryDevInfo()
        {
            _rowsCurrent = -1;
            _rowLastAdded = "None";
            _bindingResourceHistoryDevInfo.Update();
        }

        /// <summary>
        /// Called when a game is done being loaded.
        /// </summary>
        [Preserve]
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);

            // Initialize only for game mode.
            if (mode == GameMode.Game)
            {
                // Initialize data by executing OnUpdate.
                OnUpdate();

                // Initialize current rows for resource history dev info.
                DynamicBuffer<ResourceHistoryElement> buffer = GetResourceHistoryBuffer();
                _rowsCurrent = ResourceHistoryRows(in buffer);
                _bindingResourceHistoryDevInfo.Update();
            }
        }

        /// <summary>
        /// Called every update interval while game simulation is running.
        /// </summary>
        [Preserve]
        protected override void OnUpdate()
        {
            // Get processing company production and consumption amounts.
            _myProcessingCompanySystem.GetAmounts(
                out int[] processingCompanyProductionAmounts,
                out int[] processingCompanyConsumptionAmounts,
                out Dictionary<Entity, int> companyProductions);

            // Get extractor company production amounts.
            // Extractors have no resource consumption.
            _myExtractorCompanySystem.GetAmounts(out int[] extractorCompanyProductionAmounts);

            // Get city resource usage (aka final consumption) data.
            // Logic adapted from Game.UI.InGame.ProductionUISystem.WriteFinalConsumption().
            // Note that CityProductionStatisticSystem updates the city resource usage data only once every 45 game minutes.
            // Therefore, the real-time company data obtained above is combined with this slower city resource usage data.
            // This will have to be good enough because it would be too complicated to duplicate
            // the 6-7 systems that provide the city resource usage data to try to get it more often.
            NativeArray<CityProductionStatisticSystem.CityResourceUsage> cityResourceUsages = _cityProductionStatisticSystem.GetCityResourceUsages();

            // Lock thread while writing data.
            lock (_dataLock)
            {
                // Do each resource index.
                for (int resourceIndex = 0; resourceIndex < ResourceCount; resourceIndex++)
                {
                    // Current production is processing company production amount plus extractor company production amount.
                    _currentProductions[resourceIndex] =
                        processingCompanyProductionAmounts[resourceIndex] +
                        extractorCompanyProductionAmounts [resourceIndex];

                    // Current consumption is processing company consumption amount plus city resource usage excluding import/export.
                    int currentConsumption = processingCompanyConsumptionAmounts[resourceIndex];
                    CityProductionStatisticSystem.CityResourceUsage cityResourceUsagesForResource = cityResourceUsages[resourceIndex];
                    for (int i = 0; i < (int)CityProductionStatisticSystem.CityResourceUsage.Consumer.Count; i++)
                    {
                        if (i != (int)CityProductionStatisticSystem.CityResourceUsage.Consumer.ImportExport)
                        {
                            currentConsumption += cityResourceUsagesForResource[i];
                        }
                    }

                    // Current surplus is simply current production minus current consumption.
                    // Negative surplus is a deficit.
                    _currentSurpluses[resourceIndex] = _currentProductions[resourceIndex] - currentConsumption;
                }

                // Save company productions.
                _companyProductions = new(companyProductions);

                // Update resource history.
                DynamicBuffer<ResourceHistoryElement> buffer = GetResourceHistoryBuffer();
                if (_cityResourceUsageSystem.Updated)
                {
                    AddResourceHistoryRow(in buffer);
                    _cityResourceUsageSystem.Updated = false;
                }
                else
                {
                    SaveNewResourceHistoryValues(in buffer);
                }
            }
        }

        /// <summary>
        /// Get resource history buffer.
        /// Buffer must always be used immediately after being obtained because Unity can change its location in memory.
        /// </summary>
        private DynamicBuffer<ResourceHistoryElement> GetResourceHistoryBuffer()
        {
            // Check there is a singleton entity with component ResourceHistoryData and buffer of ResourceHistoryElement.
            if (SystemAPI.TryGetSingleton(out ResourceHistoryData resourceHistoryData) &&
                SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<ResourceHistoryElement> resourceHistoryBuffer, false))
            {
                // Singleton entity exists.
                
                // Check if buffer needs to be updated because it is from an older resource history version.
                //if (resourceHistoryData.VersionFromGame == oldVersion)
                //{
                //    // Do the update here.

                //    // Buffer is now updated to the current version.
                //    resourceHistoryData.VersionFromGame = ResourceHistoryData.CurrentVersion;
                //    SystemAPI.SetComponent(resourceHistoryEntity, resourceHistoryData);
                //}
                
                // Return the buffer.
                return resourceHistoryBuffer;
            }
            else
            {
                // Singleton entity does not exist.

                // Create a new resource history singleton entity.
                Entity resourceHistoryEntity = EntityManager.CreateSingleton(new ResourceHistoryData() { VersionFromGame = ResourceHistoryData.CurrentVersion });

                // Return a new empty resource history buffer on the singleton entity.
                return EntityManager.AddBuffer<ResourceHistoryElement>(resourceHistoryEntity);
            }
        }

        // Get number of rows in the resource history buffer.
        private int ResourceHistoryRows(in DynamicBuffer<ResourceHistoryElement> buffer)
        {
            return buffer.Length / ResourceCount;
        }

        // Two dimensional array accessors (zero based).
        // Rows are one for each update during the game day.
        // Columns are one for each resource.
        private ResourceHistoryElement GetResourceHistoryElement(in DynamicBuffer<ResourceHistoryElement> buffer, int row, int resourceIndex)
        {
            return buffer[row * ResourceCount + resourceIndex];
        }
        private void SetResourceHistoryElement(DynamicBuffer<ResourceHistoryElement> buffer, int row, int resourceIndex, ResourceHistoryElement value)
        {
            buffer[row * ResourceCount + resourceIndex] = value;
        }

        /// <summary>
        /// Add a row of production and surplus values to the resource history buffer.
        /// </summary>
        private void AddResourceHistoryRow(in DynamicBuffer<ResourceHistoryElement> buffer)
        {
            // Check if production and surplus values to be added are all zeros.
            bool allZeroes = true;
            for (int resourceIndex = 0; resourceIndex < ResourceCount; resourceIndex++)
            {
                if (_currentProductions[resourceIndex] != 0 || _currentSurpluses[resourceIndex] != 0)
                {
                    allZeroes = false;
                    break;
                }
            }

            // Add a new row at the end of the buffer if:
            //      There are no rows (i.e. buffer must always have a row), OR
            //      The data is not all zeroes and there are less than max rows (i.e. current data will be used)
            // Checking for all zeroes before adding a row prevents including all zeroes in the history.
            // This is important especially for a new city that does not yet have any production.
            // Of course, after one game day the zeroes would drop off on their own, but the history would be affected in the mean time.
            int rows = ResourceHistoryRows(in buffer);
            if (rows == 0 || (!allZeroes && rows < CityProdStatisticUpdatesPerDay))
            {
                for (int resourceIndex = 0; resourceIndex < ResourceCount; resourceIndex++)
                {
                    buffer.Add(new ResourceHistoryElement(0, 0));
                }
                rows++;
            }

            // The newest values are always in row 0.
            // So move all the rows to the next higher index.
            // The last row (with the oldest values) gets overwritten.
            for (int row = rows - 1; row > 0; row--)
            {
                for (int resourceIndex = 0; resourceIndex < ResourceCount; resourceIndex++)
                {
                    SetResourceHistoryElement(buffer, row, resourceIndex, GetResourceHistoryElement(in buffer, row - 1, resourceIndex));
                }
            }

            // Save the new values.
            SaveNewResourceHistoryValues(in buffer);

            // Save and update the resource history dev info.
            // The game stores the month in DateTime.DayOfYear field.
            DateTime currentDateTime = _timeSystem.GetCurrentDateTime();
            _rowsCurrent = rows;
            _rowLastAdded = $"{currentDateTime.Year}.{currentDateTime.DayOfYear:D2} {currentDateTime.Hour:D2}:{currentDateTime.Minute:D2}";
            _bindingResourceHistoryDevInfo.Update();
        }

        /// <summary>
        /// Save new resource history values.
        /// </summary>
        private void SaveNewResourceHistoryValues(in DynamicBuffer<ResourceHistoryElement> buffer)
        {
            // Check for no row to update.
            if (ResourceHistoryRows(in buffer) == 0)
            {
                // This will result in one recursive call back to SaveNewResourceHistoryValues.
                // But by then the buffer will have a row.
                AddResourceHistoryRow(in buffer);
            }
            else
            {
                // Save the current values for each resource.
                // The most recent values are always stored in row 0.
                for (int resourceIndex = 0; resourceIndex < ResourceCount; resourceIndex++)
                {
                    SetResourceHistoryElement(buffer, 0, resourceIndex,
                        new ResourceHistoryElement(_currentProductions[resourceIndex], _currentSurpluses[resourceIndex]));
                }
            }
        }

        /// <summary>
        /// Get production and surplus amounts.
        /// </summary>
        public void GetAmounts(
            out int[] currentProductions,
            out int[] currentSurpluses,
            out int[] averageProductions,
            out int[] averageSurpluses,
            out Dictionary<Entity, int> companyProductions)
        {
            // Initialize return values.
            currentProductions = new int[ResourceCount];
            currentSurpluses   = new int[ResourceCount];
            averageProductions = new int[ResourceCount];
            averageSurpluses   = new int[ResourceCount];
            
            // Lock thread while reading data.
            lock (_dataLock)
            {
                // Return copies of current amounts.
                Array.Copy(_currentProductions, currentProductions, _currentProductions.Length);
                Array.Copy(_currentSurpluses,   currentSurpluses,   _currentSurpluses  .Length);
                companyProductions = new(_companyProductions);

                // If resource history has any rows, compute average production and surplus.
                // There should always be at least one row.
                DynamicBuffer<ResourceHistoryElement> buffer = GetResourceHistoryBuffer();
                int rows = ResourceHistoryRows(in buffer);
                if (rows >= 1)
                {
                    // Do each resource.
                    for (int resourceIndex = 0; resourceIndex < ResourceCount; resourceIndex++)
                    {
                        // Compute total production and surplus over the rows.
                        // Must compute total as long because total can exceed max int in large cities.
                        long totalProduction = 0L;
                        long totalSurplus    = 0L;
                        for (int row = 0; row < rows; row++)
                        {
                            ResourceHistoryElement resourceHistoryElement = GetResourceHistoryElement(in buffer, row, resourceIndex);
                            totalProduction += resourceHistoryElement.Production;
                            totalSurplus    += resourceHistoryElement.Surplus;
                        }

                        // Compute average production and surplus.
                        averageProductions[resourceIndex] = (int)(totalProduction / rows);
                        averageSurpluses  [resourceIndex] = (int)(totalSurplus    / rows);
                    }
                }
            }
        }

        /// <summary>
        /// Write resource history development mode info to the UI.
        /// </summary>
        private void WriteResourceHistoryDevInfo(IJsonWriter writer)
        {
            // Write the resource history dev info to the UI.
            writer.TypeBegin(ModAssemblyInfo.Name + ".ResourceHistoryDevInfo");
            writer.PropertyName("rowsCurrent");
            writer.Write(_rowsCurrent);
            writer.PropertyName("rowsMaximum");
            writer.Write(CityProdStatisticUpdatesPerDay);
            writer.PropertyName("rowLastAdded");
            writer.Write(_rowLastAdded);
            writer.TypeEnd();
        }
    }
}
