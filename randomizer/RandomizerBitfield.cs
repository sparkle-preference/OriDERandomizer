using System;

public class RandomizerBitfield {
    public readonly int StartId;
    public readonly int Size;

    public RandomizerBitfield(int randomizerIdStart, int numIds) {
        StartId = randomizerIdStart;
        Size = numIds * 32;
    }

    public bool Get(int bit) {
        if (bit < 0 || bit >= Size) {
            Randomizer.log($"Tried to read non-existent bit of bitfield. Bitfield has size {Size}, but tried to read bit {bit}.\n{Environment.StackTrace}");
            return false;
        }

        var valueIdx = bit / 32;
        var valueBit = bit % 32;
        var mask = 1 << valueBit;
        return (Randomizer.Inventory.GetRandomizerItem(StartId + valueIdx) & mask) != 0;
    }

    public void Set(int bit) {
        if (bit < 0 || bit >= Size) {
            Randomizer.log($"Tried to write non-existent bit of bitfield. Bitfield has size {Size}, but tried to read bit {bit}.\n{Environment.StackTrace}");
            return;
        }

        var valueIdx = bit / 32;
        var valueBit = bit % 32;
        var mask = 1 << valueBit;

        var oldValue = Randomizer.Inventory.GetRandomizerItem(StartId + valueIdx);
        var newValue = oldValue | mask;
        if (oldValue != newValue) {
            Randomizer.Inventory.SetRandomizerItem(StartId + valueIdx, newValue);
        }
    }

    public void Clear(int bit) {
        if (bit < 0 || bit >= Size) {
            Randomizer.log($"Tried to write non-existent bit of bitfield. Bitfield has size {Size}, but tried to read bit {bit}.\n{Environment.StackTrace}");
            return;
        }

        var valueIdx = bit / 32;
        var valueBit = bit % 32;
        var mask = 1 << valueBit;

        var oldValue = Randomizer.Inventory.GetRandomizerItem(StartId + valueIdx);
        var newValue = oldValue & ~mask;
        if (oldValue != newValue) {
            Randomizer.Inventory.SetRandomizerItem(StartId + valueIdx, newValue);
        }
    }

    public void Set(int bit, bool on) {
        if (on) {
            Set(bit);
        } else {
            Clear(bit);
        }
    }
}
