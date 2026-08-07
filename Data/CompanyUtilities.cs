using Game.Economy;
using Game.Prefabs;
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

        /// <summary>
        /// Get the company type for storage and vehicles overrides.
        /// </summary>
        public static CompanyType GetCompanyType(
            Entity companyPrefab,
            in ComponentLookup<CommercialCompanyData> componentLookupCommercialCompanyData,
            in ComponentLookup<ExtractorCompanyData > componentLookupExtractorCompanyData,
            in ComponentLookup<IndustrialCompanyData> componentLookupIndustrialCompanyData,
            in ComponentLookup<StorageCompanyData   > componentLookupStorageCompanyData,
            in ComponentLookup<IndustrialProcessData> componentLookupIndustrialProcessData
            )
        {
            // Company type is mostly based on the component(s) present on the prefab.

            // Check for commercial.
            if (componentLookupCommercialCompanyData.HasComponent(companyPrefab))
            {
                return CompanyType.Commercial;
            }
            
            // Check for industrial, which includes extractor, industrial, office, and warehouse.
            if (componentLookupIndustrialCompanyData.HasComponent(companyPrefab))
            {
                // For industrial, company type is based on an additional component present on the prefab.

                // Check for extractor.
                if (componentLookupExtractorCompanyData.HasComponent(companyPrefab))
                {
                    return CompanyType.Extractor;
                }
                
                // Check for warehouse.
                if (componentLookupStorageCompanyData.HasComponent(companyPrefab))
                {
                    return CompanyType.Warehouse;
                }
                
                // There are no components whose presence can distinguish between industrial and office company prefabs.
                // Use output resource instead.
                if (componentLookupIndustrialProcessData.TryGetComponent(companyPrefab, out IndustrialProcessData industrialProcessData))
                {
                    if (EconomyUtils.IsOfficeResource(industrialProcessData.m_Output.m_Resource))
                    {
                        return CompanyType.Office;
                    }
                    else
                    {
                        return CompanyType.Industrial;
                    }
                }
            }

            // No company type.
            return CompanyType.None;
        }
    }
}
