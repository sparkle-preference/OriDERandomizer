using CatlikeCoding.TextBox;
using Game;
using UnityEngine;

// Version stamp in the bottom-right of the menus, absent during gameplay.
//
// The pause screen dims the world in its own camera pass, so the stamp has to
// follow the menu's layer and camera while the menu is up, not the world's.
public class RandomizerVersionLabel : MonoBehaviour {
    // fraction of the hint message's text size
    private const float Scale = 0.1f;

    // share of the menu camera's visible height
    private const float MenuTextHeight = 0.025f;

    // Viewport fractions in from the bottom-right; the text hangs below its
    // anchor, so the y inset clears its own height too.
    private const float InsetX = 0.015f;
    private const float InsetY = 0.058f;

    public static void Initialize() {
        Instance = new GameObject("randomizerVersionLabel").AddComponent<RandomizerVersionLabel>();
    }

    public void Update() {
        try {
            if (!EnsureLabel()) {
                return;
            }

            var show = ShouldShow();
            if (m_label.activeSelf != show) {
                m_label.SetActive(show);
                if (show) {
                    // TextBox.Start resets to its serialised default text, so
                    // wait a frame for it before writing ours
                    m_textPending = true;
                    return;
                }
            }

            if (!show) {
                return;
            }

            if (m_textPending) {
                ApplyText();
                m_textPending = false;
            }

            Place();
        } catch (System.Exception e) {
            Randomizer.log($"version label: {e}");
            enabled = false;
        }
    }

    private static bool ShouldShow() {
        if (GameController.Instance == null) {
            return false;
        }

        // the pause menu and the ability tree are the same screen stack
        return GameController.Instance.GameInTitleScreen || UI.MainMenuVisible;
    }

    private bool EnsureLabel() {
        if (m_label != null) {
            return true;
        }

        var controller = UI.MessageController;
        var hintMessage = controller == null ? null : controller.HintMessage;
        var text = hintMessage == null ? null : hintMessage.transform.FindChild("text");
        if (text == null) {
            return false;
        }

        // deactivating the source before cloning keeps the clone from running
        // Awake/Start before we have finished setting it up
        var wasActive = hintMessage.activeSelf;
        hintMessage.SetActive(false);
        var clone = (GameObject)InstantiateUtility.Instantiate(text.gameObject);
        hintMessage.SetActive(wasActive);

        clone.name = "versionText";
        clone.transform.SetParent(transform, false);
        clone.transform.localScale *= Scale;
        // the menu path overwrites this, so keep it to restore on the way out
        m_baseScale = clone.transform.localScale;

        // the HUD layer this came from is only drawn by the gui camera, which
        // is disabled outside gameplay
        SetLayerRecursively(clone, ArtLayer);

        // that component sizes a message background this label does not have
        var scaleToTextBox = clone.GetComponent<ScaleToTextBox>();
        if (scaleToTextBox != null) {
            Destroy(scaleToTextBox);
        }

        m_textBox = clone.GetComponent<TextBox>();
        if (m_textBox == null) {
            Randomizer.log("version label: clone has no TextBox");
            Destroy(clone);
            return false;
        }

        m_label = clone;
        m_label.SetActive(false);
        return true;
    }

    private void ApplyText() {
        m_textBox.alignment = AlignmentMode.Right;
        m_textBox.horizontalAnchor = HorizontalAnchorMode.Right;
        m_textBox.verticalAnchor = VerticalAnchorMode.Bottom;
        m_textBox.CreateRendersIfThereAreNone();
        m_textBox.SetText("v" + Randomizer.DisplayVersion);
        m_textBox.RenderText();

        // a clone never runs MessageBoxVisibility, so it keeps the prefab's
        // leftover alpha
        foreach (var renderer in m_label.GetComponentsInChildren<Renderer>(true)) {
            renderer.enabled = true;
            UberShaderAPI.SetColor(renderer, Color.white, true);
        }
    }

    private void Place() {
        // a live entry is the only reliable way to find the menu's own layer
        var item = UI.MainMenuVisible && UI.Menu != null
            ? UI.Menu.GetComponentInChildren<CleverMenuItem>()
            : null;

        if (item != null) {
            PlaceInMenu(item);
        } else {
            PlaceAgainstWorld();
        }
    }

    // the menu camera is orthographic and centred on the origin, so its corners
    // are just its size and the screen aspect
    private void PlaceInMenu(CleverMenuItem item) {
        var layer = item.gameObject.layer;
        if (m_label.layer != layer) {
            SetLayerRecursively(m_label, layer);
        }

        var camera = MenuCamera(layer);
        if (camera == null || !camera.orthographic) {
            return;
        }

        var halfHeight = camera.orthographicSize;
        var halfWidth = halfHeight * ((float)Screen.width / Screen.height);

        m_label.transform.position = new Vector3(
            halfWidth - InsetX * 2f * halfWidth,
            -halfHeight + InsetY * 2f * halfHeight,
            // the menu entries' plane sits in front of the dim
            item.transform.position.z
        );
        m_label.transform.rotation = camera.transform.rotation;

        var textHeight = m_textBox.boundsTop - m_textBox.boundsBottom;
        if (textHeight > 0f) {
            m_label.transform.localScale = Vector3.one * (halfHeight * 2f * MenuTextHeight / textHeight);
        }
    }

    private void PlaceAgainstWorld() {
        if (m_label.layer != ArtLayer) {
            SetLayerRecursively(m_label, ArtLayer);
        }

        // set every frame rather than on the way in: whichever path ran last
        // owns the scale, and guessing wrong is a screen-filling version number
        m_label.transform.localScale = m_baseScale;

        var camera = ActiveCamera();
        if (camera == null) {
            return;
        }

        // sit just in front of the camera so foreground art cannot cover it
        var distance = camera.nearClipPlane + 1f;
        m_label.transform.position = camera.ViewportToWorldPoint(new Vector3(1f - InsetX, InsetY, distance));
        m_label.transform.rotation = camera.transform.rotation;
    }

    // the menu camera is manually driven, so it is disabled and Camera.allCameras
    // never lists it
    private static Camera MenuCamera(int layer) {
        Camera best = null;
        foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>()) {
            if (!camera.gameObject.activeInHierarchy || (camera.cullingMask & (1 << layer)) == 0) {
                continue;
            }

            if (best == null || camera.depth > best.depth) {
                best = camera;
            }
        }

        return best;
    }

    private static void SetLayerRecursively(GameObject obj, int layer) {
        if (layer < 0) {
            return;
        }

        obj.layer = layer;
        foreach (Transform child in obj.transform) {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    // Current is the gameplay camera and is null in the menus, so fall back to
    // whichever camera is actually drawing
    private static Camera ActiveCamera() {
        var current = UI.Cameras.Current;
        if (current != null && current.Camera != null && current.Camera.isActiveAndEnabled) {
            return current.Camera;
        }

        if (Camera.main != null) {
            return Camera.main;
        }

        var cameras = Camera.allCameras;
        return cameras.Length > 0 ? cameras[0] : null;
    }

    private static int ArtLayer => LayerMask.NameToLayer("art");

    public static RandomizerVersionLabel Instance;

    private GameObject m_label;

    private TextBox m_textBox;

    private bool m_textPending;

    private Vector3 m_baseScale = Vector3.one;
}
