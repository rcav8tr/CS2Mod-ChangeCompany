using Colossal.Collections;
using Game.Economy;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;

namespace ChangeCompany
{
    /// <summary>
    /// Utilities to help with calculations for production and consumption amounts and counts.
    /// These utilities are used in Resource Locator and Change Company mods.
    /// </summary>
    public static class ProductionConsumptionUtils
    {
        // Get resource count only once.
        private static readonly int ResourceCount = EconomyUtils.ResourceCount;

        /// <summary>
        /// Create nested native arrays to hold production and consumption amounts and counts.
        /// </summary>
        public static NativeArray<NativeArray<int>> CreateArrays()
        {
            // Nested arrays to hold production and consumption amounts and counts.
            // The outer array is one for each possible thread.
            // The inner array is one for each resource index.

            // Scheduled jobs can write to the inner array without parallel threads interfering with each other
            // because each parallel thread writes to a different outer array determined by the thread index.

            // Arrays are persistent so they do not need to be recreated each time a job runs.

            // Outer array is one for each possible thread.
            int threadCount = JobsUtility.ThreadIndexCount;
            NativeArray<NativeArray<int>> nestedArrays = new(threadCount, Allocator.Persistent);

            // Do each thread.
            for (int i = 0; i < threadCount; i++)
            {
                // Inner array is one for each resource.
                nestedArrays[i] = new(ResourceCount, Allocator.Persistent);
            }

            // Return the nested arrays.
            return nestedArrays;
        }

        /// <summary>
        /// Dispose nested native arrays that hold production and consumption amounts and counts.
        /// </summary>
        public static void DisposeArrays(in NativeArray<NativeArray<int>> nestedArrays)
        {
            // The input parameter must be "in" to get a reference, not a copy.

            // Dispose inner arrays.
            foreach (NativeArray<int> innerArray in nestedArrays)
            {
                innerArray.Dispose();
            }

            // Dispose outer array.
            nestedArrays.Dispose();
        }

        /// <summary>
        /// Initialize the inner arrays of nested arrays.
        /// </summary>
        public static void InitializeArrays(in NativeArray<NativeArray<int>> nestedArrays)
        {
            // The input parameter must be "in" to get a reference, not a copy.
            // The inner arrays can still be updated even though the outer array is read only.

            // Do each outer array entry.
            for (int i = 0; i < nestedArrays.Length; i++)
            {
                // Fill the inner array with zeroes.
                nestedArrays[i].Fill(0);
            }
        }

        /// <summary>
        /// Add a value to inner array for specified thread and resource.
        /// </summary>
        public static void AddValue(in NativeArray<NativeArray<int>> nestedArrays, int threadIndex, Resource resource, int value)
        {
            // The input parameter must be "in" to get a reference, not a copy.
            // The inner array can still be updated even though the outer array is read only.

            // Add only if not zero.
            if (value != 0)
            {
                // Add the value for the thread and resource.
                NativeArray<int> innerArray = nestedArrays[threadIndex];
                int resourceIndex = EconomyUtils.GetResourceIndex(resource);
                innerArray[resourceIndex] = innerArray[resourceIndex] + value;
            }
        }

        /// <summary>
        /// Consolidate values by resource.
        /// </summary>
        public static void ConsolidateValues(in NativeArray<NativeArray<int>> nestedArrays, out int[] consolidatedValues)
        {
            // Initialize return array, one for each resource.
            consolidatedValues = new int[ResourceCount];

            // Do each thread entry in the outer array.
            foreach (NativeArray<int> innerArray in nestedArrays)
            {
                // Do each resource index in the inner array.
                for (int resourceIndex = 0; resourceIndex < innerArray.Length; resourceIndex++)
                {
                    // Add inner array value.
                    consolidatedValues[resourceIndex] += innerArray[resourceIndex];
                }
            }
        }
    }
}
