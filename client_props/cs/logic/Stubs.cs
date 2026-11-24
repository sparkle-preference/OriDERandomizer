// The engine state Inventory.FromCharacter reads, as plain fields (its scratch copy imports this namespace).
using System.Collections.Generic;

namespace ProtogenStubs {
    public class Ability {
        public bool HasAbility;
    }

    public class Abilities {
        public Ability WallJump = new Ability(), ChargeFlame = new Ability(), DoubleJump = new Ability(), Bash = new Ability(),
            Stomp = new Ability(), Glide = new Ability(), Climb = new Ability(), ChargeJump = new Ability(),
            Grenade = new Ability(), Dash = new Ability();
    }

    public class HealthStub {
        public float MaxHealth;
    }

    public class MortalityStub {
        public HealthStub Health = new HealthStub();
    }

    public class EnergyStub {
        public float Max;
    }

    public class InventoryStub {
        public int Keystones, MapStones, SkillPointsCollected;
    }

    public class SeinStub {
        public MortalityStub Mortality = new MortalityStub();
        public EnergyStub Energy = new EnergyStub();
        public InventoryStub Inventory = new InventoryStub();
        public Abilities PlayerAbilities = new Abilities();
    }

    public static class Characters {
        public static SeinStub Sein = new SeinStub();
    }

    public static class Events {
        public static bool WaterPurified, WindRestored;
    }

    public static class Keys {
        public static bool GinsoTree, ForlornRuins, MountHoru;
    }

    public class Teleporter {
        public bool Activated;
        public string Identifier;
    }

    public class TeleporterController {
        public static TeleporterController Instance = new TeleporterController();
        public List<Teleporter> Teleporters = new List<Teleporter>();
    }
}
