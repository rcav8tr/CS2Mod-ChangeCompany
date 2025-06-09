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
    public class CompanyInfos : List<CompanyInfo>, IJsonWritable
    {
        // Resources in the order they are displayed on the Production tab of the Economy view.
        // Excludes resources this mod does not care about (e.g. money, mail, garbage).
        public static readonly List<Resource> ResourceOrder = new List<Resource>
        { 
            // Materials
            Resource.Wood,
            Resource.Grain,
            Resource.Livestock,
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

        /// <summary>
        /// Constructor with no parameters.
        /// </summary>
        public CompanyInfos() { }

        /// <summary>
        /// Constructor based on a list of company prefabs.
        /// </summary>
        public CompanyInfos(List<Entity> companyPrefabs, ComponentLookup<IndustrialProcessData> componentLookupIndustrialProcessData)
        {
            // Add each company.
            foreach (Entity companyPrefab in companyPrefabs)
            {
                Add(new CompanyInfo(companyPrefab, componentLookupIndustrialProcessData[companyPrefab]));
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
        public void Write(IJsonWriter writer)
        {
            writer.PropertyName("companyResourceDatas");
            writer.ArrayBegin(this.Count);
			foreach (CompanyInfo companyInfo in this)
			{
				companyInfo.Write(writer);
			}
			writer.ArrayEnd();
        }
    }
}
