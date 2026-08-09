using System;
using Game;
using UnityEngine;

public class SeinPickupProcessor : SaveSerialize, ISeinReceiver, IPickupCollector, ICheckpointZoneReciever {
    public void OnCollectSkillPointPickup(SkillPointPickup skillPointPickup) {
        if (!RandomizerLocationManager.IsPickupRepeatable(skillPointPickup.MoonGuid) || Randomizer.RepeatableCheck()) {
            RandomizerLocationManager.GivePickup(skillPointPickup.MoonGuid);
        }

        if (RandomizerLocationManager.IsPickupRepeatable(skillPointPickup.MoonGuid)) {
            return;
        }

        skillPointPickup.Collected();
        if (GameWorld.Instance.CurrentArea != null) {
            GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
        }
    }

    public void OnCollectEnergyOrbPickup(EnergyOrbPickup energyOrbPickup) {
        float num = energyOrbPickup.Amount;
        if (Sein.PlayerAbilities.EnergyEfficiency.HasAbility) {
            num *= 1.5f;
        }

        bool couldAffordBefore = Sein.SoulFlame.CanAffordSoulFlame;
        AchievementsLogic.Instance.OnCollectedEnergyShard();
        Sein.Energy.Gain(num);
        energyOrbPickup.Collected();
        if (!couldAffordBefore && Sein.SoulFlame.CanAffordSoulFlame) {
            UI.SeinUI.ShakeSoulFlame();
        }

        if (!Sein.PlayerAbilities.WallJump.HasAbility) {
            EnergyOrbInfo.RunActionIfFirstTime();
        }

        UI.SeinUI.ShakeEnergyOrbBar();
    }

    public void OnCollectMaxEnergyContainerPickup(MaxEnergyContainerPickup energyContainerPickup) {
        if (!RandomizerLocationManager.IsPickupRepeatable(energyContainerPickup.MoonGuid) || Randomizer.RepeatableCheck()) {
            RandomizerLocationManager.GivePickup(energyContainerPickup.MoonGuid);
        }

        if (RandomizerLocationManager.IsPickupRepeatable(energyContainerPickup.MoonGuid)) {
            return;
        }

        energyContainerPickup.Collected();
        if (GameWorld.Instance.CurrentArea != null) {
            GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
        }
    }

    public void OnCollectExpOrbPickup(ExpOrbPickup expOrbPickup) {
        int num = RandomizerBonus.ExpWithBonuses(expOrbPickup.Amount, false);
        if (expOrbPickup.MessageType == ExpOrbPickup.ExpOrbMessageType.None) {
            expOrbPickup.Collected();
            if (Randomizer.IgnoreEnemyExp)
                return;
            RandomizerBonus.ExpWithBonuses(expOrbPickup.Amount, true);
            Sein.Level.GainExperience(num);
            if (m_expText && m_expText.gameObject.activeInHierarchy)
                m_expText.Amount += num;
            else
                m_expText = Orbs.OrbDisplayText.Create(Characters.Sein.Transform, Vector3.up, num);
            UI.SeinUI.ShakeExperienceBar();
            if (GameWorld.Instance.CurrentArea != null)
                GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
        } else {
            if (!RandomizerLocationManager.IsPickupRepeatable(expOrbPickup.MoonGuid) || Randomizer.RepeatableCheck()) {
                RandomizerLocationManager.GivePickup(expOrbPickup.MoonGuid);
            }

            if (RandomizerLocationManager.IsPickupRepeatable(expOrbPickup.MoonGuid)) {
                return;
            }

            if (GameWorld.Instance.CurrentArea != null)
                GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
            expOrbPickup.Collected();
        }
    }

    public void OnCollectKeystonePickup(KeystonePickup keystonePickup) {
        if (!RandomizerLocationManager.IsPickupRepeatable(keystonePickup.MoonGuid) || Randomizer.RepeatableCheck()) {
            RandomizerLocationManager.GivePickup(keystonePickup.MoonGuid);
        }

        if (RandomizerLocationManager.IsPickupRepeatable(keystonePickup.MoonGuid)) {
            return;
        }

        keystonePickup.Collected();
        if (GameWorld.Instance.CurrentArea != null) {
            GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
        }
    }

