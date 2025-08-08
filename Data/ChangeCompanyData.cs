using Unity.Entities;

namespace ChangeCompany
{
    /// <summary>
    /// Company data passed from ChangeCompanySection to ChangeCompanySystem for changing the company on a property.
    /// </summary>
    public class ChangeCompanyData
    {
        // The data.
        public RequestType  requestType;        // The change company request type.
        public Entity       newCompanyPrefab;   // The prefab to use for the specified new company.
        public CompanyInfos companyInfos;       // The company infos from which to choose a random new company.
        public Entity       propertyEntity;     // The property entity to be changed.
        public Entity       propertyPrefab;     // The property prefab to be changed.
        public PropertyType propertyType;       // The property type of the property to be changed.
    }
}
