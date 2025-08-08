using Colossal.UI.Binding;
using Game.Economy;
using Game.Prefabs;
using Unity.Entities;

namespace ChangeCompany
{
    /// <summary>
    /// Information for a company.
    /// </summary>
    public class CompanyInfo
    {
        // Special company type.
        public SpecialCompanyType SpecialType { get; set; }

        // Company prefab.
        public Entity CompanyPrefab { get; set; }  

        // Resources for the company prefab.
        public Resource ResourceOutput { get; set; }
        public Resource ResourceInput1 { get; set; }
        public Resource ResourceInput2 { get; set; }

        // Prevent construction without parameters.
        private CompanyInfo() { }

        /// <summary>
        /// Constructor based on a company prefab.
        /// </summary>
        public CompanyInfo(Entity companyPrefab, IndustrialProcessData industrialProcessData)
        {
            // This is not a special company type.
            SpecialType = SpecialCompanyType.None;

            // Save the company prefab.
            CompanyPrefab = companyPrefab;

            // Get the resources from IndustrialProcessData.
            ResourceOutput = industrialProcessData.m_Output.m_Resource;
            ResourceInput1 = industrialProcessData.m_Input1.m_Resource;
            ResourceInput2 = industrialProcessData.m_Input2.m_Resource;
        }

        /// <summary>
        /// Constructor for special company type.
        /// </summary>
        public CompanyInfo(SpecialCompanyType specialCompanyType)
        {
            // Save the special company type.
            SpecialType = specialCompanyType;

            // Use defaults for other data.
            CompanyPrefab = Entity.Null;
            ResourceOutput = Resource.NoResource;
            ResourceInput1 = Resource.NoResource;
            ResourceInput2 = Resource.NoResource;
        }
     
        /// <summary>
        /// Write the company info to the UI.
        /// </summary>
        public void Write(IJsonWriter writer)
        {
            // Write only the special company type and resources.
            // Company prefab is not needed by the UI.
            writer.TypeBegin(ModAssemblyInfo.Name + ".CompanyInfo");
            writer.PropertyName("specialType");
            writer.Write((int)SpecialType);
            writer.PropertyName("resourceOutput");
            writer.Write(ResourceOutput.ToString());
            writer.PropertyName("resourceInput1");
            writer.Write(ResourceInput1.ToString());
            writer.PropertyName("resourceInput2");
            writer.Write(ResourceInput2.ToString());
            writer.TypeEnd();
        }
    }
}
