using Colossal.Serialization.Entities;
using Unity.Entities;

namespace ChangeCompany
{
    /// <summary>
    /// Data component that when present on a company specifies how to override company workplaces.
    /// The presence of this component on a company means there is an override.
    /// </summary>
    public struct WorkplacesOverride : IComponentData, IQueryTypeParameter, ISerializable
    {
        // Current version of the component data.
        private const int CurrentVersion = 1;

        // The override value.
        public int Value;

        /// <summary>
        /// Save override data.
        /// </summary>
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(CurrentVersion);
            writer.Write(Value);
        }

        /// <summary>
        /// Retrieve override data.
        /// </summary>
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int version);
            reader.Read(out Value);
        }
    }
}
