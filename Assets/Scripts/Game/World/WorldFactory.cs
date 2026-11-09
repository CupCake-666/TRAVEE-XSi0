using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Travee.TicTacToe.World
{
    /// <summary>Construiește obiectele 3D din cod: primitive colorate, materiale, mesh-ul de tor pentru piesa 0.</summary>
    public static class WorldFactory
    {
        static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

        /// <summary>
        /// Material Lit al pipeline-ului curent (URP), cu culoarea dată. Pornim de la materialul implicit al
        /// pipeline-ului ca să fim siguri că shader-ul există și în build. Materialele cu aceeași culoare sunt refolosite.
        /// </summary>
        public static Material Material(Color color, float smoothness = 0.25f)
        {
            if (materials.TryGetValue(color, out var cached) && cached != null) return cached;

            var source = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.defaultMaterial
                : null;
            var material = source != null ? new Material(source) : new Material(Shader.Find("Standard"));
            material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            materials[color] = material;
            return material;
        }

        /// <summary>
        /// Primitivă Unity (cub, cilindru…) cu poziție/scală locale și culoare.
        /// Colliderul e păstrat doar dacă <paramref name="keepCollider"/> (ex. căsuțele pe care se dă click).
        /// </summary>
        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Color color, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = Material(color);
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Color color,
            bool keepCollider = false) =>
            Primitive(PrimitiveType.Cube, name, parent, localPosition, size, color, keepCollider);

        /// <summary>Obiect gol, folosit ca părinte (ca un "Scene Component" gol din Unreal).</summary>
        public static Transform Group(string name, Transform parent, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        /// <summary>
        /// Mesh de tor (inel) culcat în planul XZ.
        /// <paramref name="radius"/> = raza inelului, <paramref name="thickness"/> = raza "tubului".
        /// </summary>
        public static Mesh Torus(float radius, float thickness, int segments = 40, int sides = 16)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments * Mathf.PI * 2f;          // unghiul în jurul inelului
                var ringDir = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                for (int j = 0; j <= sides; j++)
                {
                    float v = j / (float)sides * Mathf.PI * 2f;         // unghiul în jurul tubului
                    var normal = ringDir * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    vertices.Add(ringDir * radius + normal * thickness);
                    normals.Add(normal);
                }
            }

            int stride = sides + 1;
            for (int i = 0; i < segments; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * stride + j, b = a + stride, c = a + 1, d = b + 1;
                triangles.AddRange(new[] { a, c, b, b, c, d });
            }

            var mesh = new Mesh { name = "Torus" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
