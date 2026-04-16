using Game;
using Game.Buildings;
using Game.SceneFlow;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;

namespace ChangeCompany
{
    /// <summary>
    /// Remove the workplaces override for companies with no property.
    /// </summary>
    public partial class RemoveWorkplacesOverrideSystem : GameSystemBase
    {
        /// <summary>
        /// Called once every simulation phase, even when the simulation is not running.
        /// </summary>
        [Preserve]
        protected override void OnUpdate()
        {
            // Skip if not in a game.
            if (GameManager.instance.gameMode != GameMode.Game)
            {
                return;
            }

            // Remove the WorkplacesOverride component from each company that has no PropertyRenter.
            // This can happen, for example, when a building is bulldozed:  the company remains and
            // is available to be assigned to a new property, but still has its WorkplacesOverride.
            // When assigned to a new property, the company should not already have a WorkplacesOverride.
            // Because this situation should be rare, an entity command buffer is not used.
            EntityQuery query = SystemAPI.QueryBuilder()
                .WithAll<WorkplacesOverride>()
                .WithNone<PropertyRenter>()
                .Build();
            NativeArray<Entity> companies = query.ToEntityArray(Allocator.Temp);
            foreach (Entity companyEntity in companies)
            {
                EntityManager.RemoveComponent<WorkplacesOverride>(companyEntity);
            }
        }
    }
}
