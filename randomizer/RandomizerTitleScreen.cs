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
        var bannerRenderer = banner.GetComponent<Renderer>();
        if (bannerRenderer == null) {
            Randomizer.log("title screen: banner has no renderer to match, skipping branding");
            return null;
        }

        var wordmark = RandomizerQuad.Build("randomizerWordmark", "menu_text_randomizer.png", bannerRenderer);
        if (wordmark == null) {
            Randomizer.log("title screen: no material that maps a whole texture, skipping branding");
            return null;
        }

        var placed = wordmark.transform;
        placed.SetParent(parent, false);
        placed.localRotation = banner.localRotation;
        placed.localScale = new Vector3(width, height, 1f);
        return placed;
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


}
