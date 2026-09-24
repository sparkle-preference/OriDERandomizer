using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using System.IO;
using Core;
using Game;
using UnityEngine;

// Translucent replays, to race against or to watch other players by. Samples are timestamped
// sprite transforms, so framerate does not matter and facing, roll and bash spin come free.
// RandomizerGhost owns recording and the shared lookups; RandomizerGhostView renders.
public static class RandomizerGhost {
    public struct Sample {
        public float Time;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public string Animation;
        public float AnimationTime;
        public int Charge;
        public float BashAngle;
        // Arrow goes here and not on Ori
        public Vector2 BashTarget;
        public Vector2 GrenadeAim;
        public float WallAim;
        public bool Triple;
        public bool Died;
        // where their soul link stands; NaN when there is none
        public Vector2 SoulLink;
        // a menu is up on their side
        public bool InMenu;
        // no Ori to sample: they are on the title screen
        public bool OnTitle;
    }

    public static void Update() {
        try {
            RandomizerGhostNet.Update();
            RandomizerGhostSignal.Update();
            Drive();
            if (Recording) {
                Record();
            }
        } catch (System.Exception e) {
            if (!Complained) {
                Complained = true;
                Randomizer.log("ghost: update threw, everything after it is dead this frame -- " + e);
            }
        }
    }

    // Every ghost on screen, walked backwards so a finished one can be removed in place.
    private static void Drive() {
        var here = Sprite();

        for (var i = Shown.Count - 1; i >= 0; i--) {
            var source = Sources[i];
            var loopback = source as LoopbackGhostSource;
            if (loopback != null) {
                loopback.Feed();
            }

            var view = Shown[i];
            var silence = source.Silence;
            var keep = Kept(source);
            // a peer parked on the title says so for a while, then goes
            if (!view.Alive ||
                (!keep && (source.Done || silence > Retire + FadeOut || view.TitleFor > TitleRetire))) {
                view.Vanish();
                Leaving.Add(view);
                Shown.RemoveAt(i);
                Sources.RemoveAt(i);
                continue;
            }

            // a stalled peer holds its pose at full opacity for Retire seconds, then fades
            view.Tick(source);
            var fade = FadeOut > 0.001f ? (silence - Retire) / FadeOut : 1f;
            view.Fade(keep || silence <= Retire ? 1f : 1f - fade);
            view.Cull(here != null &&
                (view.Position - here.position).sqrMagnitude > CullRadius * CullRadius);
            view.Sink();
        }

        // nobody feeds these any more; they are only finishing their fade
        for (var i = Leaving.Count - 1; i >= 0; i--) {
            Leaving[i].Sink();
            if (Leaving[i].Gone) {
                Leaving[i].Despawn();
                Leaving.RemoveAt(i);
            }
        }
    }

    // An echo is yours until you clear it: its script stops growing whenever yours does.
    private static bool Kept(IGhostSource source) {
        var loopback = source as LoopbackGhostSource;
        return loopback != null && Echoes.Contains(loopback);
    }

    private static Color Shade(IGhostSource source) {
        return ShadeOf(source.PlayerId);
    }

    // Keyed on the player. Player zero is your own replay.
    public static Color ShadeOf(int player) {
        return player < 1 ? Tint : Palette[(player - 1) % Palette.Length];
    }

    private static bool Add(IGhostSource source) {
        var shade = Shade(source);
        var view = new RandomizerGhostView(source.Label, shade);
        if (!view.Spawn()) {
            Randomizer.log("ghost " + source.Label + ": could not spawn, Ori is not in the world");
            Randomizer.showHint(RandomizerUI.Message.InfoMessage("Ghost: Ori is not in the world", 3));
            return false;
        }

        Shown.Add(view);
        Sources.Add(source);
        Randomizer.log("ghost " + source.Label + ": added, " + Shown.Count + " on screen, shade " +
            shade.r.ToString("F2") + "/" + shade.g.ToString("F2") + "/" + shade.b.ToString("F2"));

        if (!Checked && RandomizerSettings.Dev.Value) {
            Checked = true;
            CheckCodec(source.Samples.Count > 1 ? source.Samples : Ghost);
            RandomizerGhostNet.Begin();
        }

        return true;
    }

    public static void Remove(IGhostSource source) {
        var i = Sources.IndexOf(source);
        if (i < 0) {
            return;
        }

        Shown[i].Vanish();
        Leaving.Add(Shown[i]);
        Shown.RemoveAt(i);
        Sources.RemoveAt(i);
    }

    private static void Clear() {
        foreach (var view in Shown) {
            view.Vanish();
            Leaving.Add(view);
        }

        Shown.Clear();
        Sources.Clear();
    }

    public static bool Playing { get { return Shown.Count > 0; } }

    // TODO: do we use this anymore?
    public static void ToggleRace() {
        var starting = !Recording;
        ToggleRecording();
        if (starting && Recording && !Playing) {
            TogglePlayback();
        } else if (!starting && Playing) {
            Clear();
        }
    }

