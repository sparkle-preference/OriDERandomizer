using Game;

public class SaveGameAction : ActionMethod {
    public override void Perform(IContext context) {
        SaveSlotBackupsManager.CreateCurrentBackup();
        var current = 0f;
        var amount = 0f;
        SeinCharacter sein = Characters.Sein;
        if (sein) {
            current = sein.Energy.Current;
            amount = sein.Mortality.Health.Amount;
            if (Characters.Sein.Energy.Current < Characters.Sein.Energy.Max) {
                sein.Energy.Current = sein.Energy.Max;
            }

            if (Characters.Sein.Mortality.Health.Amount < Characters.Sein.Mortality.Health.MaxHealth) {
                sein.Mortality.Health.Amount = sein.Mortality.Health.MaxHealth;
            }
        }

        GameController.Instance.CreateCheckpoint();
        if (sein) {
            sein.Energy.Current = current;
            sein.Mortality.Health.Amount = amount;
        }

        GameController.Instance.SaveGameController.PerformSave();
        GameController.Instance.PerformSaveGameSequence();
    }
}
