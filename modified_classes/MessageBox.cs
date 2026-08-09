using System;
using System.Collections.Generic;
using System.Linq;
using CatlikeCoding.TextBox;
using Game;
using UnityEngine;

[ExecuteInEditMode]
public class MessageBox : MonoBehaviour {
    public event Action OnMessageScreenHide = delegate { };

    public event Action OnNextMessage = delegate { };

    public HashSet<ISuspendable> GetSuspendables() {
        HashSet<ISuspendable> hashSet = new HashSet<ISuspendable>();
        foreach (ISuspendable item in GetComponentsInChildren(typeof(ISuspendable))) {
            hashSet.Add(item);
        }

        return hashSet;
    }

    public void OverrideLanuage(Language language) {
        m_language = language;
        m_forceLanguage = true;
    }

    public void SetAvatar(GameObject avatarPrefab) {
        if (m_avatar) {
            InstantiateUtility.Destroy(m_avatar);
            m_avatar = null;
        }

        if (avatarPrefab) {
            m_avatar = Instantiate(avatarPrefab);
            m_avatar.transform.parent = Avatar;
            m_avatar.transform.localPosition = Vector3.zero;
            m_avatar.transform.localRotation = avatarPrefab.transform.localRotation;
            m_avatar.transform.localScale = avatarPrefab.transform.localScale;
        }
    }

    public void SetAvatarArray(GameObject[] avatarPrefabs) {
        m_avatarPrefabs = avatarPrefabs;
    }

    public void HideMessageScreen() {
        Visibility.HideMessageScreen();
        OnMessageScreenHide();
    }

    public void Awake() {
        if (Application.isPlaying) {
            Events.Scheduler.OnGameLanguageChange.Add(RefreshText);
            Events.Scheduler.OnGameControlSchemeChange.Add(RefreshText);
        }
    }

    public void OnDestroy() {
        if (Application.isPlaying) {
            Events.Scheduler.OnGameLanguageChange.Remove(RefreshText);
            Events.Scheduler.OnGameControlSchemeChange.Remove(RefreshText);
        }
    }

    public void Start() {
        RefreshText();
        if (WriteOutTextBox) {
            WriteOutTextBox.GoToStart();
        }
    }

    public void Update() {
        if (m_previousOverrideText != OverrideText) {
            m_previousOverrideText = OverrideText;
            RefreshText();
        }
    }

    public void RemoveMessageFade() {
        SetMessageFade(999999f);
    }

    public void SetMessageFade(float time) {
        if (TextBox.textRenderers != null) {
            foreach (TextRenderer textRenderer in TextBox.textRenderers) {
                MoonTextMeshRenderer moonTextMeshRenderer = textRenderer as MoonTextMeshRenderer;
                if (moonTextMeshRenderer != null) {
                    Renderer component = moonTextMeshRenderer.GetComponent<Renderer>();
                    if (component) {
                        float val = time / FadeSpread;
                        UberShaderAPI.SetFloat(component, val, "_TxtTime", true);
                    }
                }
            }
        }
    }

    public void SetMessage(MessageDescriptor messageDescriptor) {
        MessageProvider = null;
        m_messageDescriptors = null;
        m_currentMessage = messageDescriptor;
        if (FormatText) {
            string text = MessageParserUtility.ProcessString(m_currentMessage.Message);
            TextBox.SetText(text);
        } else {
            TextBox.SetText(m_currentMessage.Message);
        }

        RefreshText();
    }

