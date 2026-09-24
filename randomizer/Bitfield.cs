using System;

public class Bitfield {
    private readonly int _size;
    private readonly uint[] _words;

    public Bitfield(int size) {
        _size = size;
        _words = new uint[(size + 31) / 32];
    }

    public bool Get(int bit) {
        if (bit < 0 || bit >= _size) {
            Randomizer.log($"Tried to read non-existent bit of bitfield. Bitfield has size {_size}, but tried to read bit {bit}.\n{Environment.StackTrace}");
            return false;
        }

        var valueIdx = bit / 32;
        var valueBit = bit % 32;
        var mask = 1 << valueBit;
        return (_words[valueIdx]  & mask) != 0;
    }

    public void Set(int bit) {
        if (bit < 0 || bit >= _size) {
            Randomizer.log($"Tried to write non-existent bit of bitfield. Bitfield has size {_size}, but tried to read bit {bit}.\n{Environment.StackTrace}");
            return;
        }

        var valueIdx = bit / 32;
        var valueBit = bit % 32;
        var mask = 1u << valueBit;
        _words[valueIdx] |= mask;
    }

    public void Clear(int bit) {
        if (bit < 0 || bit >= _size) {
            Randomizer.log($"Tried to write non-existent bit of bitfield. Bitfield has size {_size}, but tried to read bit {bit}.\n{Environment.StackTrace}");
            return;
        }

        var valueIdx = bit / 32;
        var valueBit = bit % 32;
        var mask = 1u << valueBit;
        _words[valueIdx] &= ~mask;
    }

    public void Set(int bit, bool on) {
        if (on) {
            Set(bit);
        } else {
            Clear(bit);
        }
    }

    public void ReadFrom(RandomizerBitfield other) {
        if (other.Size != _size) {
            Randomizer.log($"Tried to read randomizer bitfield with wrong length. Bitfield contains {_size} values, but other has {other.Size} values.\n{Environment.StackTrace}");
            return;
        }

        for (var i = 0; i < _words.Length; ++i) {
            _words[i] = (uint)Randomizer.Inventory.GetRandomizerItem(other.StartId + i);
        }
    }

    public void WriteTo(RandomizerBitfield other) {
        if (other.Size != _size) {
            Randomizer.log($"Tried to write randomizer bitfield with wrong length. Bitfield contains {_size} values, but other has {other.Size} values.\n{Environment.StackTrace}");
            return;
        }

        for (var i = 0; i < _words.Length; ++i) {
            Randomizer.Inventory.SetRandomizerItem(other.StartId + i, (int)_words[i]);
        }
    }
}