    // TODO: do we use this anymore?
    public static void ToggleRecording() {
        if (Recording) {
            Recording = false;
            if (Take.Count > 1) {
                Ghost = new List<Sample>(Take);
                Save();
                Randomizer.showHint(RandomizerUI.Message.InfoMessage(
                    "Ghost recorded: " + Length(Ghost).ToString("F1") + "s", 3));
            } else {
                Randomizer.showHint(RandomizerUI.Message.InfoMessage("Ghost: nothing recorded", 3));
            }

            return;
        }

        var sprite = Sprite();
        if (sprite == null) {
            return;
        }

        Take.Clear();
        RecordStart = Time.time;
        Recording = true;
        Randomizer.showHint(RandomizerUI.Message.InfoMessage("Ghost: recording", 2));
    }

    // Echoes: loopback ghosts of your own recording, each a step further behind. Never while
    // real ghosts or a practice run are on.
    private static readonly List<LoopbackGhostSource> Echoes = new List<LoopbackGhostSource>();

    private static bool echoesRecord;

    public static void SpawnEcho() {
        if (RandomizerGhostSignal.Joined || PracticeController.Active) {
            Randomizer.showHint(RandomizerUI.Message.InfoMessage("Echoes are for playing alone", 3));
            return;
        }

        if (Sprite() == null) {
            return;
        }

        PruneEchoes();
        // nothing left of the last chain: the next one starts from here, not from that recording
        if (Echoes.Count == 0 && echoesRecord) {
            StopEchoTake();
        }

        if (!Recording) {
            Take.Clear();
            RecordStart = Time.time;
            Recording = true;
            echoesRecord = true;
        }

        var settings = RandomizerSettings.DevSettings.EchoDelay;
        var spacing = RandomizerSettings.DevSettings.EchoSpacing;
        var step = 0;
        foreach (var live in Echoes) {
            step = Mathf.Max(step, live.PlayerId - 1);
        }

        step++;
        var behind = Mathf.Max(0f, (settings == null ? 0.5f : settings.Value)
            + (step - 1) * (spacing == null ? 0.5f : spacing.Value));
        // you are player one, so the first echo wears the second player's colour
        var who = step + 1;
        // the interpolation hold comes off the start, so the echo trails by exactly `behind`
        var started = Mathf.Max(RecordStart, RecordStart + behind - InterpolationDelay);
        var echo = new LoopbackGhostSource(Take, "echo" + step, who, InterpolationDelay, started);
        if (Add(echo)) {
            Echoes.Add(echo);
            Randomizer.showHint(RandomizerUI.Message.InfoMessage(
                "Echo " + step + ": " + behind.ToString("F1") + "s behind you", 3));
        }
    }

    // An echo the coordinator has already taken down is gone as far as the chain is concerned.
    private static void PruneEchoes() {
        for (var i = Echoes.Count - 1; i >= 0; i--) {
            if (!Showing(Echoes[i])) {
                Echoes.RemoveAt(i);
            }
        }
    }

    // Only ever with the chain empty: the live echoes read from this list.
    private static void StopEchoTake() {
        echoesRecord = false;
        Recording = false;
        Take.Clear();
    }

    public static void ClearEchoes() {
        foreach (var echo in Echoes) {
            Remove(echo);
        }

        if (Echoes.Count > 0) {
            Randomizer.showHint(RandomizerUI.Message.InfoMessage("Echoes cleared", 2));
        }

        Echoes.Clear();
        // the recording was ours: it stops without becoming a stored ghost
        if (echoesRecord) {
            StopEchoTake();
        }
    }

    // TODO: do we use this anymore?
    public static void TogglePlayback() {
        if (Playing) {
            Clear();
            return;
        }

        if (!Stored()) {
            return;
        }

        Add(new RecordedGhostSource(Ghost, "replay", 0f));
    }

    // A loopback peer replaying the stored ghost; each call adds another.
    public static void TogglePeer() {
        if (!Stored()) {
            return;
        }

        var who = Shown.Count + 1;
        if (Add(new LoopbackGhostSource(Ghost, "peer" + who, who, InterpolationDelay))) {
            Randomizer.showHint(RandomizerUI.Message.InfoMessage(
                "Ghost: " + Shown.Count + " on screen", 2));
        }
    }

    // Stalls every loopback peer mid-run, to watch the hold-fade-retire path.
    public static void ToggleStall() {
        var stalled = 0;
        foreach (var source in Sources) {
            var loopback = source as LoopbackGhostSource;
            if (loopback != null) {
                loopback.Stalled = !loopback.Stalled;
                stalled += loopback.Stalled ? 1 : 0;
            }
        }

        Randomizer.log("ghost: stalled " + stalled + " of " + Sources.Count);
        Randomizer.showHint(RandomizerUI.Message.InfoMessage("Ghost: stalled " + stalled, 2));
    }

