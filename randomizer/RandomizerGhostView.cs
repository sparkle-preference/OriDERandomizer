using System.Collections.Generic;
using CatlikeCoding.TextBox;
using Core;
using Game;
using UnityEngine;

using Sample = RandomizerGhost.Sample;

// One rendered ghost: one per peer or echo, or practice mode's replay.
public class RandomizerGhostView {
    public RandomizerGhostView(string label, Color tint) {
        Label = label;
        Shade = tint;
    }

    public readonly string Label;

    private readonly Color Shade;

    public bool Alive { get { return GhostObject != null; } }

    public Vector3 Position { get { return GhostTransform == null ? Vector3.zero : GhostTransform.position; } }

    public bool Spawn() {
        var sprite = RandomizerGhost.Sprite();
        if (sprite == null) {
            return false;
        }

        GhostObject = Object.Instantiate(sprite.gameObject);
        GhostObject.name = "randomizerGhost";
        GhostObject.transform.parent = null;
        // Instantiate drops the parent but keeps the local offset: seat it on the live Ori
        GhostObject.transform.position = sprite.position;
        GhostObject.transform.rotation = sprite.rotation;
        Object.DontDestroyOnLoad(GhostObject);

        // the clone would otherwise keep taking orders from the live Ori it was copied from
        foreach (var behavior in GhostObject.GetComponentsInChildren<MonoBehaviour>(true)) {
            if (behavior != null && RandomizerGhost.Detach.Contains(behavior.GetType().Name)) {
                Object.Destroy(behavior);
            }
        }

        // true: the instance material; false is the shared one the live Ori renders with
        foreach (var renderer in GhostObject.GetComponentsInChildren<Renderer>(true)) {
            UberShaderAPI.SetColor(renderer, new Color(Shade.r, Shade.g, Shade.b, 0f), true);
        }

        GhostTransform = GhostObject.transform;
        GhostAnimator = GhostObject.GetComponent<SpriteAnimatorWithTransitions>();
        Cursor = 0;
        Posed = null;
        PosedTime = 0f;
        AuraShown = 0;
        Hidden = false;
        Faded = 1f;
        // it fades up from nothing the same way it will fade away
        Veil = 0f;
        Placed = false;
        Leaving = false;
        WarpUntil = 0f;
        return true;
    }

    public void Despawn() {
        if (GhostObject != null) {
            // tells a broken effect from one that never fired
            Randomizer.log("ghost " + Label + ": used " + Used.Aura + " auras, " + Used.Arrow +
                " arrows, " + Used.Aim + " aim lines, " + Used.WallArrow + " wall arrows, " +
                Used.Burst + " bursts, " + Used.Link + " links");
        }

        Used = new Counts();
        Drop(ref GhostObject);
        Drop(ref AuraObject);
        Drop(ref ArrowObject);
        Drop(ref AimObject);
        Drop(ref WallObject);
        Drop(ref LinkObject);
        Drop(ref LabelObject);
        GhostTransform = null;
        GhostAnimator = null;
        ArrowPivot = null;
        AimRenderer = null;
        LabelBox = null;
        Labelled = null;
    }

    // Hiding is SetActive: it stops the animator, and transforms still apply while inactive.
    public void Cull(bool hidden) {
        if (GhostObject == null || Hidden == hidden) {
            return;
        }

        Hidden = hidden;
        if (!hidden) {
            // a corpse stays down: Dead only acts when the death flag changes
            GhostObject.SetActive(!DeadShown);
            if (LabelObject != null) {
                LabelObject.SetActive(Labelled != null);
                LabelFresh = true;
                LabelAlpha = 0f;
            }

            // the animator missed everything it slept through, so make the next Pose re-seat it
            Posed = null;
        }
    }

    // On its way out for good: the coordinator drops it once Gone.
    public void Vanish() {
        Leaving = true;
    }

    public bool Gone { get { return GhostObject == null || (Leaving && Veil <= 0f); } }

    // Steps the veil toward shown or hidden and repaints; every frame, leaving ghosts included.
    public void Sink() {
        if (GhostObject == null) {
            return;
        }

        var want = Leaving || Hidden || !Placed || Time.time < WarpUntil ? 0f : 1f;
        if (Veil != want) {
            var span = want < Veil ? FadeOut : FadeIn;
            Veil = span > 0.001f
                ? Mathf.MoveTowards(Veil, want, Time.deltaTime / span)
                : want;
            Repaint();
        }

        // faded out where it stood: now it can stop costing anything
        if (Veil <= 0f && Hidden && !Leaving && GhostObject.activeSelf) {
            GhostObject.SetActive(false);
            if (LabelObject != null) {
                LabelObject.SetActive(false);
            }
        }
    }

