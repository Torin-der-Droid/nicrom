namespace Akila.FPSFramework
{
    internal static class InventorySelection
    {
        internal static int Normalize(int index, int count, bool hasDefault)
        {
            if (count <= 0 || (index == -1 && hasDefault)) return -1;
            return index < 0 ? 0 : index >= count ? count - 1 : index;
        }

        internal static int Cycle(int index, int count, int direction)
        {
            if (count <= 0) return -1;
            if (index < 0) return direction < 0 ? count - 1 : 0;
            return (int)(((long)index + direction) % count + count) % count;
        }
    }
}
