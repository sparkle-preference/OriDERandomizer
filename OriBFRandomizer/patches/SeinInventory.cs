namespace OriBFRandomizer.patches
{
    public static class SeinInventoryExt

    {
        public static int SetRandomizerItem(this SeinInventory _, int code, int value)
        {
            return Randomizer.Inventory.SetRandomizerItem(code, value);
        }

        public static int GetRandomizerItem(this SeinInventory _, int code)
        {
            return Randomizer.Inventory.GetRandomizerItem(code);
        }

        public static int IncRandomizerItem(this SeinInventory _, int code, int value)
        {
            return Randomizer.Inventory.IncRandomizerItem(code, value);
        }
    }
}