    private static bool Stored() {
        if (Ghost.Count < 2) {
            Load();
        }

        if (Ghost.Count < 2) {
            if (!Recording) {
                Randomizer.showHint(RandomizerUI.Message.InfoMessage("Ghost: none recorded yet", 3));
            }

            return false;
        }

        return true;
    }

    // Adds a ghost for any source; false when Ori is not in the world.
    public static bool AddLive(IGhostSource source) {
        return Add(source);
    }

    // Whether a source still has a view; a peer retired for silence keeps its channel open.
    public static bool Showing(IGhostSource source) {
        return Sources.Contains(source);
    }

    public struct Marker {
        public int PlayerId;
        public Vector3 Position;
        public Color Shade;
        // where their soul link stands; NaN when there is none
        public Vector2 SoulLink;
    }

    // List of peer players we want drawn on map; a culled ghost still has its position
    // updated. Player zero is not a peer.
    public static void Markers(List<Marker> into) {
        into.Clear();
        for (var i = 0; i < Sources.Count; i++) {
            var id = Sources[i].PlayerId;
            if (id < 1) {
                continue;
            }

            into.Add(new Marker {
                PlayerId = id,
                Position = Shown[i].Position,
                Shade = Shade(Sources[i]),
                SoulLink = LinkOf(Sources[i])
            });
        }
    }

    private static Vector2 LinkOf(IGhostSource source) {
        var samples = source.Samples;
        return samples.Count > 0 ? samples[samples.Count - 1].SoulLink : new Vector2(float.NaN, float.NaN);
    }

    // The nearest peer's soul link within reach of a point, for saving at it.
    public static bool AllyLinkNear(Vector3 at, float radius, out Vector3 link, out int player) {
        link = Vector3.zero;
        player = 0;
        var best = radius;
        for (var i = 0; i < Sources.Count; i++) {
            // an echo's link is your own; saving at it would be saving at yours twice
            if (Sources[i].PlayerId < 1 || Kept(Sources[i])) {
                continue;
            }

            var there = LinkOf(Sources[i]);
            if (float.IsNaN(there.x)) {
                continue;
            }

            var distance = Vector2.Distance(new Vector2(at.x, at.y), there);
            if (distance <= best) {
                best = distance;
                link = new Vector3(there.x, there.y, at.z);
                player = Sources[i].PlayerId;
            }
        }

        return player > 0;
    }

    // The live Ori as a Sample, for sending. Identical to what Record stores, minus the
    // recording clock: a packet carries the sender's own time.
    public static bool SampleLive(out Sample sample) {
        return Capture(Time.time, out sample);
    }

    private static void Record() {
        Sample sample;
        if (Capture(Time.time - RecordStart, out sample)) {
            Take.Add(sample);
        }
    }

    internal static bool Capture(float at, out Sample sample) {
        sample = new Sample();
        // the title screen keeps an Ori of its own on show; a peer there is not anywhere
        var game = GameController.Instance;
        if (game != null && game.GameInTitleScreen) {
            return OnTitle(at, out sample);
        }

        TitleSince = -1f;
        var sprite = Downed() ? null : Sprite();
        if (sprite == null) {
            return Dying(at, out sample);
        }

        var animator = sprite.GetComponent<SpriteAnimatorWithTransitions>();
        var clip = animator == null ? null : animator.CurrentTextureAnimationTransitions;
        if (clip != null) {
            Animations[clip.name] = clip;
        }

        var charger = Charger();
        Vector2 bashTarget;
        var bashAngle = BashAim(clip == null ? null : clip.name, out bashTarget);
        sample = new Sample {
            Charge = charger == null ? 0 : (charger.IsCharged ? 2 : (charger.IsCharging ? 1 : 0)),
            BashAngle = bashAngle,
            BashTarget = bashTarget,
            GrenadeAim = Aim(clip == null ? null : clip.name),
            WallAim = WallArrowAim(clip == null ? null : clip.name),
            Triple = OnLastAirJump(),
            SoulLink = SoulLinkAt(),
            InMenu = UI.MainMenuVisible,
            Time = at,
            Position = sprite.position,
            Rotation = sprite.rotation,
            Scale = sprite.lossyScale,
            Animation = clip == null ? "" : clip.name,
            AnimationTime = animator == null ? 0f : animator.CurrentAnimationTime
        };

        Held = sample;
        Have = true;
        return true;
    }

    // Death switches Ori off without destroying it, so Sprite() keeps handing back the corpse.
    private const float DeathHold = 5f;

    private static bool Downed() {
        if (Time.time - DiedAt > DeathHold) {
            return false;
        }

        var sein = Characters.Sein;
        return sein == null || !sein.Active || !sein.gameObject.activeInHierarchy;
    }

    public static void OnDeath(GameObject effect) {
        DiedAt = Time.time;
        if (effect != null) {
            DeathPrefab = effect;
        }
    }

