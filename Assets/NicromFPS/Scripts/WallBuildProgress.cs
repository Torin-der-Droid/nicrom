namespace NicromFPS
{
    public sealed class WallBuildProgress
    {
        public const int Width = 5;
        public int BuiltCount { get; private set; }
        public int Layer => BuiltCount / Width;
        public int Column => BuiltCount % Width;

        public bool TrySupply(ResourceInventory inventory, bool spaceIsClear)
        {
            if (!spaceIsClear || inventory == null || !inventory.TryRemove("gray_cube", 1)) return false;
            BuiltCount++;
            return true;
        }
    }
}
