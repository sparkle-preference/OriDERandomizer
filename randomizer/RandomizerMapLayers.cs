using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game;
using UnityEngine;
using Object = UnityEngine.Object;

// Map pictures vanilla never drew, on the area map in world space like the icons: the Misty layer
// its world event has loaded, and the Forlorn and Horu escapes, which are copies set apart.
public static class RandomizerMapLayers {
    private const float FadeSeconds = 0.4f;

    // vanilla's Misty texture draws restored Misty, so it's hidden while any layer shows
    private static readonly Sheet[] Sheets = {
        new Sheet("mistyWoodsAreaMap", "misty", 1655, MistyLayer, MistyState, MistyHolds, true),
        new Sheet("forlornRuinsAreaMap", "forlorn_escape", 1663, () => 0, () => 0, need => true, false),
        new Sheet("horuAreaMap", "horu_escape", 1671, () => 0, () => 0, need => true, false)
    };

    // a sheet's reveal cells are bits in this many inventory ids, from the id it is given
    private const int CellIds = 8;

    // Misty states that share another's picture, having the same scenes loaded; 13 and 14 never occur
    private static readonly Dictionary<int, int> MistyAlias = new Dictionary<int, int> { { 10, 1 }, { 7, 6 } };

    // the Misty states a save has been in and the marks it has earned, a bit each, which the rules of the layers file ask after
    private static readonly RandomizerBitfield mistySeen = new RandomizerBitfield(1679, 1);

    // bits below this are states; the layers file numbers its marks from here
    private const int MistyStates = 16;

    // a place in the Misty cycle picked on the open map instead of the current state's; closing the map forgets it
    private static int mistyPreview = -1;

    // whether the map was open at its last update
    private static bool mapOpen;

    // one picture is decoded a frame, so opening the map doesn't stall on all of them at once
    private static int builtFrame = -1;

    // Steps the map to the next place in the Misty cycle, round to the current state's again; true if it changed.
    public static bool Cycle() {
        var current = MistyCurrent();
        var places = Sheets[0].Offered(current);
        if (places.Count == 0) {
            return false;
        }

        var home = Sheets[0].Place(current);
        var at = places.IndexOf(MistyPlace());
        // past the last place the cycle comes home, which is the first place when home is among them
        var next = at + 1 < places.Count ? places[at + 1] : places.Contains(home) ? places[0] : home;
        mistyPreview = next == home ? -1 : next;
        return true;
    }

    public static void Closed() {
        mistyPreview = -1;
        if (!mapOpen) {
            return;
        }

        mapOpen = false;
        foreach (var sheet in Sheets) {
            sheet.Closed();
        }
    }

    // the map's line for the Misty layer, while Misty is the area in view
    public static string MistyLayerText {
        get {
            var area = GameMapUI.Instance == null ? null : GameMapUI.Instance.CurrentHighlightedArea;
            if (area == null || area.Area == null || area.Area.name != "mistyWoodsAreaMap" || !Sheets[0].Listed()) {
                return null;
            }

            var place = MistyPlace();
            var label = Sheets[0].Label(place);
            if (place == Sheets[0].Place(MistyCurrent())) {
                label = label == null ? "Current" : $"{label} (Current)";
            }

            var onPad = PlayerInput.Instance != null && !PlayerInput.Instance.WasKeyboardUsedLast;
            return $"Misty Layout ({RandomizerRebinding.CycleMapLayer.BindNameFor(onPad)}): *{label}*";
        }
    }

    // doors and breakables off the Misty layer shown aren't drawn; pickups stay, their glow put out by StyleIcon
    public static bool Hides(WorldMapIconType type, Vector2 position, MoonGuid guid) {
        return HiddenOffLayer.Contains(type) && Sheets[0].OnLayer(guid, position) == false;
    }

    // the Misty layers a pickup is on, by name, for its tooltip, when the layer shown isn't one of them
    public static string LayerNote(MoonGuid guid, Vector2 position) {
        var misty = Sheets[0];
        if (misty.OnLayer(guid, position) != false) {
            return null;
        }

        var current = MistyCurrent();
        var on = new List<string>();
        foreach (var place in misty.Offered(current)) {
            var picture = misty.Picture(place, current);
            if (misty.OnLayer(guid, position, picture) == true && !misty.Unnamed(guid, picture)) {
                on.Add(misty.Label(place));
            }
        }

        return on.Count == 0 ? null : string.Join(", ", on.ToArray());
    }

    // putting out the glow barely dims these, so off the layer they also fade
    private static readonly HashSet<RandomizerWorldMapIconType> FadedOffLayer = new HashSet<RandomizerWorldMapIconType> {
        RandomizerWorldMapIconType.GumonSeal, RandomizerWorldMapIconType.Plant, RandomizerWorldMapIconType.Keystone, RandomizerWorldMapIconType.SkillTree
    };

    private const float OffLayerShare = 0.6f;

    // the seal's bright shards still read clearly at the others' share
    private const float SealOffLayerShare = 0.35f;