    // I'm dying, Squirtle
    private static bool Dying(float at, out Sample sample) {
        sample = Held;
        if (!Have || Time.time - DiedAt > DeathHold) {
            return false;
        }

        sample.Time = at;
        sample.Died = true;
        sample.Charge = 0;
        sample.Triple = false;
        sample.BashAngle = float.NaN;
        sample.BashTarget = new Vector2(float.NaN, float.NaN);
        sample.WallAim = float.NaN;
        sample.GrenadeAim = new Vector2(float.NaN, float.NaN);
        return true;
    }

    // No Ori on the title screen, so the last pose stands in, flagged, for TitleHold seconds.
    private const float TitleHold = 12f;

    private static float TitleSince = -1f;

    private static bool OnTitle(float at, out Sample sample) {
        sample = Held;
        if (!Have) {
            return false;
        }

        if (TitleSince < 0f) {
            TitleSince = Time.time;
        }

        if (Time.time - TitleSince > TitleHold) {
            return false;
        }

        sample.Time = at;
        sample.OnTitle = true;
        sample.InMenu = false;
        sample.Died = false;
        sample.Charge = 0;
        sample.Triple = false;
        sample.BashAngle = float.NaN;
        sample.BashTarget = new Vector2(float.NaN, float.NaN);
        sample.WallAim = float.NaN;
        sample.GrenadeAim = new Vector2(float.NaN, float.NaN);
        return true;
    }

    // Cached copy of the death effect.
    internal static GameObject DeathEffect() {
        if (DeathPrefab != null) {
            return DeathPrefab;
        }

        try {
            var receiver = Ability<SeinDamageReciever>();
            var provider = receiver == null ? null : receiver.DeathEffectProvider;
            return provider == null ? null : provider.Prefab(new DamageContext(
                new Damage(0f, Vector2.zero, Vector3.zero, DamageType.Enemy, null)));
        } catch (System.Exception) {
            return null;
        }
    }

    // Scans for the bash game only during a bash clip; it sits on the target, so it gives both.
    private static float BashAim(string animation, out Vector2 target) {
        target = new Vector2(float.NaN, float.NaN);
        try {
            return BashAimInner(animation, out target);
        } catch (System.Exception) {
            target = new Vector2(float.NaN, float.NaN);
            return float.NaN;
        }
    }

    private static float BashAimInner(string animation, out Vector2 target) {
        target = new Vector2(float.NaN, float.NaN);
        // the aiming clips only: the bash game outlives the launch in its disappear animation
        if (animation == null || !(animation.StartsWith("bashCharge") || animation.StartsWith("swimBash"))) {
            return float.NaN;
        }

        var game = BashGame();
        if (game == null) {
            return float.NaN;
        }

        target = new Vector2(game.transform.position.x, game.transform.position.y);
        return game.Angle;
    }

    // The grenade's launch velocity while its trajectory is showing, else NaN.
    private static Vector2 Aim(string animation) {
        try {
            return AimInner(animation);
        } catch (System.Exception) {
            return new Vector2(float.NaN, float.NaN);
        }
    }

    private static Vector2 AimInner(string animation) {
        // activeInHierarchy: the trajectory is switched on from a parent, so activeSelf stays set
        var grenade = Grenader();
        if (grenade == null || grenade.Trajectory == null ||
                !grenade.Trajectory.gameObject.activeInHierarchy ||
                animation == null || !animation.StartsWith("grenade")) {
            return new Vector2(float.NaN, float.NaN);
        }

        return grenade.Trajectory.InitialVelocity;
    }

