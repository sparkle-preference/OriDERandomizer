using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CatlikeCoding.TextBox;
using Game;
using UnityEngine;

[ExecuteInEditMode]
public class MessageBox : MonoBehaviour {
    public event Action OnMessageScreenHide = delegate { };

    public event Action OnNextMessage = delegate { };

    public HashSet<ISuspendable> GetSuspendables() {
        var hashSet = new HashSet<ISuspendable>();
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
            foreach (var textRenderer in TextBox.textRenderers) {
                var moonTextMeshRenderer = textRenderer as MoonTextMeshRenderer;
                if (moonTextMeshRenderer != null) {
                    var component = moonTextMeshRenderer.GetComponent<Renderer>();
                    if (component) {
                        var val = time / FadeSpread;
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
            var text = MessageParserUtility.ProcessString(m_currentMessage.Message);
            TextBox.SetText(text);
        } else {
            TextBox.SetText(m_currentMessage.Message);
        }

        RefreshText();
    }

    public void RefreshText() {
        var styleCollection = m_forceLanguage ? LanguageStyles.GetStyle(m_language) : LanguageStyles.Current;

        if (MessageProvider) {
            m_messageDescriptors = MessageProvider.GetMessages().ToArray();
            MessageIndex = Mathf.Clamp(MessageIndex, 0, m_messageDescriptors.Length);
            m_currentMessage = m_messageDescriptors[MessageIndex];

            var text = m_currentMessage.Message;
            ProcessStyleTags(styleCollection, text);

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
                var p = new Queue<string>(text.Split('_'));
                p.Dequeue();
                TextBox.paddingBottom = float.Parse(p.Dequeue());
                TextBox.paddingLeft = float.Parse(p.Dequeue());
                TextBox.paddingRight = float.Parse(p.Dequeue());
                TextBox.paddingTop = float.Parse(p.Dequeue());
                text = string.Join("_", p.ToArray());
            }

            if (text.StartsWith("PARAMS")) {
                var p = new Queue<string>(text.Split('_'));
                p.Dequeue();
                TextBox.maxHeight = float.Parse(p.Dequeue());
                TextBox.width = float.Parse(p.Dequeue());
                TextBox.TabSize = float.Parse(p.Dequeue());
                text = string.Join("_", p.ToArray());
            }

            float r = 0f, g = 0f, b = 0f, a = 0f;
            if (text.StartsWith("BGCOLOR")) {
                var p = new Queue<string>(text.Split('_'));
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
                if (m_hasBackgroundColor) {
                    text += $"Color: {r},{g},{b},{a}";
                }
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

        TextBox.SetStyleCollection(styleCollection);
        TextBox.CreateRendersIfThereAreNone();
        var textRenderers = TextBox.textRenderers;
        for (var i = 0; i < textRenderers.Length; i++) {
            var moonTextMeshRenderer = textRenderers[i] as MoonTextMeshRenderer;
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

    private static readonly Regex StyleTagRegex = new(
        @"(?inx)
        <style
                (\s+color=(?<color>[0-9a-f]{6,8}))?
                (\s+font=(?<font>[a-z]+))?
                (\s+letterspacing=(?<letter_spacing>[-.0-9]+))?
                (\s+fontscale=(?<font_scale>[-.0-9]+))?
                (\s+linescale=(?<line_scale>[-.0-9]+))?
        >"
    );

    private void ProcessStyleTags(TextStyleCollection styleCollection, string message) {
        List<TextStyle> styles = null;
        Dictionary<string, TextFont> fonts = null;

        foreach (Match match in StyleTagRegex.Matches(message)) {
            var styleName = match.Groups[0].Value.Substring(1, match.Groups[0].Value.Length - 2);

            if (styles == null) {
                if (styleCollection.styles.Any(s => s.name == styleName)) {
                    continue;
                }

                styles = new List<TextStyle>(styleCollection.styles);
            }

            if (styles.Any(s => s.name == styleName)) {
                continue;
            }

            var style = new TextStyle { name = styleName };

            if (match.Groups["color"] is { Success: true, Value: var colorString }) {
                if (colorString.Length != 6 && colorString.Length != 8) {
                    Randomizer.log($"Invalid font color property: \"{colorString}\"");
                    continue;
                }

                var r = byte.Parse(colorString.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(colorString.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(colorString.Substring(4, 2), NumberStyles.HexNumber);
                var a = colorString.Length == 8 ? byte.Parse(colorString.Substring(6, 2), NumberStyles.HexNumber) : (byte)255;

                style.color = new Color32(r, g, b, a);
                style.hasColor = true;
            }

            if (match.Groups["font"] is { Success: true, Value: var fontName }) {
                fonts ??= GetFonts();

                if (!fonts.TryGetValue(fontName, out var textFont)) {
                    Randomizer.log($"Invalid font: \"{fontName}\". Available: {string.Join(", ", fonts.Keys.Select(n => $"'{n}'").ToArray())}");
                    continue;
                }

                style.font = textFont.Font;
                style.renderer = textFont.Renderer;
            }

            if (match.Groups["letter_spacing"] is { Success: true, Value: var letterSpacing }) {
                style.letterSpacing = float.Parse(letterSpacing, CultureInfo.InvariantCulture);
                style.hasLetterSpacing = true;
            }

            if (match.Groups["font_scale"] is { Success: true, Value: var fontScale }) {
                style.fontScale = float.Parse(fontScale, CultureInfo.InvariantCulture);
                style.hasFontScale = true;
            }

            if (match.Groups["line_scale"] is { Success: true, Value: var lineScale }) {
                style.lineScale = float.Parse(lineScale, CultureInfo.InvariantCulture);
                style.hasLineScale = true;
            }

            styles.Add(style);
        }

        if (styles != null) {
            styleCollection.styles = styles.ToArray();
            styleCollection.ComputeRendererCount();
            // Force text box to refresh style collection
            TextBox.styleCollection = null;
        }
    }

    private Dictionary<string, TextFont> GetFonts() {
        var fonts = new Dictionary<string, TextFont>();
        foreach (Language lang in Enum.GetValues(typeof(Language))) {
            foreach (var style in LanguageStyles.GetStyle(lang).styles) {
                if (style.font != null && !fonts.ContainsKey(style.font.name)) {
                    fonts.Add(style.font.name, new TextFont(style.font, style.renderer));
                }
            }
        }

        return fonts;
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

    private struct TextFont {
        public BitmapFont Font;
        public TextRenderer Renderer;

        public TextFont(BitmapFont font, TextRenderer renderer) {
            Font = font;
            Renderer = renderer;
        }
    }
}