    public void RefreshText() {
        if (m_forceLanguage) {
            TextBox.SetStyleCollection(LanguageStyles.GetStyle(m_language));
        } else {
            TextBox.SetStyleCollection(LanguageStyles.Current);
        }

        if (MessageProvider) {
            m_messageDescriptors = MessageProvider.GetMessages().ToArray();
            MessageIndex = Mathf.Clamp(MessageIndex, 0, m_messageDescriptors.Length);
            m_currentMessage = m_messageDescriptors[MessageIndex];
            string text = m_currentMessage.Message;
            if (text.StartsWith("ALIGNLEFT")) {
                TextBox.alignment = AlignmentMode.Left;
                text = text.Substring(9);
            } else if (text.StartsWith("ALIGNRIGHT")) {
                TextBox.alignment = AlignmentMode.Right;
                text = text.Substring(10);
            }

            if (text.StartsWith("ANCHORTOP")) {
                TextBox.verticalAnchor = VerticalAnchorMode.Top;
                text = text.Substring(9);
            } else if (text.StartsWith("ANCHORBOT")) {
                TextBox.verticalAnchor = VerticalAnchorMode.Bottom;
                text = text.Substring(9);
            }

            if (text.StartsWith("ANCHORLEFT")) {
                TextBox.horizontalAnchor = HorizontalAnchorMode.Left;
                text = text.Substring(10);
            } else if (text.StartsWith("ANCHORRIGHT")) {
                TextBox.horizontalAnchor = HorizontalAnchorMode.Right;
                text = text.Substring(11);
            }

            if (text.StartsWith("PADDING")) {
                Queue<string> p = new Queue<string>(text.Split('_'));
                p.Dequeue();
                TextBox.paddingBottom = float.Parse(p.Dequeue());
                TextBox.paddingLeft = float.Parse(p.Dequeue());
                TextBox.paddingRight = float.Parse(p.Dequeue());
                TextBox.paddingTop = float.Parse(p.Dequeue());
                text = string.Join("_", p.ToArray());
            }

            if (text.StartsWith("PARAMS")) {
                Queue<string> p = new Queue<string>(text.Split('_'));
                p.Dequeue();
                TextBox.maxHeight = float.Parse(p.Dequeue());
                TextBox.width = float.Parse(p.Dequeue());
                TextBox.TabSize = float.Parse(p.Dequeue());
                text = string.Join("_", p.ToArray());
            }

            float r = 0f, g = 0f, b = 0f, a = 0f;
            if (text.StartsWith("BGCOLOR")) {
                Queue<string> p = new Queue<string>(text.Split('_'));
                p.Dequeue();
                r = float.Parse(p.Dequeue());
                g = float.Parse(p.Dequeue());
                b = float.Parse(p.Dequeue());
                a = float.Parse(p.Dequeue());
                text = string.Join("_", p.ToArray());
                SetBackgroundColor(new Color(r / 510f, g / 510f, b / 510f, a / 510f));
                m_hasBackgroundColor = true;
            }

            if (text.StartsWith("SHOWINFO")) {
                text = $"{text.Substring(8)}\nHeight: {TextBox.maxHeight},\nWidth: {TextBox.width}\n";
                text += $"Anchors: {TextBox.horizontalAnchor} {TextBox.verticalAnchor}\n";
                text += $"Padding: {TextBox.paddingBottom}/{TextBox.paddingLeft}/{TextBox.paddingRight}/{TextBox.paddingTop}\n";
                if (m_hasBackgroundColor)
                    text += $"Color: {r},{g},{b},{a}";
            }

            if (FormatText) {
                text = MessageParserUtility.ProcessString(text);
                TextBox.SetText(text);
            } else {
                TextBox.SetText(text);
            }
        } else if (OverrideText != string.Empty) {
            if (FormatText) {
                TextBox.SetText(MessageParserUtility.ProcessString(OverrideText));
            } else {
                TextBox.SetText(OverrideText);
            }
        }

        TextBox.CreateRendersIfThereAreNone();
        TextRenderer[] textRenderers = TextBox.textRenderers;
        for (int i = 0; i < textRenderers.Length; i++) {
            MoonTextMeshRenderer moonTextMeshRenderer = textRenderers[i] as MoonTextMeshRenderer;
            if (moonTextMeshRenderer) {
                moonTextMeshRenderer.FadeSpread = FadeSpread;
            }
        }

        TextBox.size = ScaleOverLetterCount.Evaluate(TextBoxExtended.CountLetters(TextBox));
        TextBox.RenderText();
        if (WriteOutTextBox) {
            WriteOutTextBox.OnTextChange();
        } else {
            RemoveMessageFade();
        }

        if (m_avatarPrefabs != null) {
            SetAvatar(m_avatarPrefabs[MessageIndex]);
        }

        if (!Application.isPlaying) {
            RemoveMessageFade();
        }
    }

    public void OnEnable() {
        if (!Application.isPlaying) {
            RemoveMessageFade();
        }
    }

    public void SetMessageProvider(MessageProvider messageProvider) {
        MessageProvider = messageProvider;
        RefreshText();
    }

    public int MessageCount {
        get {
            if (m_messageDescriptors == null) {
                return 1;
            }

            return m_messageDescriptors.Length;
        }
    }

    public void SetWaitDuration(float duration) {
        Visibility.WaitDuration = duration;
    }

    public EmotionType CurrentEmotion => m_currentMessage.Emotion;

    public SoundProvider CurrentMessageSound => m_currentMessage.Sound;

    public void FinishWriting() {
        if (WriteOutTextBox) {
            WriteOutTextBox.AnimatorDriver.GoToEnd();
        }
    }

    public bool IsLastMessage => m_messageDescriptors == null || MessageIndex == m_messageDescriptors.Length - 1;

    public bool FinishedWriting => WriteOutTextBox == null || WriteOutTextBox.AtEnd;

    public void NextMessage() {
        MessageIndex++;
        RefreshText();
        if (WriteOutTextBox) {
            WriteOutTextBox.GoToStart();
        }

        OnNextMessage();
        if (NextMessageAnimator) {
            NextMessageAnimator.AnimatorDriver.Restart();
        }
    }

    public void SetBackgroundColor(Color bgColor) {
        if (m_hasBackgroundColor) {
            return;
        }

        var backgroundRenderer = Visibility.transform.FindChild("background/hintMessageBackground").GetComponent<Renderer>();
        UberShaderAPI.SetMainTexture(backgroundRenderer, WhiteBackground, true);
        UberShaderAPI.SetColor(backgroundRenderer, bgColor, true);
    }

    private static Texture2D _hintMessageBackgroundWhite;

    private static Texture2D WhiteBackground {
        get {
            if (_hintMessageBackgroundWhite == null) {
                _hintMessageBackgroundWhite = new Texture2D(0, 0);
                _hintMessageBackgroundWhite.LoadImage(
                    RandomizerResources.ReadResource("hintMessageBackgroundWhite.png")
                );
            }

            return _hintMessageBackgroundWhite;
        }
    }

    public const float WaitTimeBetweenMessages = 0.3f;

    public MessageBoxLanguageStyles LanguageStyles;

    public WriteOutTextBox WriteOutTextBox;

    public MessageBoxVisibility Visibility;

    public TextBox TextBox;

    public Transform Avatar;

    public int MessageIndex;

    public MessageProvider MessageProvider;

    public AnimationCurve ScaleOverLetterCount = AnimationCurve.Linear(0f, 1f, 150f, 1f);

    private float m_remainingWaitTime;

    private GameObject m_avatar;

    private GameObject[] m_avatarPrefabs;

    public BaseAnimator NextMessageAnimator;

    public bool FormatText = true;

    private bool m_forceLanguage;

    private Language m_language;

    public float FadeSpread = 5f;

    public string OverrideText;

    private string m_previousOverrideText = string.Empty;

    private MessageDescriptor[] m_messageDescriptors;

    private MessageDescriptor m_currentMessage;

    private bool m_hasBackgroundColor;
}