    private static readonly HashSet<WorldMapIconType> HiddenOffLayer = new HashSet<WorldMapIconType> {
        WorldMapIconType.KeystoneDoorFour, WorldMapIconType.KeystoneDoorTwo, WorldMapIconType.KeystoneDoorOpen,
        WorldMapIconType.StompableFloor, WorldMapIconType.StompableFloorBroken,
        WorldMapIconType.BreakableWall, WorldMapIconType.BreakableWallBroken
    };

    // Puts out the glow of a map icon in Misty that's off the layer shown, and fades the icons that are mostly sprite.
    public static void StyleIcon(GameObject icon, Vector2 position, WorldMapIconType type, RandomizerWorldMapIconType randoType, MoonGuid guid) {
        if (icon == null) {
            return;
        }

        // a breakable's icon goes in the gap the layer leaves for it; the game's own spot can be a unit or two off
        if (type == WorldMapIconType.StompableFloor || type == WorldMapIconType.StompableFloorBroken
                || type == WorldMapIconType.BreakableWall || type == WorldMapIconType.BreakableWallBroken) {
            foreach (var sheet in Sheets) {
                var gap = sheet.GapNear(position);
                if (gap.HasValue) {
                    icon.transform.localPosition = new Vector3(gap.Value.x, gap.Value.y, icon.transform.localPosition.z);
                    break;
                }
            }
        }

        var off = Sheets[0].OnLayer(guid, position) == false;
        var share = !off ? 1f
            : randoType == RandomizerWorldMapIconType.GumonSeal ? SealOffLayerShare
            : FadedOffLayer.Contains(randoType) || type == WorldMapIconType.Keystone || type == WorldMapIconType.AbilityPedestal ? OffLayerShare
            : 1f;
        foreach (var renderer in icon.GetComponentsInChildren<Renderer>(true)) {
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(ShaderProperties.Color)) {
                TransparencyAnimator.SetShare(renderer, share);
            }

            if (renderer.transform == icon.transform || renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(ShaderProperties.MainTexture)) {
                continue;
            }

            Texture glow;
            if (!glows.TryGetValue(renderer, out glow)) {
                if (!off) {
                    continue;
                }

                // a glow is a child quad drawing one of the game's glow textures
                glow = renderer.sharedMaterial.GetTexture(ShaderProperties.MainTexture);
                if (glow != null && glow.name.IndexOf("glow", StringComparison.OrdinalIgnoreCase) < 0) {
                    glow = null;
                }

                glows[renderer] = glow;
            }

            // by its texture: the map's fade animator rewrites the color's alpha and switches renderers back on
            if (glow != null) {
                renderer.material.SetTexture(ShaderProperties.MainTexture, off ? Clear() : glow);
            }
        }
    }

    public static void Update(AreaMapUI map) {
        var opacity = map.FadeOutAnimator == null ? 1f : map.FadeOutAnimator.FinalOpacity;
        var step = Time.fixedDeltaTime / FadeSeconds;
        var opened = !mapOpen;
        mapOpen = true;
        foreach (var sheet in Sheets) {
            sheet.Update(map, opacity, step, opened);
        }
    }

    // a cell turns from gray to color once Ori has stood in it, as the vanilla map's faces do
    public static void Tick() {
        RandomizerMapAssets.Start();
        // the escapes show one picture in every save, so the title screen can hide its decoding
        var state = GameStateMachine.Instance;
        if (RandomizerPerf.On && state != null && state.CurrentState <= GameStateMachine.State.TitleScreen && !Sheets[1].Warm()) {
            Sheets[2].Warm();
        }

        if (Characters.Sein == null || Randomizer.Inventory == null || Core.Scenes.Manager == null || Core.Scenes.Manager.CurrentScene == null) {
            return;
        }

        var scene = Core.Scenes.Manager.CurrentScene.Scene;
        foreach (var sheet in Sheets) {
            sheet.Visit(scene, Characters.Sein.Position);
        }

        // the Misty state marks itself as one the save has been in, and 10 with it from 11 or 12, which only 10 leads to
        var misty = MistyValue();
        if (misty >= 0 && misty < MistyStates) {
            mistySeen.Set(misty);
            if (misty == 11 || misty == 12) {
                mistySeen.Set(10);
            }

            Sheets[0].Mark(misty, Characters.Sein.Position, mistySeen);
        }
    }

    // the picture the map draws for Misty: the picked place's, or the current state's
    private static int MistyLayer() {
        return Sheets[0].Picture(MistyPlace(), MistyCurrent());
    }

    // the place in the cycle the map shows: the picked one while the files still list it, else the current state's
    private static int MistyPlace() {
        return mistyPreview >= 0 && Sheets[0].Lists(mistyPreview) ? mistyPreview : Sheets[0].Place(MistyCurrent());
    }

    // the picture of the Misty state Ori is in
    private static int MistyCurrent() {
        return Sheets[0].Resolve(MistyState());
    }

    // the layer of the Misty state Ori is in, which its reveal cells are read from
    private static int MistyState() {
        var state = MistyValue();
        int shared;
        return MistyAlias.TryGetValue(state, out shared) ? shared : state;
    }

    private static int MistyValue() {
        // MistySim is made with new, which Unity's == calls null
        return ReferenceEquals(Randomizer.MistySim, null) ? -1 : World.Events.Find(Randomizer.MistySim).Value;
    }

    // whether a need of a rule holds: a state the save has been in, the one it is in counted, a mark it has earned, or below zero a pickup it has taken
    private static bool MistyHolds(int need) {
        if (need < 0) {
            return Characters.Sein != null && Randomizer.HaveCoord(need);
        }

        return (need < MistyStates && need == MistyValue()) || (Randomizer.Inventory != null && need < mistySeen.Size && mistySeen.Get(need));
    }

    private static Texture2D Clear() {
        if (clear == null) {
            clear = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            clear.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
            clear.Apply();
        }

        return clear;
    }

    // a box blur in place, three times over, about the softness of vanilla's own mask
    private static void Soften(float[] v, int w, int h, int radius) {
        var tmp = new float[v.Length];
        for (var pass = 0; pass < 3; pass++) {
            for (var y = 0; y < h; y++) {
                for (var x = 0; x < w; x++) {
                    float sum = 0;
                    var n = 0;
                    for (var k = Math.Max(0, x - radius); k <= Math.Min(w - 1, x + radius); k++, n++) {
                        sum += v[y * w + k];
                    }

                    tmp[y * w + x] = sum / n;
                }
            }

            for (var y = 0; y < h; y++) {
                for (var x = 0; x < w; x++) {
                    float sum = 0;
                    var n = 0;
                    for (var k = Math.Max(0, y - radius); k <= Math.Min(h - 1, y + radius); k++, n++) {
                        sum += tmp[k * w + x];
                    }

                    v[y * w + x] = sum / n;
                }
            }
        }
    }

    private class Sheet {
        public Sheet(string area, string prefix, int id, Func<int> layerOf, Func<int> stateOf, Func<int, bool> holds, bool hidesVanilla) {
            this.area = area;
            this.prefix = prefix;
            this.id = id;
            this.layerOf = layerOf;
            this.stateOf = stateOf;
            this.holds = holds;
            this.hidesVanilla = hidesVanilla;
            visited = new RandomizerBitfield(id, CellIds);
        }

        // the places the cycle steps through, in the file's order: a late one once its needs hold, a gone one until they do, the current picture's always
        public List<int> Offered(int current) {
            var offered = new List<int>();
            if (!Load()) {
                return offered;
            }

            var home = Place(current);
            foreach (var place in order) {
                int[] needs;
                var early = late.TryGetValue(place, out needs) && !All(needs);
                var left = dropped.TryGetValue(place, out needs) && All(needs);
                if (place == home || (!early && !left)) {
                    offered.Add(place);
                }
            }

            return offered;
        }

        private bool All(int[] needs) {
            return Array.TrueForAll(needs, need => holds(need));
        }

        // the place a picture is drawn in: its own, or the one it stands in
        public int Place(int picture) {
            int place;
            return Load() && places.TryGetValue(picture, out place) ? place : picture;
        }

        // the picture drawn in a place: the current one where that belongs, else a stand-in whose needs hold, else the place's own; whole if it may be
        public int Picture(int place, int current) {
            if (!Load() || Place(current) == place) {
                return Whole(current);
            }

            List<Stand> ins;
            if (stands.TryGetValue(place, out ins)) {
                foreach (var stand in ins) {
                    First first;
                    // a stand-in that is a state drawn at first with this place's layer waits for that to end as well
                    var waits = firsts.TryGetValue(stand.Picture, out first) && first.Layer == place && !Ended(first);
                    if (Draws(stand.Picture) && All(stand.Needs) && !waits) {
                        return Whole(stand.Picture);
                    }
                }
            }

            // a place whose own picture can't be drawn gives way to the state drawn at first with it
            foreach (var pair in firsts) {
                if (pair.Value.Layer == place && Place(pair.Key) == place && Draws(pair.Key) && !Draws(place)) {
                    return Whole(pair.Key);
                }
            }

            return Whole(place);
        }

        // a layer's whole picture once that one's needs hold, else the layer
        private int Whole(int layer) {
            Stand whole;
            return wholes.TryGetValue(layer, out whole) && Draws(whole.Picture) && All(whole.Needs) ? whole.Picture : layer;
        }

        // gives the save each mark whose rect Ori stands in, in the mark's state
        public void Mark(int state, Vector2 at, RandomizerBitfield seen) {
            if (!Load()) {
                return;
            }

            foreach (var mark in marks) {
                if (mark.State == state && mark.Need < seen.Size && mark.Rect.Contains(at)) {
                    seen.Set(mark.Need);
                }
            }
        }

        // whether the layers file is here and lists any layer
        public bool Listed() {
            return Load();
        }

        // whether a layer has a place of its own in the file's order
        public bool Lists(int place) {
            return Load() && order.Contains(place);
        }

        // the number of a picture's place in the file's order, from 1, which no progress changes; 0 for one the file doesn't list
        public int Number(int layer) {
            return Load() ? order.IndexOf(Place(layer)) + 1 : 0;
        }

        // the name of a picture's place, or its number where the file gives none; null for one the file doesn't list
        public string Label(int layer) {
            var number = Number(layer);
            string name;
            return number == 0 ? null : names.TryGetValue(Place(layer), out name) ? name : number.ToString();
        }

        // the middle of the breakable nearest a point, within a few units
        public Vector2? GapNear(Vector2 at) {
            Vector2? best = null;
            var bestDistance = 4f;
            if (!Load()) {
                return null;
            }

            foreach (var gap in gaps) {
                var distance = Vector2.Distance(gap, at);
                if (distance < bestDistance) {
                    bestDistance = distance;
                    best = gap;
                }
            }

            return best;
        }

        // inside any of the sheet's scenes
        public bool Covers(Vector2 at) {
            if (!Load()) {
                return false;
            }

            foreach (var bounds in sceneRects.Values) {
                if (bounds.Contains(at)) {
                    return true;
                }
            }

            return false;
        }

        // on the layer shown when the icon's scene loads there, the file keeps it to no other layers and the layer is open under it; null with nothing to ask
        public bool? OnLayer(MoonGuid guid, Vector2 at) {
            return OnLayer(guid, at, layerOf());
        }

        public bool? OnLayer(MoonGuid guid, Vector2 at, int layer) {
            string scene;
            if (!Load() || !homes.TryGetValue(guid, out scene)) {
                return Covers(at) ? Shows(at, layer) : null;
            }

            string[] scenes;
            if (!drawn.TryGetValue(layer, out scenes)) {
                return null;
            }

            int[] layers;
            if (only.TryGetValue(guid, out layers) && Array.IndexOf(layers, layer) < 0) {
                return false;
            }

            return Array.IndexOf(scenes, scene) >= 0 && Shows(at, layer) != false;
        }

        // a layer the file leaves out of the icon's tooltip, though the icon is on it
        public bool Unnamed(MoonGuid guid, int layer) {
            int[] layers;
            return Load() && unnamed.TryGetValue(guid, out layers) && Array.IndexOf(layers, layer) >= 0;
        }

        // on the open space of a layer, give or take a couple of units; null with no picture to ask
        public bool? Shows(Vector2 at, int layer) {
            if (!Load() || !rects.ContainsKey(layer)) {
                return null;
            }

            var grid = Open(layer);
            if (grid == null) {
                return null;
            }

            var rect = rects[layer];
            var cx = Mathf.FloorToInt(at.x - rect.xMin);
            var cy = Mathf.FloorToInt(at.y - rect.yMin);
            for (var y = Math.Max(0, cy - 2); y <= Math.Min(grid.Height - 1, cy + 2); y++) {
                for (var x = Math.Max(0, cx - 2); x <= Math.Min(grid.Width - 1, cx + 2); x++) {
                    if (grid.Drawn[y * grid.Width + x]) {
                        return true;
                    }
                }
            }

            return false;
        }

        // where a layer's picture is drawn, a cell a unit, read once; null with no file to read
        private Grid Open(int layer) {
            Grid grid;
            if (opens.TryGetValue(layer, out grid)) {
                return grid;
            }

            var bytes = RandomizerMapAssets.Read($"{prefix}_{layer:00}_open.png");
            if (bytes != null) {
                var picture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                picture.LoadImage(bytes);
                var pixels = picture.GetPixels32();
                var drawn = new bool[pixels.Length];
                for (var i = 0; i < pixels.Length; i++) {
                    drawn[i] = pixels[i].r > 127;
                }

                grid = new Grid { Width = picture.width, Height = picture.height, Drawn = drawn };
                Object.Destroy(picture);
            }

            opens[layer] = grid;
            return grid;
        }

        // marks the cell Ori stands in, read from the picture of the state he is in, whatever layer the map shows
        public void Visit(string scene, Vector2 at) {
            if (!Load() || !sceneRects.ContainsKey(scene)) {
                return;
            }

            var cell = CellAt(Cells(Whole(stateOf())), at);
            if (cell > 0 && cell <= visited.Size) {
                visited.Set(cell - 1);
            }
        }

        // the reveal cell at a world point of a layer's cell picture, 0 for none
        private static int CellAt(CellMap map, Vector2 at) {
            if (map == null || !map.Rect.Contains(at)) {
                return 0;
            }

            var x = Math.Min(map.Width - 1, (int)((at.x - map.Rect.xMin) * map.Width / map.Rect.width));
            var y = Math.Min(map.Height - 1, (int)((at.y - map.Rect.yMin) * map.Height / map.Rect.height));
            return map.Ids[y * map.Width + x];
        }

        // a layer's cell picture, read once; null while it hasn't been fetched
        private CellMap Cells(int layer) {
            CellMap map;
            if (cells.TryGetValue(layer, out map)) {
                return map;
            }

            Rect rect;
            var bytes = cellRects.TryGetValue(layer, out rect) ? RandomizerMapAssets.Read($"{prefix}_{layer:00}_cells.png") : null;
            if (bytes != null) {
                var picture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                picture.LoadImage(bytes);
                var pixels = picture.GetPixels32();
                var ids = new ushort[pixels.Length];
                for (var i = 0; i < pixels.Length; i++) {
                    ids[i] = (ushort)(pixels[i].r | (pixels[i].g << 8));
                }

                map = new CellMap { Rect = rect, Width = picture.width, Height = picture.height, Ids = ids };
                Object.Destroy(picture);
            }

            cells[layer] = map;
            return map;
        }

        public void Update(AreaMapUI map, float opacity, float step, bool opened) {
            var canvas = Canvas(map);
            if (canvas == null) {
                return;
            }

            var current = Load() ? layerOf() : -1;
            if (!rects.ContainsKey(current)) {
                current = -1;
            }

            // a map just opened shows its layer at once: nothing fades over from the last time it was open
            if (opened) {
                gone.Clear();
                foreach (var pair in shown) {
                    if (pair.Key == current) {
                        pair.Value.Weight = 1f;
                    } else {
                        gone.Add(pair.Key);
                    }
                }

                foreach (var key in gone) {
                    shown[key].Destroy();
                    shown.Remove(key);
                }
            }

            if (current >= 0 && !shown.ContainsKey(current) && !failed.Contains(current) && builtFrame != Time.frameCount) {
                builtFrame = Time.frameCount;
                var made = Build(current, map, canvas);
                if (made == null) {
                    failed.Add(current);
                    // the state may now be drawn with another layer, which the icons weren't styled for
                    if (firsts.Count > 0 || wholes.Count > 0) {
                        map.IconManager.ShowAreaIcons();
                    }
                } else {
                    // with no layer to fade over from, it's there at once
                    made.Weight = shown.Count == 0 ? 1f : 0f;
                    shown[current] = made;
                }
            }

            for (var i = 0; i < CellIds; i++) {
                words[i] = Randomizer.Inventory == null ? 0 : Randomizer.Inventory.GetRandomizerItem(id + i);
            }

            gone.Clear();
            foreach (var pair in shown) {
                var layer = pair.Value;
                if (layer.Quad == null) {
                    gone.Add(pair.Key);
                    continue;
                }

                // the game's own map has no fade of ours to cross with, so it swaps in at once
                layer.Weight = hidesVanilla && current < 0 ? 0f : Mathf.MoveTowards(layer.Weight, pair.Key == current ? 1f : 0f, step);
                if (layer.Weight <= 0f) {
                    gone.Add(pair.Key);
                    continue;
                }

                if (!Same(layer.MaskWords, words)) {
                    if (layer.Mask != null) {
                        Object.Destroy(layer.Mask);
                    }

                    layer.Mask = Mask(pair.Key);
                    layer.MaskWords = (int[])words.Clone();
                    layer.Material.SetTexture(ShaderProperties.MapMaskTextureA, layer.Mask);
                    layer.Material.SetTexture(ShaderProperties.MapMaskTextureB, layer.Mask);
                }

                var alpha = layer.Weight * opacity;
                layer.Renderer.enabled = alpha > 0.01f;
                // the canvas color is 0.5 gray at full strength; the map shader doubles it
                layer.Material.SetColor(ShaderProperties.Color, new Color(0.5f, 0.5f, 0.5f, 0.5f * alpha));
            }

            foreach (var key in gone) {
                shown[key].Destroy();
                shown.Remove(key);
            }

            if (hidesVanilla) {
                HideVanilla(canvas, shown.Count > 0);
            }
        }

        private AreaMapCanvas Canvas(AreaMapUI map) {
            if (cached != null) {
                return cached;
            }

            foreach (var canvas in map.Canvases) {
                if (canvas != null && canvas.Area != null && canvas.Area.name == area) {
                    cached = canvas;
                }
            }

            return cached;
        }

        private Layer Build(int layer, AreaMapUI map, AreaMapCanvas canvas) {
            var texture = Texture(layer);
            if (texture == null) {
                return null;
            }

            // the canvas's own material, so the layer is lit and grayed exactly as the map is
            var vanilla = canvas.MapPlaneTexture.GetComponent<Renderer>();
            var material = new Material(vanilla.sharedMaterial);
            material.SetTexture(ShaderProperties.MainTexture, texture);
            material.SetFloat(ShaderProperties.MapFade, 0f);
            var quad = new GameObject($"randomizerMapLayer_{prefix}_{layer}");
            quad.layer = vanilla.gameObject.layer;
            quad.AddComponent<MeshFilter>().mesh = RandomizerQuad.UnitQuad();
            var renderer = quad.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.castShadows = false;
            renderer.receiveShadows = false;
            renderer.sortingLayerID = vanilla.sortingLayerID;
            renderer.sortingOrder = vanilla.sortingOrder + 1;

            var rect = rects[layer];
            quad.transform.parent = map.Navigation.MapPivot;
            quad.transform.localPosition = new Vector3(rect.center.x, rect.center.y, canvas.MapPlaneTexture.localPosition.z);
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(rect.width, rect.height, 1f);
            return new Layer { Quad = quad, Renderer = renderer, Material = material };
        }

        // color from a JPEG, alpha from a gray PNG; kept while the map is open, so stepping back to a layer costs nothing
        private Texture2D Texture(int layer) {
            Texture2D made;
            if (textures.TryGetValue(layer, out made) && made != null) {
                return made;
            }

            var colorBytes = RandomizerMapAssets.Read($"{prefix}_{layer:00}.jpg");
            var alphaBytes = RandomizerMapAssets.Read($"{prefix}_{layer:00}_alpha.png");
            if (colorBytes == null || alphaBytes == null) {
                return null;
            }

            var color = new Texture2D(2, 2);
            var alpha = new Texture2D(2, 2);
            color.LoadImage(colorBytes);
            alpha.LoadImage(alphaBytes);
            made = null;
            if (color.width == alpha.width && color.height == alpha.height) {
                var pixels = color.GetPixels32();
                var mask = alpha.GetPixels32();
                for (var i = 0; i < pixels.Length; i++) {
                    pixels[i].a = mask[i].r;
                }

                made = new Texture2D(color.width, color.height, TextureFormat.ARGB32, true);
                made.wrapMode = TextureWrapMode.Clamp;
                made.SetPixels32(pixels);
                made.Apply(true, true);
                textures[layer] = made;
            } else {
                Randomizer.log($"map layers: {prefix} {layer} has a {color.width}x{color.height} picture and a {alpha.width}x{alpha.height} alpha");
            }

            Object.Destroy(color);
            Object.Destroy(alpha);
            return made;
        }

        private static bool Same(int[] a, int[] b) {
            if (a == null) {
                return false;
            }

            for (var i = 0; i < a.Length; i++) {
                if (a[i] != b[i]) {
                    return false;
                }
            }

            return true;
        }

        // white over the layer's visited cells and red elsewhere, which the map shader grays, a pixel per unit
        private Texture2D Mask(int layer) {
            var rect = rects[layer];
            var w = Mathf.Max(1, Mathf.CeilToInt(rect.width));
            var h = Mathf.Max(1, Mathf.CeilToInt(rect.height));
            var lit = new float[w * h];
            var map = Cells(layer);
            for (var y = 0; y < h; y++) {
                for (var x = 0; x < w; x++) {
                    var cell = CellAt(map, new Vector2(rect.xMin + (x + 0.5f) * rect.width / w, rect.yMin + (y + 0.5f) * rect.height / h));
                    if (cell > 0 && cell <= visited.Size && (words[(cell - 1) / 32] & (1 << ((cell - 1) % 32))) != 0) {
                        lit[y * w + x] = 1f;
                    }
                }
            }

            Soften(lit, w, h, 2);
            var pixels = new Color32[w * h];
            for (var i = 0; i < pixels.Length; i++) {
                var g = (byte)Mathf.RoundToInt(lit[i] * 255f);
                pixels[i] = new Color32(255, g, g, 255);
            }

            var texture = new Texture2D(w, h, TextureFormat.ARGB32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // each layer's world rect, scenes and cell picture's rect, and each scene's bounds, from the tool that makes them
        private bool Load() {
            if (rects != null && loadedVersion == RandomizerMapAssets.Version) {
                return rects.Count > 0;
            }

            loadedVersion = RandomizerMapAssets.Version;
            rects = new Dictionary<int, Rect>();
            order.Clear();
            opens.Clear();
            cells.Clear();
            cellRects.Clear();
            firsts.Clear();
            names.Clear();
            late.Clear();
            dropped.Clear();
            stands.Clear();
            wholes.Clear();
            marks.Clear();
            places.Clear();
            failed.Clear();
            // whatever the map shows was built from the old files
            foreach (var layer in shown.Values) {
                layer.Destroy();
            }

            shown.Clear();
            foreach (var texture in textures.Values) {
                Object.Destroy(texture);
            }

            textures.Clear();
            drawn.Clear();
            sceneRects.Clear();
            gaps.Clear();
            homes.Clear();
            only.Clear();
            unnamed.Clear();
            var bytes = RandomizerMapAssets.Read(prefix + "_layers.txt");
            if (bytes == null) {
                return false;
            }

            foreach (var line in Encoding.UTF8.GetString(bytes).Split('\n')) {
                var parts = line.Trim().Split(' ');
                if (parts[0].StartsWith("#")) {
                    continue;
                }

                int layer;
                if (parts.Length >= 3 && parts[0] == "name") {
                    names[int.Parse(parts[1], CultureInfo.InvariantCulture)] = string.Join(" ", parts, 2, parts.Length - 2);
                } else if (parts.Length >= 3 && (parts[0] == "late" || parts[0] == "gone")) {
                    (parts[0] == "late" ? late : dropped)[int.Parse(parts[1], CultureInfo.InvariantCulture)] = Needs(parts, 2);
                } else if (parts.Length >= 3 && parts[0] == "stands") {
                    var picture = int.Parse(parts[1], CultureInfo.InvariantCulture);
                    var place = int.Parse(parts[2], CultureInfo.InvariantCulture);
                    if (!stands.ContainsKey(place)) {
                        stands[place] = new List<Stand>();
                    }

                    stands[place].Add(new Stand { Picture = picture, Needs = Needs(parts, 3) });
                    places[picture] = place;
                } else if (parts.Length >= 4 && parts[0] == "whole") {
                    wholes[int.Parse(parts[2], CultureInfo.InvariantCulture)] = new Stand { Picture = int.Parse(parts[1], CultureInfo.InvariantCulture), Needs = Needs(parts, 3) };
                } else if (parts.Length == 7 && parts[0] == "mark") {
                    marks.Add(new Mark {
                        Need = int.Parse(parts[1], CultureInfo.InvariantCulture),
                        State = int.Parse(parts[2], CultureInfo.InvariantCulture),
                        Rect = RectOf(parts, 3)
                    });
                } else if (parts.Length == 7 && parts[0] == "picture") {
                    // a picture with no place of its own in the order
                    layer = int.Parse(parts[1], CultureInfo.InvariantCulture);
                    rects[layer] = RectOf(parts, 2);
                    drawn[layer] = parts[6].Split(',');
                } else if (parts.Length == 2 && parts[0] == "game") {
                    // the game's own map has a place in the order and no picture
                    if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out layer) && !order.Contains(layer)) {
                        order.Add(layer);
                    }
                } else if (parts.Length == 7 && parts[0] == "scene") {
                    sceneRects[parts[1]] = RectOf(parts, 3);
                } else if (parts.Length == 5 && parts[0] == "break") {
                    gaps.Add(RectOf(parts, 1).center);
                } else if (parts.Length == 6 && parts[0] == "home") {
                    var g = new int[4];
                    for (var i = 0; i < 4; i++) {
                        g[i] = int.Parse(parts[1 + i], CultureInfo.InvariantCulture);
                    }

                    homes[new MoonGuid(g[0], g[1], g[2], g[3])] = parts[5];
                } else if (parts.Length == 6 && (parts[0] == "only" || parts[0] == "unnamed")) {
                    var g = new int[4];
                    for (var i = 0; i < 4; i++) {
                        g[i] = int.Parse(parts[1 + i], CultureInfo.InvariantCulture);
                    }

                    (parts[0] == "only" ? only : unnamed)[new MoonGuid(g[0], g[1], g[2], g[3])] = Array.ConvertAll(parts[5].Split(','), n => int.Parse(n, CultureInfo.InvariantCulture));
                } else if (parts.Length == 6 && parts[0] == "cells") {
                    cellRects[int.Parse(parts[1], CultureInfo.InvariantCulture)] = RectOf(parts, 2);
                } else if (parts.Length == 4 && parts[0] == "first") {
                    firsts[int.Parse(parts[1], CultureInfo.InvariantCulture)] = new First {
                        Layer = int.Parse(parts[2], CultureInfo.InvariantCulture),
                        Cell = int.Parse(parts[3], CultureInfo.InvariantCulture)
                    };
                } else if (parts.Length == 6 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out layer)) {
                    if (!order.Contains(layer)) {
                        order.Add(layer);
                    }

                    rects[layer] = RectOf(parts, 1);
                    drawn[layer] = parts[5].Split(',');
                }
            }

            // a state drawn at first with another layer, and without a place of its own, stands in that layer's
            foreach (var pair in firsts) {
                if (!order.Contains(pair.Key) && !places.ContainsKey(pair.Key)) {
                    places[pair.Key] = pair.Value.Layer;
                }
            }

            return rects.Count > 0;
        }

        private static int[] Needs(string[] parts, int at) {
            var needs = new int[parts.Length - at];
            for (var i = 0; i < needs.Length; i++) {
                needs[i] = int.Parse(parts[at + i], CultureInfo.InvariantCulture);
            }

            return needs;
        }

        private static Rect RectOf(string[] parts, int at) {
            var v = new float[4];
            for (var i = 0; i < 4; i++) {
                v[i] = float.Parse(parts[at + i], CultureInfo.InvariantCulture);
            }

            return Rect.MinMaxRect(v[0], v[1], v[2], v[3]);
        }

        // the layer a state is drawn with: its first one until Ori has stood in the cell that ends it, or stands where only its own is drawn
        public int Resolve(int layer) {
            if (!Load()) {
                return layer;
            }

            First first;
            if (!firsts.TryGetValue(layer, out first)) {
                // a first layer that can't be drawn gives way to the layer of the state it stands in for
                foreach (var pair in firsts) {
                    if (pair.Value.Layer == layer && !Draws(layer) && Draws(pair.Key)) {
                        return pair.Key;
                    }
                }

                return layer;
            }

            if (!Draws(first.Layer) || Ended(first)) {
                return layer;
            }

            var sein = Characters.Sein;
            // a missing grid can't say where Ori stands, so the first layer stays
            return sein != null && Shows(sein.Position, layer) == true && Shows(sein.Position, first.Layer) == false ? layer : first.Layer;
        }

        // whether Ori has stood in the cell that ends a state's first layer
        private bool Ended(First first) {
            return Randomizer.Inventory != null && visited.Get(first.Cell - 1);
        }

        // whether a layer has a picture that hasn't failed to build
        private bool Draws(int layer) {
            return rects.ContainsKey(layer) && !failed.Contains(layer);
        }

        // decodes the picture and cells of the layer the map would show, ahead of its opening; false with nothing to do
        public bool Warm() {
            if (!Load()) {
                return false;
            }

            var layer = layerOf();
            if (!rects.ContainsKey(layer) || textures.ContainsKey(layer) || failed.Contains(layer)) {
                return false;
            }

            if (Texture(layer) == null) {
                failed.Add(layer);
            }

            Cells(layer);
            return true;
        }

        // drops the pictures kept for stepping through layers, but for those the map still shows
        public void Closed() {
            gone.Clear();
            foreach (var layer in textures.Keys) {
                if (!shown.ContainsKey(layer)) {
                    gone.Add(layer);
                }
            }

            foreach (var layer in gone) {
                Object.Destroy(textures[layer]);
                textures.Remove(layer);
            }
        }

        // the art lives only on the material (Area.WorldMapTexture is null), so keep it to put back
        private void HideVanilla(AreaMapCanvas canvas, bool hide) {
            var material = canvas.MapPlaneTexture.GetComponent<Renderer>().material;
            var current = material.GetTexture(ShaderProperties.MainTexture);
            if (current != Clear()) {
                vanillaTexture = current;
            }

            var wanted = hide ? Clear() : vanillaTexture;
            if (wanted != null && current != wanted) {
                material.SetTexture(ShaderProperties.MainTexture, wanted);
            }
        }

        private readonly string area;
        private readonly string prefix;
        private readonly int id;
        private readonly Func<int> layerOf;
        // the layer of the state Ori is in, which a layer picked on the map doesn't change
        private readonly Func<int> stateOf;
        // whether a need of one of the layers file's rules holds for the save
        private readonly Func<int, bool> holds;
        private readonly bool hidesVanilla;
        private readonly RandomizerBitfield visited;
        private readonly int[] words = new int[CellIds];
        private readonly Dictionary<int, Rect> cellRects = new Dictionary<int, Rect>();
        private readonly Dictionary<int, First> firsts = new Dictionary<int, First>();
        private readonly Dictionary<int, string> names = new Dictionary<int, string>();
        // the file's rules, each with needs that must all hold: a place offered only then, a place dropped then, a picture drawn in a place then
        private readonly Dictionary<int, int[]> late = new Dictionary<int, int[]>();
        private readonly Dictionary<int, int[]> dropped = new Dictionary<int, int[]>();
        private readonly Dictionary<int, List<Stand>> stands = new Dictionary<int, List<Stand>>();
        // each layer's whole picture, drawn wherever the layer would be once its needs hold
        private readonly Dictionary<int, Stand> wholes = new Dictionary<int, Stand>();
        private readonly List<Mark> marks = new List<Mark>();
        // the place of each picture that has none of its own in the order
        private readonly Dictionary<int, int> places = new Dictionary<int, int>();
        private readonly Dictionary<int, CellMap> cells = new Dictionary<int, CellMap>();
        private readonly Dictionary<int, Layer> shown = new Dictionary<int, Layer>();
        private readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();
        // layers whose pictures didn't build from the files of this version
        private readonly HashSet<int> failed = new HashSet<int>();
        private readonly List<int> gone = new List<int>();
        private readonly Dictionary<int, string[]> drawn = new Dictionary<int, string[]>();
        private readonly Dictionary<string, Rect> sceneRects = new Dictionary<string, Rect>();
        private readonly Dictionary<int, Grid> opens = new Dictionary<int, Grid>();
        private readonly List<Vector2> gaps = new List<Vector2>();
        private readonly Dictionary<MoonGuid, string> homes = new Dictionary<MoonGuid, string>();
        // icons the file puts on some layers alone, whatever layers their scene loads in
        private readonly Dictionary<MoonGuid, int[]> only = new Dictionary<MoonGuid, int[]>();
        // layers an icon is on that its tooltip's list leaves out
        private readonly Dictionary<MoonGuid, int[]> unnamed = new Dictionary<MoonGuid, int[]>();
        private Dictionary<int, Rect> rects;
        // layers as the file lists them, which is the order they're numbered in
        private readonly List<int> order = new List<int>();
        private int loadedVersion = -1;
        private AreaMapCanvas cached;
        private Texture vanillaTexture;
    }

    // a layer's reveal cells: the cell number at each pixel of a world rect, bottom row first
    private class CellMap {
        public Rect Rect;
        public int Width;
        public int Height;
        public ushort[] Ids;
    }

    // where a layer's picture is drawn, a cell a unit from its rect's corner, bottom row first
    private class Grid {
        public int Width;
        public int Height;
        public bool[] Drawn;
    }

    private class Layer {
        public GameObject Quad;
        public Renderer Renderer;
        public Material Material;
        public Texture2D Mask;
        public int[] MaskWords;
        public float Weight;

        public void Destroy() {
            foreach (var made in new Object[] { Quad, Material, Mask }) {
                if (made != null) {
                    Object.Destroy(made);
                }
            }
        }
    }

    private static Texture2D clear;

    // each glow's own texture, or null for a renderer that isn't one, from the first time its icon was off the layer
    private static readonly Dictionary<Renderer, Texture> glows = new Dictionary<Renderer, Texture>();

    // a picture drawn in another layer's place once its needs hold
    private class Stand {
        public int Picture;
        public int[] Needs;
    }

    // the layer a state is drawn with at first, until its reveal cell is lit
    private class First {
        public int Layer;
        public int Cell;
    }

    // a need the save has once Ori has stood in a world rect in a state
    private class Mark {
        public int Need;
        public int State;
        public Rect Rect;
    }
}
