using UnityEngine;
using UnityEngine.Rendering;

namespace Travee.TicTacToe.World
{
    /// <summary>Cum arata cerul unui peisaj: culorile gradientului, plus (optional) stele si luna.</summary>
    [System.Serializable]
    public struct SkySettings
    {
        public Color Zenith;        // culoarea de sus
        public Color Horizon;       // culoarea de la orizont (si a cetii)
        public Color Ground;        // culoarea de sub orizont
        public int Stars;           // cate stele (0 = zi)
        public Vector3 MoonDirection;   // spre luna, din ochii pacientului (zero = fara luna)

        // Varianta fotografica: o panorama 360° din biblioteca. Daca lipseste, cerul se deseneaza din cod.
        public string Panorama;         // numele imaginii din Assets/Art/Sky
        public float Exposure;          // luminozitatea (1 = cum e fotografia)
        public float Rotation;          // grade: roteste panorama in jurul pacientului
        public Color Tint;              // nuanta; negru (nesetat) = neutru
    }

    /// <summary>
    /// Cerul e o singura imagine „panoramica” (ca o harta a lumii: latime = 360° in jur, inaltime = de jos pana
    /// sus), pusa pe un material Skybox/Panoramic. Imaginea e fie o fotografie 360° din biblioteca
    /// (<see cref="SkySettings.Panorama"/>), fie – daca fotografia lipseste – una desenata din cod pixel cu pixel
    /// (gradient, stele, luna).
    /// </summary>
    public static class Sky
    {
        const int Width = 2048, Height = 1024;
        const float MoonRadius = 2.4f;      // grade
        const float MoonHalo = 9f;          // grade

        static Texture2D texture;
        static Material material;

        /// <summary>Deseneaza cerul si il seteaza ca fundal al scenei. Fara material: fundal intr-o singura culoare.</summary>
        static readonly Color NeutralTint = new Color(0.5f, 0.5f, 0.5f);    // valoarea „fara nuanta” a shader-ului

        public static void Apply(Material skyTemplate, Camera camera, SkySettings sky, Texture2D panorama = null)
        {
            if (skyTemplate == null)
            {
                RenderSettings.skybox = null;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = sky.Horizon;
                return;
            }

            if (material == null) material = new Material(skyTemplate) { name = "Sky" };

            if (panorama != null)
            {
                // Fotografie 360°: o punem direct pe material si ii reglam luminozitatea, rotatia si nuanta.
                material.mainTexture = panorama;
                material.SetFloat("_Exposure", sky.Exposure > 0f ? sky.Exposure : 1f);
                material.SetFloat("_Rotation", sky.Rotation);
                material.SetColor("_Tint", sky.Tint.maxColorComponent > 0f ? sky.Tint : NeutralTint);
            }
            else
            {
                if (texture == null)
                {
                    texture = new Texture2D(Width, Height, TextureFormat.RGB24, false)
                    {
                        name = "SkyTexture",
                        wrapModeU = TextureWrapMode.Repeat,     // imaginea se inchide in cerc, pe orizontala
                        wrapModeV = TextureWrapMode.Clamp,
                    };
                }
                texture.SetPixels32(Paint(sky));
                texture.Apply();
                material.mainTexture = texture;
                material.SetFloat("_Exposure", 1f);
                material.SetFloat("_Rotation", 0f);
                material.SetColor("_Tint", NeutralTint);
            }

            RenderSettings.skybox = material;
            camera.clearFlags = CameraClearFlags.Skybox;
            DynamicGI.UpdateEnvironment();      // reflexiile (ex. pe mare) iau culorile noului cer
        }

        static Color32[] Paint(SkySettings sky)
        {
            var pixels = new Color32[Width * Height];
            bool moon = sky.MoonDirection != Vector3.zero;
            var moonDir = moon ? sky.MoonDirection.normalized : Vector3.up;
            var moonColor = new Color(1f, 0.97f, 0.86f);
            float cosDisc = Mathf.Cos(MoonRadius * Mathf.Deg2Rad);
            float cosHalo = Mathf.Cos(MoonHalo * Mathf.Deg2Rad);

            // Coloana → unghiul in jur (longitudine). Formula e inversa celei din shader-ul Skybox/Panoramic.
            var cosLon = new float[Width];
            var sinLon = new float[Width];
            for (int x = 0; x < Width; x++)
            {
                float lon = (0.5f - (x + 0.5f) / Width) * 2f * Mathf.PI;
                cosLon[x] = Mathf.Cos(lon);
                sinLon[x] = Mathf.Sin(lon);
            }

            for (int y = 0; y < Height; y++)
            {
                float v = (y + 0.5f) / Height;              // 0 = drept in jos, 0,5 = orizont, 1 = drept in sus
                float lat = (v - 0.5f) * Mathf.PI;
                float cosLat = Mathf.Cos(lat), sinLat = Mathf.Sin(lat);

                Color row = v >= 0.5f
                    ? Color.Lerp(sky.Horizon, sky.Zenith, Mathf.Pow((v - 0.5f) * 2f, 0.55f))
                    : Color.Lerp(sky.Horizon, sky.Ground, Mathf.Clamp01((0.5f - v) * 12f));
                Color32 row32 = row;

                for (int x = 0; x < Width; x++)
                {
                    if (!moon)
                    {
                        pixels[y * Width + x] = row32;
                        continue;
                    }

                    // Cat de aproape e directia acestui pixel de directia lunii (produs scalar = cosinusul unghiului).
                    float dot = cosLon[x] * cosLat * moonDir.x + sinLat * moonDir.y + sinLon[x] * cosLat * moonDir.z;
                    if (dot < cosHalo)
                    {
                        pixels[y * Width + x] = row32;
                    }
                    else if (dot >= cosDisc)
                    {
                        pixels[y * Width + x] = moonColor;
                    }
                    else
                    {
                        float halo = Mathf.InverseLerp(cosHalo, cosDisc, dot);
                        pixels[y * Width + x] = Color.Lerp(row, moonColor, halo * halo * 0.45f);
                    }
                }
            }

            if (sky.Stars > 0) PaintStars(pixels, sky.Stars);
            return pixels;
        }

        /// <summary>Stele: puncte albe la pozitii aleatoare deasupra orizontului; cele mari au si o „cruce” palida.</summary>
        static void PaintStars(Color32[] pixels, int count)
        {
            var rng = new System.Random(7);     // aceeasi samanta → acelasi cer de fiecare data
            for (int i = 0; i < count; i++)
            {
                int x = rng.Next(Width);
                // Intre 3° si 80° deasupra orizontului (mai sus imaginea panoramica deformeaza punctele).
                float elevation = Mathf.Lerp(3f, 80f, (float)rng.NextDouble());
                int y = Mathf.Clamp((int)((0.5f + elevation / 180f) * Height), 0, Height - 1);
                float brightness = Mathf.Lerp(0.45f, 1f, (float)rng.NextDouble());
                bool big = rng.NextDouble() < 0.18;

                Blend(pixels, x, y, brightness);
                if (!big) continue;
                Blend(pixels, x + 1, y, brightness * 0.5f);
                Blend(pixels, x - 1, y, brightness * 0.5f);
                Blend(pixels, x, y + 1, brightness * 0.5f);
                Blend(pixels, x, y - 1, brightness * 0.5f);
            }
        }

        static void Blend(Color32[] pixels, int x, int y, float amount)
        {
            x = (x + Width) % Width;
            if (y < 0 || y >= Height) return;
            int i = y * Width + x;
            pixels[i] = Color32.Lerp(pixels[i], new Color32(255, 250, 235, 255), amount);
        }
    }
}
