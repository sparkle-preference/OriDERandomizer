using Game;
using UnityEngine;

// Draws the boxes in force over the finished frame: filled world-space quads with a
// brighter edge. It runs as the camera's last image effect so the post-process
// (motion blur, bloom, grading) never touches them; nothing in this game is a Unity
// sprite, so there is no renderer to borrow instead.
public class RandomizerBoxView : MonoBehaviour {
    private static RandomizerBoxView instance;

    private static Material paint;

    private Camera cam;

    private const float EdgeWidth = 0.25f;

    private static readonly Color DraftFill = new Color(1f, 1f, 0.6f, 0.15f);

    private static readonly Color DraftEdge = new Color(1f, 1f, 0.6f, 0.8f);

    // called every tick: the camera the game renders through can change
    public static void Attach() {
        var camera = Game.UI.Cameras.Current == null ? null : Game.UI.Cameras.Current.Camera;
        if (camera == null) {
            return;
        }

        if (instance != null) {
            if (instance.gameObject == camera.gameObject) {
                return;
            }

            Destroy(instance);
        }

        instance = camera.gameObject.AddComponent<RandomizerBoxView>();
    }

    // Image effects run in component order, and a component added at runtime
    // is last, so source here is the frame after every game effect.
    public void OnRenderImage(RenderTexture source, RenderTexture destination) {
        Graphics.Blit(source, destination);
        var boxes = RandomizerBoxes.Active;
        var draft = PracticeEditor.Draft;
        // drawn after the UI camera, so a menu would sit under them
        if ((boxes.Count == 0 && !draft.HasValue) || Characters.Sein == null || Game.UI.MainMenuVisible || !Ready()) {
            return;
        }

        if (cam == null) {
            cam = GetComponent<Camera>();
        }

        RenderTexture.active = destination;
        GL.PushMatrix();
        GL.LoadProjectionMatrix(cam.projectionMatrix);
        GL.modelview = cam.worldToCameraMatrix;
        paint.SetPass(0);
        GL.Begin(GL.QUADS);
        foreach (var box in boxes) {
            if (!box.Paint.HasValue || box.Consumed) {
                continue;
            }

            var colour = box.Paint.Value;
            var edge = new Color(colour.r, colour.g, colour.b, Mathf.Min(1f, colour.a + 0.45f));
            Outline(box.Area, colour, edge);
        }

        // the box being drawn, in the editor
        if (draft.HasValue) {
            Outline(draft.Value, DraftFill, DraftEdge);
        }

        GL.End();
        GL.PopMatrix();
    }

    private static void Outline(Rect area, Color fill, Color edge) {
        Fill(area.xMin, area.yMin, area.xMax, area.yMax, fill);
        Fill(area.xMin, area.yMin, area.xMax, area.yMin + EdgeWidth, edge);
        Fill(area.xMin, area.yMax - EdgeWidth, area.xMax, area.yMax, edge);
        Fill(area.xMin, area.yMin, area.xMin + EdgeWidth, area.yMax, edge);
        Fill(area.xMax - EdgeWidth, area.yMin, area.xMax, area.yMax, edge);
    }

    private static void Fill(float x1, float y1, float x2, float y2, Color colour) {
        GL.Color(colour);
        GL.Vertex3(x1, y1, 0f);
        GL.Vertex3(x2, y1, 0f);
        GL.Vertex3(x2, y2, 0f);
        GL.Vertex3(x1, y2, 0f);
    }

    // Internal-Colored ships with the engine, so this asks the game for nothing
    private static bool Ready() {
        if (paint != null) {
            return true;
        }

        var shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) {
            Randomizer.LogError("boxes: no colour shader, they will not draw");
            return false;
        }

        paint = new Material(shader);
        paint.hideFlags = HideFlags.HideAndDontSave;
        paint.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        paint.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        paint.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        paint.SetInt("_ZWrite", 0);
        paint.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        return true;
    }
}
