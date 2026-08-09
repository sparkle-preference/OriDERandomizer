using UnityEngine;

public class AreaMapCanvas : MonoBehaviour {
    public void Awake() {
        RuntimeArea = GameWorld.Instance.FindRuntimeArea(Area);
        Mask = Area.WorldMapTexture;
    }

    public void ResetMap() {
        gameObject.SetActive(true);
        MapPlaneTexture.localScale = new Vector3(Bounds.size.x, Bounds.size.y);
        MapPlaneTexture.localPosition = Bounds.center;
        if (WorldMapTexture) {
            MapPlaneTexture.GetComponent<Renderer>().material.SetTexture(ShaderProperties.MainTexture, WorldMapTexture);
        }

        UpdateAreaMaskTextureA();
        if (m_addToMap) {
            InstantiateUtility.Destroy(m_addToMap);
        }

        SetFade(0f);
    }

    public Texture WorldMapTexture => Area.WorldMapTexture;

    public Bounds Bounds => Area.Bounds;

    public CageStructureTool CageStructureTool => Area.CageStructureTool;

    public Vector2 WorldMapTextureSize => new Vector2(WorldMapTexture.width, WorldMapTexture.height);

    public RenderTexture GenerateAreaMaskMaskTexture() {
        int width = (int)Mathf.Min(1024f, Bounds.size.x * PixelsPerUnit);
        int height = (int)Mathf.Min(1024f, Bounds.size.y * PixelsPerUnit);
        RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        temporary.name = "worldMapCanvas";
        Graphics.SetRenderTarget(temporary);
        GL.Clear(false, true, Color.clear);
        GL.PushMatrix();
        GL.LoadIdentity();
        GL.LoadPixelMatrix(Bounds.min.x + 0.5f, Bounds.max.x + 0.5f, Bounds.min.y + 0.5f, Bounds.max.y + 0.5f);
        Matrix4x4 localToWorldMatrix = CageStructureTool.transform.localToWorldMatrix;
        GL.MultMatrix(localToWorldMatrix);
        Material material = new Material(SetRGBAShader);
        material.SetColor(ShaderProperties.Color, Color.white / 2f);
        material.SetPass(0);
        GL.Begin(4);
        GL.Color(Color.white);
        for (int i = 0; i < CageStructureTool.Faces.Count; i++) {
            CageStructureTool.Face face = CageStructureTool.Faces[i];
            for (int j = 0; j < face.Triangles.Count; j++) {
                int index = face.Triangles[j];
                GL.Vertex(CageStructureTool.VertexByIndex(face.Vertices[index]).Position);
            }
        }

        GL.End();
        GL.PopMatrix();
        return temporary;
    }

    public Color GetColor(WorldMapAreaState worldState) {
        switch (worldState) {
            case WorldMapAreaState.Hidden:
                if (AreaMapUI.Instance.DebugNavigation.UndiscoveredMapVisible) {
                    return Color.white;
                }

                return Color.red;
            case WorldMapAreaState.Discovered:
                return Color.red;
            case WorldMapAreaState.Visited:
                return Color.white;
            default:
                return Color.red;
        }
    }

    public RenderTexture GenerateAreaMaskTexture() {
        int width = (int)Mathf.Min(1024f, Bounds.size.x * PixelsPerUnit);
        int height = (int)Mathf.Min(1024f, Bounds.size.y * PixelsPerUnit);
        RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        temporary.name = "worldmapCanvas";
        Graphics.SetRenderTarget(temporary);
        GL.Clear(false, true, Color.clear);
        GL.PushMatrix();
        GL.LoadIdentity();
        GL.LoadPixelMatrix(Bounds.min.x + 0.5f, Bounds.max.x + 0.5f, Bounds.min.y + 0.5f, Bounds.max.y + 0.5f);
        Material material = new Material(SetRGBAShader);
        material.SetColor(ShaderProperties.Color, Color.white / 2f);
        material.SetPass(0);
        GL.Begin(4);
        Matrix4x4 localToWorldMatrix = CageStructureTool.transform.localToWorldMatrix;
        foreach (CageStructureTool.Face face in CageStructureTool.Faces) {
            WorldMapAreaState faceState = RuntimeArea.GetFaceState(face.ID);
            GL.Color(GetColor(faceState));
            foreach (int index in face.Triangles) {
                GL.Vertex(localToWorldMatrix.MultiplyPoint(CageStructureTool.VertexByIndex(face.Vertices[index]).Position));
            }
        }

        GL.End();
        GL.PopMatrix();
        RenderTexture result = BlurTextures(temporary);
        RenderTexture.ReleaseTemporary(temporary);
        return result;
    }

