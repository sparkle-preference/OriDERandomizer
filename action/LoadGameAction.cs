public class LoadGameAction : ActionMethod {
    public override void Perform(IContext context) {
        SaveSlotBackupsManager.ResetBackupDelay();
        InstantLoadScenesController.Instance.LockFinishingLoading = true;
        // a practice session keeps its own clock and nothing else
        RandomizerStatsManager.Active = !PracticeController.Active;
        GameStateMachine.Instance.SetToGame();
        if (!GameController.Instance.SaveGameController.PerformLoad()) {
        }

        RandomizerBonusSkill.DelayDrainUpdate();
    }
}
