using Unity.Entities;

namespace ChangeCompany
{
    /// <summary>
    /// Company data passed from ChangeCompanySection and ProductionBalanceSystem to
    /// ChangeCompanySystem for changing the company on a property.
    /// </summary>
    public class ChangeCompanyData
    {
        // The data.
        public RequestType  RequestType;        // The change company request type.
        public Entity       NewCompanyPrefab;   // The prefab to use for the specified new company.
        public CompanyInfos CompanyInfos;       // The company infos from which to choose a random new company.
        public Entity       PropertyEntity;     // The entity        of the property to be changed.
        public Entity       PropertyPrefab;     // The prefab        of the property to be changed.
        public PropertyType PropertyType;       // The property type of the property to be changed.
    }
}
