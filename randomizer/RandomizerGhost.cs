using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using System.IO;
using Core;
using Game;
using UnityEngine;

// Translucent replays, to race against or to watch other players by. Samples carry their own
// timestamp so a ghost recorded at one framerate plays back correctly at another, and they
// store the sprite's transform rather than Ori's state: facing, roll and bash spin come along
// for free. This half owns recording and the shared lookups; RandomizerGhostView draws one.
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
        // where the arrow belongs, which is the thing being bashed rather than Ori
        public Vector2 BashTarget;
        public Vector2 GrenadeAim;
        public float WallAim;
        public bool Triple;
        public bool Died;
        // where their soul link stands; NaN when there is none
        public Vector2 SoulLink;
    }

    public static void Update() {
        if (Recording) {
            Record();
        }

        // a throw out of Update goes to Player.log and silently stops everything below it;
        // say so once in randomizer.log instead
        try {
            RandomizerGhostNet.Update();
            RandomizerGhostSignal.Update();
            Drive();
        } catch (System.Exception e) {
            if (!Complained) {
                Complained = true;
                Randomizer.log("ghost: update threw, everything after it is dead this frame -- " + e);
            }
        }
    }

    // Every ghost on screen, whatever is feeding it. Walked backwards so a source that finishes
    // can be dropped without disturbing the ones after it.
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
            if (source.Done || !view.Alive || silence > Retire + FadeTime) {
                view.Despawn();
                Shown.RemoveAt(i);
                Sources.RemoveAt(i);
                continue;
            }

            // a stalled peer holds its pose (the cursor runs out of samples): full opacity
            // while it might come back, then a fade, then gone
            view.Tick(source);
            view.Fade(silence <= Retire ? 1f : 1f - (silence - Retire) / FadeTime);
            view.Cull(here != null &&
                (view.Position - here.position).sqrMagnitude > CullRadius * CullRadius);
        }
    }

    // Keyed on the player, never on arrival order: the same player has to be the same colour on
    // everyone's screen, and arrival order differs per client. Player zero is your own replay.
    private static Color Shade(IGhostSource source) {
        var id = source.PlayerId;
        return id < 1 ? Tint : Palette[(id - 1) % Palette.Length];
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

        Shown[i].Despawn();
        Shown.RemoveAt(i);
        Sources.RemoveAt(i);
    }

    private static void Clear() {
        foreach (var view in Shown) {
            view.Despawn();
        }

        Shown.Clear();
        Sources.Clear();
    }

    public static bool Playing { get { return Shown.Count > 0; } }

    // Racing is one press: the new run starts recording and the old one starts running
    // beside it. Recording alone is what you get the first time, with no ghost stored yet.
    public static void ToggleRace() {
        var starting = !Recording;
        ToggleRecording();
        if (starting && Recording && !Playing) {
            TogglePlayback();
        } else if (!starting && Playing) {
            Clear();
        }
    }

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

    // Adds a ghost fed the way a networked peer's would be, to exercise the multi-ghost path
    // without a transport. Stacks: press it again for another.
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

    // Cuts every loopback peer off mid-run, to watch the hold-fade-retire path happen. The one
    // failure that matters most and the one hardest to produce on purpose.
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

    // Adds a ghost driven by a peer. Same render path as everything else -- the source is the
    // only thing that differs, which is what the refactor was for.
    public static bool AddLive(IGhostSource source) {
        return Add(source);
    }

    // A peer retired for silence is dropped from here but its data channel is still open, so
    // the transport has to be able to ask whether the ghost it fed is still on screen.
    public static bool Showing(IGhostSource source) {
        return Sources.Contains(source);
    }

    public struct Marker {
        public int PlayerId;
        public Vector3 Position;
        public Color Shade;
    }

    // Every other player worth drawing on the map; a culled ghost still has its position
    // updated. Player zero -- your own replay -- is not another player.
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
                Shade = Shade(Sources[i])
            });
        }
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

    // Death switches Ori off without destroying it, so Sprite() keeps handing back a corpse;
    // ask whether Ori is on its feet, not whether it exists.
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

    // What Ori's own death spawns, kept from the last local death; before one has happened,
    // the provider is asked the same way with an ordinary enemy death.
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

    // Only worth a scene scan while a bash animation is up. The object sits on the bash target,
    // so it carries the position; BashAttackGame is internal, so both come back as plain values.
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
        // the aiming clips only. The bash game hangs around through its own disappear
        // animation, so its mere existence keeps the arrow up well past the launch.
        if (animation == null || !(animation.StartsWith("bashCharge") || animation.StartsWith("swimBash"))) {
            return float.NaN;
        }

        var game = Object.FindObjectOfType<BashAttackGame>();
        if (game == null) {
            return float.NaN;
        }

        target = new Vector2(game.transform.position.x, game.transform.position.y);
        return game.Angle;
    }

    // The trajectory object is shown by being switched on, so its own activeSelf is the
    // whole question of whether Ori is aiming.
    private static Vector2 Aim(string animation) {
        try {
            return AimInner(animation);
        } catch (System.Exception) {
            return new Vector2(float.NaN, float.NaN);
        }
    }

    private static Vector2 AimInner(string animation) {
        // activeInHierarchy, not activeSelf: the trajectory is switched on from above it, so its
        // own flag stays set once anything has shown it. Every grenade animation is named grenade*.
        var grenade = Grenader();
        if (grenade == null || grenade.Trajectory == null ||
                !grenade.Trajectory.gameObject.activeInHierarchy ||
                animation == null || !animation.StartsWith("grenade")) {
            return new Vector2(float.NaN, float.NaN);
        }

        return grenade.Trajectory.InitialVelocity;
    }

    // The counter the game tests is private and stays where the jump left it until Ori lands,
    // so any time in the clip gives the same answer. ExtraJumpsAvailable is the max, not the rest.
    private static bool OnLastAirJump() {
        // FindObjectsOfTypeAll hands back prefabs and half-wired instances too; a skill flag
        // is never worth throwing over, sampling has to survive anywhere in the game
        try {
            var ability = Ability<SeinDoubleJump>();
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

    // IsCharged and CanChargeJump throw here (Sein.Abilities is half empty); the arrow's
    // animator says the same thing, running backwards when the aim goes away.
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

        // CurrentTime is how far the arrow has faded in, zero until the aim starts; IsReversed
        // is false from boot and only says which way it is heading
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

    // Abilities are CharacterStates and are off most of the time, so only FindObjectsOfTypeAll
    // finds them -- and it also returns prefab assets, in no order. A prefab has no scene.
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

    // A cloned effect brings its object's whole cast of components, and any of them that reads
    // input or spawns things will happily go on doing so for a ghost. Only `keep` survives.
    internal static void Strip(GameObject target, string keep) {
        var removed = new List<string>();
        foreach (var behaviour in target.GetComponentsInChildren<MonoBehaviour>(true)) {
            if (behaviour == null) {
                continue;
            }

            var name = behaviour.GetType().Name;
            if (name == keep) {
                continue;
            }

            removed.Add(name);
            Object.Destroy(behaviour);
        }

        // a clone whose drawing lives outside the cloned object comes out empty: the renderer
        // count is the tell, said once per prefab
        if (Cloned.Add(target.name)) {
            Randomizer.log("ghost: cloned " + target.name + " with " +
                target.GetComponentsInChildren<Renderer>(true).Length + " renderers, stripped " +
                (removed.Count == 0 ? "nothing" : string.Join(", ", removed.ToArray())));
        }
    }

    // A cloned effect leaves two things behind: its fader, which would switch off what Paint
    // switched on, and its SoundSource, since a ghost is silent.
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
            // disabled as well as destroyed: Destroy lands at the end of the frame, and until
            // then the component still gets its turn and switches the renderers back off
            fade.enabled = false;
            Object.Destroy(fade);
        }
    }

    private static Color Scale(Color colour, float factor) {
        return new Color(colour.r * factor, colour.g * factor, colour.b * factor, colour.a * factor);
    }

    internal static void Recolour(GameObject target) {
        Paint(target, EffectTint);
    }

    // TransparencyAnimator drives opacity through whichever of these its Mode selects, so a
    // clone repainted on _Color alone can still be sitting at whatever alpha its fader left it.
    private static readonly string[] ColourProperties = {
        "_Color", "_TintColor", "_MaskDissolveColor", "_AdditiveLayerColor"
    };

    internal static void Paint(GameObject target, Color colour) {
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true)) {
            // TransparencyAnimator switches renderers off when it fades out, so a clone taken
            // while hidden arrives dark and nothing turns it back on once the fader is gone
            renderer.enabled = true;
            var material = renderer.material;
            if (material == null) {
                continue;
            }

            foreach (var property in ColourProperties) {
                if (material.HasProperty(property)) {
                    material.SetColor(property, colour);
                }
            }
        }
    }

    // Multiplied rather than replaced: these effects are a dozen pieces with their own colours.
    // Colour scales along with alpha because additive blending never consults the alpha channel.
    internal static void Dim(GameObject target, float factor) {
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true)) {
            var material = renderer.material;
            if (material == null) {
                continue;
            }

            foreach (var property in ColourProperties) {
                if (!material.HasProperty(property)) {
                    continue;
                }

                var colour = material.GetColor(property);
                material.SetColor(property, Scale(colour, factor));
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

    // A clip begins by replacing another or by re-triggering under its own name -- the triple
    // jump replays doubleJump. Only one-shot clips re-trigger; a loop's time wraps every cycle.
    internal static bool Began(Sample now, Sample prev) {
        if (now.Animation != prev.Animation) {
            return true;
        }

        return now.AnimationTime < prev.AnimationTime - Rewind && !Loops(Resolve(now.Animation));
    }

    internal static bool Loops(TextureAnimationWithTransitions clip) {
        return clip != null && clip.Animation != null && clip.Animation.Loop;
    }

    // Recording keeps a reference to every clip it sees, which covers a ghost replayed in the
    // session that made it. One off a file needs the whole loaded set, swept once.
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

        // a ghost that cannot find its clips still moves, it just idles the whole way, which
        // looks like a bug in the recording rather than a missing animation
        if (Missing.Add(name)) {
            Randomizer.log("ghost: no animation named " + name + ", that stretch will idle");
        }

        return null;
    }

    // Regenerates the wire table's input. Which clips a sweep finds depends on what the game has
    // loaded, so this is a dev tool run deliberately, not something the packet layer consults.
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

    // Ori's own sprite scale, which is the same for every player, so nobody sends it. A mirror
    // lives in the rotation rather than in a sign here -- lossyScale cannot report one.
    internal static Vector3 GhostScale() {
        var sprite = Sprite();
        return sprite == null ? DefaultScale : sprite.lossyScale;
    }

    // With Dev on, the first ghost of a session round-trips its recording through the packet
    // codec and reports the worst error each lossy field introduced.
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

    // how far behind a peer's newest sample to draw it, so there is always something to
    // interpolate towards rather than past
    internal const float InterpolationDelay = 0.12f;

    // a peer this quiet is not coming back; short enough that a dead ghost does not loiter,
    // long enough to ride out a bad stretch of connection
    private const float Retire = 5f;

    private const float FadeTime = 1f;

    // about two screens. Beyond it a ghost keeps its position but stops drawing and animating.
    private const float CullRadius = 40f;

    // slack on the backwards-time test, so sampling jitter alone never reads as a re-trigger
    internal const float Rewind = 0.001f;

    // ordinary rotation peaks around 15 degrees between samples; a facing flip is 180
    internal const float FlipAngle = 90f;

    // further than Ori can travel between samples, so only a teleport crosses it
    internal const float WarpDistance = 8f;

    // the game's own line is thinner than a clone of it looked; tune here
    internal const float AimWidth = 0.15f;

    // thin but strong reads louder than the real one, which is drawn faint
    internal const float AimAlpha = 0.5f;

    internal const float WallArrowAlpha = 0.5f;

    internal const float WallArrowScale = 0.85f;

    internal const float LinkAlpha = 0.5f;

    internal const float StompBurstAlpha = 0.5f;

    // The jump burst fires several times a second where the others fire once, so it carries the
    // room and wants to sit further back than they do.
    internal const float JumpBurstAlpha = 0.4f;

    // A death is the loudest thing Ori's effects do, and a ghost dying is not the player's
    // emergency: it wants to read as something that happened, not something happening to you.
    internal const float DeathBurstAlpha = 0.5f;

    // How wide a ghost's effect may be, in world units: the camera sees about twenty and the
    // death glows are authored at up to 150; dimming cannot help, their animators rewrite colour.
    internal const float EffectSpan = 12f;

    internal const float Tick = 1f / 60f;

    // the sprite pivot sits above the ground the stomp is supposed to crack
    internal static readonly Vector3 FeetOffset = new Vector3(0f, -0.5f, 0f);

    internal static readonly Color Tint = new Color(0.55f, 0.8f, 1f, 0.35f);

    // Hues are the website's, from player_icons() in map/src/common.js, so a ghost and its map
    // icon agree; saturation and value are ours. Past six players the hues start colliding.
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

    // effects take the colour but keep their own alpha; dimming a faint effect erases it
    internal static readonly Color EffectTint = new Color(0.55f, 0.8f, 1f, 1f);

    // the aura is a big bloom and reads far stronger than the rest of the ghost
    internal const float AuraAlpha = 0.35f;

    // both repaint the sprite every frame, which is the tint's whole problem. The animator
    // stays: it is what keeps the ghost running rather than sliding along frozen.
    internal static readonly HashSet<string> Detach = new HashSet<string> {
        "EnvironmentTintModifier", "LegacyColorFlashAnimator"
    };
}
