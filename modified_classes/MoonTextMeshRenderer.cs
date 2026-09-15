using System;
using UnityEngine;

namespace CatlikeCoding.TextBox {
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [ExecuteInEditMode]
    public class MoonTextMeshRenderer : TextRenderer {
        protected void OnDestroy() {
            UnityEngine.Object.Destroy(mesh);
            mesh = null;
        }

        public void Start() {
            if (Application.isPlaying) {
                GetComponent<Renderer>().material.SetFloat("_TxtTime", 999999f);
                TransparencyAnimator.Register(transform);
            } else {
                GetComponent<Renderer>().sharedMaterial.SetFloat("_TxtTime", 999999f);
            }
        }

        public override void Prepare() {
            if (mesh == null) {
                GetComponent<MeshFilter>().mesh = mesh = new Mesh();
                mesh.hideFlags = HideFlags.HideAndDontSave;
                mesh.name = "Text Box Mesh";
            }

            currentVertexIndex = 0;
            if (lastRendererCharCount < renderedCharCount) {
                if (vertices == null || vertices.Length < renderedCharCount * 4) {
                    int num;
                    int i;
                    int num2;
                    if (vertices == null) {
                        num = ((renderedCharCount - 1) / chunkSize + 1) * chunkSize;
                        i = 0;
                        num2 = 0;
                    } else {
                        num = vertices.Length / 4 + ((renderedCharCount - lastRendererCharCount - 1) / chunkSize + 1) * chunkSize;
                        i = vertices.Length;
                        num2 = triangles.Length;
                    }

                    Array.Resize(ref vertices, num * 4);
                    Array.Resize(ref colors, vertices.Length);
                    Array.Resize(ref uv, vertices.Length);
                    Array.Resize(ref uv2, vertices.Length);
                    Array.Resize(ref triangles, num * 6);
                    Array.Resize(ref normals, num * 4);
                    while (i < vertices.Length) {
                        triangles[num2] = i;
                        triangles[num2 + 1] = i + 1;
                        triangles[num2 + 2] = i + 2;
                        triangles[num2 + 3] = i;
                        triangles[num2 + 4] = i + 2;
                        triangles[num2 + 5] = i + 3;
                        i += 4;
                        num2 += 6;
                    }

                    meshResized = true;
                }
            } else if (lastRendererCharCount > renderedCharCount) {
                var j = renderedCharCount * 4;
                var num3 = lastRendererCharCount * 4;
                while (j < num3) {
                    vertices[j] = vertices[j + 1] = vertices[j + 2] = vertices[j + 3] = hidden;
                    j += 4;
                }
            }
        }

        public override void Add(CharMetaData meta, Vector2 offset) {
            var bitmapFontChar = meta.font[meta.id];
            var num = currentVertexIndex;
            Vector2 vector;
            vector.x = bitmapFontChar.uMin;
            vector.y = bitmapFontChar.vMax;
            uv2[num] = vector;
            vector.x = bitmapFontChar.uMax;
            uv2[num + 1] = vector;
            vector.y = bitmapFontChar.vMin;
            uv2[num + 2] = vector;
            vector.x = bitmapFontChar.uMin;
            uv2[num + 3] = vector;
            uv[num] = new Vector2(0f, 1f);
            uv[num + 1] = new Vector2(1f, 1f);
            uv[num + 2] = new Vector2(1f, 0f);
            uv[num + 3] = new Vector2(0f, 0f);
            var num2 = Mathf.Max(0f, (float)meta.unstyledIndex / FadeSpread);
            normals[num] = normals[num + 1] = normals[num + 2] = normals[num + 3] = Vector3.right * num2;
            colors[num] = colors[num + 1] = colors[num + 2] = colors[num + 3] = meta.color;
            Vector3 vector2;
            var num3 = vector2.x = offset.x + meta.scale * bitmapFontChar.xOffset + meta.positionInBox.x;
            vector2.y = offset.y + meta.scale * bitmapFontChar.yOffset + meta.positionInBox.y;
            vector2.z = 0f;
            vertices[num] = vector2;
            vector2.x += meta.scale * bitmapFontChar.width;
            vertices[num + 1] = vector2;
            vector2.y -= meta.scale * bitmapFontChar.height;
            vertices[num + 2] = vector2;
            vector2.x = num3;
            vertices[num + 3] = vector2;
            currentVertexIndex += 4;
        }

        public override void Apply() {
            if (renderedCharCount == 0) {
                gameObject.SetActive(false);
            } else {
                mesh.vertices = vertices;
                mesh.colors32 = colors;
                mesh.uv = uv;
                mesh.uv2 = uv2;
                mesh.normals = normals;
                if (meshResized) {
                    mesh.triangles = triangles;
                }

                mesh.RecalculateBounds();
                gameObject.SetActive(true);
            }

            lastRendererCharCount = renderedCharCount;
        }

        protected static Vector3 hidden = Vector3.zero;

        public int chunkSize = 1;

        protected Mesh mesh;

        protected Vector3[] vertices;

        protected Color32[] colors;

        protected Vector2[] uv;

        protected Vector2[] uv2;

        protected Vector3[] normals;

        protected int[] triangles;

        protected bool meshResized;

        protected int lastRendererCharCount;

        protected int currentVertexIndex;

        public float FadeSpread = 5f;
    }
}
