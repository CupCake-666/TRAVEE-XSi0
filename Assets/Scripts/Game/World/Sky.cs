using UnityEngine;
using UnityEngine.Rendering;

namespace Travee.TicTacToe.World
{
    /// <summary>Cum arată cerul unui peisaj: culorile gradientului, plus (opțional) stele și lună.</summary>
    public struct SkySettings
    {
        public Color Zenith;        // culoarea de sus
        public Color Horizon;       // culoarea de la orizont (și a ceții)
        public Color Ground;        // culoarea de sub orizont
        public int Stars;           // câte stele (0 = zi)
        public Vector3 MoonDirection;   // spre lună, din ochii pacientului (zero = fără lună)

        // Varianta fotografică: o panoramă 360° din bibliotecă. Dacă lipsește, cerul se desenează din cod.
        public string Panorama;         // numele imaginii din Assets/Art/Sky
        public float Exposure;          // luminozitatea (1 = cum e fotografia)
        public float Rotation;          // grade: rotește panorama în jurul pacientului
        public Color Tint;              // nuanță; negru (nesetat) = neutru
    }

    /// <summary>
    /// Cerul e o singură imagine „panoramică” (ca o hartă a lumii: lățime = 360° în jur, înălțime = de jos până
    /// sus), pusă pe un material Skybox/Panoramic. Imaginea e fie o fotografie 360° din bibliotecă
    /// (<see cref="SkySettings.Panorama"/>), fie – dacă fotografia lipsește – una desenată din cod pixel cu pixel
    /// (gradient, stele, lună).
    /// </summary>
    public static class Sky
    {
        const int Width = 2048, Height = 1024;
        const float MoonRadius = 2.4f;      // grade
        const float MoonHalo = 9f;          // grade

        static Texture2D texture;
        static Material material;

        /// <summary>Desenează cerul și îl setează ca fundal al scenei. Fără material: fundal într-o singură culoare.</summary>
        static readonly Color NeutralTint = new Color(0.5f, 0.5f, 0.5f);    // valoarea „fără nuanță” a shader-ului

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
                // Fotografie 360°: o punem direct pe material și îi reglăm luminozitatea, rotația și nuanța.
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
                        wrapModeU = TextureWrapMode.Repeat,     // imaginea se închide în cerc, pe orizontală
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

            // Coloana → unghiul în jur (longitudine). Formula e inversa celei din shader-ul Skybox/Panoramic.
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
                float v = (y + 0.5f) / Height;              // 0 = drept în jos, 0,5 = orizont, 1 = drept în sus
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

                    // Cât de aproape e direcția acestui pixel de direcția lunii (produs scalar = cosinusul unghiului).
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

        /// <summary>Stele: puncte albe la poziții aleatoare deasupra orizontului; cele mari au și o „cruce” palidă.</summary>
        static void PaintStars(Color32[] pixels, int count)
        {
            var rng = new System.Random(7);     // aceeași sămânță → același cer de fiecare dată
            for (int i = 0; i < count; i++)
            {
                int x = rng.Next(Width);
                // Între 3° și 80° deasupra orizontului (mai sus imaginea panoramică deformează punctele).
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
