using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SeinController))]
    public class SeinControllerPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(SeinController.HandleJumping))]
        public static bool HandleJumpPatch(SeinController __instance)
        {
            if (__instance.IgnoreControllerInput || __instance.LockMovementInput || !__instance.CanMove)
            {
                return false;
            }

            bool flag = false;
            bool flag2 = false;
            if (RandomizerSettings.Controls.GrenadeJump == RandomizerSettings.GrenadeJumpMode.Free)
            {
                flag = RandomizerRebinding.FreeGrenadeJump.OnPressed;
                flag2 = RandomizerRebinding.FreeGrenadeJump.Pressed;
            }

            if (Randomizer.GrenadeJumpQueued)
            {
                Randomizer.GrenadeJumpQueued = false;
                if (flag2 && CharacterState.IsActive(__instance.Sein.Abilities.WallChargeJump) &&
                    __instance.Sein.Abilities.GrabWall && __instance.Sein.Abilities.WallChargeJump.CanChargeJump &&
                    __instance.IsAimingGrenade)
                {
                    Core.Input.LeftShoulder.IsPressed = true;
                    Core.Input.Jump.IsPressed = true;
                }
            }

            if (flag && CharacterState.IsActive(__instance.Sein.Abilities.WallChargeJump) &&
                __instance.Sein.Abilities.GrabWall && __instance.Sein.Abilities.WallChargeJump.CanChargeJump &&
                __instance.Sein.Abilities.Grenade && __instance.Sein.Abilities.Grenade.CanAim &&
                !__instance.IsAimingGrenade)
            {
                Randomizer.GrenadeJumpQueued = true;
                Core.Input.LeftShoulder.IsPressed = true;
                Core.Input.Jump.IsPressed = false;
            }

            if (Core.Input.Jump.OnPressed)
            {
                __instance.PerformJump();
            }

            return false;
        }
    }
}