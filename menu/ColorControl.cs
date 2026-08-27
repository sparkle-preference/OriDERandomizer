using UnityEngine;

// Edits a colour in place on a settings row: up/down picks a channel, left/right moves it,
// Enter commits, Backspace restores the default. Shaped after KeybindControl, including
// its two-frame wait -- the key that opened the editor is still down on the first frame.
//
// Works in the setting's own file units rather than 0..1, so what the row shows is what
// RandomizerSettings.txt says, whatever scale that setting is on.
public class ColorControl : MonoBehaviour {
    public void Init(RandomizerSettings.ColorSetting setting, CustomSettingsScreen owner, Transform swatch, string tooltip, bool asMessage) {
        this.setting = setting;
        this.owner = owner;
        this.swatch = swatch;
        this.tooltip = tooltip;
        this.asMessage = asMessage;
        messageBox = transform.FindChild("text/stateText").GetComponent<MessageBox>();
        BuildWheel();
        BuildBars();
        tooltipProvider = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        tooltipProvider.SetMessage(tooltip);
        GetComponent<CleverMenuItemTooltip>().Tooltip = tooltipProvider;
        Show();
    }

    public void BeginEditing() {
        channel = 0;
        exit = 0;
        editing = true;
        held = 0f;
        repeat = 0f;
        original = setting.Value;
        RetitleLegend(true);
        SuspensionManager.SuspendAll();
        ShowBars(true);
        if (wheel != null) {
            wheel.gameObject.SetActive(true);
            reticle.gameObject.SetActive(true);
        }
        // the legend below carries the keys, so this only has to cover the mouse
        tooltipProvider.SetMessage("Drag the wheel for a hue, or a bar for one channel.");
        owner.tooltipController.UpdateTooltip();
        Show();
    }

    public void Update() {
        if (!editing) {
            return;
        }

        if (exit < 2) {
            exit++;
            if (exit == 2) {
                // A cloned TextBox resets itself to its serialised text in Start, which
                // lands a frame after the object is switched on -- so the channel letters
                // written during BeginEditing are thrown away. Write them again here.
                Show();
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) {
            Finish();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) {
            setting.Value = original;
            RandomizerSettings.SetDirty();
            Finish();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete)) {
            setting.Reset();
            RandomizerSettings.SetDirty();
            Show();
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow)) {
            channel = (channel + 3) % 4;
            Show();
        }

        if (Input.GetKeyDown(KeyCode.DownArrow)) {
            channel = (channel + 1) % 4;
            Show();
        }

