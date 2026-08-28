using System.Collections.Generic;

// The shared clip table the wire format indexes into. Clips are ScriptableObjects resolved by
// name, and which ones a runtime sweep finds depends on what the game has loaded, so an index
// into a live sweep is not stable between two clients. This list is fixed at build time and
// every client carries the same one; Hash goes in the handshake so a mismatch is detectable
// rather than silently animating a peer wrongly.
//
// Regenerate with Dev on: the ghost sweep writes ghost-animations.txt beside the seed, and that
// file is this array. A name missing from it encodes as Unknown, which renders but does not
// animate -- a sliding Ori beats a wrong one.
public static class RandomizerGhostAnimations {
    public const int Unknown = 0xFFFF;

    public static int IndexOf(string name) {
        if (string.IsNullOrEmpty(name)) {
            return Unknown;
        }

        if (Lookup == null) {
            Lookup = new Dictionary<string, int>(Names.Length);
            for (var i = 0; i < Names.Length; i++) {
                Lookup[Names[i]] = i;
            }
        }

        return Lookup.ContainsKey(name) ? Lookup[name] : Unknown;
    }

    public static string NameOf(int index) {
        return index >= 0 && index < Names.Length ? Names[index] : null;
    }

    // FNV-1a over the table, computed rather than pasted so it cannot drift from the array.
    public static uint Hash {
        get {
            if (Hashed == 0u) {
                var hash = 2166136261u;
                foreach (var name in Names) {
                    foreach (var c in name) {
                        hash = (hash ^ c) * 16777619u;
                    }

                    hash = (hash ^ 10u) * 16777619u;
                }

                Hashed = hash;
            }

            return Hashed;
        }
    }

    private static uint Hashed;

    private static Dictionary<string, int> Lookup;

