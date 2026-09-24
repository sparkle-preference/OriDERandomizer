using System;
using Game;
using UnityEngine;

// Archipelago DeathLink, armed by the seed's DeathLink flag.
// OUT: death counters ride the tick as dl=<total>.<linked>; the server diffs them.
// IN: a dl:<token>;<source> signal queues a kill applied on a stable frame. Every kill we cause
// bumps linked as well as total, so it never echoes back out.
public static class RandomizerDeathLink {
    // KeptOnDeath ids: they must not roll back under the server's last-seen count
    public const int Deaths = 4090;
    public const int LinkedDeaths = 4091;
    public const int LastToken = 4092;

    public static bool Enabled;

    private static bool killPending;
    private static string killSource;
    private static bool killInFlight;
    private static int stableFrames;
    // deaths count only while armed (stable, hp > 0); a save made mid-death respawns at 0 hp and never arms
    private static bool armed;

    // CanMove goes true before the respawn fade finishes
    private const int StableFramesNeeded = 15;

    public static void Reset() {
        Enabled = false;
        killPending = false;
        killSource = null;
        killInFlight = false;
        stableFrames = 0;
        armed = false;
    }

    // payload "<token>;<source>". Tokens only grow and the last one acted on is kept on death, so a
    // signal still riding the tick is not a second death.
    public static void OnSignal(string payload) {
        try {
            if (!Enabled || !Characters.Sein || Characters.Sein.Inventory == null) {
                return;
            }

            var parts = payload.Split(new[] { ';' }, 2);
            if (!int.TryParse(parts[0], out var token)
                || token <= Characters.Sein.Inventory.GetRandomizerItem(LastToken)) {
                return;
            }

            Characters.Sein.Inventory.SetRandomizerItem(LastToken, token);
            killPending = true;
            killSource = parts.Length > 1 ? parts[1] : "";
        } catch (Exception e) {
            Randomizer.LogError("DeathLink.OnSignal: " + e.Message);
        }
    }

    // Every death, ours or theirs. OnRecieveDamage calls this inline, so Apply() tells a landed kill
    // (killInFlight spent here) from a refused one (still owed).
    public static void OnDeath() {
        try {
            if (!Enabled || !Characters.Sein || Characters.Sein.Inventory == null) {
                return;
            }

            if (!armed) {
                // a re-fired hook (dead-save load) is the same death again
                killPending = false;
                killInFlight = false;
                stableFrames = 0;
                return;
            }
            armed = false;
            Characters.Sein.Inventory.IncRandomizerItem(Deaths, 1);
            if (killInFlight) {
                killInFlight = false;
                Characters.Sein.Inventory.IncRandomizerItem(LinkedDeaths, 1);
            }

            // any death pays an owed kill: no second death waiting after the respawn
            killPending = false;
            stableFrames = 0;
        } catch (Exception e) {
            Randomizer.LogError("DeathLink.OnDeath: " + e.Message);
        }
    }

    public static void Update() {
        try {
            if (!Enabled) {
                return;
            }

            var stable = Characters.Sein && Characters.Sein.Active
                && Characters.Sein.Controller.CanMove
                && !Characters.Sein.IsSuspended && !UI.MainMenuVisible
                && !Randomizer.CreditsActive
                && Characters.Sein.gameObject.activeInHierarchy
                && Randomizer.DamageModifier > 0f;
            if (!armed && stable && Characters.Sein.Mortality.Health.Amount > 0f) {
                armed = true;
            }

            if (!killPending) {
                return;
            }

            stableFrames = stable ? stableFrames + 1 : 0;
            if (stableFrames < StableFramesNeeded) {
                return;
            }

            stableFrames = 0;
            Apply();
        } catch (Exception e) {
            killPending = false;
            killInFlight = false;
            Randomizer.LogError("DeathLink.Update: " + e.Message);
        }
    }

    // the Wither kill (RandomizerBonusSkill 111). Lava, not Water, keeps OnRecieveDamage's
    // immortality and CanMove guards, so a refused kill stays owed.
    private static void Apply() {
        killInFlight = true;
        try {
            Characters.Sein.Mortality.DamageReciever.OnRecieveDamage(
                new Damage(
                    9000f,
                    Vector2.zero,
                    Characters.Sein.Position,
                    DamageType.Lava,
                    Characters.Sein.GameObject
                )
            );
        } finally {
            if (killInFlight) {
                killInFlight = false; // refused; try again on a later frame
            } else {
                killPending = false;
                Randomizer.printInfo(
                    string.IsNullOrEmpty(killSource)
                        ? "Killed by Archipelago"
                        : "Killed by " + killSource,
                    180
                );
            }
        }
    }

    // tick field: "<total>.<linked>", or null on every seed without the option
    public static string Field() {
        if (!Enabled || !Characters.Sein || Characters.Sein.Inventory == null) {
            return null;
        }

        return Characters.Sein.Inventory.GetRandomizerItem(Deaths) + "."
            + Characters.Sein.Inventory.GetRandomizerItem(LinkedDeaths);
    }
}
