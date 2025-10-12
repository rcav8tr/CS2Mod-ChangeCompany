using Colossal.UI.Binding;
using Game.Economy;
using Game.Prefabs;
using System.Collections.Generic;
using Unity.Entities;

namespace ChangeCompany
{
    /// <summary>
    /// A list of company info.
    /// </summary>
    public class CompanyInfos : List<CompanyInfo>
    {
        // Resources in the order they are displayed on the Production tab of the Economy view.
        // Excludes resources this mod does not care about (e.g. money, mail, garbage).
        public static readonly List<Resource> ResourceOrder = new List<Resource>
        { 
            // Materials
            Resource.Wood,
            Resource.Grain,
            Resource.Livestock,
            Resource.Fish,
            Resource.Vegetables,
            Resource.Cotton,
            Resource.Oil,
            Resource.Ore,
            Resource.Coal,
            Resource.Stone,

            // Material goods.
            Resource.Metals,
            Resource.Steel,
            Resource.Minerals,
            Resource.Concrete,
            Resource.Machinery,
            Resource.Petrochemicals,
            Resource.Chemicals,
            Resource.Plastics,
            Resource.Pharmaceuticals,
            Resource.Electronics,
            Resource.Vehicles,
            Resource.Beverages,
            Resource.ConvenienceFood,
            Resource.Food,
            Resource.Textiles,
            Resource.Timber,
            Resource.Paper,
            Resource.Furniture,
            Resource.Software,
            Resource.Telecom,
            Resource.Financial,
            Resource.Media,

            // Immaterial goods.
            Resource.Lodging,
            Resource.Meals,
            Resource.Entertainment,
            Resource.Recreation,

            // Always sort NoResource last.
            Resource.NoResource,
        };

        // Define company infos for special companies.
        private static readonly CompanyInfo _companyInfoRandomCompany = new CompanyInfo(SpecialCompanyType.Random);
        private static readonly CompanyInfo _companyInfoRemoveCompany = new CompanyInfo(SpecialCompanyType.Remove);

        /// <summary>
        /// Constructor with no parameters.
        /// </summary>
        public CompanyInfos() { }

        /// <summary>
        /// Constructor based on a list of company prefabs.
        /// </summary>
        public CompanyInfos(List<Entity> companyPrefabs, ComponentLookup<IndustrialProcessData> componentLookupIndustrialProcessData)
        {
            // Add each company whose output resource is one of the resources for ordering.
            foreach (Entity companyPrefab in companyPrefabs)
            {
                IndustrialProcessData industrialProcessData = componentLookupIndustrialProcessData[companyPrefab];
                if (ResourceOrder.Contains(industrialProcessData.m_Output.m_Resource))
                {
                    Add(new CompanyInfo(companyPrefab, industrialProcessData));
                }
            }

            // Use nested loops to sort companies by the resources they have.
            for (int i = 0; i < Count - 1; i++)
            {
                // Get resource orders for company info i.
                int iResourceOrderOutput = ResourceOrder.IndexOf(this[i].ResourceOutput);
                int iResourceOrderInput1 = ResourceOrder.IndexOf(this[i].ResourceInput1);
                int iResourceOrderInput2 = ResourceOrder.IndexOf(this[i].ResourceInput2);

                for (int j = i + 1; j < Count; j++)
                {
                    // Get resource orders for company info j.
                    int jResourceOrderOutput = ResourceOrder.IndexOf(this[j].ResourceOutput);
                    int jResourceOrderInput1 = ResourceOrder.IndexOf(this[j].ResourceInput1);
                    int jResourceOrderInput2 = ResourceOrder.IndexOf(this[j].ResourceInput2);

                    // If resources for company info i are after resources for company info j, then swap company infos in the list.
                    // Sort first by output resource, then input1 resource, then input2 resource.
                    if (
                        (iResourceOrderOutput >  jResourceOrderOutput) ||
                        (iResourceOrderOutput == jResourceOrderOutput && iResourceOrderInput1 >  jResourceOrderInput1) ||
                        (iResourceOrderOutput == jResourceOrderOutput && iResourceOrderInput1 == jResourceOrderInput1 && iResourceOrderInput2 > jResourceOrderInput2))
                    {
                        (this[i], this[j]) = (this[j], this[i]);
                        iResourceOrderOutput = jResourceOrderOutput;
                        iResourceOrderInput1 = jResourceOrderInput1;
                        iResourceOrderInput2 = jResourceOrderInput2;
                    }
                }
            }
        }

        /// <summary>
        /// Write company infos to the UI.
        /// </summary>
        public void Write(IJsonWriter writer, bool includeRemoveCompany)
        {
            // Include random company only if there is more than one company info.
            bool includeRandomCompany = Count > 1;

            writer.PropertyName("companyInfos");
            writer.ArrayBegin(Count + (includeRandomCompany ? 1 : 0) + (includeRemoveCompany ? 1 : 0));

            // Write each standard company info.
            foreach (CompanyInfo companyInfo in this)
            {
                companyInfo.Write(writer);
            }

            // Write the special company info for random company.
            if (includeRandomCompany)
            {
                _companyInfoRandomCompany.Write(writer);
            }

            // Write the special company info for remove company.
            if (includeRemoveCompany)
            {
                _companyInfoRemoveCompany.Write(writer);
            }

            writer.ArrayEnd();
        }
    }
}
