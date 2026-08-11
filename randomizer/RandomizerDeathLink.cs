using System;
using Game;
using UnityEngine;

// Archipelago DeathLink. Enabled by the seed's `DeathLink` flag, so every
// other seed pays one bool check and sends exactly the bytes it always did.
//
// OUT: two counters on the 1 Hz tick (field `dl=<total>.<linked>`), which the
// server diffs. A counter rather than an event because it is idempotent under
// a dropped tick, needs no ack channel, and one second of granularity turns a
// death storm into one death for the room. Both live in save items 1500-1599,
// the range RandomizerInventory preserves through death and reload, so they
// never roll back out from under the server's last-seen value.
//
// IN: the `dl:<token>;<source>` tick signal. The kill is queued and applied
// on a stable frame -- dying inside a cutscene or a load is not a death, it
// is a wedge -- and every kill we cause increments `linked` as well as
// `total`, so the net count the server sees does not move and an incoming
// death can never bounce back out as an outgoing one.
public static class RandomizerDeathLink {
    // preserved-range save items (see RandomizerInventory.Serialize)
    public const int Deaths = 1590;
    public const int LinkedDeaths = 1591;
    public const int LastToken = 1592;

    public static bool Enabled;

    private static bool killPending;
    private static string killSource;
    private static bool killInFlight;
    private static int stableFrames;
    // deaths only count while armed; arming needs the player genuinely alive
    // (hp > 0, stable). A save made mid-death re-fires the death hook on
    // every load and respawns at 0 hp, so it can never re-arm.
    private static bool armed;

    // CanMove goes true before the respawn fade finishes; the win message
    // waits the same way (Randomizer.UpdatePendingWin)
    private const int StableFramesNeeded = 15;

    public static void Reset() {
        Enabled = false;
        killPending = false;
        killSource = null;
        killInFlight = false;
        stableFrames = 0;
        armed = false;
    }

    // tick signal payload: "<token>;<source>". The server's token only ever
    // grows, and the one we last acted on is saved in the preserved range, so
    // a signal still riding the tick -- after a respawn, or a seed reload
    // inside the confirm window -- is not a second death.
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

    // every death, ours or theirs. Consuming killInFlight here is what proves
    // the queued kill actually landed: OnRecieveDamage calls OnKill inline, so
    // by the time Apply() returns the latch is either spent or the damage was
    // refused and the kill is still owed.
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

            // dying of anything else while one is owed pays the debt: nobody
            // wants a second death waiting on the other side of the respawn
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

    // the shipped instant-kill recipe (RandomizerBonusSkill 111, Wither).
    // Lava rather than Water keeps the immortality and CanMove guards in
    // SeinDamageReciever.OnRecieveDamage, so a refused kill stays owed
    // instead of landing somewhere it shouldn't.
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