    public void Update() {
        if (m_areaMaskTextureA && !m_areaMaskTextureA.IsCreated()) {
            UpdateAreaMaskTextureA();
        }

        if (m_areaMaskTextureB && !m_areaMaskTextureB.IsCreated()) {
            UpdateAreaMaskTextureB();
        }
    }

    public void UpdateAreaMaskTextureA() {
        if (m_areaMaskTextureA) {
            DestroyObject(m_areaMaskTextureA);
        }

        m_areaMaskTextureA = GenerateAreaMaskTexture();
        MapPlaneTexture.GetComponent<Renderer>().material.SetTexture(ShaderProperties.MapMaskTextureA, m_areaMaskTextureA);
    }

    public void UpdateAreaMaskTextureB() {
        if (m_areaMaskTextureB) {
            DestroyObject(m_areaMaskTextureB);
        }

        m_areaMaskTextureB = GenerateAreaMaskTexture();
        MapPlaneTexture.GetComponent<Renderer>().material.SetTexture(ShaderProperties.MapMaskTextureB, m_areaMaskTextureB);
    }

    public void SetFade(float fade) {
        MapPlaneTexture.GetComponent<Renderer>().material.SetFloat(ShaderProperties.MapFade, fade);
    }

    public void OnDestroy() {
        Release();
    }

    public void Release() {
        if (m_areaMaskTextureA) {
            DestroyObject(m_areaMaskTextureA);
            m_areaMaskTextureA = null;
        }

        if (m_areaMaskTextureB) {
            DestroyObject(m_areaMaskTextureB);
            m_areaMaskTextureB = null;
        }
    }

    public RenderTexture BlurTextures(Texture originalTexture) {
        Texture mask = Mask;
        Material material = new Material(WorldMapBlurShader);
        material.SetTexture(ShaderProperties.MaskTex, mask);
        int width = originalTexture.width;
        int height = originalTexture.height;
        Vector2 vector = new Vector2(1.5f / MapPlaneTexture.localScale.x, 1.5f / MapPlaneTexture.localScale.y);
        RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture temporary2 = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        temporary.name = "worldMapCanvas";
        temporary2.name = "worldMapCanvasB";
        Texture texture = originalTexture;
        RenderTexture renderTexture = temporary;
        RenderTexture renderTexture2 = temporary2;
        renderTexture.name = "current";
        renderTexture2.name = "next";
        int num = 5;
        for (int i = 0; i < num; i++) {
            material.SetVector(ShaderProperties.BlurSize, new Vector4(vector.x, vector.y, 0f, 0f) * (1f + i / 6f));
            material.SetVector(ShaderProperties.TextureScalingAndOffset, new Vector4(1f, 1f, 0f, 0f));
            RenderTexture.active = renderTexture;
            Graphics.Blit(texture, renderTexture, material);
            texture = renderTexture;
            renderTexture = renderTexture2;
            renderTexture2 = (RenderTexture)texture;
        }

        RenderTexture renderTexture3 = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        renderTexture3.hideFlags = HideFlags.DontSave;
        Graphics.Blit(texture, renderTexture3);
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(temporary);
        RenderTexture.ReleaseTemporary(temporary2);
        return renderTexture3;
    }

    public void ReleaseAreaMaskTextureB() {
        if (m_areaMaskTextureB) {
            DestroyObject(m_areaMaskTextureB);
            m_areaMaskTextureB = null;
        }
    }

    public GameWorldArea Area;

    public RuntimeGameWorldArea RuntimeArea;

    public Shader WorldMapBlurShader;

    public Transform MapPlaneTexture;

    public Texture Mask;

    public int PixelsPerUnit = 5;

    private GameObject m_addToMap;

    private RenderTexture m_areaMaskTextureA;

    private RenderTexture m_areaMaskTextureB;

    public Shader SetRGBAShader;
}
