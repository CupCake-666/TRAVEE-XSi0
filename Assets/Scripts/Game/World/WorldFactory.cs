using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Travee.TicTacToe.World
{
    /// <summary>Construieste obiectele 3D din cod: primitive colorate, materiale, mesh-ul de tor pentru piesa 0.</summary>
    public static class WorldFactory
    {
        static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

        /// <summary>
        /// Materialul URP Lit (asset din proiect) din care se fac toate materialele colorate. Trebuie sa fie un
        /// asset referit din scena: in build, materialul implicit al pipeline-ului nu exista (e doar in editor),
        /// iar un shader cautat dupa nume poate lipsi. Il seteaza TicTacToeBootstrap.
        /// </summary>
        public static Material Template { get; set; }

        /// <summary>Material Lit cu culoarea data. Materialele cu aceeasi culoare sunt refolosite.</summary>
        public static Material Material(Color color, float smoothness = 0.25f)
        {
            if (materials.TryGetValue(color, out var cached) && cached != null) return cached;

            var source = Template;
            if (source == null && GraphicsSettings.currentRenderPipeline != null)
                source = GraphicsSettings.currentRenderPipeline.defaultMaterial;   // merge doar in editor
            Material material;
            if (source != null)
            {
                material = new Material(source);
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError("[TRAVEE] Lipsește materialul de bază al scenei 3D. Rulează TRAVEE → Creează scena X și 0 (3D).");
                    return null;
                }
                material = new Material(shader);
            }
            material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            materials[color] = material;
            return material;
        }

        /// <summary>
        /// Primitiva Unity (cub, cilindru…) cu pozitie/scala locale si culoare.
        /// Colliderul e pastrat doar daca <paramref name="keepCollider"/> (ex. casutele pe care se da click).
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
            if (!keepCollider) Remove(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>
        /// Sterge un obiect sau o componenta. La rulare se foloseste Destroy (sterge la sfarsitul cadrului); in
        /// editor, in afara Play-ului, Destroy nu e permis si se foloseste DestroyImmediate.
        /// </summary>
        public static void Remove(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        public static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Color color,
            bool keepCollider = false) =>
            Primitive(PrimitiveType.Cube, name, parent, localPosition, size, color, keepCollider);

        /// <summary>Obiect gol, folosit ca parinte (ca un "Scene Component" gol din Unreal).</summary>
        public static Transform Group(string name, Transform parent, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        /// <summary>
        /// Material cu o textura de „pete” in doua nuante, generata din cod cu zgomot Perlin: solul nu mai e o
        /// culoare uniforma. Textura se repeta in oglinda (Mirror), deci nu se vad cusaturi intre repetari.
        /// </summary>
        /// <param name="metersPerTile">Cati metri acopera o repetare a texturii.</param>
        /// <param name="size">Latura obiectului pe care se pune (metri), ca sa calculam numarul de repetari.</param>
        public static Material MottledMaterial(Color dark, Color light, float metersPerTile, float size)
        {
            const int resolution = 256;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGB24, true)
            {
                name = "Mottled",
                wrapMode = TextureWrapMode.Mirror,
            };
            var pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                float u = x / (float)resolution, v = y / (float)resolution;
                // Doua „octave” de zgomot: pete mari + detaliu fin.
                float noise = 0.65f * Mathf.PerlinNoise(u * 5f + 3.1f, v * 5f + 7.7f)
                            + 0.35f * Mathf.PerlinNoise(u * 19f + 11.3f, v * 19f + 2.9f);
                pixels[y * resolution + x] = Color.Lerp(dark, light, Mathf.SmoothStep(0.25f, 0.75f, noise));
            }
            texture.SetPixels(pixels);
            texture.Apply();

            var material = new Material(Material(Color.white)) { name = "Mottled", color = Color.white };
            material.mainTexture = texture;
            material.mainTextureScale = Vector2.one * (size / metersPerTile);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
            return material;
        }

        /// <summary>
        /// Mesh de munte: un con (raza 1, inaltime 1) din mai multe inele de varfuri, fiecare varf impins
        /// aleator spre interior/exterior si in sus/jos, ca sa iasa creste neregulate. Fetele au varfurile lor
        /// (nepartajate), deci se vad plate, „low-poly”. Are doua sub-mesh-uri: fetele de jos (stanca) si cele
        /// de deasupra pragului <paramref name="topFrom"/> (zapada); fiecare primeste alt material.
        /// </summary>
        public static Mesh Mountain(System.Random rng, float topFrom, int sides = 13, int rings = 5)
        {
            // points[ring][side]; inelul 0 e baza, ultimul „inel” e varful.
            var points = new Vector3[rings + 1][];
            for (int r = 0; r <= rings; r++)
            {
                points[r] = new Vector3[sides];
                float t = r / (float)rings;                         // 0 = baza, 1 = varf
                float ringRadius = Mathf.Pow(1f - t, 1.25f);        // profil usor concav, ca la un munte
                for (int i = 0; i < sides; i++)
                {
                    float angle = (i + (r % 2) * 0.5f) / sides * Mathf.PI * 2f;     // inelele sunt decalate intre ele
                    float radius = ringRadius * (1f + Jitter(rng, 0.28f));
                    float height = r == 0 ? 0f : t + Jitter(rng, 0.06f);
                    points[r][i] = r == rings
                        ? new Vector3(0f, 1f, 0f)
                        : new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
                }
            }

            var vertices = new List<Vector3>();
            var body = new List<int>();
            var top = new List<int>();
            for (int r = 0; r < rings; r++)
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                AddFace(vertices, body, top, topFrom, points[r + 1][i], points[r][j], points[r][i]);
                if (r < rings - 1) AddFace(vertices, body, top, topFrom, points[r + 1][i], points[r + 1][j], points[r][j]);
            }

            var mesh = new Mesh { name = "Mountain", subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(top, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static float Jitter(System.Random rng, float amount) => ((float)rng.NextDouble() * 2f - 1f) * amount;

        static void AddFace(List<Vector3> vertices, List<int> body, List<int> top, float topFrom, Vector3 a, Vector3 b, Vector3 c)
        {
            var target = (a.y + b.y + c.y) / 3f >= topFrom ? top : body;   // dupa inaltimea medie a fetei
            int index = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            target.Add(index);
            target.Add(index + 1);
            target.Add(index + 2);
        }

        /// <summary>
        /// Mesh de tor (inel) culcat in planul XZ.
        /// <paramref name="radius"/> = raza inelului, <paramref name="thickness"/> = raza "tubului".
        /// </summary>
        public static Mesh Torus(float radius, float thickness, int segments = 40, int sides = 16)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments * Mathf.PI * 2f;          // unghiul in jurul inelului
                var ringDir = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                for (int j = 0; j <= sides; j++)
                {
                    float v = j / (float)sides * Mathf.PI * 2f;         // unghiul in jurul tubului
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
