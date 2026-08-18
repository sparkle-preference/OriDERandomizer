using UnityEngine;

// Title screen branding: lifts the vanilla logo and hangs a "Randomizer"
// wordmark under the Definitive Edition banner.
public class RandomizerTitleScreen {
    // Fractions of the banner's mesh, which is mostly transparent glow padding
    // -- the visible brushstroke is about half its width.
    private const float WidthFactor = 0.47f;

    // wordmark centre below the banner centre
    private const float DropFactor = 0.34f;

    // how far the lockup rises; more than this clips off the top
    private const float RiseFactor = 0.22f;

    private const float TextureAspect = 653f / 181f;

    public static void Bootstrap(Transform ui) {
        try {
            var titleScreen = ui.GetComponent<TitleScreenManager>();
            RandomizerUpdater.BindMainMenu(
                titleScreen == null ? null : titleScreen.MainMenuScreen,
                titleScreen == null ? null : titleScreen.ExitGameScreen
            );

            var group = ui.FindChild("group");
            if (group == null) {
                Randomizer.log("title screen: no ui/group, skipping branding");
                return;
            }

            var oriLogo = group.FindChild("oriLogo");
            var definitiveEdition = group.FindChild("definitiveEdition");
            var banner = definitiveEdition == null ? null : definitiveEdition.FindChild("logoOriDefinitiveEditionA");
            if (oriLogo == null || definitiveEdition == null || banner == null) {
                Randomizer.log("title screen: logo objects missing, skipping branding");
                return;
            }

            var bannerSize = MeshSize(banner);
            if (bannerSize.x <= 0f || bannerSize.y <= 0f) {
                Randomizer.log("title screen: banner has no measurable mesh, skipping branding");
                return;
            }

            // sized off the banner so this survives any future art change
            var width = bannerSize.x * WidthFactor;
            var height = width / TextureAspect;

            var wordmark = BuildWordmark(banner, definitiveEdition, width, height);
            if (wordmark == null) {
                return;
            }

            wordmark.localPosition = new Vector3(
                banner.localPosition.x,
                banner.localPosition.y - bannerSize.y * DropFactor,
                banner.localPosition.z
            );

            var rise = new Vector3(0f, bannerSize.y * RiseFactor, 0f);
            oriLogo.localPosition += rise;
            definitiveEdition.localPosition += rise;
        } catch (System.Exception e) {
            // branding must never keep the game off the title screen
            Randomizer.log($"title screen branding: {e}");
        }
    }

    // Logo sprites cannot be re-textured: their UberShader variants have the
    // atlas rect baked in. The hint background's variant maps a whole texture,
    // so borrow that material. Stock shaders are stripped from this build.
    private static Transform BuildWordmark(Transform banner, Transform parent, float width, float height) {
        var texture = WordmarkTexture;
        if (texture == null) {
            return null;
        }

        var bannerRenderer = banner.GetComponent<Renderer>();
        if (bannerRenderer == null) {
            Randomizer.log("title screen: banner has no renderer to match, skipping branding");
            return null;
        }

        var material = WholeTextureMaterial();
        if (material == null) {
            Randomizer.log("title screen: no material that maps a whole texture, skipping branding");
            return null;
        }

        material.SetTexture("_MainTex", texture);
        material.SetColor("_Color", Color.white);

        var wordmark = new GameObject("randomizerWordmark");
        wordmark.layer = banner.gameObject.layer;
        wordmark.AddComponent<MeshFilter>().mesh = UnitQuad();

        var renderer = wordmark.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.castShadows = false;
        renderer.receiveShadows = false;
        renderer.sortingLayerID = bannerRenderer.sortingLayerID;
        renderer.sortingOrder = bannerRenderer.sortingOrder;

        var placed = wordmark.transform;
        placed.SetParent(parent, false);
        placed.localRotation = banner.localRotation;
        placed.localScale = new Vector3(width, height, 1f);
        return placed;
    }

    private static Material WholeTextureMaterial() {
        var controller = Game.UI.MessageController;
        var hintMessage = controller == null ? null : controller.HintMessage;
        if (hintMessage == null) {
            return null;
        }

        var background = hintMessage.transform.FindChild("background/hintMessageBackground");
        if (background == null) {
            return null;
        }

        var renderer = background.GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null) {
            return null;
        }

        return new Material(renderer.sharedMaterial);
    }

    // 1x1 quad centred on its origin, UVs spanning the whole texture
    private static Mesh UnitQuad() {
        var mesh = new Mesh();
        mesh.vertices = new Vector3[4] {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        mesh.uv = new Vector2[4] {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.colors = new Color[4] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new int[6] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // local-space size of a mesh quad, scale included
    private static Vector3 MeshSize(Transform obj) {
        var filter = obj.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) {
            return Vector3.zero;
        }

        var bounds = filter.sharedMesh.bounds.size;
        return new Vector3(
            bounds.x * obj.localScale.x,
            bounds.y * obj.localScale.y,
            bounds.z * obj.localScale.z
        );
    }

    private static Texture2D WordmarkTexture {
        get {
            if (_wordmarkTexture == null) {
                var bytes = RandomizerResources.ReadResource("menu_text_randomizer.png");
                if (bytes == null) {
                    return null;
                }

                _wordmarkTexture = new Texture2D(0, 0);
                _wordmarkTexture.LoadImage(bytes);
            }

            return _wordmarkTexture;
        }
    }

    private static Texture2D _wordmarkTexture;
}
