using System;
using System.Collections.Generic;
using Core;
using UnityEngine;

// The file select, with two more faces: the practice chooser, where every card is a
// segment, and the session view, where the three practice slots sit at positions 1-3.
public class SaveSlotsUI : MonoBehaviour, ISuspendable {
    public static SaveSlotsUI Instance;

    public SaveSlotsItemsUI ItemsUI;

    public SaveSlotUI CopyingFrom;

    public ConfirmOrCancel OverrideQuestion;

    public ConfirmOrCancel DeleteQuestion;

    public int CurrentSlotIndex;

    private ConfirmOrCancel m_prompt;

    public ActionMethod EmptySaveSlotPressedAction;

    public ActionMethod UsedSaveSlotPressedAction;

    public ActionMethod PressedNotReadyAction;

    public ActionMethod OnBackPressedAction;

    public bool Active = true;

    public SoundProvider SelectSound;

    public SoundProvider BeginCopySound;

    public SoundProvider CopySound;

    public SoundProvider BeginDeleteSound;

    public SoundProvider DeleteSound;

    public SoundProvider CancelCopySound;

    public SoundProvider CancelDeleteSound;

    public SoundProvider OpenDifficultyMenuSound;

    public SoundProvider CancelDifficultyMenuSound;

    public MessageProvider CompletedGameMessageProvider;

    public MessageProvider CopyLegendMessageProvider;

    public MessageProvider PasteLegendMessageProvider;

    public MessageBox CopyLegend;

    public TransparencyAnimator FadeAnimator;

    private bool m_isVisible;

    private GameObject m_difficultyScreen;

    public SaveSlotUI CurrentSaveSlot {
        get {
            if (CurrentSlotIndex < 0 || CurrentSlotIndex >= Items.Count) {
                return null;
            }

            return Items[CurrentSlotIndex];
        }
    }

    public List<SaveSlotUI> Items => ItemsUI.Items;

    public bool IsVisible => m_isVisible;

    public SaveSlotUI SaveSlotUnderCursor {
        get {
            Vector2 cursorPositionUI = Core.Input.CursorPositionUI;
            foreach (var item in Items) {
                if (item != null && item.Bounds.Contains(cursorPositionUI)) {
                    return item;
                }
            }

            return null;
        }
    }

    public bool PromptIsOpen => m_prompt != null;

    public bool IsCopying => CopyingFrom != null;

    public bool SelectingDifficulty => m_difficultyScreen != null;

    public bool ClickedCurrentItem {
        get {
            return Core.Input.LeftClick.OnPressed && CurrentSaveSlot != null && CurrentSaveSlot == SaveSlotUnderCursor;
        }
    }

    public bool IsSuspended { get; set; }

    public void SetVisible(bool visible) {
        if (visible) {
            FadeAnimator.gameObject.SetActive(true);
            m_isVisible = true;
            FadeAnimator.Initialize();
            FadeAnimator.AnimatorDriver.ContinueForward();
        } else {
            m_isVisible = false;
            FadeAnimator.Initialize();
            FadeAnimator.AnimatorDriver.ContinueBackwards();
        }
    }

    public void SetVisibleImmediate(bool visible) {
        if (visible) {
            FadeAnimator.gameObject.SetActive(true);
            m_isVisible = true;
            FadeAnimator.Initialize();
            FadeAnimator.AnimatorDriver.GoToEnd();
            FadeAnimator.AnimatorDriver.Pause();
        } else {
            m_isVisible = false;
            FadeAnimator.Initialize();
            FadeAnimator.AnimatorDriver.GoToStart();
            FadeAnimator.AnimatorDriver.Pause();
            FadeAnimator.gameObject.SetActive(false);
        }
    }

    public void OnEnable() {
        m_isVisible = true;
        Active = true;
        if (FadeAnimator) {
            FadeAnimator.Initialize();
            FadeAnimator.AnimatorDriver.ContinueForward();
        }

        PracticeSelect.Shown(this);
        RefreshSlots();
    }

    public void OnDisable() {
        m_isVisible = false;
        if (m_prompt) {
            InstantiateUtility.Destroy(m_prompt.gameObject);
        }

        if (IsCopying) {
            CancelCopying();
        }

        PracticeSelect.Hidden(this);
    }

    public void Awake() {
        Instance = this;
        SuspensionManager.Register(this);
    }

    public void RefreshSlots() {
        ItemsUI.Refresh();
        ClampCurrentItemIndex();
        if (CurrentSaveSlot) {
            CurrentSaveSlot.Highlight(true);
        }
    }

