using Colossal.Serialization.Entities;
using Game;
using Game.Simulation;
using UnityEngine.Scripting;

namespace ChangeCompany
{
    /// <summary>
    /// Identify when city resource usage was updated by the game.
    /// This system is used in Resource Locator and Change Company mods.
    /// </summary>
    public partial class CityResourceUsageSystem : GameSystemBase
    {
        // Whether or not city resource usage was just updated by the game.
        // The ProductionSurplusSystem monitors this flag.
        public bool Updated;

        /// <summary>
        /// Set update interval same as CityProductionStatisticSystem.
        /// </summary>
        [Preserve]
        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return World.GetOrCreateSystemManaged<CityProductionStatisticSystem>().GetUpdateInterval(phase);
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
        /// Called when a game, main menu, editor, etc. is about to be loaded.
        /// </summary>
        [Preserve]
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            // City resource usage was not just updated.
            Updated = false;
        }

        /// <summary>
        /// Called every update interval while game simulation is running.
        /// </summary>
        [Preserve]
        protected override void OnUpdate()
        {
            // There is nothing in CityProductionStatisticSystem to indicate that its OnUpdate just ran.
            // Mere execution of this OnUpdate means city resource usage was just updated by CityProductionStatisticSystem.
            // Setting this flag is the entire purpose of this system.
            Updated = true;
        }
    }
}
