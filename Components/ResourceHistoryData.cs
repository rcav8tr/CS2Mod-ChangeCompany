using Colossal.Serialization.Entities;
using Unity.Entities;
using UnityEngine.Scripting;

namespace ChangeCompany
{
    /// <summary>
    /// Data component that resides on a singleton entity to store production and surplus resource history.
    /// This struct is used in Resource Locator and Change Company mods.
    /// </summary>
    public struct ResourceHistoryData : IComponentData, IQueryTypeParameter, ISerializable
    {
        // Current version of the resource history data.
        public const int CurrentVersion = 1;

        // The version read from the game file.
        // Version will be zero if component is not in the game file.
        public int VersionFromGame;

        /// <summary>
        /// Save the values.
        /// </summary>
        [Preserve]
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(CurrentVersion);
        }

        /// <summary>
        /// Retrieve the values.
        /// </summary>
        [Preserve]
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out VersionFromGame);
        }
    }
}
