using System;
using Game;
using UnityEngine;
using Random = UnityEngine.Random;

public class RandomizerChaosGravity : RandomizerChaosEffect {
    public override void Clear() {
        Countdown = 0;
        WellActive = false;
        Characters.Sein.PlatformBehaviour.Gravity.BaseSettings.GravityAngle = 0f;
        ApplyGravityMultiplier(1f);
    }

    public override void Start() {
        Countdown = Random.Range(600, 1200);
        WellActive = false;
        int num = Random.Range(0, 16);
        if (num <= 2) {
            Randomizer.showChaosEffect("Gravity increase");
            ApplyGravityMultiplier(Random.Range(1.1f, 2f));
            return;
        }

        if (num <= 7) {
            Randomizer.showChaosEffect("Gravity decrease");
            ApplyGravityMultiplier(Random.Range(0.1f, 0.9f));
            return;
        }

        if (num <= 10) {
            Randomizer.showChaosEffect("Gravity shift");
            Characters.Sein.PlatformBehaviour.Gravity.BaseSettings.GravityAngle = Random.Range(45f, 315f);
            return;
        }

        if (num <= 13) {
            Randomizer.showChaosEffect("Weird gravity");
            Characters.Sein.PlatformBehaviour.Gravity.BaseSettings.GravityAngle = Random.Range(0f, 360f);
            ApplyGravityMultiplier(1f / Random.Range(0.5f, 2f));
            return;
        }

        if (num <= 15) {
            Randomizer.showChaosEffect("Gravity well");
            WellActive = true;
            WellPosition = new Vector2(Characters.Sein.Position.x + Random.Range(-20f, 20f), Characters.Sein.Position.y + Random.Range(-20f, 20f));
            WellStrength = Random.Range(16f, 32f);
        }
    }

    public override void Update() {
        if (Countdown == 0) {
            Clear();
        }

        Countdown--;
        if (Countdown < -600) {
            Countdown = 0;
        }

        if (WellActive) {
            Characters.Ori.Position = new Vector3(WellPosition.x, WellPosition.y);
            Vector2 vector = new Vector2(WellPosition.x - Characters.Sein.Position.x, WellPosition.y - Characters.Sein.Position.y);
            float num = WellStrength / Math.Max(1f, vector.sqrMagnitude);
            float friction = 1f;
            vector.Normalize();
            if (Characters.Sein.PlatformBehaviour.PlatformMovement.IsOnGround) {
                friction = 0.25f;
            }

            Characters.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedX = Characters.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedX + friction * num * vector.x;
            if (!Characters.Sein.PlatformBehaviour.PlatformMovement.IsOnGround || num > 26f) {
                Characters.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedY = Characters.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedY + num * vector.y;
            }
        }
    }

    public void ApplyGravityMultiplier(float multiplier) {
        Characters.Sein.PlatformBehaviour.Gravity.BaseSettings.GravityStrength = 26f * multiplier;
        Characters.Sein.PlatformBehaviour.Gravity.BaseSettings.MaxFallSpeed = 32f * multiplier;
        float jumpHeight = 3f / multiplier;
        Characters.Sein.Abilities.Jump.BackflipJumpHeight = jumpHeight;
        Characters.Sein.Abilities.Jump.CrouchJumpHeight = jumpHeight;
        Characters.Sein.Abilities.Jump.FirstJumpHeight = jumpHeight;
        Characters.Sein.Abilities.Jump.JumpIdleHeight = jumpHeight;
        Characters.Sein.Abilities.Jump.SecondJumpHeight = jumpHeight;
        Characters.Sein.Abilities.Jump.ThirdJumpHeight = jumpHeight;
    }

    public int Countdown;

    public bool WellActive;

    public Vector2 WellPosition;

    public float WellStrength;
}