    public void Fade(float alpha) {
        if (GhostObject == null || Mathf.Abs(alpha - Faded) < 0.02f) {
            return;
        }

        Faded = alpha;
        Repaint();
    }

    private void Repaint() {
        var alpha = Faded * Veil;
        var faded = new Color(Shade.r, Shade.g, Shade.b, Shade.a * alpha);
        foreach (var renderer in GhostObject.GetComponentsInChildren<Renderer>(true)) {
            UberShaderAPI.SetColor(renderer, faded, true);
        }

        if (LabelObject != null) {
            Paint(LabelObject, alpha * LabelAlpha);
        }
    }

    private static void Drop(ref GameObject target) {
        if (target != null) {
            Object.Destroy(target);
        }

        target = null;
    }

    // Walks the cursor forward to the source's time and draws that pose; it rewinds only to seek.
    public void Tick(IGhostSource source) {
        var samples = source.Samples;
        if (GhostTransform == null || samples.Count < 2) {
            return;
        }

        var at = source.At;
        // trimmed under the cursor, or At moved back: re-seek from the start without effects
        var reseek = false;
        if (Cursor > samples.Count - 2 || samples[Cursor].Time > at) {
            Cursor = 0;
            reseek = true;
        }

        // every sample crossed is checked: a slow frame can step over the one where a clip began
        while (Cursor < samples.Count - 2 && samples[Cursor + 1].Time <= at) {
            Cursor++;
            if (!reseek && !Hidden && RandomizerGhost.Began(samples[Cursor], samples[Cursor - 1])) {
                Effects(samples[Cursor], VelocityAt(samples, Cursor));
            }
        }

        var from = samples[Cursor];
        var to = samples[Cursor + 1];
        var span = to.Time - from.Time;
        var t = span > 0.0001f ? Mathf.Clamp01((at - from.Time) / span) : 0f;

        // a gap past WarpDistance is a teleport, cut rather than interpolated
        var warp = (to.Position - from.Position).sqrMagnitude >
            RandomizerGhost.WarpDistance * RandomizerGhost.WarpDistance;
        if (warp && Cursor != Warped) {
            Warped = Cursor;
            Randomizer.log("ghost " + Label + ": holding across a " +
                (to.Position - from.Position).magnitude.ToString("F1") + " unit warp");
            // out where it stood, in where it lands: by the time it moves it is invisible
            WarpUntil = Time.time + FadeOut;
            WarpHold = from.Position;
        }

        // a facing flip is cut: slerping through 180 degrees about Y goes edge-on
        var flip = Quaternion.Angle(from.Rotation, to.Rotation) > RandomizerGhost.FlipAngle;
        GhostTransform.position = Time.time < WarpUntil
            ? WarpHold
            : (warp ? from.Position : Vector3.Lerp(from.Position, to.Position, t));
        GhostTransform.rotation = warp || flip
            ? from.Rotation : Quaternion.Slerp(from.Rotation, to.Rotation, t);
        GhostTransform.localScale = from.Scale;
        Placed = true;
        // kept for a culled ghost too, or a distant peer back from the title still retires
        TitleSince = from.OnTitle ? (TitleSince < 0f ? Time.time : TitleSince) : -1f;
        if (Hidden) {
            return;
        }

        Dead(from.Died);
        Pose(from);
        Aura(from.Charge);
        Arrow(from.BashAngle, from.BashTarget);
        AimLine(from.GrenadeAim);
        WallArrow(from.WallAim);
        SoulLink(from.SoulLink);
        Status(from);
    }

    private static Vector3 VelocityAt(List<Sample> samples, int index) {
        if (index + 1 >= samples.Count) {
            return Vector3.zero;
        }

        var span = samples[index + 1].Time - samples[index].Time;
        return span <= 0.0001f ? Vector3.zero
            : (samples[index + 1].Position - samples[index].Position) / span;
    }

