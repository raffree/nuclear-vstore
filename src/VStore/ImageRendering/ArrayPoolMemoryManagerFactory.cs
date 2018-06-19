using SixLabors.ImageSharp.Memory;

namespace NuClear.VStore.ImageRendering
{
    public static class ArrayPoolMemoryManagerFactory
    {
        /// <summary>
        /// Similar to <see cref="ArrayPoolMemoryManager.CreateWithModeratePooling"/> option, but with one bucket per every pool only
        /// </summary>
        /// <returns>The memory manager</returns>
        public static ArrayPoolMemoryManager CreateWithLimitedSmallPooling()
        {
            return new ArrayPoolMemoryManager(350 * 350 * 4, 1, 1, 1);
        }

        /// <summary>
        /// Similar to <see cref="ArrayPoolMemoryManager.CreateWithAggressivePooling"/> option, but with less amount of buckets per every pool
        /// </summary>
        /// <returns>The memory manager</returns>
        public static ArrayPoolMemoryManager CreateWithLimitedLargePooling()
        {
            return new ArrayPoolMemoryManager(32 * 1024 * 1024 * 4, 8 * 1024 * 1024 * 4, 2, 4);
        }
    }
}