    // ExtraJumpsAvailable is the max; the private counter stays put from the jump until landing.
    private static bool OnLastAirJump() {
        // a skill flag is never worth a throw out of sampling
        try {
            var ability = Jumper();
            if (ability == null || ability.ExtraJumpsAvailable != 2) {
                return false;
            }

            if (JumpsLeft == null) {
                JumpsLeft = typeof(SeinDoubleJump).GetField("m_numberOfJumpsAvailable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            }

            return JumpsLeft != null && (int)JumpsLeft.GetValue(ability) == 0;
        } catch (System.Exception) {
            return false;
        }
    }

    // IsCharged and CanChargeJump can throw here; the arrow's animator gives the same answer.
    private static float WallArrowAim(string animation) {
        try {
            return WallArrowAimInner(animation);
        } catch (System.Exception) {
            return float.NaN;
        }
    }

    private static float WallArrowAimInner(string animation) {

        var wall = Waller();
        if (wall == null || wall.Arrow == null) {
            return float.NaN;
        }

        // CurrentTime is how far the arrow has faded in; IsReversed only gives the direction
        var driver = wall.Arrow.AnimatorDriver;
        if (driver == null || driver.CurrentTime <= 0.01f || !wall.Arrow.gameObject.activeInHierarchy) {
            return float.NaN;
        }

        return wall.Arrow.transform.eulerAngles.z;
    }

    internal static SeinWallChargeJump Waller() {
        if (WallState == null) {
            WallState = Ability<SeinWallChargeJump>();
        }

        return WallState;
    }

    internal static SeinGrenadeAttack Grenader() {
        if (GrenadeState == null) {
            GrenadeState = Ability<SeinGrenadeAttack>();
        }

        return GrenadeState;
    }

    // the placed flame only; a spirit well is a respawn point but not a soul link
    private static Vector2 SoulLinkAt() {
        try {
            var flame = Flamer();
            if (flame == null || !flame.SoulFlameExists) {
                return new Vector2(float.NaN, float.NaN);
            }

            var at = flame.SoulFlamePosition;
            return new Vector2(at.x, at.y);
        } catch (System.Exception) {
            return new Vector2(float.NaN, float.NaN);
        }
    }

    internal static SeinSoulFlame Flamer() {
        if (FlameState == null) {
            FlameState = Ability<SeinSoulFlame>();
        }

        return FlameState;
    }

    internal static SeinChargeJumpCharging Charger() {
        if (ChargeState == null) {
            ChargeState = Ability<SeinChargeJumpCharging>();
        }

        return ChargeState;
    }

    internal static SeinDoubleJump Jumper() {
        if (JumpState == null) {
            JumpState = Ability<SeinDoubleJump>();
        }

        return JumpState;
    }

    internal static SeinStomp Stomper() {
        if (StompState == null) {
            StompState = Ability<SeinStomp>();
        }

        return StompState;
    }

    internal static SeinBashAttack Basher() {
        if (BashState == null) {
            BashState = Ability<SeinBashAttack>();
        }

        return BashState;
    }

    private static BashAttackGame BashGame() {
        if (BashGameFound == null || !BashGameFound.gameObject.activeInHierarchy) {
            BashGameFound = Object.FindObjectOfType<BashAttackGame>();
        }

        return BashGameFound;
    }

    // Abilities are CharacterStates and are off when not found so getting them
    // has to be done by runtime. This search is nontrivial - callers should cache.
    internal static T Ability<T>() where T : MonoBehaviour {
        var all = Resources.FindObjectsOfTypeAll<T>();
        T fallback = null;
        foreach (var found in all) {
            if (found == null) {
                continue;
            }

            if (found.gameObject.scene.IsValid()) {
                return found;
            }

            if (fallback == null) {
                fallback = found;
            }
        }

        return fallback;
    }

    // Every MonoBehaviour but `keep` goes: on a clone they go on reading input and spawning.
    internal static void Strip(GameObject target, string keep) {
        var removed = new List<string>();
        foreach (var behavior in target.GetComponentsInChildren<MonoBehaviour>(true)) {
            if (behavior == null) {
                continue;
            }

            var name = behavior.GetType().Name;
            if (name == keep) {
                continue;
            }

            removed.Add(name);
            Object.Destroy(behavior);
        }

        // once per prefab; zero renderers means the drawing lives outside the cloned object
        if (Cloned.Add(target.name)) {
            Randomizer.log("ghost: cloned " + target.name + " with " +
                target.GetComponentsInChildren<Renderer>(true).Length + " renderers, stripped " +
                (removed.Count == 0 ? "nothing" : string.Join(", ", removed.ToArray())));
        }
    }

    // A cloned effect leaves two things behind: its fader, which would switch off what Paint
    // switched on, and its SoundSource, since ghosts are silent.
    internal static void Hush(GameObject target) {
        foreach (var sound in target.GetComponentsInChildren<SoundSource>(true)) {
            Object.Destroy(sound);
        }

        // an AudioSource is not a MonoBehaviour, so Strip walks straight past one
        foreach (var audio in target.GetComponentsInChildren<AudioSource>(true)) {
            Object.Destroy(audio);
        }
    }

    internal static void Quiet(GameObject target) {
        Hush(target);
        foreach (var fade in target.GetComponentsInChildren<TransparencyAnimator>(true)) {
            // disabled too: Destroy lands at end of frame, and the fader would still run this one
            fade.enabled = false;
            Object.Destroy(fade);
        }
    }

    private static Color Scale(Color color, float factor) {
        return new Color(color.r * factor, color.g * factor, color.b * factor, color.a * factor);
    }

    internal static void Recolor(GameObject target) {
        Paint(target, EffectTint);
    }

    // TransparencyAnimator may drive opacity through any of these, so all of them are painted.
    private static readonly string[] ColorProperties = {
        "_Color", "_TintColor", "_MaskDissolveColor", "_AdditiveLayerColor"
    };

    internal static void Paint(GameObject target, Color color) {
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true)) {
            // a clone taken mid-fade arrives with its renderers switched off
            renderer.enabled = true;
            var material = renderer.material;
            if (material == null) {
                continue;
            }

            foreach (var property in ColorProperties) {
                if (material.HasProperty(property)) {
                    material.SetColor(property, color);
                }
            }
        }
    }