    public void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }

        SuspensionManager.Unregister(this);
    }

    public void CopySaveSlotsNoQuestion() {
        CopySaveSlots();
    }

    public void CopySaveSlots() {
        SaveSlotsManager.CopySlot(CopyingFrom.SaveSlotIndex, CurrentSaveSlot.SaveSlotIndex);
        if (CopyingFrom.SaveSlot.Difficulty == DifficultyMode.OneLife) {
            SaveSlotsManager.DeleteSlot(CopyingFrom.SaveSlotIndex);
        }

        CurrentSaveSlot.RefreshBackups();
        ExitCopyingState();
        RefreshSlots();
        if (CopySound) {
            Sound.Play(CopySound.GetSound(null), transform.position, null);
        }
    }

    public void CancelCopying() {
        if (CancelCopySound) {
            Sound.Play(CancelCopySound.GetSound(null), transform.position, null);
        }

        ExitCopyingState();
    }

    private void ExitCopyingState() {
        CopyLegend.SetMessageProvider(CopyLegendMessageProvider);
        CopyingFrom.SetCopying(false);
        CopyingFrom = null;
    }

    public void OnOverrideNewGame() {
    }

    public void OnOverrideCopyCancelled() {
        m_prompt = null;
    }

    public void OnOverrideCopyConfirmed() {
        m_prompt = null;
        CopySaveSlots();
    }

    public void AskPrompt(ConfirmOrCancel question, Action confirm, Action cancel) {
        var promptPosition = CurrentSaveSlot.PromptPosition;
        m_prompt = (ConfirmOrCancel)Instantiate(question, promptPosition.position, Quaternion.identity);
        m_prompt.transform.parent = CurrentSaveSlot.PromptPosition;
        var prompt = m_prompt;
        prompt.OnCancel = (Action)Delegate.Combine(prompt.OnCancel, cancel);
        m_prompt.OnConfirm += confirm;
    }

    public void OnDeleteSaveConfirmed() {
        DeleteSlot();
        CurrentSaveSlot.SetDeleting(false);
        m_prompt = null;
        if (DeleteSound) {
            Sound.Play(DeleteSound.GetSound(null), transform.position, null);
        }
    }

    public void DeleteSlot() {
        SaveSlotsManager.DeleteSlot(CurrentSaveSlot.SaveSlotIndex);
        CurrentSaveSlot.RefreshBackups();
        RefreshSlots();
    }

    public void OnDeleteSaveCancelled() {
        if (CancelDeleteSound) {
            Sound.Play(CancelDeleteSound.GetSound(null), transform.position, null);
        }

        CurrentSaveSlot.SetDeleting(false);
        m_prompt = null;
    }

    public void ClampCurrentItemIndex() {
        CurrentSlotIndex = Items.Count == 0 ? 0 : Utility.Wrap(CurrentSlotIndex, 0, Items.Count);
    }

    private bool CanCopyOrDelete() {
        return true;
    }

    public void HandleNavigation() {
        if (Core.Input.MenuLeft.OnPressed) {
            SetCurrentItemAndScroll(CurrentSlotIndex - 1);
        }

        if (Core.Input.MenuRight.OnPressed) {
            SetCurrentItemAndScroll(CurrentSlotIndex + 1);
        }

        if (Core.Input.CursorMoved) {
            var saveSlotUnderCursor = SaveSlotUnderCursor;
            if (saveSlotUnderCursor && saveSlotUnderCursor != CurrentSaveSlot) {
                SetCurrentItem(saveSlotUnderCursor);
            }
        }

        if (GameSettings.Instance.CurrentControlScheme != 0 && GameController.IsFocused && CursorController.IsVisible) {
            if (Core.Input.CursorPosition.x < 0.05f && Core.Input.CursorPosition.x >= 0f) {
                ItemsUI.TargetScroll -= Time.deltaTime * 3f;
                CursorController.ResetIdleTime();
            }

            if (Core.Input.CursorPosition.x > 0.95f && Core.Input.CursorPosition.x <= 1f) {
                ItemsUI.TargetScroll += Time.deltaTime * 3f;
                CursorController.ResetIdleTime();
            }
        }
    }

    public void SetCurrentItem(SaveSlotUI saveSlot) {
        var currentItem = Items.FindIndex(a => a == saveSlot);
        SetCurrentItem(currentItem);
    }

    public void SetCurrentItemAndScroll(int index) {
        SetCurrentItem(index);
        ItemsUI.SetScrollFromIndex(CurrentSlotIndex);
    }

    public void SetCurrentItem(int index) {
        if (CurrentSaveSlot) {
            CurrentSaveSlot.Highlight(false);
        }

        CurrentSlotIndex = index;
        ClampCurrentItemIndex();
        if (CurrentSaveSlot) {
            CurrentSaveSlot.Highlight(true);
        }

        if (SelectSound) {
            Sound.Play(SelectSound.GetSound(null), transform.position, null);
        }
    }

    public void FixedUpdate() {
        if (IsSuspended || !GameController.IsFocused) {
            return;
        }

        if (!IsVisible) {
            if (FadeAnimator && FadeAnimator.AnimatorDriver.IsReversed && !FadeAnimator.AnimatorDriver.IsPlaying) {
                FadeAnimator.gameObject.SetActive(false);
            }

            return;
        }

        if (PromptIsOpen || !Active) {
            return;
        }

        ItemsUI.UpdateScroll();
        if (SelectingDifficulty) {
            return;
        }

        HandleNavigation();
        // a segment card has nothing to copy, delete or back up: pick it or leave
        if (PracticeSelect.Choosing) {
            if (ClickedCurrentItem || (Core.Input.ActionButtonA.OnPressed && !Core.Input.ActionButtonA.Used)) {
                PracticeSelect.Choose(this);
            } else if (Core.Input.Cancel.OnPressed && !Core.Input.Cancel.Used) {
                PracticeSelect.Leave(this);
            }

            return;
        }

        if (CurrentSaveSlot == null) {
            if (Core.Input.Cancel.OnPressed && OnBackPressedAction) {
                OnBackPressedAction.Perform(null);
            }

            return;
        }

        if (IsCopying) {
            if (CanCopyOrDelete() && (ClickedCurrentItem || (Core.Input.Jump.OnPressed && !Core.Input.Jump.Used) || (Core.Input.Copy.OnPressed && !Core.Input.Copy.Used)) && CopyingFrom != CurrentSaveSlot) {
                if (CurrentSaveSlot.HasSave) {
                    AskPrompt(OverrideQuestion, OnOverrideCopyConfirmed, OnOverrideCopyCancelled);
                } else {
                    CopySaveSlotsNoQuestion();
                }
            } else if (Core.Input.Cancel.OnPressed && !Core.Input.Cancel.Used) {
                CancelCopying();
            }
        } else if (Core.Input.Copy.OnPressed && !Core.Input.Copy.Used) {
            if (CurrentSaveSlot.CanBeCopied) {
                CopyingFrom = CurrentSaveSlot;
                CopyingFrom.SetCopying(true);
                if (BeginCopySound) {
                    Sound.Play(BeginCopySound.GetSound(null), transform.position, null);
                }

                CopyLegend.SetMessageProvider(PasteLegendMessageProvider);
            }
        } else if (Core.Input.Delete.OnPressed && !Core.Input.Delete.Used) {
            if (CurrentSaveSlot.HasSave) {
                CurrentSaveSlot.SetDeleting(true);
                if (BeginDeleteSound) {
                    Sound.Play(BeginDeleteSound.GetSound(null), transform.position, null);
                }

                AskPrompt(DeleteQuestion, OnDeleteSaveConfirmed, OnDeleteSaveCancelled);
            }
        } else if (ClickedCurrentItem || (Core.Input.ActionButtonA.OnPressed && !Core.Input.ActionButtonA.Used)) {
            if (CurrentSaveSlot.HasSave) {
                if (CurrentSaveSlot.IsReady) {
                    UsedSaveSlotSelected();
                    Active = false;
                } else {
                    PressedSaveSlotNotReady();
                }
            } else if (!PracticeController.Active) {
                // an empty practice slot is a parking space, never a new game
                EmptySaveSlotSelected();
            }
        } else if (Core.Input.Cancel.OnPressed && OnBackPressedAction) {
            OnBackPressedAction.Perform(null);
        }
    }

    public void UsedSaveSlotSelected() {
        SaveSlotsManager.BackupIndex = CurrentSaveSlot.BackupIndex;
        // the card's slot, which is only the card's position outside a practice session
        SaveSlotsManager.CurrentSlotIndex = CurrentSaveSlot.SaveSlotIndex;
        UsedSaveSlotPressedAction.Perform(null);
        if (SelectSound) {
            Sound.Play(SelectSound.GetSound(null), transform.position, null);
        }
    }

    public void PressedSaveSlotNotReady() {
        SaveSlotsManager.CurrentSlotIndex = CurrentSaveSlot.SaveSlotIndex;
        PressedNotReadyAction.Perform(null);
        if (SelectSound) {
            Sound.Play(SelectSound.GetSound(null), transform.position, null);
        }
    }

    public void EmptySaveSlotSelected() {
        SaveSlotsManager.CurrentSlotIndex = CurrentSaveSlot.SaveSlotIndex;
        m_difficultyScreen = (GameObject)InstantiateUtility.Instantiate(CurrentSaveSlot.DifficultyScreen);
        m_difficultyScreen.GetComponent<CleverMenuItemSelectionManager>().SetVisible(true);
        m_difficultyScreen.transform.parent = CurrentSaveSlot.HighlightAnimator.transform;
        m_difficultyScreen.transform.localScale = Vector3.one * 1.5384f;
        m_difficultyScreen.transform.localPosition = Vector3.zero;
        if (OpenDifficultyMenuSound) {
            Sound.Play(OpenDifficultyMenuSound.GetSound(null), transform.position, null);
        }
    }

    public void SetDifficulty(DifficultyMode difficulty) {
        m_difficultyScreen.GetComponent<CleverMenuItemSelectionManager>().SetVisible(false);
        DifficultyController.Instance.SetDifficulty(difficulty);
        InstantiateUtility.Destroy(m_difficultyScreen, 2f);
        m_difficultyScreen = null;
        EmptySaveSlotPressedAction.Perform(null);
        Active = false;
    }

    public void CancelDifficultyScreen() {
        m_difficultyScreen.GetComponent<CleverMenuItemSelectionManager>().SetVisible(false);
        InstantiateUtility.Destroy(m_difficultyScreen, 2f);
        m_difficultyScreen = null;
        if (CancelDifficultyMenuSound) {
            Sound.Play(CancelDifficultyMenuSound.GetSound(null), transform.position, null);
        }
    }
}