    // Drives the sample's clip and playhead into the clone's animator.
    private void Pose(Sample from) {
        if (GhostAnimator == null || string.IsNullOrEmpty(from.Animation)) {
            return;
        }

        var clip = RandomizerGhost.Resolve(from.Animation);
        if (clip == null) {
            return;
        }

        // only where a clip begins (a per-frame SetAnimation traps it mid-transition); a
        // re-trigger keeps its name, so time stepping back is the only marker
        if (clip == Posed && (RandomizerGhost.Loops(clip) ||
                from.AnimationTime >= PosedTime - RandomizerGhost.Rewind)) {
            PosedTime = from.AnimationTime;
            return;
        }

        // ignoreIfSameAnimation would block the rewind a re-trigger needs
        GhostAnimator.SetAnimation(clip, clip != Posed);
        GhostAnimator.CurrentAnimationTime = from.AnimationTime;
        Posed = clip;
        PosedTime = from.AnimationTime;
    }

    // Ori is switched off on death, not animated, and so is the ghost. The held sample's aims
    // are NaN, so arrows and lines clear themselves.
    private void Dead(bool died) {
        if (died == DeadShown || GhostObject == null) {
            return;
        }

        DeadShown = died;
        if (died) {
            var effect = RandomizerGhost.DeathEffect();
            if (effect == null && !Mourned) {
                Mourned = true;
                Randomizer.log("ghost: no death effect to copy; ghosts will vanish silently");
            }

            Burst(effect, GhostTransform.position, Quaternion.identity,
                RandomizerGhost.DeathBurstAlpha);
        }

        GhostObject.SetActive(!died);
    }

    // Followed by position, not parented: the ghost transform carries Ori's scale.
    private void Aura(int charge) {
        if (charge != AuraShown) {
            AuraShown = charge;
            Drop(ref AuraObject);

            var ability = RandomizerGhost.Charger();
            var prefab = ability == null || charge == 0 ? null
                : (charge == 2 ? ability.ChargedEffectToSpawn : ability.ChargingEffectToSpawn);
            if (prefab != null) {
                Used.Aura++;
                AuraObject = (GameObject)Object.Instantiate(
                    prefab, GhostTransform.position, GhostTransform.rotation);
                RandomizerGhost.Quiet(AuraObject);
                RandomizerGhost.Dim(AuraObject, RandomizerGhost.AuraAlpha);
            }
        }

        if (AuraObject != null) {
            AuraObject.transform.position = GhostTransform.position;
        }
    }