        var step = Step();
        if (step != 0) {
            Nudge(step * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 25 : 5));
        }

        if (Input.GetMouseButton(0)) {
            PickFromMouse(Input.GetMouseButtonDown(0));
        }
    }

    // Drag the disc for hue and saturation, or a bar for that one channel. A fresh press
    // on a bar also makes it the one the arrow keys move.
    private void PickFromMouse(bool pressed) {
        var cursor = Core.Input.CursorPositionUI;
        for (var c = 0; c < 4; c++) {
            if (barQuads[c] == null) {
                continue;
            }

            var half = 0.5f * barQuads[c].lossyScale.x;
            var reach = 0.5f * barQuads[c].lossyScale.y + BarGrabPad;
            var offset = cursor.x - barQuads[c].position.x;
            if (Mathf.Abs(cursor.y - barQuads[c].position.y) > reach || Mathf.Abs(offset) > half) {
                continue;
            }

            // a click on a bar that is not the live one only picks it up; nothing moves
            // until you are holding the bar you meant to hold
            if (pressed && channel != c) {
                channel = c;
                grab = 0f;
                Show();
                return;
            }

            if (channel != c) {
                return;
            }

            if (pressed) {
                grab = 0f;
            }

            // the gradient starts past the label, so the grab does too
            var t = Mathf.Clamp01(((offset + half) / (2f * half) - LabelShare) / (1f - LabelShare));
            grab += Time.unscaledDeltaTime;
            // eases in, so a nudge is a nudge and a hold crosses the bar
            var rate = Mathf.Lerp(GrabSlow, GrabFast, Mathf.Clamp01(grab / GrabRamp));
            var now = Channel(setting.Value, c);
            setting.Value = WithChannel(setting.Value, c, Mathf.MoveTowards(now, t, rate * Time.unscaledDeltaTime));
            RandomizerSettings.SetDirty();
            Show();
            return;
        }

        if (!PickFromWheel(cursor) && pressed) {
            // clicking off the picker reads as "I am done with this", not as nothing
            setting.Value = original;
            RandomizerSettings.SetDirty();
            Finish();
        }
    }

    private bool PickFromWheel(Vector3 cursor) {
        if (wheel == null) {
            return false;
        }

        var centre = wheel.position;
        var radius = 0.5f * wheel.lossyScale.x;
        var dx = cursor.x - centre.x;
        var dy = cursor.y - centre.y;
        var distance = Mathf.Sqrt(dx * dx + dy * dy);
        if (radius <= 0f || distance > radius) {
            return false;
        }

        // the texture puts red at the top and runs hue clockwise, so the angle is measured
        // from up rather than from the x axis
        float h, s, v;
        Color.RGBToHSV(setting.Value, out h, out s, out v);
        h = Mathf.Repeat(Mathf.Atan2(dx, dy) / (2f * Mathf.PI), 1f);
        s = Mathf.Clamp01(distance / radius);
        var picked = Color.HSVToRGB(h, s, v > 0f ? v : 1f);
        setting.Value = new Color(picked.r, picked.g, picked.b, setting.Value.a);
        RandomizerSettings.SetDirty();
        Show();
        return true;
    }

    private void BuildWheel() {
        var order = GetComponentInChildren<Renderer>();
        var disc = RandomizerQuad.Build("colorWheel", "color_wheel.png", order);
        var mark = RandomizerQuad.Build("colorReticle", "color_reticle.png", order);
        if (disc == null || mark == null) {
            Randomizer.log("colour picker: wheel art did not load, numbers only");
            return;
        }

        wheel = disc.transform;
        wheel.SetParent(transform, false);
        wheel.localPosition = new Vector3(WheelX, 0f, 0f);
        wheel.localScale = new Vector3(WheelSize, WheelSize, 1f);
        wheel.gameObject.SetActive(false);

        reticle = mark.transform;
        reticle.SetParent(wheel, false);
        reticle.localScale = new Vector3(ReticleSize, ReticleSize, 1f);
        reticle.gameObject.SetActive(false);
    }

    private void PlaceReticle() {
        if (reticle == null) {
            return;
        }

        float h, s, v;
        Color.RGBToHSV(setting.Value, out h, out s, out v);
        // A drag keeps the value it finds, so a dark colour stays dark -- but a disc drawn
        // at full brightness hides that. Dim it to what it is actually offering.
        var lit = Mathf.Max(v, 0.15f);
        var discRenderer = wheel.GetComponent<Renderer>();
        if (discRenderer != null && discRenderer.sharedMaterial != null) {
            discRenderer.sharedMaterial.SetColor("_Color", new Color(lit, lit, lit, 1f));
        }

        var angle = h * 2f * Mathf.PI;
        // in the wheel's own space, so it rides along with whatever size the disc is
        reticle.localPosition = new Vector3(0.5f * s * Mathf.Sin(angle), 0.5f * s * Mathf.Cos(angle), -0.01f);
    }

    // GetKeyDown does not repeat, so a held arrow is timed the way the menus time theirs.
    // One timer, reset the instant no arrow is down, so a key cannot stay latched across a
    // channel change.
    private int Step() {
        var dir = 0;
        if (Input.GetKey(KeyCode.RightArrow)) {
            dir += 1;
        }

        if (Input.GetKey(KeyCode.LeftArrow)) {
            dir -= 1;
        }

        if (dir == 0) {
            held = 0f;
            repeat = 0f;
            return 0;
        }

        if (held == 0f) {
            held = Time.unscaledDeltaTime;
            repeat = 0f;
            return dir;
        }

        held += Time.unscaledDeltaTime;
        repeat += Time.unscaledDeltaTime;
        if (repeat < (held < FirstRepeat ? FirstRepeat : NextRepeat)) {
            return 0;
        }

        repeat = 0f;
        return dir;
    }

    private void Nudge(int by) {
        var v = setting.Value;
        var parts = new[] { v.r, v.g, v.b, v.a };
        parts[channel] = Mathf.Clamp01((Mathf.Round(parts[channel] * setting.divisor) + by) / setting.divisor);
        setting.Value = new Color(parts[0], parts[1], parts[2], parts[3]);
        RandomizerSettings.SetDirty();
        Show();
    }

    private void Show() {
        messageBox.SetMessage(new MessageDescriptor(""));
        PlaceReticle();
        Paint();
    }

    // Every bar is repainted from the live colour, so each channel's gradient shows what
    // moving THAT channel does to THIS colour rather than a generic black-to-red ramp.
    private void Paint() {
        var v = setting.Value;
        if (asMessage && swatch != null) {
            var renderer = swatch.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null) {
                renderer.sharedMaterial.SetColor("_Color", v);
            }
        }

        if (swatchTexture != null) {
            var px = swatchTexture.GetPixels();
            for (var i = 0; i < px.Length; i++) {
                var x = i % SwatchW;
                var y = i / SwatchW;
                var col = Over(v, Checker(x, y));
                col.a = Edge(x, y, SwatchW, SwatchH) * Grain(x, y);
                px[i] = col;
            }

            swatchTexture.SetPixels(px);
            swatchTexture.Apply();
        }

        for (var c = 0; c < 4; c++) {
            if (bars[c] == null) {
                continue;
            }

            var px = bars[c].GetPixels();
            var picked = editing && c == channel;
            var span = BarW - 1 - LabelW;
            var mark = LabelW + Mathf.Clamp(Mathf.RoundToInt(Channel(v, c) * span), 2, span - 2);
            for (var y = 0; y < BarH; y++) {
                for (var x = 0; x < BarW; x++) {
                    Color col;
                    if (x < LabelW) {
                        // The cap carries the selection -- an outline round a wavy bar reads
                        // as a fringe. The letter inverts with it so it stays legible.
                        var cap = picked ? new Color(0.78f, 0.78f, 0.82f) : new Color(0.13f, 0.13f, 0.15f);
                        var ink = picked ? new Color(0.10f, 0.10f, 0.12f) : new Color(0.72f, 0.72f, 0.78f);
                        col = Letter(c, x - 3, y - 3, picked) ? ink : cap;
                    } else {
                        var swept = WithChannel(v, c, (x - LabelW) / (float)span);
                        // only the alpha bar is about transparency; drawing the other three
                        // through the colour's own alpha just checks every gradient
                        col = c == 3 ? Over(swept, Checker(x, y))
                                     : new Color(swept.r, swept.g, swept.b, 1f);
                        var d = Mathf.Abs(x - mark);
                        if (d <= 1) {
                            col = picked ? Color.white : new Color(0.75f, 0.75f, 0.75f);
                        } else if (d == 2) {
                            col = Color.Lerp(col, Color.black, 0.7f);
                        }
                    }

                    // the cap carries the selection now: an outline round a wavy bar reads
                    // as a fringe rather than as a highlight
                    col.a = Edge(x, y, BarW, BarH) * Grain(x, y);
                    px[y * BarW + x] = col;
                }
            }

            bars[c].SetPixels(px);
            bars[c].Apply();
        }
    }

    // The scrollbar's look, ported to the runtime painter, so these read as drawn rather
    // than as UI rectangles. Kept separate from the grain: the highlight on the selected
    // bar keys off the edge, and grain in that test speckles the whole bar white.
    private static float Edge(int x, int y, int w, int h) {
        var u = x / (w - 1f);
        var wobble = 0.28f * Mathf.Sin(u * 21f) + 0.15f * Mathf.Sin(u * 47f + 1.7f);
        var half = 0.5f * (h - 1);
        return Mathf.Clamp01((half - (Mathf.Abs(y - half) - wobble)) / ChalkFeather);
    }

    // a hand does not lay ink evenly
    private static float Grain(int x, int y) {
        return 1f - ChalkGrain * Noise(x, y / 2);
    }

    private static float Noise(int x, int y) {
        var h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }

    // A 5x7 letter per channel, painted into the bar. The menu's own font was the nicer
    // idea and did not survive four attempts through a cloned TextBox; see the notes.
    private static bool Letter(int channel, int x, int y, bool bold) {
        if (Lit(channel, x, y)) {
            return true;
        }

        // Dark ink on a light cap reads thinner than light ink on a dark one, so the
        // inverted letter is grown by a pixel to weigh the same.
        return bold && (Lit(channel, x - 1, y) || Lit(channel, x, y - 1));
    }

    private static bool Lit(int channel, int x, int y) {
        if (x < 0 || x > 4 || y < 0 || y > 6) {
            return false;
        }

        // SetPixels puts row 0 at the bottom, and these are written top row first
        return Glyphs[channel][6 - y][x] == 'X';
    }

    private static readonly string[][] Glyphs = {
        new[] { "XXXX.", "X...X", "X...X", "XXXX.", "X.X..", "X..X.", "X...X" },
        new[] { ".XXX.", "X...X", "X....", "X..XX", "X...X", "X...X", ".XXX." },
        new[] { "XXXX.", "X...X", "X...X", "XXXX.", "X...X", "X...X", "XXXX." },
        new[] { ".XXX.", "X...X", "X...X", "XXXXX", "X...X", "X...X", "X...X" }
    };

    private static float Channel(Color c, int i) {
        return i == 0 ? c.r : i == 1 ? c.g : i == 2 ? c.b : c.a;
    }

    private static Color WithChannel(Color c, int i, float v) {
        return new Color(i == 0 ? v : c.r, i == 1 ? v : c.g, i == 2 ? v : c.b, i == 3 ? v : c.a);
    }

    // a mid-grey check, so a transparent or near-black colour still reads as something
    private static Color Checker(int x, int y) {
        var on = ((x / 4) + (y / 4)) % 2 == 0;
        return on ? new Color(0.62f, 0.62f, 0.64f) : new Color(0.42f, 0.42f, 0.45f);
    }

    private static Color Over(Color c, Color under) {
        return new Color(Mathf.Lerp(under.r, c.r, c.a), Mathf.Lerp(under.g, c.g, c.a),
                         Mathf.Lerp(under.b, c.b, c.a), 1f);
    }

    private static Texture2D Canvas(int w, int h) {
        var tex = new Texture2D(w, h);
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    private void BuildBars() {
        var order = GetComponentInChildren<Renderer>();
        // a message background previews as itself: the same art the message uses, tinted,
        // rather than a swatch over a check
        if (swatch != null && !asMessage) {
            swatchTexture = Canvas(SwatchW, SwatchH);
            var renderer = swatch.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null) {
                renderer.sharedMaterial.SetTexture("_MainTex", swatchTexture);
                renderer.sharedMaterial.SetColor("_Color", Color.white);
            }
        }

        for (var c = 0; c < 4; c++) {
            bars[c] = Canvas(BarW, BarH);
            var obj = RandomizerQuad.BuildTextured("colorBar" + c, bars[c], order);
            if (obj == null) {
                bars[c] = null;
                continue;
            }

            barQuads[c] = obj.transform;
            barQuads[c].SetParent(transform, false);
            // stacked under the wheel, so the two read as one panel
            barQuads[c].localPosition = new Vector3(WheelX, BarTop - c * BarGap, 0f);
            barQuads[c].localScale = new Vector3(BarWidth, BarHeight, 1f);
            barQuads[c].gameObject.SetActive(false);
        }
    }

    private void ShowBars(bool visible) {
        for (var c = 0; c < 4; c++) {
            if (barQuads[c] != null) {
                barQuads[c].gameObject.SetActive(visible);
            }

        }
    }

    public void Reset() {
        editing = false;
        Show();
    }

    // The legend says Navigate/Select/Back, none of which is true in here. The key icons
    // come out of the text itself -- <icon> switches to a font whose letters are key
    // images -- so five entries across three slots is three strings, not three prefabs.
    // v/r are the up and down arrows, s/t left and right, D Enter, y Esc, M Del.
    private void RetitleLegend(bool editingNow) {
        var legend = owner.transform.FindChild("highlightFade/legend/pcLegend");
        if (legend == null) {
            return;
        }

        var labels = new[] { "navigate", "select", "back" };
        var swapped = new[] {
            "<icon>vr</> Channel  <icon>st</> Value",
            "<icon>D</> Confirm",
            "<icon>y</> Cancel   <icon>M</> Default"
        };
        for (var i = 0; i < labels.Length; i++) {
            var child = legend.FindChild(labels[i]);
            var box = child == null ? null : child.GetComponentInChildren<MessageBox>();
            if (box == null) {
                continue;
            }

            if (editingNow) {
                if (legendWas[i] == null) {
                    legendWas[i] = box.MessageProvider;
                }

                box.MessageProvider = null;
                box.SetMessage(new MessageDescriptor(swapped[i]));
            } else if (legendWas[i] != null) {
                box.SetMessageProvider(legendWas[i]);
            }
        }
    }

    private void Finish() {
        editing = false;
        RetitleLegend(false);
        ShowBars(false);
        if (wheel != null) {
            wheel.gameObject.SetActive(false);
            reticle.gameObject.SetActive(false);
        }

        SuspensionManager.ResumeAll();
        RandomizerSettings.WriteSettings();
        tooltipProvider.SetMessage(tooltip);
        owner.tooltipController.UpdateTooltip();
        Show();
    }

    private const int SwatchW = 48;

    private const int SwatchH = 24;

    private const int LabelW = 12;

    // what share of the bar's width the label takes, for hit-testing a drag
    private const float LabelShare = LabelW / (float)BarW;

    private const int BarW = 128;

    private const int BarH = 12;

    private const float BarWidth = 2.2f;

    private const float BarHeight = 0.19f;

    private const float BarTop = -1.5f;

    private const float BarGap = 0.27f;

    private const float WheelX = 7.4f;

    private const float WheelSize = 2.4f;

    private const float ReticleSize = 0.16f;

    // how far above and below a bar still counts as grabbing it
    private const float ChalkFeather = 2.2f;

    private const float ChalkGrain = 0.22f;

    private const float BarGrabPad = 0.04f;

    private const float GrabSlow = 0.35f;

    private const float GrabFast = 2.4f;

    private const float GrabRamp = 0.7f;

    private const float FirstRepeat = 0.35f;

    private const float NextRepeat = 0.05f;

    private RandomizerSettings.ColorSetting setting;

    private CustomSettingsScreen owner;

    private string tooltip;

    private bool asMessage;

    private Transform swatch;

    private Transform wheel;

    private Transform reticle;

    private Texture2D swatchTexture;

    private readonly Texture2D[] bars = new Texture2D[4];

    private readonly Transform[] barQuads = new Transform[4];



    private MessageBox messageBox;

    private RandomizerMessageProvider tooltipProvider;

    private bool editing;

    private int channel;

    private int exit;

    private float held;

    private float repeat;

    private float grab;

    private Color original;

    private readonly MessageProvider[] legendWas = new MessageProvider[3];
}
