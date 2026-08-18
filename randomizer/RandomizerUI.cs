using System.Collections.Generic;
using CatlikeCoding.TextBox;
using Game;
using UnityEngine;
using Input = Core.Input;

public class RandomizerUI : MonoBehaviour {
    public static void Initialize() {
        Instance = new GameObject("randomizerUI").AddComponent<RandomizerUI>();
    }

    public void Awake() {
        // deactivating the existing prefab before cloning it prevents the clone from doing Awake/Start/etc.
        // this gives us a chance to do setup before things get locked in
        var wasActive = UI.MessageController.HintMessage.activeSelf;
        UI.MessageController.HintMessage.SetActive(false);
        var obj = (GameObject)InstantiateUtility.Instantiate(UI.MessageController.HintMessage);
        UI.MessageController.HintMessage.SetActive(wasActive);
        Message.SideNotificationPrefab = obj;

        // nb: removing the DestroyOnRestoreCheckpoint component allows notifs to stick around through S&Q
        obj.name = "pickupMessage";
        obj.transform.parent = transform;
        Destroy(obj.GetComponent<DestroyOnRestoreCheckpoint>());
        Destroy(obj.GetComponent<SoundSource>());

        // position adjustment on the textbox moves it forward (-Z) so that the text + bg render on top of the world map
        var messageBox = obj.GetComponentInChildren<MessageBox>();
        messageBox.transform.localScale *= 0.9f;
        messageBox.TextBox.transform.localPosition = new Vector3(0f, 0f, -1f);
        messageBox.TextBox.alignment = AlignmentMode.Left;
        messageBox.TextBox.horizontalAnchor = HorizontalAnchorMode.Left;
        messageBox.TextBox.verticalAnchor = VerticalAnchorMode.Top;

        messageBox.Visibility.TransitionInDuration = 0.2f;
        messageBox.Visibility.TransitionOutDuration = 0.15f;

        var scaleToTextBox = messageBox.GetComponentInChildren<ScaleToTextBox>();
        scaleToTextBox.TopLeftPadding = new Vector2(0.5f, 0.15f);
        scaleToTextBox.BottomRightPadding = new Vector2(0.5f, 0.15f);
    }

    public void OnGUI() {
        if (DebugMenuB.DebugControlsEnabled && Characters.Sein != null && Characters.Sein.Active) {
            var infoStyle = new GUIStyle();
            infoStyle.fontSize = 16;
            infoStyle.alignment = TextAnchor.LowerLeft;
            infoStyle.normal.textColor = Color.white;
            var camera = UI.Cameras.Current.Camera;
            var cursorPosition = Input.CursorPosition;
            Vector2 cursorWorldPos = camera.ViewportToWorldPoint(new Vector3(cursorPosition.x, cursorPosition.y, -camera.transform.position.z));
            var text = string.Format(
                "Ori (World) X: {0} / Y: {1}\nCursor (World) X {2} / Y: {3}",
                Characters.Sein.Position.x,
                Characters.Sein.Position.y,
                cursorWorldPos.x,
                cursorWorldPos.y
            );
            GUI.Label(new Rect(4f, GameSettings.Instance.Resolution.y - 54f, 200f, 50f), text, infoStyle);
        }
    }

    public void Update() {
        if (m_sideNotificationsDisplaying.Count == 0 && m_sideNotificationsAwaiting.Count == 0) {
            return;
        }

        var updateDisplay = false;

        // message objects destroy themselves automatically when their time elapses, so we have to clean up after them
        var i = 0;
        while (i < m_sideNotificationsDisplaying.Count) {
            if (!m_sideNotificationsDisplaying[i].MessageBox) {
                m_sideNotificationsDisplaying.RemoveAt(i);
                updateDisplay = true;
            } else {
                i++;
            }
        }

        while (m_sideNotificationsAwaiting.Count > 0 && (m_sideNotificationsDisplaying.Count < 5 || m_extendedAltTShown)) {
            var nextMessage = m_sideNotificationsAwaiting.Dequeue();
            m_sideNotificationsDisplaying.Add(nextMessage);
            m_recentSideNotifications.Enqueue(nextMessage);
            updateDisplay = true;
        }

        if (m_sideNotificationsDisplaying.Count > 5) {
            for (var j = 0; j < m_sideNotificationsDisplaying.Count - 5; j++) {
                if (m_sideNotificationsDisplaying[j].MessageBox != null) {
                    m_sideNotificationsDisplaying[j].MessageBox.Visibility.HideMessageScreenImmediately();
                }
            }

            m_sideNotificationsDisplaying.RemoveRange(0, m_sideNotificationsDisplaying.Count - 5);
        }

        while (m_recentSideNotifications.Count > 5) {
            m_recentSideNotifications.Dequeue();
        }

        if (updateDisplay) {
            var nextY = 2.2f;
            foreach (var displayingMessage in m_sideNotificationsDisplaying) {
                if (!displayingMessage.MessageBox) {
                    displayingMessage.Instantiate();
                }

                displayingMessage.MessageBox.transform.position = new Vector3(-5.7f, nextY, 0f);
                var scaledHeight = displayingMessage.MessageBox.TextBox.boundsTop - displayingMessage.MessageBox.TextBox.boundsBottom;
                scaledHeight *= displayingMessage.MessageBox.TextBox.transform.lossyScale.y;
                nextY -= scaledHeight + 0.35f;
            }
        }
    }

