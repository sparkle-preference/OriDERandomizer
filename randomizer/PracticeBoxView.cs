using Game;
using UnityEngine;

// Draws a segment's boxes over the finished frame: filled world-space quads
// with a brighter edge. It runs as the camera's last image effect so the
// post-process (motion blur, bloom, grading) never touches them; nothing in
// this game is a Unity sprite, so there is no renderer to borrow instead.
public class PracticeBoxView : MonoBehaviour {
    private static PracticeBoxView instance;

    private static Material paint;

    private Camera cam;

    private const float EdgeWidth = 0.25f;

    // a box that grants something unseen still has to be findable while testing
    private static readonly Color Unpainted = new Color(1f, 1f, 1f, 0.1f);

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

        instance = camera.gameObject.AddComponent<PracticeBoxView>();
    }

    public static void Detach() {
        if (instance != null) {
            Destroy(instance);
            instance = null;
        }
    }

    // Image effects run in component order, and a component added at runtime
    // is last, so source here is the frame after every game effect.
    public void OnRenderImage(RenderTexture source, RenderTexture destination) {
        Graphics.Blit(source, destination);
        var segment = PracticeController.Segment;
        // drawn after the UI camera, so a menu would sit under them
        if (!PracticeController.Active || segment == null || Characters.Sein == null
                || Game.UI.MainMenuVisible || !Ready()) {
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
        foreach (var box in segment.Boxes) {
            if (box.Spent) {
                continue;
            }

            var colour = box.Paint.HasValue ? box.Paint.Value : Unpainted;
            var edge = new Color(colour.r, colour.g, colour.b, Mathf.Min(1f, colour.a + 0.45f));
            var area = box.Area;
            Fill(area.xMin, area.yMin, area.xMax, area.yMax, colour);
            Fill(area.xMin, area.yMin, area.xMax, area.yMin + EdgeWidth, edge);
            Fill(area.xMin, area.yMax - EdgeWidth, area.xMax, area.yMax, edge);
            Fill(area.xMin, area.yMin, area.xMin + EdgeWidth, area.yMax, edge);
            Fill(area.xMax - EdgeWidth, area.yMin, area.xMax, area.yMax, edge);
        }

        // the box being drawn, in the editor
        var draft = PracticeEditor.Draft;
        if (draft.HasValue) {
            var area = draft.Value;
            Fill(area.xMin, area.yMin, area.xMax, area.yMax, DraftFill);
            Fill(area.xMin, area.yMin, area.xMax, area.yMin + EdgeWidth, DraftEdge);
            Fill(area.xMin, area.yMax - EdgeWidth, area.xMax, area.yMax, DraftEdge);
            Fill(area.xMin, area.yMin, area.xMin + EdgeWidth, area.yMax, DraftEdge);
            Fill(area.xMax - EdgeWidth, area.yMin, area.xMax, area.yMax, DraftEdge);
        }

        GL.End();
        GL.PopMatrix();
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
            Randomizer.LogError("practice: no colour shader, boxes will not draw");
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
