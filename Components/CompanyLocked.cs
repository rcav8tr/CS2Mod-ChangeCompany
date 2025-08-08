using Colossal.Serialization.Entities;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace ChangeCompany
{
    /// <summary>
    /// Tag component that when present on a company indicates the company is locked on its property.
    /// Adapted from game's Signature tag component.
    /// </summary>
	[StructLayout(LayoutKind.Sequential, Size = 1)]
    public struct CompanyLocked : IComponentData, IQueryTypeParameter, IEmptySerializable
    {
    }
}
