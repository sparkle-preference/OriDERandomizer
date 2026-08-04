using System;
using System.Collections.Generic;
using System.IO;
using Core;
using Game;

[Serializable]
public class SaveGameController {
    public int CurrentSlotIndex => SaveSlotsManager.CurrentSlotIndex;

    public int CurrentBackupIndex => SaveSlotsManager.BackupIndex;

    public bool SaveGameQueried => true;

    public void SaveToFile(string filename) {
        using (var binaryWriter = new BinaryWriter(File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))) {
            SaveToWriter(binaryWriter);
        }
    }

    public bool LoadFromFile(string filename) {
        bool result;
        using (var binaryReader = new BinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) {
            result = LoadFromReader(binaryReader);
        }

        return result;
    }

    public byte[] SaveToBytes() {
        var memoryStream = new MemoryStream();
        using (var binaryWriter = new BinaryWriter(memoryStream)) {
            SaveToWriter(binaryWriter);
        }

        return memoryStream.ToArray();
    }

    public void SaveToWriter(BinaryWriter writer) {
        SaveSlotsManager.CurrentSaveSlot.SaveToWriter(writer);
        Game.Checkpoint.SaveGameData.SaveToWriter(writer);
    }

    private bool SaveWasOneLifeAndKilled {
        get {
            var currentSaveSlot = SaveSlotsManager.CurrentSaveSlot;
            return currentSaveSlot.Difficulty == DifficultyMode.OneLife && currentSaveSlot.WasKilled;
        }
    }

    public bool LoadFromReader(BinaryReader reader) {
        if (!SaveSlotsManager.CurrentSaveSlot.LoadFromReader(reader)) {
            return false;
        }

        if (!Game.Checkpoint.SaveGameData.LoadFromReader(reader)) {
            return false;
        }

        if (SaveWasOneLifeAndKilled) {
            SaveSceneManager.ClearSaveSlotForOneLife(Game.Checkpoint.SaveGameData);
        }

        return true;
    }

    public bool LoadFromBytes(byte[] binary) {
        bool result;
        using (var binaryReader = new BinaryReader(new MemoryStream(binary))) {
            result = LoadFromReader(binaryReader);
        }

        return result;
    }

    public bool SaveExists(int slotIndex) {
        if (!CanPerformLoad()) {
            return false;
        }

        if (Recorder.Instance && Recorder.Instance.State == Recorder.RecorderState.Playing) {
            var frameDataOfType = Recorder.Instance.CurrentFrame.GetFrameDataOfType<InputData>();
            return frameDataOfType != null && frameDataOfType.SaveFileExists;
        }

        return File.Exists(GetSaveFilePath(slotIndex));
    }

    public bool SaveFileExists {
        get {
            if (!CanPerformLoad()) {
                return false;
            }

            if (Recorder.Instance && Recorder.Instance.State == Recorder.RecorderState.Playing) {
                var frameData = Recorder.Instance.CurrentFrame.GetFrameData<InputData>();
                if (frameData != null) {
                    var inputData = frameData[0];
                    if (inputData != null) {
                        return inputData.SaveFileExists;
                    }
                }

                return false;
            }

            return File.Exists(CurrentSaveFilePath);
        }
    }

    public string CurrentSaveFilePath => GetSaveFilePath(CurrentSlotIndex);

    public string GetSaveFilePath(int slotIndex, int backupIndex = -1) {
        if (backupIndex == -1) {
            return Path.Combine(OutputFolder.PlayerDataFolderPath, "saveFile" + slotIndex + ".sav");
        }

        return Path.Combine(OutputFolder.PlayerDataFolderPath, string.Format("saveFile{0}_bkup{1}.sav", slotIndex, backupIndex));
    }

    public void Refresh() {
        CanPerformLoad();
    }

    public bool PerformLoad() {
        if (Recorder.IsPlaying) {
            return Recorder.Instance.OnPerformLoad();
        }

        if (!CanPerformLoad()) {
            return false;
        }

        var result = LoadFromFile(GetSaveFilePath(CurrentSlotIndex, CurrentBackupIndex));
        RestoreCheckpoint();
        return result;
    }

    public bool PerformLoadWithoutCheckpointRestore() {
        if (Recorder.IsPlaying) {
            return Recorder.Instance.OnPerformLoad();
        }

        return CanPerformLoad() && LoadFromFile(GetSaveFilePath(CurrentSlotIndex, CurrentBackupIndex));
    }

    public bool OnLoadComplete(byte[] buffer) {
        var result = LoadFromBytes(buffer);
        RestoreCheckpoint();
        return result;
    }

    public void PerformSave() {
        if (!CanPerformSave()) {
            return;
        }

        Randomizer.OnSave();
        SaveSlotsManager.CurrentSaveSlot.FillData();
        SaveSlotsManager.BackupIndex = -1;
        SaveToFile(CurrentSaveFilePath);
        if (Recorder.IsRecordering) {
            Recorder.Instance.OnPerformSave();
        }
    }

    public bool CanPerformLoad() {
        return !GameController.Instance.IsDemo;
    }

    public bool CanPerformSave() {
        return !Recorder.IsPlaying && !GameController.Instance.IsDemo;
    }

    public void OnSaveComplete() {
    }

    public void RestoreCheckpoint() {
        GameController.Instance.IsLoadingGame = true;
        LateStartHook.AddLateStartMethod(RestoreCheckpointPart1);
    }

    public void RestoreCheckpointPart1() {
        GameController.Instance.IsLoadingGame = true;
        Game.Checkpoint.SaveGameData.ClearPendingScenes();
        var hashSet = new HashSet<SaveSerialize>();
        hashSet.Add(Scenes.Manager);
        hashSet.Add(GameController.Instance);
        hashSet.Add(SeinWorldState.Instance);
        SaveSceneManager.Master.Load(Game.Checkpoint.SaveGameData.Master, hashSet);
        Scenes.Manager.AutoLoadingUnloading = false;
        GoToSceneController.Instance.StartInScene = MoonGuid.Empty;
        Game.Checkpoint.SaveGameData.ClearPendingScenes();
        Scenes.Manager.MarkLoadingScenesAsCancel();
        if (SaveWasOneLifeAndKilled) {
            var sceneInformation = Scenes.Manager.GetSceneInformation("sunkenGladesRunaway");
            GameController.Instance.RequireInitialValues = true;
            GameStateMachine.Instance.SetToGame();
            DifficultyController.Instance.ChangeDifficulty(DifficultyMode.OneLife);
            GoToSceneController.Instance.StartInScene = sceneInformation.SceneMoonGuid;
            GameController.Instance.IsLoadingGame = false;
            GoToSceneController.Instance.GoToSceneAsync(sceneInformation, OnFinishedLoading, false);
            return;
        }

        InstantLoadScenesController.Instance.OnScenesEnabledCallback = OnFinishedLoading;
        InstantLoadScenesController.Instance.LoadScenesAtPosition(null, true, false);
    }

    public void OnFinishedLoading() {
        GameController.Instance.MainMenuCanBeOpened = true;
        UI.Cameras.Current.Controller.PuppetController.Reset();
        GameController.Instance.RestoreCheckpointImmediate();
        Scenes.Manager.MarkActiveScenesAsKeepLoaded();
    }

    public const int MAX_SAVES = 10;
}
