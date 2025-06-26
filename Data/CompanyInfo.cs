using Colossal.UI.Binding;
using Game.Economy;
using Game.Prefabs;
using Unity.Entities;

namespace ChangeCompany
{
    /// <summary>
    /// Information for a company.
    /// </summary>
    public class CompanyInfo : IJsonWritable
    {
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
            // Save the company prefab.
            CompanyPrefab = companyPrefab;

            // Get the resources from IndustrialProcessData.
            ResourceOutput = industrialProcessData.m_Output.m_Resource;
            ResourceInput1 = industrialProcessData.m_Input1.m_Resource;
            ResourceInput2 = industrialProcessData.m_Input2.m_Resource;
        }
     
        /// <summary>
        /// Write the company info to the UI.
        /// </summary>
        public void Write(IJsonWriter writer)
        {
            // Company prefab is not needed by the UI, just the resources.
            writer.TypeBegin(ModAssemblyInfo.Name + ".CompanyResourceData");
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
