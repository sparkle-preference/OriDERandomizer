using UnityEngine;

namespace Game {
    public static class UI {
        public static MessageControllerB MessageController {
            get {
                LoadMessageController();
                return m_messageController;
            }
        }

        public static void LoadMessageController() {
            if (m_messageController == null) {
                m_messageController = (Resources.Load("MessageControllerB") as GameObject).GetComponent<MessageControllerB>();
            }
        }

        public static MenuScreenManager Menu {
            get { return m_sMenu; }
            set { m_sMenu = value; }
        }

        public static bool MainMenuVisible {
            get { return m_sMenu != null && (m_sMenu.MainMenuVisible || m_sMenu.ResumeScreenVisible); }
        }

        public static bool MainMenuExists {
            get { return m_sMenu != null; }
        }

        public static bool IsInventoryVisible() {
            return MainMenuVisible && m_sMenu.IsInventoryVisible();
        }

        private static MessageControllerB m_messageController;

        public static FaderB Fader;

        public static SeinUI SeinUI;

        private static MenuScreenManager m_sMenu;

        public static Vignette Vignette;

        public static class Cameras {
            public static CameraSystem System;

            public static GameplayCamera Current;

            public static CameraManager Manager;
        }

        public static class Hints {
            public static Vector3 HintPosition {
                get { return OnScreenPositions.TopCenter; }
            }

            public static void HideExistingHint() {
                HideExistingHint(false);
            }

            private static bool LayerShouldShow(HintLayer layer) {
                return !m_currentHint || layer >= m_currentLayer;
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
                    m_currentLayer = layer;
                    if (ShorterHintZone.IsInside) {
                        duration = 1f;
                    }

                    if (layer == HintLayer.Randomizer) {
                        m_currentHint = MessageController.ShowHintMessage(messageProvider, new Vector3(HintPosition.x, HintPosition.y, -7f), duration);
                    } else {
                        m_currentHint = MessageController.ShowHintMessage(messageProvider, HintPosition, duration);
                    }

                    return m_currentHint;
                }

                return null;
            }

            public static bool IsShowingHint {
                get { return m_currentHint; }
            }

            public static void HideExistingHint(bool force) {
                if (m_currentLayer == HintLayer.Randomizer && !force) {
                    return;
                }

                if (m_currentHint) {
                    m_currentHint.Visibility.HideMessageScreenImmediately();
                    m_currentHint = null;
                }
            }

            private static MessageBox m_currentHint;

            private static HintLayer m_currentLayer;

            private static bool m_showHints;
        }
    }
}