    // BashAttackGame reads the live stick, so it goes; the angle drives the arrow's parent instead.
    private void Arrow(float angle, Vector2 target) {
        if (float.IsNaN(angle)) {
            Drop(ref ArrowObject);
            ArrowPivot = null;
            return;
        }

        if (ArrowObject == null) {
            var ability = RandomizerGhost.Basher();
            if (ability == null || ability.BashAttackGamePrefab == null) {
                return;
            }

            Used.Arrow++;
            ArrowObject = (GameObject)Object.Instantiate(ability.BashAttackGamePrefab);
            var game = ArrowObject.GetComponent<BashAttackGame>();
            if (game != null) {
                ArrowPivot = game.ArrowSprite == null ? null : game.ArrowSprite.parent;
                Object.Destroy(game);
            }

            RandomizerGhost.Quiet(ArrowObject);
            RandomizerGhost.Recolor(ArrowObject);
        }

        ArrowObject.transform.position = float.IsNaN(target.x)
            ? GhostTransform.position
            : new Vector3(target.x, target.y, GhostTransform.position.z);
        if (ArrowPivot != null) {
            ArrowPivot.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    // Drawn, not cloned: a cloned trajectory still drives the live player's LineRenderer.
    private void AimLine(Vector2 velocity) {
        if (float.IsNaN(velocity.x)) {
            Drop(ref AimObject);
            AimRenderer = null;
            return;
        }

        if (AimObject == null) {
            var grenade = RandomizerGhost.Grenader();
            var source = grenade == null ? null : grenade.Trajectory;
            if (source == null || source.LineRenderer == null) {
                return;
            }

            Used.Aim++;
            AimObject = new GameObject("ghostAimLine");
            AimRenderer = AimObject.AddComponent<LineRenderer>();
            AimRenderer.material = new Material(source.LineRenderer.sharedMaterial);
            AimRenderer.SetWidth(RandomizerGhost.AimWidth, RandomizerGhost.AimWidth);
            // the default sorting layer draws behind the scenery; borrow the real line's
            AimRenderer.sortingLayerID = source.LineRenderer.sortingLayerID;
            AimRenderer.sortingOrder = source.LineRenderer.sortingOrder;
            AimObject.layer = source.LineRenderer.gameObject.layer;
            RandomizerGhost.Recolor(AimObject);
            RandomizerGhost.Dim(AimObject, RandomizerGhost.AimAlpha);
        }

        // read every frame: our own trajectory's LinePoints is near zero until it initialises
        var live = RandomizerGhost.Grenader();
        var shape = live == null ? null : live.Trajectory;
        var gravity = shape == null ? 0f : shape.Gravity;
        var count = shape == null ? 0 : shape.LinePoints;
        if (count < 2 || gravity <= 0f) {
            if (!Warned) {
                Warned = true;
                Randomizer.log("ghost: no usable grenade trajectory to copy (points " + count +
                    ", gravity " + gravity + "); their aim line will not draw");
            }

            AimRenderer.SetVertexCount(0);
            return;
        }

        var at = GhostTransform.position;
        var speed = new Vector3(velocity.x, velocity.y, 0f);
        var points = new List<Vector3>();
        for (var i = 0; i < count; i++) {
            for (var step = 0; step < 2; step++) {
                at += speed * RandomizerGhost.Tick;
                speed += Vector3.down * gravity * RandomizerGhost.Tick;
            }

            if (speed.y < 0f && i > 5) {
                break;
            }

            points.Add(at);
        }

        AimRenderer.SetVertexCount(points.Count);
        for (var i = 0; i < points.Count; i++) {
            AimRenderer.SetPosition(i, points[i]);
        }
    }

    // Like the bash arrow, but the angle is already a world rotation.
    private void WallArrow(float angle) {
        if (float.IsNaN(angle)) {
            Drop(ref WallObject);
            return;
        }

        if (WallObject == null) {
            var wall = RandomizerGhost.Waller();
            if (wall == null || wall.Arrow == null) {
                return;
            }

            Used.WallArrow++;
            WallObject = (GameObject)Object.Instantiate(wall.Arrow.gameObject);
            // unparented, so the parent's scale is baked in
            WallObject.transform.localScale =
                wall.Arrow.transform.lossyScale * RandomizerGhost.WallArrowScale;
            // keep the drawing, drop everything still wired to the player
            RandomizerGhost.Strip(WallObject, null);
            RandomizerGhost.Quiet(WallObject);
            WallObject.SetActive(true);
            RandomizerGhost.Recolor(WallObject);
            RandomizerGhost.Dim(WallObject, RandomizerGhost.WallArrowAlpha);
        }

        WallObject.transform.position = GhostTransform.position;
        WallObject.transform.eulerAngles = new Vector3(0f, 0f, angle);
    }

    // The game's own link marker: its animators run, its SoulFlame component goes.
    private void SoulLink(Vector2 at) {
        if (float.IsNaN(at.x)) {
            Drop(ref LinkObject);
            return;
        }

        var where = new Vector3(at.x, at.y, RandomizerGhost.LinkBehind);
        if (LinkObject == null) {
            var flame = RandomizerGhost.Flamer();
            if (flame == null || flame.CheckpointMarker == null) {
                return;
            }

            Used.Link++;
            LinkObject = (GameObject)Object.Instantiate(flame.CheckpointMarker, where, Quaternion.identity);
            LinkObject.name = "ghostSoulLink";
            foreach (var marker in LinkObject.GetComponentsInChildren<SoulFlame>(true)) {
                Object.Destroy(marker);
            }

            foreach (var animator in LinkObject.GetComponentsInChildren<BaseAnimator>(true)) {
                if (animator.GetType().Name.StartsWith("UberPost")) {
                    Object.DestroyImmediate(animator);
                } else if (animator.AnimatorDriver != null) {
                    animator.AnimatorDriver.RestartForward();
                }
            }

            RandomizerGhost.Quiet(LinkObject);
            // theirs, not yours: it wears their color and stays faint
            RandomizerGhost.Paint(LinkObject, RandomizerGhost.LinkShade(Shade));
            RandomizerGhost.Dim(LinkObject, RandomizerGhost.LinkAlpha);
        }

        LinkObject.transform.position = where;
    }

    // Seconds this ghost has stood under "..."; the coordinator takes it down after a while.
    public float TitleFor { get { return TitleSince < 0f ? 0f : Time.time - TitleSince; } }

    private float TitleSince = -1f;

    // what a peer in a menu wears; a static so the glyph can be tried live
    internal static string PauseGlyph = "II";

    private const float LabelHeight = 0.8f;

    // Every way of not being there takes this beat; coming back takes the shorter one.
    // Statics, so both can be tuned live.
    internal static float FadeOut = 0.167f;

    internal static float FadeIn = 0.05f;

    // the label's height in world units, whatever size the hint text comes at
    private const float LabelSize = 1f;

    // A word over the head for a peer who is not really there: "..." on the title, a pause mark
    // in a menu. Followed by position, so Ori's flip and scale stay out of it.
    private void Status(Sample from) {
        var text = from.OnTitle ? "..." : (from.InMenu ? PauseGlyph : null);
        if (text == null && (LabelObject == null || !LabelObject.activeSelf)) {
            return;
        }

        if (LabelObject == null && !MakeLabel()) {
            return;
        }

        if (!LabelObject.activeSelf) {
            LabelObject.SetActive(true);
            LabelFresh = true;
            LabelAlpha = 0f;
            Paint(LabelObject, 0f);
        }

        // TextBox resets its text when it comes up, so ours waits for the frame after
        if (LabelFresh) {
            LabelFresh = false;
            Labelled = null;
            return;
        }

        if (text != null && Labelled != text) {
            Labelled = text;
            LabelBox.SetText(text);
            LabelBox.RenderText();
            var height = LabelBox.boundsTop - LabelBox.boundsBottom;
            if (height > 0.001f) {
                LabelObject.transform.localScale = Vector3.one * (LabelSize / height);
            }
        }

        var beat = text == null ? FadeOut : FadeIn;
        LabelAlpha = Mathf.MoveTowards(LabelAlpha, text == null ? 0f : 1f, Time.deltaTime / beat);
        var alpha = Faded * Veil * LabelAlpha;
        if (Mathf.Abs(alpha - LabelPainted) > 0.001f) {
            LabelPainted = alpha;
            Paint(LabelObject, alpha);
        }
        if (LabelAlpha <= 0f) {
            Labelled = null;
            LabelObject.SetActive(false);
            return;
        }

        LabelObject.transform.position = GhostTransform.position + Vector3.up * LabelHeight;
        LabelObject.transform.rotation = Quaternion.identity;
    }

    private void Paint(GameObject target, float alpha) {
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true)) {
            renderer.enabled = true;
            UberShaderAPI.SetColor(renderer, new Color(Shade.r, Shade.g, Shade.b, alpha), true);
        }
    }

