using Colossal.Serialization.Entities;
using Unity.Entities;
using UnityEngine.Scripting;

namespace ChangeCompany
{
    /// <summary>
    /// Define one resource history buffer element.
    /// This struct is used in Resource Locator and Change Company mods.
    /// </summary>
    [InternalBufferCapacity(ProductionSurplusSystem.CityProdStatisticUpdatesPerDay * ProductionSurplusSystem.ResourceCount)] 
    public struct ResourceHistoryElement : IBufferElementData, ISerializable
    {
        // The production and surplus historical values.
        public int Production;
        public int Surplus;

        // Constructor
        public ResourceHistoryElement(int production, int surplus)
        {
            Production = production;
            Surplus    = surplus;
        }

        /// <summary>
        /// Save the values.
        /// </summary>
        [Preserve]
        public readonly void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(Production);
            writer.Write(Surplus);
        }

        /// <summary>
        /// Retrieve the values.
        /// </summary>
        [Preserve]
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out Production);
            reader.Read(out Surplus);
        }
    }
}
