using UnityEngine;

public static class RandomizerLayers {
    public static void Initialize() {
        SetupSolidDamageLayer();
    }

    private static void SetupSolidDamageLayer() {
        for (var i = 0; i < 32; ++i) {
            var ignoreSolids = Physics.GetIgnoreLayerCollision(i, Solids);
            var ignoreDamage = Physics.GetIgnoreLayerCollision(i, KillEverything);
            Physics.IgnoreLayerCollision(i, RandomizerSolidDamage, ignoreSolids && ignoreDamage);
        }
    }

    // Vanilla Layers

    // Layer 0
    public static readonly int Default = LayerMask.NameToLayer("Default");

    // Layer 1
    public static readonly int TransparentFX = LayerMask.NameToLayer("TransparentFX");

    // Layer 2
    public static readonly int IgnoreRaycast = LayerMask.NameToLayer("Ignore Raycast");

    // Layer 4
    public static readonly int Water = LayerMask.NameToLayer("Water");

    // Layer 5
    public static readonly int UI = LayerMask.NameToLayer("UI");

    // Layer 8
    public static readonly int Ui = LayerMask.NameToLayer("ui");

    // Layer 9
    public static readonly int Player = LayerMask.NameToLayer("player");

    // Layer 10
    public static readonly int Solids = LayerMask.NameToLayer("solids");

    // Layer 11
    public static readonly int Art = LayerMask.NameToLayer("art");

    // Layer 12
    public static readonly int Character = LayerMask.NameToLayer("character");

    // Layer 13
    public static readonly int CharacterMovement = LayerMask.NameToLayer("characterMovement");

    // Layer 14
    public static readonly int WorldMap1 = 14; // Name is "worldMap", same as layer 29

    // Layer 15
    public static readonly int Items = LayerMask.NameToLayer("items");

    // Layer 16
    public static readonly int KillCharacter = LayerMask.NameToLayer("killCharacter");

    // Layer 17
    public static readonly int KillEverything = LayerMask.NameToLayer("killEverything");

    // Layer 18
    public static readonly int PushPullBlock = LayerMask.NameToLayer("pushPullBlock");

    // Layer 19
    public static readonly int Platform = LayerMask.NameToLayer("platform");

    // Layer 20
    public static readonly int ResampleBuffer = LayerMask.NameToLayer("resampleBuffer");

    // Layer 21
    public static readonly int CharacterMovementIgnorePlatforms = LayerMask.NameToLayer("characterMovementIgnorePlatforms");

    // Layer 22
    public static readonly int ArtReflected = LayerMask.NameToLayer("artReflected");

    // Layer 23
    public static readonly int Debris = LayerMask.NameToLayer("debris");

    // Layer 24
    public static readonly int DebrisNoCollision = LayerMask.NameToLayer("debrisNoCollsion");

    // Layer 25
    public static readonly int ArtBlurred = LayerMask.NameToLayer("artBlurred");

    // Layer 26
    public static readonly int Projectile = LayerMask.NameToLayer("projectile");

    // Layer 27
    public static readonly int EarlyZ = LayerMask.NameToLayer("earlyZ");

    // Layer 28
    public static readonly int ArtBlurredReflected = LayerMask.NameToLayer("artBlurredReflected");

    // Layer 29
    public static readonly int WorldMap2 = 29; // Name is "worldMap", same as layer 14

    // Layer 30
    public static readonly int Laser = LayerMask.NameToLayer("laser");

    // Randomizer Layers

    // A layer that collides with everything the "solids" and/or "killEverything" layers collide with
    public static readonly int RandomizerSolidDamage = 3;
}
