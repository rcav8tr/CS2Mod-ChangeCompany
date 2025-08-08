using Game.UI.InGame;
using Unity.Entities;

namespace ChangeCompany
{
    /// <summary>
    /// Utilities for companies.
    /// </summary>
    public static class CompanyUtilities
    {
        /// <summary>
        /// Try to get the company at the property.
        /// </summary>
        public static bool TryGetCompanyAtProperty(EntityManager entityManager, Entity propertyEntity, Entity propertyPrefab, out Entity companyEntity)
        {
            // Use the game's definition of a property having a company.
            if (CompanyUIUtils.HasCompany(entityManager, propertyEntity, propertyPrefab, out companyEntity))
            {
                // The returned company must be valid.
                return companyEntity != Entity.Null;
            }

            // Property does not have a company.
            companyEntity = Entity.Null;
            return false;
        }
    }
}