    public void OnCollectMaxHealthContainerPickup(MaxHealthContainerPickup maxHealthContainerPickup) {
        if (!RandomizerLocationManager.IsPickupRepeatable(maxHealthContainerPickup.MoonGuid) || Randomizer.RepeatableCheck()) {
            RandomizerLocationManager.GivePickup(maxHealthContainerPickup.MoonGuid);
        }

        if (RandomizerLocationManager.IsPickupRepeatable(maxHealthContainerPickup.MoonGuid)) {
            return;
        }

        maxHealthContainerPickup.Collected();
        if (GameWorld.Instance.CurrentArea != null) {
            GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
        }
    }

    public void OnCollectRestoreHealthPickup(RestoreHealthPickup restoreHealthPickup) {
        int amount = restoreHealthPickup.Amount * ((!Sein.PlayerAbilities.HealthEfficiency.HasAbility) ? 1 : 2);
        Sein.Mortality.Health.GainHealth(amount);
        restoreHealthPickup.Collected();
        UI.SeinUI.ShakeHealthbar();
        if (!Sein.PlayerAbilities.WallJump.HasAbility) {
            HealthOrbInfo.RunActionIfFirstTime();
        }
    }

    public void OnCollectMapStonePickup(MapStonePickup mapStonePickup) {
        if (!RandomizerLocationManager.IsPickupRepeatable(mapStonePickup.MoonGuid) || Randomizer.RepeatableCheck()) {
            RandomizerLocationManager.GivePickup(mapStonePickup.MoonGuid);
        }

        if (RandomizerLocationManager.IsPickupRepeatable(mapStonePickup.MoonGuid)) {
            return;
        }

        mapStonePickup.Collected();
        if (GameWorld.Instance.CurrentArea != null) {
            GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
        }
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
    }

    public void OnEnterCheckpoint(InvisibleCheckpoint checkpoint) {
        if (Sein.IsSuspended) {
            return;
        }

        Vector3 position = Sein.Position;
        if (checkpoint.RespawnPosition != Vector2.zero) {
            Sein.Position = new Vector3(checkpoint.RespawnPosition.x, checkpoint.RespawnPosition.y) + checkpoint.transform.position;
        }

        GameController.Instance.CreateCheckpoint();
        Sein.Position = position;
        checkpoint.OnCheckpointCreated();
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref ExpOrbInfo.HasBeenCollectedBefore);
        ar.Serialize(ref KeystoneInfo.HasBeenCollectedBefore);
        ar.Serialize(ref EnergyOrbInfo.HasBeenCollectedBefore);
        ar.Serialize(ref HealthOrbInfo.HasBeenCollectedBefore);
        ar.Serialize(ref SmallExpOrbInfo.HasBeenCollectedBefore);
        ar.Serialize(ref MediumExpOrbInfo.HasBeenCollectedBefore);
        ar.Serialize(ref LargeExpOrbInfo.HasBeenCollectedBefore);
        ar.Serialize(ref m_collectedMaxEnergySlotsCount);
        ar.Serialize(ref m_energySlotsAchievementAwarded);
        ar.Serialize(ref m_collectedHealthSlotsCount);
        ar.Serialize(ref m_healthSlotsAchievementAwarded);
    }

    public SeinCharacter Sein;

    public CollectableInformation ExpOrbInfo = new CollectableInformation();

    public CollectableInformation KeystoneInfo = new CollectableInformation();

    public CollectableInformation EnergyOrbInfo = new CollectableInformation();

    public CollectableInformation HealthOrbInfo = new CollectableInformation();

    public CollectableInformation SmallExpOrbInfo = new CollectableInformation();

    public CollectableInformation MediumExpOrbInfo = new CollectableInformation();

    public CollectableInformation LargeExpOrbInfo = new CollectableInformation();

    public ActionMethod HeartContainerSequence;

    public ActionMethod SkillPointSequence;

    public ActionMethod EnergyContainerSequence;

    public ActionMethod MapStoneSequence;

    private ExpText m_expText;

    public AchievementAsset Collect200EnergyCrystalsAchievementAsset;

    public AchievementAsset AllEnergyCellsCollected;

    public AchievementAsset AllHealthCellsCollected;

    private int m_collectedMaxEnergySlotsCount;

    private bool m_energySlotsAchievementAwarded;

    private int m_collectedHealthSlotsCount;

    private bool m_healthSlotsAchievementAwarded;

    public static Action OnCollectMaxEnergyContainer = delegate { };

    [Serializable]
    public class CollectableInformation {
        public void RunActionIfFirstTime() {
        }

        public bool HasBeenCollectedBefore;

        public ActionMethod FirstTimeCollectedSequence;
    }
}