    private bool MakeLabel() {
        var controller = UI.MessageController;
        var hint = controller == null ? null : controller.HintMessage;
        var text = hint == null ? null : hint.transform.FindChild("text");
        if (text == null) {
            return false;
        }

        // cloned while inactive, so the clone does not wake up as the hint's own text
        var wasActive = hint.activeSelf;
        hint.SetActive(false);
        var clone = Object.Instantiate(text.gameObject) as GameObject;
        hint.SetActive(wasActive);
        if (clone == null) {
            return false;
        }

        clone.name = "ghostStatus";
        clone.transform.parent = null;
        Object.DontDestroyOnLoad(clone);
        // the hint's layer is the gui camera's; the ghost is in the world
        var art = LayerMask.NameToLayer("art");
        foreach (var child in clone.GetComponentsInChildren<Transform>(true)) {
            child.gameObject.layer = art;
        }

        // that component sizes a message background this label does not have
        var sizer = clone.GetComponent<ScaleToTextBox>();
        if (sizer != null) {
            Object.Destroy(sizer);
        }

        LabelBox = clone.GetComponent<TextBox>();
        if (LabelBox == null) {
            Object.Destroy(clone);
            return false;
        }

        LabelBox.alignment = AlignmentMode.Center;
        LabelBox.horizontalAnchor = HorizontalAnchorMode.Center;
        LabelBox.verticalAnchor = VerticalAnchorMode.Bottom;
        LabelBox.CreateRendersIfThereAreNone();
        LabelObject = clone;
        LabelObject.SetActive(false);
        return true;
    }

    private void Effects(Sample sample, Vector3 velocity) {
        if (GhostTransform == null) {
            return;
        }

        if (sample.Animation == "doubleJump") {
            var ability = RandomizerGhost.Jumper();
            if (ability != null) {
                // faces the way Ori was travelling
                var facing = Quaternion.Euler(0f, 0f, -Mathf.Atan2(velocity.x, velocity.y) * Mathf.Rad2Deg);
                // TrippleJumpAfterShock will not start outside the game's own spawn path, and
                // the two bursts differ only by audio
                Burst(ability.DoubleJumpAfterShock, GhostTransform.position, facing,
                    RandomizerGhost.JumpBurstAlpha);
            }
        } else if (sample.Animation == "stompLand") {
            var ability = RandomizerGhost.Stomper();
            if (ability != null) {
                Burst(ability.StompLandEffect,
                    GhostTransform.position + RandomizerGhost.FeetOffset, Quaternion.identity,
                    RandomizerGhost.StompBurstAlpha);
            }
        }
    }

