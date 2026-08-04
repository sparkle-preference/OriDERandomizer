using UnityEngine;

namespace Game {
    public static class UI {
        public static MessageControllerB MessageController {
            get {
                LoadMessageController();
                return messageController;
            }
        }

        public static void LoadMessageController() {
            if (messageController == null) {
                messageController = (Resources.Load("MessageControllerB") as GameObject).GetComponent<MessageControllerB>();
            }
        }

        public static MenuScreenManager Menu {
            get => menu;
            set => menu = value;
        }

        public static bool MainMenuVisible => menu != null && (menu.MainMenuVisible || menu.ResumeScreenVisible);

        public static bool MainMenuExists => menu != null;

        public static bool IsInventoryVisible() {
            return MainMenuVisible && menu.IsInventoryVisible();
        }

        private static MessageControllerB messageController;

        public static FaderB Fader;

        public static SeinUI SeinUI;

        private static MenuScreenManager menu;

        public static Vignette Vignette;

        public static class Cameras {
            public static CameraSystem System;

            public static GameplayCamera Current;

            public static CameraManager Manager;
        }

        public static class Hints {
            public static Vector3 HintPosition => OnScreenPositions.TopCenter;

            public static void HideExistingHint() {
                HideExistingHint(false);
            }

            private static bool LayerShouldShow(HintLayer layer) {
                return !currentHint || layer >= currentLayer;
            }

            public static MessageBox Show(MessageProvider messageProvider, HintLayer layer, float duration = 3f) {
                if (messageProvider == null) {
                    return null;
                }

                if (MessageController.AnyAbilityPickupStoryMessagesVisible) {
                    return null;
                }

                if (LayerShouldShow(layer)) {
                    HideExistingHint(true);
                    currentLayer = layer;
                    if (ShorterHintZone.IsInside) {
                        duration = 1f;
                    }

                    if (layer == HintLayer.Randomizer) {
                        currentHint = MessageController.ShowHintMessage(messageProvider, new Vector3(HintPosition.x, HintPosition.y, -7f), duration);
                    } else {
                        currentHint = MessageController.ShowHintMessage(messageProvider, HintPosition, duration);
                    }

                    return currentHint;
                }

                return null;
            }

            public static bool IsShowingHint => currentHint;

            public static void HideExistingHint(bool force) {
                if (currentLayer == HintLayer.Randomizer && !force) {
                    return;
                }

                if (currentHint) {
                    currentHint.Visibility.HideMessageScreenImmediately();
                    currentHint = null;
                }
            }

            private static MessageBox currentHint;

            private static HintLayer currentLayer;
        }
    }
}