    public static readonly string[] Names = {
        "act1WakeupLong",
        "alert",
        "backflip",
        "bashChargeDiagonalDown",
        "bashChargeDiagonalUp",
        "bashChargeDown",
        "bashChargeHorizontal",
        "bashChargeUp",
        "bashDown",
        "bashDownDiagonal",
        "bashHorizontal",
        "bashJump",
        "bashUp",
        "bashUpDiagonal",
        "bounceJumpA",
        "bounceJumpB",
        "cantPull",
        "cantPush",
        "carryDrop",
        "carryFall",
        "carryFallIdle",
        "carryGrab",
        "carryIdle",
        "carryJump",
        "carryJumpIdle",
        "carryRun",
        "chargeDash",
        "chargeJump",
        "concaveA",
        "concaveB",
        "concaveC",
        "convexA",
        "convexB",
        "convexC",
        "crouch",
        "crouchJump",
        "crouchWalk",
        "dash",
        "dashBlockedWall",
        "dead",
        "deadEdge",
        "death",
        "doubleJump",
        "drown",
        "edgeClamber",
        "edgeClimb",
        "edgeJump",
        "end",
        "fall",
        "fallIdle",
        "findOri",
        "getAbility",
        "getFeather",
        "getItem",
        "getItemB",
        "glide",
        "glideMove",
        "grabBlockIdle",
        "grabBlockPull",
        "grabBlockPush",
        "grabWall",
        "grabWallAway",
        "grabWallAwayA",
        "grabWallAwayB",
        "grabWallAwayC",
        "grabWallAwayD",
        "grabWallAwayE",
        "grabWallAwayF",
        "grabWallAwayG",
        "grabWallAwayH",
        "grabWallAwayI",
        "grabWallAwayJ",
        "grabWallAwayK",
        "grabWallAwayL",
        "grabWallDown",
        "grabWallUp",
        "grenadeA",
        "grenadeB",
        "grenadeC",
        "grenadeD",
        "grenadeE",
        "grenadeF",
        "grenadeFail",
        "grenadeG",
        "grenadeH",
        "grenadeI",
        "grenadeJ",
        "grenadeK",
        "grenadeL",
        "grenadeM",
        "grenadeN",
        "grenadeO",
        "grenadeP",
        "grenadeQ",
        "grenadeR",
        "grenadeThrow",
        "grenadeThrowDown",
        "grenadeThrowDownWall",
        "grenadeThrowFall",
        "grenadeThrowFallVertical",
        "grenadeThrowJump",
        "grenadeThrowJumpIdle",
        "grenadeThrowRun",
        "grenadeThrowUp",
        "grenadeThrowUpWall",
        "grenadeThrowVerticalWall",
        "grenadeWallA",
        "grenadeWallB",
        "grenadeWallC",
        "grenadeWallD",
        "grenadeWallE",
        "grenadeWallF",
        "grenadeWallFail",
        "grenadeWallG",
        "grenadeWallH",
        "grenadeWallI",
        "grenadeWallJ",
        "grenadeWallK",
        "grenadeWallL",
        "grenadeWallM",
        "grenadeWallN",
        "grenadeWallO",
        "grenadeWallP",
        "grenadeWallQ",
        "grenadeWallR",
        "hurt",
        "idle",
        "idleListen",
        "idleLookDistance",
        "idleSlopeDown",
        "idleSlopeUp",
        "idleUp",
        "idleYawn",
        "jog",
        "jump",
        "jumpB",
        "jumpC",
        "jumpFlip",
        "jumpIdle",
        "jumpIdleB",
        "jumpIdleC",
        "leverBackwards",
        "leverForwards",
        "leverIdle",
        "loop",
        "oriScared",
        "pushAgainstWall",
        "ragdoll",
        "respawn",
        "run",
        "slugCrawlUpright",
        "slugShootProjectile",
        "slugStarChargeUpright",
        "slugStarShootUpright",
        "slugThrown",
        "standingOnEdge",
        "standingOnEdgeBack",
        "start",
        "stomp",
        "stompLand",
        "stomped",
        "stunned",
        "superJumpSide",
        "swimBashDiagonalDown",
        "swimBashDiagonalUp",
        "swimBashDown",
        "swimBashHorizontal",
        "swimBashUp",
        "swimConcaveA",
        "swimConcaveB",
        "swimConcaveC",
        "swimConcaveD",
        "swimConcaveE",
        "swimConcaveF",
        "swimConcaveG",
        "swimConcaveH",
        "swimConcaveI",
        "swimConcaveJ",
        "swimConcaveK",
        "swimConcaveL",
        "swimConvexA",
        "swimConvexB",
        "swimConvexC",
        "swimConvexD",
        "swimConvexE",
        "swimConvexF",
        "swimConvexG",
        "swimConvexH",
        "swimConvexI",
        "swimConvexJ",
        "swimConvexK",
        "swimConvexL",
        "swimDrown",
        "swimIdle",
        "swimIdleTransitonAnticlock",
        "swimIdleTransitonClock",
        "swimJumpLeftConcaveA",
        "swimJumpLeftConcaveB",
        "swimJumpLeftConcaveC",
        "swimJumpLeftConcaveD",
        "swimJumpLeftConcaveE",
        "swimJumpLeftConcaveF",
        "swimJumpLeftConcaveG",
        "swimJumpLeftConcaveH",
        "swimJumpLeftConcaveI",
        "swimJumpLeftConcaveJ",
        "swimJumpLeftConcaveK",
        "swimJumpLeftConcaveL",
        "swimJumpLeftConvexA",
        "swimJumpLeftConvexB",
        "swimJumpLeftConvexC",
        "swimJumpLeftConvexD",
        "swimJumpLeftConvexE",
        "swimJumpLeftConvexF",
        "swimJumpLeftConvexG",
        "swimJumpLeftConvexH",
        "swimJumpLeftConvexI",
        "swimJumpLeftConvexJ",
        "swimJumpLeftConvexK",
        "swimJumpLeftConvexL",
        "swimJumpLeftMiddle",
        "swimMiddle",
        "swimMiddleToMiddleFlip",
        "swimSurface",
        "swimSurfaceIdle",
        "swimUpsideDownFlip",
        "swimUpsideDownToMiddleFlip",
        "wakeUp",
        "walk",
        "wallJumpA",
        "wallJumpAway",
        "wallJumpAwayB",
        "wallJumpB",
        "wallJumpTowards",
        "wallJumpTowards2",
        "wallSlideDown",
        "wallSlideUp",
    };
}