    private void Burst(GameObject prefab, Vector3 at, Quaternion facing, float alpha) {
        if (prefab == null) {
            return;
        }

        Used.Burst++;
        // A private clone, never a pooled one: everything below mutates it. OnPoolSpawned is
        // what starts these, so call it here.
        var spawned = (GameObject)Object.Instantiate(prefab, at, facing);
        foreach (var behavior in spawned.GetComponentsInChildren<MonoBehaviour>(true)) {
            var pooled = behavior as IPooled;
            if (pooled != null) {
                pooled.OnPoolSpawned();
            }
        }

        // BaseAnimators fade these in, and a fresh clone's driver sits where the prefab left it
        foreach (var animator in spawned.GetComponentsInChildren<BaseAnimator>(true)) {
            // UberPost* drive the full-screen post; Destroy would land after the driver starts
            if (animator.GetType().Name.StartsWith("UberPost")) {
                Object.DestroyImmediate(animator);
                continue;
            }

            if (animator.AnimatorDriver != null) {
                animator.AnimatorDriver.RestartForward();
            }
        }

        // Pieces wider than EffectSpan shrink; the scale animators above them go or undo it.
        foreach (var renderer in spawned.GetComponentsInChildren<Renderer>(true)) {
            if (renderer == null) {
                continue;
            }

            var span = renderer.transform.lossyScale.x;
            if (span <= RandomizerGhost.EffectSpan) {
                continue;
            }

            for (var above = renderer.transform; above != null; above = above.parent) {
                foreach (var scaler in above.GetComponents<LegacyScaleAnimator>()) {
                    Object.DestroyImmediate(scaler);
                }

                if (above == spawned.transform) {
                    break;
                }
            }

            renderer.transform.localScale *= RandomizerGhost.EffectSpan / span;
        }

        // a grab pass redraws the whole screen; its child goes with it
        foreach (var grab in spawned.GetComponentsInChildren<UberShaderBlockGrabPass>(true)) {
            if (grab.gameObject == spawned) {
                Object.DestroyImmediate(grab);
            } else {
                Object.DestroyImmediate(grab.gameObject);
            }
        }

        // no camera shake: the action null-checks its target, so emptying it keeps the sequence
        foreach (var shake in spawned.GetComponentsInChildren<CameraShakeAction>(true)) {
            shake.ShakeCamera = null;
        }

        foreach (var shake in spawned.GetComponentsInChildren<CameraShake>(true)) {
            Object.Destroy(shake);
        }

        foreach (var rumble in spawned.GetComponentsInChildren<ControllerShake>(true)) {
            Object.Destroy(rumble);
        }

        // only the sound comes out; the effect's own animators run the rest
        RandomizerGhost.Hush(spawned);
        RandomizerGhost.Dim(spawned, alpha);
    }

    private struct Counts {
        public int Aura;
        public int Arrow;
        public int Aim;
        public int Burst;
        public int WallArrow;
        public int Link;
    }

    private Counts Used;

    private int Cursor;

    private GameObject GhostObject;

    private Transform GhostTransform;

    private SpriteAnimatorWithTransitions GhostAnimator;

    private TextureAnimationWithTransitions Posed;

    private float PosedTime;

    private int AuraShown;

    private bool DeadShown;

    private static bool Mourned;

    private int Warped = -1;

    private bool Hidden;

    private float Faded = 1f;

    private GameObject AuraObject;

    private GameObject ArrowObject;

    private Transform ArrowPivot;

    private GameObject AimObject;

    private LineRenderer AimRenderer;

    private bool Warned;

    private GameObject WallObject;

    private GameObject LinkObject;

    private GameObject LabelObject;

    private TextBox LabelBox;

    // the text on the label, null while it is hidden
    private string Labelled;

    private bool LabelFresh;

    private float LabelAlpha;

    private float LabelPainted = -1f;

    // 1 on screen, 0 not there: the cull, the warp and the retire all ride this one alpha
    private float Veil;

    // the clone spawns on the live Ori, so it stays veiled until a sample has moved it
    private bool Placed;

    private bool Leaving;

    private float WarpUntil;

    private Vector3 WarpHold;
}
