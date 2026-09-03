using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;

using Sample = RandomizerGhost.Sample;

// One rendered ghost. Everything here is per-ghost state, which is why it is an instance:
// multiplayer draws one of these per peer, practice mode draws one.
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
        // Instantiate keeps the local transform but drops the parent, so the sprite's offset
        // within Ori would become a world position near the origin; seat it on the live Ori
        GhostObject.transform.position = sprite.position;
        GhostObject.transform.rotation = sprite.rotation;
        Object.DontDestroyOnLoad(GhostObject);

        // the clone would otherwise keep taking orders from the live Ori it was copied from
        foreach (var behaviour in GhostObject.GetComponentsInChildren<MonoBehaviour>(true)) {
            if (behaviour != null && RandomizerGhost.Detach.Contains(behaviour.GetType().Name)) {
                Object.Destroy(behaviour);
            }
        }

        // true asks for the instance material: false hands back the one the live Ori is
        // still rendering with, and tinting that tints them both
        foreach (var renderer in GhostObject.GetComponentsInChildren<Renderer>(true)) {
            UberShaderAPI.SetColor(renderer, Shade, true);
        }

        GhostTransform = GhostObject.transform;
        GhostAnimator = GhostObject.GetComponent<SpriteAnimatorWithTransitions>();
        Cursor = 0;
        Posed = null;
        PosedTime = 0f;
        AuraShown = 0;
        Hidden = false;
        Faded = 1f;
        return true;
    }

    public void Despawn() {
        if (GhostObject != null) {
            // one line a ghost, so a QA pass can tell "the effect is broken" from "it never fired"
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
        GhostTransform = null;
        GhostAnimator = null;
        ArrowPivot = null;
        AimRenderer = null;
    }

    // Hiding is SetActive, which also stops the animator, most of what a distant ghost costs;
    // transforms still apply to an inactive object, so it does not snap when it comes back.
    public void Cull(bool hidden) {
        if (GhostObject == null || Hidden == hidden) {
            return;
        }

        Hidden = hidden;
        GhostObject.SetActive(!hidden);
        if (!hidden) {
            // the animator missed everything it slept through, so make the next Pose re-seat it
            Posed = null;
        }
    }

    public void Fade(float alpha) {
        if (GhostObject == null || Mathf.Abs(alpha - Faded) < 0.02f) {
            return;
        }

        Faded = alpha;
        var faded = new Color(Shade.r, Shade.g, Shade.b, Shade.a * alpha);
        foreach (var renderer in GhostObject.GetComponentsInChildren<Renderer>(true)) {
            UberShaderAPI.SetColor(renderer, faded, true);
        }
    }

    private static void Drop(ref GameObject target) {
        if (target != null) {
            Object.Destroy(target);
        }

        target = null;
    }

    // Walks the cursor to the source's current time and draws that pose. The cursor only ever
    // moves forward, so a long recording costs no more per frame than a short one.
    public void Tick(IGhostSource source) {
        var samples = source.Samples;
        if (GhostTransform == null || samples.Count < 2) {
            return;
        }

        var at = source.At;
        // a live source trims old samples out from under the cursor; walking back to the start
        // must not replay every effect on the way
        var reseek = false;
        if (Cursor > samples.Count - 2 || samples[Cursor].Time > at) {
            Cursor = 0;
            reseek = true;
        }

        // A one-shot effect hangs off the sample where its clip began, and a slow frame can step
        // over several samples at once, so every sample crossed has to be checked.
        while (Cursor < samples.Count - 2 && samples[Cursor + 1].Time <= at) {
            Cursor++;
            // a burst nobody can see is the exact cost culling exists to avoid
            if (!reseek && !Hidden && RandomizerGhost.Began(samples[Cursor], samples[Cursor - 1])) {
                Effects(samples[Cursor], VelocityAt(samples, Cursor));
            }
        }

        var from = samples[Cursor];
        var to = samples[Cursor + 1];
        var span = to.Time - from.Time;
        var t = span > 0.0001f ? Mathf.Clamp01((at - from.Time) / span) : 0f;

        // A teleport -- soul link, spirit well, death -- puts the next sample across the map,
        // and interpolating into it sails the ghost there instead of cutting.
        var warp = (to.Position - from.Position).sqrMagnitude >
            RandomizerGhost.WarpDistance * RandomizerGhost.WarpDistance;
        if (warp && Cursor != Warped) {
            Warped = Cursor;
            Randomizer.log("ghost " + Label + ": holding across a " +
                (to.Position - from.Position).magnitude.ToString("F1") + " unit warp");
        }

        // facing is a 180 degree turn about Y: slerping through it takes the sprite edge-on, so
        // any large step is a flip and is cut rather than swept
        var flip = Quaternion.Angle(from.Rotation, to.Rotation) > RandomizerGhost.FlipAngle;
        GhostTransform.position = warp ? from.Position : Vector3.Lerp(from.Position, to.Position, t);
        GhostTransform.rotation = warp || flip
            ? from.Rotation : Quaternion.Slerp(from.Rotation, to.Rotation, t);
        // constant in practice -- Ori's own scale -- but never interpolated, so it stays right
        // if that ever stops being true
        GhostTransform.localScale = from.Scale;
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
    }

    private static Vector3 VelocityAt(List<Sample> samples, int index) {
        if (index + 1 >= samples.Count) {
            return Vector3.zero;
        }

        var span = samples[index + 1].Time - samples[index].Time;
        return span <= 0.0001f ? Vector3.zero
            : (samples[index + 1].Position - samples[index].Position) / span;
    }

    // The clone animates itself but nothing tells it what Ori was doing; driving the clip and
    // the time into it is what keeps the ghost from sliding along in one pose.
    private void Pose(Sample from) {
        if (GhostAnimator == null || string.IsNullOrEmpty(from.Animation)) {
            return;
        }

        var clip = RandomizerGhost.Resolve(from.Animation);
        if (clip == null) {
            return;
        }

        // Only where a clip begins -- a per-frame SetAnimation traps the ghost mid-transition.
        // A re-triggered clip keeps its name, so time stepping backwards is the only marker.
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

    // Ori is switched off on death rather than animated, so the ghost does the same. The held
    // sample's aims are all NaN, so the arrows and lines clear themselves.
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

    // Followed by position rather than parented: the ghost transform carries Ori's own scale,
    // and a parented effect inherits it.
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

    // The arrow's own script reads the live player's stick, so it goes and the recorded angle
    // drives the parent transform instead. It belongs on the bash target, not on Ori.
    private void Arrow(float angle, Vector2 target) {
        if (float.IsNaN(angle)) {
            Drop(ref ArrowObject);
            ArrowPivot = null;
            return;
        }

        if (ArrowObject == null) {
            var ability = RandomizerGhost.Ability<SeinBashAttack>();
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
            RandomizerGhost.Recolour(ArrowObject);
        }

        ArrowObject.transform.position = float.IsNaN(target.x)
            ? GhostTransform.position
            : new Vector3(target.x, target.y, GhostTransform.position.z);
        if (ArrowPivot != null) {
            ArrowPivot.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    // Drawn rather than cloned: a cloned trajectory keeps a LineRenderer reference that lives
    // outside the clone, so it redraws the live player's line. The arc is six lines of arithmetic.
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
            // the default sorting layer at order zero is behind the scenery here; borrow the
            // real line's place in the stack along with its material
            AimRenderer.sortingLayerID = source.LineRenderer.sortingLayerID;
            AimRenderer.sortingOrder = source.LineRenderer.sortingOrder;
            AimObject.layer = source.LineRenderer.gameObject.layer;
            RandomizerGhost.Recolour(AimObject);
            RandomizerGhost.Dim(AimObject, RandomizerGhost.AimAlpha);
        }

        // read every frame: these come from our own trajectory object, whose LinePoints is
        // near zero until it has initialised
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

    // Same shape as the bash arrow, only the aim is already a world rotation rather than a
    // number the game turns into one.
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
            // the arrow renders at its parent's scale; unparented, its localScale alone leaves
            // it about 3.4 times too small
            WallObject.transform.localScale =
                wall.Arrow.transform.lossyScale * RandomizerGhost.WallArrowScale;
            // same reasoning as the aim line: keep the drawing, drop everything that thinks it
            // is still attached to a player
            RandomizerGhost.Strip(WallObject, null);
            RandomizerGhost.Quiet(WallObject);
            WallObject.SetActive(true);
            RandomizerGhost.Recolour(WallObject);
            RandomizerGhost.Dim(WallObject, RandomizerGhost.WallArrowAlpha);
        }

        WallObject.transform.position = GhostTransform.position;
        WallObject.transform.eulerAngles = new Vector3(0f, 0f, angle);
    }

    // Their link stands where they put it, so a death reads as a return to it. The marker is the
    // game's own prefab: its animators run, its gameplay component goes.
    private void SoulLink(Vector2 at) {
        if (float.IsNaN(at.x)) {
            Drop(ref LinkObject);
            return;
        }

        var where = new Vector3(at.x, at.y, 0f);
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
            RandomizerGhost.Recolour(LinkObject);
            RandomizerGhost.Dim(LinkObject, RandomizerGhost.LinkAlpha);
        }

        LinkObject.transform.position = where;
    }

    // Called only where a clip begins, so it does not need to guard against repeats.
    private void Effects(Sample sample, Vector3 velocity) {
        if (GhostTransform == null) {
            return;
        }

        if (sample.Animation == "doubleJump") {
            var ability = RandomizerGhost.Ability<SeinDoubleJump>();
            if (ability != null) {
                // the burst is turned to face the way Ori was travelling, which the recorded
                // positions give us without having to store it
                var facing = Quaternion.Euler(0f, 0f, -Mathf.Atan2(velocity.x, velocity.y) * Mathf.Rad2Deg);
                // TrippleJumpAfterShock will not start outside the game's own spawn path, and
                // the two bursts differ only by audio, which a ghost has none of.
                Burst(ability.DoubleJumpAfterShock, GhostTransform.position, facing,
                    RandomizerGhost.JumpBurstAlpha);
            }
        } else if (sample.Animation == "stompLand") {
            var ability = RandomizerGhost.Ability<SeinStomp>();
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
        // A private clone, never a pooled one: everything below mutates what it is handed, and
        // a pooled object goes back damaged. OnPoolSpawned is what starts these, so call it here.
        var spawned = (GameObject)Object.Instantiate(prefab, at, facing);
        foreach (var behaviour in spawned.GetComponentsInChildren<MonoBehaviour>(true)) {
            var pooled = behaviour as IPooled;
            if (pooled != null) {
                pooled.OnPoolSpawned();
            }
        }

        // OnPoolSpawned only reaches IPooled. What fades and scales these in are BaseAnimators,
        // and a fresh clone's driver sits where the prefab left it -- for a fade, invisible.
        foreach (var animator in spawned.GetComponentsInChildren<BaseAnimator>(true)) {
            // UberPost* animators drive the full-screen post stack, so a clone's would paint the
            // whole screen; Destroy lands at end of frame, too late for a driver about to start
            if (animator.GetType().Name.StartsWith("UberPost")) {
                Object.DestroyImmediate(animator);
                continue;
            }

            if (animator.AnimatorDriver != null) {
                animator.AnimatorDriver.RestartForward();
            }
        }

        // An effect reaches the whole screen by grabbing it or by being bigger than it. Shrink
        // rather than drop, and take the scale animators above it or they undo the shrink.
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

        // a grab pass redraws the whole screen wherever the effect sits; nothing else in the
        // child is worth keeping, so the whole child goes
        foreach (var grab in spawned.GetComponentsInChildren<UberShaderBlockGrabPass>(true)) {
            if (grab.gameObject == spawned) {
                Object.DestroyImmediate(grab);
            } else {
                Object.DestroyImmediate(grab.gameObject);
            }
        }

        // a ghost landing is not something the camera should feel. The action null-checks its
        // own target, so emptying it is quieter than tearing the action out of its sequence.
        foreach (var shake in spawned.GetComponentsInChildren<CameraShakeAction>(true)) {
            shake.ShakeCamera = null;
        }

        foreach (var shake in spawned.GetComponentsInChildren<CameraShake>(true)) {
            Object.Destroy(shake);
        }

        foreach (var rumble in spawned.GetComponentsInChildren<ControllerShake>(true)) {
            Object.Destroy(rumble);
        }

        // only the sound comes out: spawned through the pool, the game's own animators run
        // the show
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
}
