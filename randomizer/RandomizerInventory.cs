using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RandomizerInventory : SaveSerialize {
    public bool FinishedGinsoEscape {
        get => GetRandomizerItem(950) == 1;
        set => SetRandomizerItem(950, value ? 1 : 0);
    }

    public static RandomizerInventory Initialize() {
        var inventory = new GameObject("randomizerInventory").AddComponent<RandomizerInventory>();
        inventory.MoonGuid = new MoonGuid(new Guid("3daedf09-4080-405a-8b5b-bf35a4652fb1"));
        inventory.RegisterToSaveSceneManager(GameController.Instance.GetComponent<SaveSceneManager>());
        return inventory;
    }

    private Dictionary<int, int> randomizerItems = new Dictionary<int, int>();

    public int SetRandomizerItem(int code, int value) {
        randomizerItems[code] = value;
        return value;
    }

    public int GetRandomizerItem(int code) {
        if (randomizerItems.ContainsKey(code)) {
            return randomizerItems[code];
        }

        return 0;
    }

    public int IncRandomizerItem(int code, int value) {
        return SetRandomizerItem(code, GetRandomizerItem(code) + value);
    }

    public void Clear() {
        randomizerItems.Clear();
    }

    // Ids that survive a death or a reload. One range, so that "does this persist" is a
    // property of where an id lives rather than a list to remember: 4000-4599 stats, 4501 up
    // warmth fragments (one each, no fixed end), 7000-7099 bingo goals that record something
    // that HAPPENED rather than something you hold. Anything a save should forget goes below.
    public static bool KeptOnDeath(int code) {
        return code >= 4000 && code < 10000;
    }

    // Which slot the in-memory preserved values belong to, stored as slot+1 so an absent
    // stamp claims nothing. Sits just past the preserved range so it cannot vouch for itself.
    private const int SlotStamp = 10000;

    public override void Serialize(Archive ar) {
        if (ar.Reading) {
            // preserved values follow their save slot: on any other slot's load they are
            // another game's memory, and what the file says stands
            var slot = SaveSlotsManager.CurrentSlotIndex + 1;
            var preserve = GetRandomizerItem(SlotStamp) == slot
                ? randomizerItems.Where(item => KeptOnDeath(item.Key)).ToList()
                : new List<KeyValuePair<int, int>>();

            randomizerItems.Clear();
            var count = ar.Serialize(0);
            for (var i = 0; i < count; i++) {
                randomizerItems[ar.Serialize(0)] = ar.Serialize(0);
            }

            foreach (var kvp in preserve) {
                randomizerItems[kvp.Key] = kvp.Value;
            }

            randomizerItems[SlotStamp] = slot;
        } else {
            randomizerItems[SlotStamp] = SaveSlotsManager.CurrentSlotIndex + 1;
            ar.Serialize(randomizerItems.Count);
            foreach (var kvp in randomizerItems) {
                ar.Serialize(kvp.Key);
                ar.Serialize(kvp.Value);
            }
        }
    }
}