    // Multiplied, so each piece keeps its color; rgb scales too: additive blending ignores alpha.
    internal static void Dim(GameObject target, float factor) {
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true)) {
            var material = renderer.material;
            if (material == null) {
                continue;
            }

            foreach (var property in ColorProperties) {
                if (!material.HasProperty(property)) {
                    continue;
                }

                var color = material.GetColor(property);
                material.SetColor(property, Scale(color, factor));
            }
        }

        foreach (var system in target.GetComponentsInChildren<ParticleSystem>(true)) {
            system.startColor = Scale(system.startColor, factor);
        }
    }

    internal static Transform Sprite() {
        var sein = Characters.Sein;
        if (sein == null || sein.PlatformBehaviour == null || sein.PlatformBehaviour.Visuals == null) {
            return null;
        }

        var sprite = sein.PlatformBehaviour.Visuals.Sprite;
        return sprite == null ? null : sprite.transform;
    }

    // A clip begins on a new name, or when a one-shot rewinds (the triple jump replays doubleJump).
    internal static bool Began(Sample now, Sample prev) {
        if (now.Animation != prev.Animation) {
            return true;
        }

        return now.AnimationTime < prev.AnimationTime - Rewind && !Loops(Resolve(now.Animation));
    }

    internal static bool Loops(TextureAnimationWithTransitions clip) {
        return clip != null && clip.Animation != null && clip.Animation.Loop;
    }

    // Clips seen while capturing, else one sweep of everything loaded (once per session).
    internal static TextureAnimationWithTransitions Resolve(string name) {
        if (Animations.ContainsKey(name)) {
            return Animations[name];
        }

        if (!Swept) {
            Swept = true;
            foreach (var clip in Resources.FindObjectsOfTypeAll<TextureAnimationWithTransitions>()) {
                if (clip != null && !Animations.ContainsKey(clip.name)) {
                    Animations[clip.name] = clip;
                }
            }

            Randomizer.log("ghost: swept " + Animations.Count + " animations");
            if (RandomizerSettings.Dev.Value) {
                DumpTable();
            }
        }

        if (Animations.ContainsKey(name)) {
            return Animations[name];
        }

        // said once: a missing clip idles, which otherwise looks like a bad recording
        if (Missing.Add(name)) {
            Randomizer.log("ghost: no animation named " + name + ", that stretch will idle");
        }

        return null;
    }

    // Input for regenerating RandomizerGhostAnimations; a dev tool, never read back.
    private static void DumpTable() {
        try {
            var names = new List<string>(Animations.Keys);
            names.Sort(System.StringComparer.Ordinal);
            using (var writer = File.CreateText("ghost-animations.txt")) {
                foreach (var name in names) {
                    writer.WriteLine(name);
                }
            }

            Randomizer.log("ghost: wrote ghost-animations.txt, " + names.Count + " names");
        } catch (System.Exception ex) {
            Randomizer.log("ghost: could not dump animations, " + ex.Message);
        }
    }

    internal static float Duration(string name) {
        if (string.IsNullOrEmpty(name)) {
            return 0f;
        }

        var clip = Resolve(name);
        return clip == null || clip.Animation == null ? 0f : clip.Animation.Duration;
    }

    // Ori's own sprite scale, which is the same for every player, so nobody sends it.
    internal static Vector3 GhostScale() {
        var sprite = Sprite();
        return sprite == null ? DefaultScale : sprite.lossyScale;
    }

    // Dev: round-trips a recording through the codec and logs the worst error per lossy field.
    private static void CheckCodec(List<Sample> samples) {
        var buffer = new byte[RandomizerGhostPacket.MaxSize];
        var worstPosition = 0f;
        var worstRotation = 0f;
        var worstClipTime = 0f;
        var wrongNames = 0;
        var wrongLinks = 0;
        var headerFaults = 0;
        var bytes = 0;

        foreach (var sample in samples) {
            var length = RandomizerGhostPacket.Encode(buffer, sample, 200, 40000);
            bytes += length;

            Sample back;
            byte who;
            ushort seq;
            if (!RandomizerGhostPacket.Decode(buffer, length, out back, out who, out seq)) {
                Randomizer.log("ghost codec: a packet would not decode, stopping");
                return;
            }

            if (who != 200 || seq != 40000) {
                headerFaults++;
            }

            worstPosition = Mathf.Max(worstPosition, (back.Position - sample.Position).magnitude);
            worstRotation = Mathf.Max(worstRotation, Quaternion.Angle(back.Rotation, sample.Rotation));
            worstClipTime = Mathf.Max(worstClipTime, Mathf.Abs(back.AnimationTime - sample.AnimationTime));
            if (back.Animation != (sample.Animation ?? "")) {
                wrongNames++;
            }

            if (float.IsNaN(back.SoulLink.x) != float.IsNaN(sample.SoulLink.x) ||
                    (!float.IsNaN(sample.SoulLink.x) && (back.SoulLink - sample.SoulLink).magnitude > 0.001f)) {
                wrongLinks++;
            }
        }

        Randomizer.log("ghost codec: " + samples.Count + " samples, " +
            (bytes / (float)samples.Count).ToString("F1") + " bytes mean; worst position " +
            worstPosition.ToString("F4") + ", worst rotation " + worstRotation.ToString("F2") +
            " deg, worst clip time " + worstClipTime.ToString("F4") + "s; " + wrongNames +
            " names wrong, " + wrongLinks + " links wrong, " + headerFaults + " headers wrong; table " +
            RandomizerGhostAnimations.Names.Length + " clips, hash " +
            RandomizerGhostAnimations.Hash.ToString("X8"));
    }

    internal static float Length(List<Sample> samples) {
        return samples.Count == 0 ? 0f : samples[samples.Count - 1].Time;
    }

    private static string Path() {
        return "ghost.tsv";
    }

    private static void Save() {
        try {
            using (var writer = File.CreateText(Path())) {
                foreach (var sample in Ghost) {
                    writer.WriteLine(string.Join("\t", new[] {
                        F(sample.Time), F(sample.Position.x), F(sample.Position.y), F(sample.Position.z),
                        F(sample.Rotation.x), F(sample.Rotation.y), F(sample.Rotation.z), F(sample.Rotation.w),
                        F(sample.Scale.x), F(sample.Scale.y), F(sample.Scale.z),
                        sample.Animation ?? "", F(sample.AnimationTime),
                        sample.Charge.ToString(), F(sample.BashAngle),
                        F(sample.BashTarget.x), F(sample.BashTarget.y),
                        F(sample.GrenadeAim.x), F(sample.GrenadeAim.y), F(sample.WallAim),
                        sample.Triple ? "1" : "0",
                        F(sample.SoulLink.x), F(sample.SoulLink.y)
                    }));
                }
            }
        } catch (System.Exception ex) {
            Randomizer.log("ghost: could not save, " + ex.Message);
        }
    }

    private static void Load() {
        Ghost = new List<Sample>();
        if (!File.Exists(Path())) {
            return;
        }

        try {
            foreach (var line in File.ReadAllLines(Path())) {
                var parts = line.Split('\t');
                if (parts.Length < 11) {
                    continue;
                }

                // a take from before the bash target was written has the aim two columns earlier
                var aim = parts.Length > 20 ? 17 : 15;
                Ghost.Add(new Sample {
                    Time = P(parts[0]),
                    Position = new Vector3(P(parts[1]), P(parts[2]), P(parts[3])),
                    Rotation = new Quaternion(P(parts[4]), P(parts[5]), P(parts[6]), P(parts[7])),
                    Scale = new Vector3(P(parts[8]), P(parts[9]), P(parts[10])),
                    Animation = parts.Length > 11 ? parts[11] : "",
                    AnimationTime = parts.Length > 12 ? P(parts[12]) : 0f,
                    Charge = parts.Length > 13 ? (int)P(parts[13]) : 0,
                    BashAngle = parts.Length > 14 ? P(parts[14]) : float.NaN,
                    BashTarget = parts.Length > 20
                        ? new Vector2(P(parts[15]), P(parts[16]))
                        : new Vector2(float.NaN, float.NaN),
                    GrenadeAim = parts.Length > aim + 1
                        ? new Vector2(P(parts[aim]), P(parts[aim + 1]))
                        : new Vector2(float.NaN, float.NaN),
                    WallAim = parts.Length > aim + 2 ? P(parts[aim + 2]) : float.NaN,
                    Triple = parts.Length > aim + 3 && parts[aim + 3] == "1",
                    SoulLink = parts.Length > 22
                        ? new Vector2(P(parts[21]), P(parts[22]))
                        : new Vector2(float.NaN, float.NaN)
                });
            }
        } catch (System.Exception ex) {
            Randomizer.log("ghost: could not load, " + ex.Message);
            Ghost = new List<Sample>();
        }
    }

    private static string F(float value) {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static float P(string value) {
        float parsed;
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : 0f;
    }

    public static bool Recording;

    private static readonly List<RandomizerGhostView> Shown = new List<RandomizerGhostView>();

    private static readonly List<IGhostSource> Sources = new List<IGhostSource>();

    // views with no source left, kept only until they have faded out
    private static readonly List<RandomizerGhostView> Leaving = new List<RandomizerGhostView>();

    private static readonly List<Sample> Take = new List<Sample>();

    private static List<Sample> Ghost = new List<Sample>();

    // a real death has to move this before Dying will fire, or every loading screen is one
    private static float DiedAt = -1000f;

    private static readonly HashSet<string> Cloned = new HashSet<string>();

    private static GameObject DeathPrefab;

    private static Sample Held;

    private static bool Have;

    private static float RecordStart;

    private static SeinChargeJumpCharging ChargeState;

    private static SeinDoubleJump JumpState;

    private static SeinStomp StompState;

    private static SeinBashAttack BashState;

    private static BashAttackGame BashGameFound;

    private static SeinSoulFlame FlameState;

    private static SeinGrenadeAttack GrenadeState;

    private static SeinWallChargeJump WallState;

    private static FieldInfo JumpsLeft;

    private static readonly Dictionary<string, TextureAnimationWithTransitions> Animations =
        new Dictionary<string, TextureAnimationWithTransitions>();

    private static bool Swept;

    private static bool Checked;

    private static bool Complained;


    private static readonly Vector3 DefaultScale = new Vector3(3.4f, 3.4f, 1f);

    private static readonly HashSet<string> Missing = new HashSet<string>();

    // how far behind a peer's newest sample to draw it
    internal const float InterpolationDelay = 0.12f;

    // seconds of silence before a peer's ghost starts fading out
    private const float Retire = 5f;

    // one knob for every fade-out, tuned live on the view
    private static float FadeOut { get { return RandomizerGhostView.FadeOut; } }

    // how long a ghost stands under "..." before it is taken down
    private const float TitleRetire = 10f;

    // about two screens. Beyond it a ghost keeps its position but stops drawing and animating.
    private const float CullRadius = 40f;

    // slack on the backwards-time test, so sampling jitter alone never reads as a re-trigger
    internal const float Rewind = 0.001f;

    // a facing flip is 180 degrees; ordinary turns between samples are far smaller
    internal const float FlipAngle = 90f;

    // further than Ori can travel between samples, so only a teleport crosses it
    internal const float WarpDistance = 8f;

    internal const float AimWidth = 0.15f;

    internal const float AimAlpha = 0.5f;

    internal const float WallArrowAlpha = 0.5f;

    internal const float WallArrowScale = 0.85f;

    // Dim scales color with alpha, so this is also the link's brightness
    internal const float LinkAlpha = 0.3125f;

    // how far a link's color goes from the effect tint toward its player's shade
    internal const float LinkTintStrength = 0.8f;

    internal static Color LinkShade(Color shade) {
        var mixed = Color.Lerp(EffectTint, shade, LinkTintStrength);
        return new Color(mixed.r, mixed.g, mixed.b, 1f);
    }

    // a hair behind the world plane, so a link sitting on yours draws under it, not over
    internal const float LinkBehind = 0.1f;

    internal const float StompBurstAlpha = 0.5f;

    // fainter than the other bursts: it fires several times a second
    internal const float JumpBurstAlpha = 0.4f;

    internal const float DeathBurstAlpha = 0.5f;

    // widest a ghost's effect piece may be, in world units; wider pieces are shrunk, not dimmed
    internal const float EffectSpan = 12f;

    internal const float Tick = 1f / 60f;

    // the sprite pivot sits above the ground the stomp is supposed to crack
    internal static readonly Vector3 FeetOffset = new Vector3(0f, -0.5f, 0f);

    internal static readonly Color Tint = new Color(0.55f, 0.8f, 1f, 0.35f);

    // Hues follow the website's player_icons() (map/src/common.js) so ghost and map icon agree.
    private static readonly Color[] Palette = {
        new Color(0.17f, 0.43f, 1.00f, 0.35f),   // 1 blue
        new Color(1.00f, 0.40f, 0.41f, 0.35f),   // 2 red
        new Color(0.37f, 1.00f, 0.49f, 0.35f),   // 3 green
        new Color(0.29f, 0.97f, 1.00f, 0.35f),   // 4 cyan
        new Color(0.95f, 1.00f, 0.40f, 0.35f),   // 5 yellow
        new Color(1.00f, 0.31f, 0.94f, 0.35f),   // 6 magenta
        new Color(1.00f, 0.41f, 0.40f, 0.35f),   // 7 multi-1
        new Color(0.40f, 1.00f, 0.94f, 0.35f),   // 8 multi-2
        new Color(0.40f, 1.00f, 0.76f, 0.35f),   // 9 multi-3
        new Color(0.40f, 0.76f, 1.00f, 0.35f),   // 10 skul
        new Color(1.00f, 0.40f, 0.73f, 0.35f),   // 11 peach
        new Color(1.00f, 0.43f, 0.11f, 0.35f),   // 12 orange
        new Color(0.40f, 0.80f, 1.00f, 0.35f),   // 13 arctic
        new Color(1.00f, 0.44f, 0.07f, 0.35f),   // 14 paum
        new Color(1.00f, 0.83f, 0.00f, 0.35f)    // 15 pika
    };

    // effects take the color but keep their own alpha; dimming a faint effect erases it
    internal static readonly Color EffectTint = new Color(0.55f, 0.8f, 1f, 1f);

    // the aura is a big bloom and reads far stronger than the rest of the ghost
    internal const float AuraAlpha = 0.35f;

    // both repaint the sprite every frame over the tint; the animator stays
    internal static readonly HashSet<string> Detach = new HashSet<string> {
        "EnvironmentTintModifier", "LegacyColorFlashAnimator"
    };
}
