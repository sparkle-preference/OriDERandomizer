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

    // Ids that survive a death or a reload, in two blocks:
    //   1500-1599  randomizer stats (playtime, deaths, pickup counts, ...)
    //   2300-2399  bingo goals that record something that HAPPENED rather than
    //              something you hold -- dying to a named hazard, mostly.
    public static bool KeptOnDeath(int code) {
        return (code >= 1500 && code < 1600) || (code >= 2300 && code < 2400);
    }

    public override void Serialize(Archive ar) {
        if (ar.Reading) {
            var preserve = randomizerItems.Where(item => KeptOnDeath(item.Key)).ToList();

            randomizerItems.Clear();
            var count = ar.Serialize(0);
            for (var i = 0; i < count; i++) {
                randomizerItems[ar.Serialize(0)] = ar.Serialize(0);
            }

            foreach (var kvp in preserve) {
                randomizerItems[kvp.Key] = kvp.Value;
            }
        } else {
            ar.Serialize(randomizerItems.Count);
            foreach (var kvp in randomizerItems) {
                ar.Serialize(kvp.Key);
                ar.Serialize(kvp.Value);
            }
        }
    }
}