    public void FixedUpdate() {
        bool queueEnabled = RandomizerSettings.Customization.MultiplePickupMessages;
        // only meaningful with the side queue; without it the hold state would wedge open
        var alwaysShowLastFive = RandomizerSettings.Customization.AlwaysShowLastFivePickups && queueEnabled;

        // in any case where "hold alt+T" would show nothing, replay last message OnPressed to preserve snappy response
        // (recents fill even with the side queue disabled, so holding shows the last 5 either way)
        if (RandomizerRebinding.ReplayMessage.OnPressed && (alwaysShowLastFive || m_recentSideNotifications.Count == 0)) {
            Randomizer.playLastMessage();
        }

        if (alwaysShowLastFive) {
            m_extendedAltTShown = true;
        }

        if (RandomizerRebinding.ReplayMessage.Pressed) {
            m_timeAltTHeld += Time.deltaTime;

            if (m_timeAltTHeld >= 0.2f && !m_extendedAltTShown) {
                foreach (var displayingMessage in m_sideNotificationsDisplaying) {
                    if (!m_recentSideNotifications.Contains(displayingMessage)) {
                        displayingMessage.MessageBox.Visibility.HideMessageScreenImmediately();
                    }
                }

                m_sideNotificationsDisplaying.Clear();
                m_sideNotificationsDisplaying.AddRange(m_recentSideNotifications.ToArray());
                m_extendedAltTShown = true;

                var nextY = 2.2f;
                foreach (var displayingMessage in m_sideNotificationsDisplaying) {
                    if (!displayingMessage.MessageBox) {
                        displayingMessage.Instantiate();
                    }

                    displayingMessage.MessageBox.transform.position = new Vector3(-5.7f, nextY, 0f);
                    var scaledHeight = displayingMessage.MessageBox.TextBox.boundsTop - displayingMessage.MessageBox.TextBox.boundsBottom;
                    scaledHeight *= displayingMessage.MessageBox.TextBox.transform.lossyScale.y;
                    nextY -= scaledHeight + 0.35f;
                }
            }
        }

        if (m_extendedAltTShown) {
            foreach (var displayingMessage in m_sideNotificationsDisplaying) {
                displayingMessage.MessageBox.Visibility.ResetWaitDuration();
            }
        }

        // only replay message OnReleased if we know that OnPressed would not have handled it (i.e. "hold alt+T" would show something)
        if (RandomizerRebinding.ReplayMessage.OnReleased && !m_extendedAltTShown && m_recentSideNotifications.Count > 0) {
            Randomizer.playLastMessage();
        }

        if (RandomizerRebinding.ReplayMessage.Released) {
            m_timeAltTHeld = 0f;

            if (m_extendedAltTShown && !alwaysShowLastFive) {
                m_extendedAltTShown = false;

                foreach (var displayingMessage in m_sideNotificationsDisplaying) {
                    displayingMessage.MessageBox.SetWaitDuration(3f);
                    displayingMessage.MessageBox.Visibility.ResetWaitDuration();
                }
            }
        }
    }

    public void QueueSideNotification(Message message) {
        m_sideNotificationsAwaiting.Enqueue(message);
    }

    // track a message for hold-alt+T without displaying it (side queue disabled:
    // it already showed top-center)
    public void RecordRecentNotification(Message message) {
        m_recentSideNotifications.Enqueue(message);
        while (m_recentSideNotifications.Count > 5) {
            m_recentSideNotifications.Dequeue();
        }
    }

    public void ClearRecentNotifications() {
        m_recentSideNotifications.Clear();

        foreach (var displayingMessage in m_sideNotificationsDisplaying) {
            if (displayingMessage.MessageBox) {
                displayingMessage.MessageBox.SetWaitDuration(3f);
                displayingMessage.MessageBox.Visibility.ResetWaitDuration();
            }
        }
    }

    public static RandomizerUI Instance;

    private List<Message> m_sideNotificationsDisplaying = new List<Message>();

    private Queue<Message> m_sideNotificationsAwaiting = new Queue<Message>();

    private Queue<Message> m_recentSideNotifications = new Queue<Message>();

    private float m_timeAltTHeld;

    private bool m_extendedAltTShown;

    public class Message {
        public static Message InfoMessage(string message, float baseDuration = 1f) {
            return new Message(message, VanillaBgColor, baseDuration);
        }

        public static Message PickupMessage(string message, float baseDuration = 1f) {
            return new Message(message, RandomizerSettings.Customization.PickupMessageBgColor, baseDuration);
        }

        public static Message MwPickupMessage(string message, float baseDuration = 1f) {
            return new Message(message, RandomizerSettings.Customization.MwPickupMessageBgColor, baseDuration);
        }

        public Message(string message, Color bgColor, float baseDuration = 1f) {
            MessageString = message;
            BaseDuration = baseDuration;
            BgColor = bgColor;
        }

        public void Instantiate() {
            var obj = (GameObject)InstantiateUtility.Instantiate(SideNotificationPrefab);
            obj.transform.parent = SideNotificationPrefab.transform.parent;
            obj.SetActive(true);

            MessageBox = obj.GetComponentInChildren<MessageBox>();
            var messageProvider = (RandomizerMessageProvider)ScriptableObject.CreateInstance(typeof(RandomizerMessageProvider));
            messageProvider.SetMessage(MessageString);
            MessageBox.SetMessageProvider(messageProvider);
            MessageBox.SetBackgroundColor(BgColor);

            if (MessageBox.Visibility) {
                MessageBox.SetWaitDuration(BaseDuration + 3f);
            }
        }

        public static GameObject SideNotificationPrefab;

        public static Color VanillaBgColor = new Color(0f, 0f, 0f, 0.5f);

        // dark enough that the white message text stays readable on it
        public static Color ErrorBgColor = new Color(0.55f, 0.04f, 0.04f, 0.45f);

        public string MessageString;
        public Color BgColor;
        public float BaseDuration;

        public MessageBox MessageBox;
    }
}
