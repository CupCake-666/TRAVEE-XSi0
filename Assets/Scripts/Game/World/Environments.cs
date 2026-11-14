using UnityEngine;

namespace Travee.TicTacToe.World
{
    /// <summary>Peisajul in care se joaca. Se alege din meniu si se poate schimba oricand.</summary>
    public enum WorldTheme
    {
        Forest,     // poiana in padure, ziua
        Mountain,   // platou la munte, noaptea, cu stele si foc de tabara
        Beach,      // plaja cu palmieri si mare
    }

    /// <summary>Tot ce difera de la un peisaj la altul in afara de obiecte: cer, lumina, ceata, sunet.</summary>
    [System.Serializable]      // apare in Inspector (in componenta ThemeSettings) si se salveaza in scena
    public struct ThemeLook
    {
        public SkySettings Sky;
        public Color SunColor;
        public float SunIntensity;
        public Vector3 SunAngles;           // rotatia luminii (grade)
        public Color AmbientSky, AmbientEquator, AmbientGround;
        public float FogStart, FogEnd;      // metri; FogEnd = 0 → fara ceata
        public string AmbientSound;         // numele clipului din Assets/Audio/Ambient
    }

    /// <summary>
    /// Construieste obiectele unui peisaj din modele CC0 (Quaternius Stylized Nature MegaKit si Ultimate Nature
    /// Pack, plus cateva obiecte Kenney) si din forme generate din cod (munti, mare).
    /// Pacientul sta in origine si priveste spre +Z; zona de joc (tabla si traseul personajului) ramane libera.
    /// Pozitiile „imprastiate” vin dintr-un generator cu samanta fixa: peisajul arata la fel la fiecare rulare.
    /// </summary>
    public sealed class EnvironmentBuilder
    {
        static readonly string[] Pebbles = { "Pebble_Round_1", "Pebble_Round_2", "Pebble_Round_3", "Pebble_Round_4",
            "Pebble_Square_1", "Pebble_Square_2", "Pebble_Square_3" };
        static readonly string[] Rocks = { "Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3" };
        static readonly string[] Pines = { "Pine_1", "Pine_2", "Pine_3", "Pine_4", "Pine_5" };

        readonly GameAssets assets;
        readonly Transform root;
        readonly System.Collections.Generic.List<Vector4> blocked = new System.Collections.Generic.List<Vector4>();   // x, _, z, raza
        System.Random rng;

        EnvironmentBuilder(GameAssets assets, Transform root)
        {
            this.assets = assets;
            this.root = root;
        }

        public static string DisplayName(WorldTheme theme)
        {
            switch (theme)
            {
                case WorldTheme.Mountain: return "Munte, noaptea";
                case WorldTheme.Beach: return "Plajă";
                default: return "Pădure";
            }
        }

        /// <summary>Creeaza obiectele peisajului sub <paramref name="root"/> si intoarce „aspectul” lui.</summary>
        public static ThemeLook Build(WorldTheme theme, GameAssets assets, Transform root)
        {
            var builder = new EnvironmentBuilder(assets, root) { rng = new System.Random(100 + (int)theme) };
            if (assets == null) return builder.Plain();     // fara biblioteca de modele: doar o podea si un perete
            switch (theme)
            {
                case WorldTheme.Mountain: builder.Mountain(); break;
                case WorldTheme.Beach: builder.Beach(); break;
                default: builder.Forest(); break;
            }
            return Look(theme);
        }

        /// <summary>
        /// Obiectul pe care urca personajul ca sa ajunga la tabla, potrivit peisajului: o buturuga mare in
        /// padure, o lespede de stanca la munte si pe plaja, un podium de lemn in camera. Ocupa dreptunghiul
        /// dat (centru si jumatati de latime/adancime) si are inaltimea <paramref name="height"/>.
        /// </summary>
        public static void BuildStand(WorldTheme theme, GameAssets assets, Transform root, Vector3 center,
            float halfWidth, float halfDepth, float height)
        {
            var builder = new EnvironmentBuilder(assets, root) { rng = new System.Random(7) };
            // Obiectul e mai mare decat zona pe care se sta: marginile lui rotunjite raman in afara ei.
            // Spre tabla nu are voie sa iasa (ar trece prin ea), asa ca surplusul e mutat spre spate.
            var size = new Vector3(halfWidth * 2f + 0.7f, height, halfDepth * 2f + 0.4f);
            center += Vector3.forward * 0.17f;
            Transform stand = null;
            if (theme == WorldTheme.Forest) stand = builder.FittedProp("TreeStump_Moss", center, size, 0f);
            else stand = builder.FittedProp("Rock_Medium_2", center, size + Vector3.up * 0.05f, 0f);
            if (stand != null) return;

            // Model lipsa: podium de lemn cu cate o treapta pe laterale.
            var wood = new Color(0.56f, 0.41f, 0.27f);
            var step = WorldFactory.Group("Stand", root, center);
            WorldFactory.Box("Top", step, new Vector3(0f, height / 2f, 0f), new Vector3(halfWidth * 2f, height, halfDepth * 2f), wood);
            foreach (float side in new[] { -1f, 1f })
                WorldFactory.Box("Side", step, new Vector3(side * (halfWidth + 0.15f), height / 4f, 0f),
                    new Vector3(0.3f, height / 2f, halfDepth * 2f), wood);
        }

        /// <summary>
        /// „Aspectul” unui peisaj (cer, lumina, ceata, sunet), separat de obiectele lui. E separat pentru ca
        /// obiectele pot veni din doua locuri – construite din cod sau salvate in scena si aranjate de mana –
        /// dar aspectul se aplica la fel in ambele cazuri.
        /// </summary>
        public static ThemeLook Look(WorldTheme theme)
        {
            switch (theme)
            {
                case WorldTheme.Mountain:
                    return new ThemeLook
                    {
                        Sky = new SkySettings
                        {
                            Zenith = new Color(0.01f, 0.02f, 0.08f), Horizon = new Color(0.10f, 0.12f, 0.20f),
                            Ground = new Color(0.03f, 0.04f, 0.08f),
                            Stars = 1500, MoonDirection = new Vector3(-0.50f, 0.42f, 0.76f),
                            Panorama = "sky_night_stars", Exposure = 0.55f, Rotation = 200f,
                            Tint = new Color(0.42f, 0.47f, 0.62f),
                        },
                        // „Lumina lunii”: rece si slaba. Vine din spatele pacientului, ca tabla sa fie luminata din fata.
                        SunColor = new Color(0.62f, 0.72f, 1f), SunIntensity = 0.8f, SunAngles = new Vector3(42f, -22f, 0f),
                        AmbientSky = new Color(0.20f, 0.25f, 0.42f), AmbientEquator = new Color(0.16f, 0.19f, 0.30f),
                        AmbientGround = new Color(0.07f, 0.08f, 0.12f),
                        FogStart = 35f, FogEnd = 260f,
                        AmbientSound = "night_crickets",
                    };
                case WorldTheme.Beach:
                    return new ThemeLook
                    {
                        Sky = new SkySettings
                        {
                            Zenith = new Color(0.16f, 0.50f, 0.95f), Horizon = new Color(0.76f, 0.86f, 0.96f),
                            Ground = new Color(0.30f, 0.62f, 0.76f),
                            Panorama = "sky_day_clouds", Exposure = 1.1f, Rotation = 140f,
                        },
                        SunColor = new Color(1f, 0.97f, 0.88f), SunIntensity = 1.5f, SunAngles = new Vector3(52f, 28f, 0f),
                        AmbientSky = new Color(0.66f, 0.80f, 0.95f), AmbientEquator = new Color(0.70f, 0.70f, 0.62f),
                        AmbientGround = new Color(0.50f, 0.44f, 0.32f),
                        FogStart = 80f, FogEnd = 600f,
                        AmbientSound = "beach_waves",
                    };
                default:
                    return new ThemeLook
                    {
                        Sky = new SkySettings
                        {
                            Zenith = new Color(0.26f, 0.52f, 0.90f), Horizon = new Color(0.74f, 0.83f, 0.93f),
                            Ground = new Color(0.34f, 0.48f, 0.28f),
                            Panorama = "sky_day_clouds", Exposure = 1.0f, Rotation = 0f,
                        },
                        SunColor = new Color(1f, 0.95f, 0.84f), SunIntensity = 1.3f, SunAngles = new Vector3(48f, -30f, 0f),
                        AmbientSky = new Color(0.62f, 0.72f, 0.86f), AmbientEquator = new Color(0.52f, 0.60f, 0.52f),
                        AmbientGround = new Color(0.28f, 0.34f, 0.24f),
                        FogStart = 18f, FogEnd = 85f,
                        AmbientSound = "forest_birds",
                    };
            }
        }

        // ------------------------------------------------------------------ peisajele

        void Forest()
        {
            Ground(new Color(0.33f, 0.52f, 0.20f), new Color(0.42f, 0.60f, 0.24f));
            var pond = new Vector3(-6.5f, 0, -6.5f);
            var birches = new Vector3(7.5f, 0, -6f);
            Block(pond, 5.2f);          // locuri rezervate: imprastierea de mai jos le ocoleste
            Block(birches, 4.5f);

            // Copacii „vedeta”, pusi de mana ca sa incadreze tabla.
            Prop("CommonTree_2", new Vector3(-4.2f, 0, 5.4f), 8.0f, 20);
            Prop("CommonTree_3", new Vector3(4.4f, 0, 6.0f), 8.5f, 200);
            Prop("CommonTree_1", new Vector3(-6.0f, 0, 2.4f), 7.5f, 90);
            Prop("CommonTree_4", new Vector3(6.2f, 0, 2.2f), 7.0f, 310);
            Prop("CommonTree_5", new Vector3(0.8f, 0, 10.5f), 9.5f, 45);

            Scatter(new[] { "CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4", "CommonTree_5",
                "CommonTree_1", "CommonTree_3", "TwistedTree_2", "TwistedTree_4", "Pine_2", "Pine_4" },   // putini copaci rosii, ca accent
                count: 85, minRadius: 8f, maxRadius: 40f, minSize: 6.5f, maxSize: 11f, clearRadius: 7f);
            Scatter(new[] { "Bush_Common_Flowers", "Bush_Common_Flowers", "Bush_Common_Flowers", "Bush_Common" }, 34, 4f, 18f,
                0.9f, 1.8f, 3.7f);
            Scatter(new[] { "Fern_1", "Plant_1", "Plant_1_Big", "Plant_7", "Plant_7_Big" }, 60, 2.6f, 16f, 0.5f, 1.0f, 3.2f);
            Scatter(Rocks, 12, 4f, 16f, 0.6f, 1.8f, 3.6f);
            Scatter(new[] { "Grass_Common_Short", "Grass_Common_Tall" }, 240, 1.3f, 14f, 0.3f, 0.6f, 0f, smallPlants: true);
            Scatter(new[] { "Grass_Wispy_Short", "Grass_Wispy_Tall" }, 40, 2f, 14f, 0.3f, 0.6f, 0f, true);
            Scatter(new[] { "Clover_1", "Clover_2" }, 50, 1.3f, 9f, 0.3f, 0.5f, 0f, true);
            Scatter(new[] { "Flower_3_Group", "Flower_3_Single", "Flower_4_Group", "Flower_4_Single" }, 70, 1.6f, 12f,
                0.3f, 0.55f, 0f, true);
            Scatter(new[] { "Mushroom_Common", "Mushroom_Laetiporus" }, 16, 2.2f, 10f, 0.2f, 0.35f, 0f, true);
            Scatter(Pebbles, 30, 1.5f, 10f, 0.12f, 0.3f, 0f, true);

            // Un mic loc de tabara in spate, plus un bustean langa zona de joc.
            Prop("tent_detailedOpen", new Vector3(-5.8f, 0, 9.0f), 2.8f, 140);
            Prop("campfire_stones", new Vector3(-3.9f, 0, 8.0f), 0.9f, 0);
            Prop("WoodLog_Moss", new Vector3(3.3f, 0, 4.0f), 1.8f, 35);
            Prop("TreeStump", new Vector3(-3.2f, 0, 3.6f), 0.7f, 0);
            Prop("WoodLog", new Vector3(-2.9f, 0, 9.2f), 1.5f, 100);
            Flame(new Vector3(-3.9f, 0.12f, 8.0f), 0.55f);

            // Carare de pietre de la zona de joc spre tabara.
            var path = new[] { new Vector3(-2.4f, 0, 4.4f), new Vector3(-2.8f, 0, 5.2f), new Vector3(-3.0f, 0, 6.0f),
                new Vector3(-3.3f, 0, 6.7f), new Vector3(-3.4f, 0, 7.3f) };
            for (int i = 0; i < path.Length; i++)
                Prop(i % 2 == 0 ? "RockPath_Round_Wide" : "RockPath_Round_Small_2", path[i], 0.65f, Range(0f, 360f));

            // Un iaz cu nuferi in spatele pacientului (se vede cand se intoarce), cu o salcie si pietre pe mal.
            Pond(pond, 3.4f, new Color(0.16f, 0.42f, 0.50f));
            for (int i = 0; i < 11; i++)
                Prop("Lilypad", pond + Polar(Range(0f, 360f), Range(0.4f, 2.8f)) + Vector3.up * 0.035f, Range(0.45f, 0.8f), Range(0f, 360f));
            for (int i = 0; i < 12; i++)
                Prop(i % 2 == 0 ? "Rock_Moss_1" : "Rock_Moss_3", pond + Polar(i * 30f + Range(-8f, 8f), Range(3.5f, 4.0f)),
                    Range(0.5f, 1.1f), Range(0f, 360f));
            Prop("Willow_1", pond + new Vector3(-4.6f, 0, -1.5f), 9f, 40);
            Prop("Willow_2", pond + new Vector3(2.5f, 0, -5.0f), 8f, 200);
            for (int i = 0; i < 14; i++)
                Prop("Grass_Common_Tall", pond + Polar(Range(0f, 360f), Range(3.6f, 4.6f)), Range(0.7f, 1.1f), Range(0f, 360f));

            // Un palc de mesteceni si tufe cu fructe, tot in spate / lateral.
            for (int i = 0; i < 9; i++)
                Prop("BirchTree_" + (1 + i % 3), birches + Polar(i * 40f, Range(0.5f, 4.0f)), Range(6f, 9f), Range(0f, 360f));
            Scatter(new[] { "BushBerries_1", "BushBerries_2" }, 16, 4.5f, 16f, 0.8f, 1.4f, 3.8f);
            Scatter(new[] { "Petal_1", "Petal_2", "Petal_3" }, 60, 1.5f, 9f, 0.10f, 0.18f, 0f, true);
        }

        void Mountain()
        {
            Ground(new Color(0.16f, 0.24f, 0.20f), new Color(0.22f, 0.30f, 0.24f));

            // Lantul de munti din departare: generati din cod, cu creste neregulate si varf inzapezit.
            var rock = new Color(0.20f, 0.24f, 0.36f);
            var snow = new Color(0.85f, 0.90f, 1f);
            for (int i = 0; i < 26; i++)
            {
                float angle = i / 26f * 360f + Range(-5f, 5f);
                float distance = Range(70f, 150f);
                float height = Range(26f, 52f) * (distance / 90f);
                Peak(Polar(angle, distance), height, height * Range(0.8f, 1.25f), rock, snow, 0.55f);
            }

            Prop("Pine_3", new Vector3(-4.6f, 0, 5.8f), 9.0f, 0);
            Prop("Pine_1", new Vector3(-6.6f, 0, 3.0f), 8.0f, 120);
            Prop("Pine_5", new Vector3(6.2f, 0, 8.0f), 9.5f, 60);
            Scatter(Pines, 55, 8f, 36f, 6.0f, 11f, 7.5f);
            Scatter(Rocks, 26, 4f, 24f, 0.8f, 3.2f, 3.8f);
            Scatter(Pebbles, 40, 1.5f, 10f, 0.15f, 0.4f, 0f, true);
            Scatter(new[] { "Grass_Wispy_Short", "Grass_Wispy_Tall" }, 120, 1.4f, 12f, 0.3f, 0.55f, 0f, true);
            Scatter(new[] { "Fern_1", "Plant_7" }, 22, 3.8f, 14f, 0.6f, 1.2f, 3.6f);

            // Focul de tabara: lumineaza cald zona de joc si palpaie.
            var fire = new Vector3(3.2f, 0, 5.0f);
            Prop("campfire_logs", fire, 0.95f, 15);
            Prop("tent_smallOpen", new Vector3(5.2f, 0, 6.6f), 2.3f, 215);
            Prop("WoodLog", new Vector3(2.2f, 0, 3.7f), 1.4f, 100);
            var flame = WorldFactory.Group("FireLight", root, fire + Vector3.up * 0.5f).gameObject.AddComponent<Light>();
            flame.type = LightType.Point;
            flame.color = new Color(1f, 0.58f, 0.22f);
            flame.range = 12f;
            flame.intensity = 7f;
            flame.shadows = LightShadows.None;
            flame.gameObject.AddComponent<Flicker>();
            Flame(fire + Vector3.up * 0.12f, 0.7f);
            for (int i = 0; i < 9; i++)     // cerc de pietre in jurul focului
                Prop(Pebbles[i % Pebbles.Length], fire + Polar(i * 40f, 0.75f), 0.28f, Range(0f, 360f));
            Prop("WoodLog", fire + new Vector3(1.5f, 0, 0.6f), 1.3f, 20);

            // Mai departe si mai sus e zapada: brazi si stanci inzapezite, de jur imprejur.
            Scatter(new[] { "PineTree_Snow_1", "PineTree_Snow_2", "PineTree_Snow_3" }, 40, 16f, 46f, 6f, 11f, 7.5f);
            Scatter(new[] { "Rock_Snow_1", "Rock_Snow_2", "Rock_Snow_3" }, 22, 7f, 30f, 0.9f, 3.0f, 4f);

            // Licurici in jurul zonei de joc.
            Fireflies(55, new Vector3(0f, 1.1f, 1f), 3f, 13f);
        }

        void Beach()
        {
            Ground(new Color(0.90f, 0.80f, 0.58f), new Color(0.96f, 0.88f, 0.66f));

            // Marea: o placa albastra, lucioasa, care incepe la 10 m in fata pacientului si vine/pleaca usor (maree),
            // cu o dunga de spuma pe margine.
            const float shore = 10f;
            var sea = WorldFactory.Group("Sea", root, new Vector3(0, 0, shore));
            var waterColor = new Color(0.08f, 0.50f, 0.70f);
            var water = WorldFactory.Box("Water", sea, new Vector3(0, 0.02f, 400f), new Vector3(2400f, 0.04f, 800f), waterColor);
            // Material propriu, foarte lucios (smoothness 0,92), ca apa sa reflecte cerul.
            water.GetComponent<Renderer>().sharedMaterial = new Material(WorldFactory.Material(waterColor)) { name = "Water" };
            water.GetComponent<Renderer>().sharedMaterial.SetFloat("_Smoothness", 0.92f);
            WorldFactory.Box("Shallows", sea, new Vector3(0, 0.022f, 2.2f), new Vector3(2400f, 0.04f, 4.4f),
                new Color(0.30f, 0.74f, 0.78f));
            WorldFactory.Box("Foam", sea, new Vector3(0, 0.025f, 0.1f), new Vector3(2400f, 0.045f, 0.45f),
                new Color(0.97f, 0.99f, 1f));
            sea.gameObject.AddComponent<Tide>();

            // Insule in larg.
            var island = new Color(0.30f, 0.50f, 0.34f);
            var islandTop = new Color(0.40f, 0.60f, 0.38f);
            Peak(new Vector3(-90f, 0, 210f), 30f, 80f, island, islandTop, 0.45f);
            Peak(new Vector3(-30f, 0, 260f), 18f, 55f, island, islandTop, 0.45f);
            Peak(new Vector3(120f, 0, 230f), 36f, 90f, island, islandTop, 0.45f);

            Prop("PalmTree_1", new Vector3(-4.0f, 0, 5.6f), 7.5f, 30);
            Prop("PalmTree_3", new Vector3(4.5f, 0, 6.4f), 7.0f, 250);
            Prop("PalmTree_4", new Vector3(-6.6f, 0, 8.2f), 8.0f, 120);
            Prop("PalmTree_2", new Vector3(6.8f, 0, 3.2f), 6.5f, 0);
            Prop("PalmTree_3", new Vector3(-6.0f, 0, 2.0f), 5.5f, 80);
            Scatter(new[] { "PalmTree_1", "PalmTree_2", "PalmTree_3", "PalmTree_4" }, 40, 7.5f, 34f, 5f, 9f, 7f,
                maxZ: shore - 1.5f);
            Scatter(new[] { "Plant_1_Big", "Plant_7_Big", "Plant_1", "Plant_7", "Fern_1", "Bush_Common_Flowers" },
                40, 3.8f, 20f, 0.6f, 1.4f, 3.6f, maxZ: shore - 1f);
            Scatter(new[] { "Grass_Wispy_Short", "Grass_Wispy_Tall" }, 70, 2f, 14f, 0.3f, 0.6f, 0f, true, shore - 1f);
            Scatter(Rocks, 10, 4f, 16f, 0.5f, 1.6f, 3.6f, maxZ: shore + 4f);
            Scatter(Pebbles, 60, 1.5f, 12f, 0.12f, 0.35f, 0f, true, shore + 1f);

            // Stanci in apa si o canoe trasa pe mal.
            Prop("Rock_Medium_1", new Vector3(-8f, -0.3f, 14f), 3.2f, 40);
            Prop("Rock_Medium_3", new Vector3(10f, -0.3f, 16f), 4.0f, 0);
            Prop("Rock_Medium_2", new Vector3(3.5f, -0.3f, 21f), 2.6f, 200);
            Prop("canoe", new Vector3(3.0f, 0, 8.0f), 2.8f, 65);
            Prop("canoe_paddle", new Vector3(2.0f, 0.02f, 7.2f), 1.4f, 20);
            Prop("canoe", new Vector3(-5.5f, -0.08f, 12.2f), 2.8f, 100);          // a doua, pe apa, langa mal

            // Un loc de foc pe nisip, cu busteni de stat.
            var bonfire = new Vector3(-3.6f, 0, 7.0f);
            Prop("campfire_stones", bonfire, 0.9f, 0);
            Flame(bonfire + Vector3.up * 0.12f, 0.5f);
            Prop("WoodLog", bonfire + new Vector3(-1.3f, 0, 0.4f), 1.4f, 80);
            Prop("WoodLog", bonfire + new Vector3(0.6f, 0, 1.3f), 1.3f, 160);

            // Interiorul insulei, in spatele pacientului: dealuri verzi, stanci cu muschi, iarba de duna.
            var hill = new Color(0.26f, 0.48f, 0.26f);
            var hillTop = new Color(0.36f, 0.58f, 0.30f);
            Peak(new Vector3(-60f, 0, -190f), 42f, 120f, hill, hillTop, 0.4f);
            Peak(new Vector3(80f, 0, -230f), 60f, 150f, hill, hillTop, 0.4f);
            Peak(new Vector3(-200f, 0, -90f), 38f, 110f, hill, hillTop, 0.4f);
            Peak(new Vector3(210f, 0, -60f), 34f, 100f, hill, hillTop, 0.4f);
            Scatter(new[] { "Rock_Moss_1", "Rock_Moss_2", "Rock_Moss_3" }, 14, 5f, 22f, 0.6f, 2.0f, 4f, maxZ: shore - 2f);
            Scatter(new[] { "Wheat" }, 70, 2.5f, 18f, 0.5f, 0.9f, 0f, true, shore - 1.5f);
            Scatter(new[] { "BushBerries_1", "BushBerries_2" }, 14, 5f, 20f, 0.8f, 1.5f, 4f, maxZ: shore - 2f);
        }

        /// <summary>Decor minim, folosit doar daca lipseste biblioteca de asset-uri (nu se poate alege din meniu).</summary>
        ThemeLook Plain()
        {
            var wall = new Color(0.86f, 0.90f, 0.93f);
            WorldFactory.Box("Floor", root, new Vector3(0, -0.05f, 1f), new Vector3(8f, 0.1f, 8f), new Color(0.78f, 0.72f, 0.64f));
            WorldFactory.Box("Wall", root, new Vector3(0, 1.5f, 2.6f), new Vector3(8f, 3f, 0.1f), wall);
            return new ThemeLook
            {
                Sky = new SkySettings { Zenith = wall, Horizon = wall, Ground = wall },
                SunColor = new Color(1f, 0.96f, 0.9f), SunIntensity = 1.1f, SunAngles = new Vector3(50f, -25f, 0f),
                AmbientSky = new Color(0.55f, 0.57f, 0.62f), AmbientEquator = new Color(0.55f, 0.57f, 0.62f),
                AmbientGround = new Color(0.55f, 0.57f, 0.62f),
            };
        }

        // ------------------------------------------------------------------ unelte

        /// <summary>Solul: un plan de 1 km cu o textura de pete in doua nuante, generata din cod.</summary>
        void Ground(Color dark, Color light)
        {
            // Plane-ul Unity are 10 × 10 m; scalat cu 100 → 1 km, cat sa ajunga pana la orizont.
            var ground = WorldFactory.Primitive(PrimitiveType.Plane, "Ground", root, Vector3.zero, new Vector3(100f, 1f, 100f), dark);
            ground.GetComponent<Renderer>().sharedMaterial = WorldFactory.MottledMaterial(dark, light, metersPerTile: 9f, size: 1000f);
        }

        /// <summary>Iaz: un disc subtire, lucios, putin deasupra solului.</summary>
        void Pond(Vector3 center, float radius, Color color)
        {
            // Cilindrul Unity are diametrul 1 si inaltimea 2: il turtim pana devine un disc de 2 cm.
            var pond = WorldFactory.Primitive(PrimitiveType.Cylinder, "Pond", root, center + Vector3.up * 0.012f,
                new Vector3(radius * 2f, 0.01f, radius * 2f), color);
            var material = new Material(WorldFactory.Material(color)) { name = "PondWater" };
            material.SetFloat("_Smoothness", 0.92f);
            pond.GetComponent<Renderer>().sharedMaterial = material;
            pond.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// Flacara: doua conuri care „lumineaza” singure (material Unlit, neafectat de umbre), unul portocaliu si
        /// unul galben in interior, care tremura (<see cref="FlameDance"/>).
        /// </summary>
        void Flame(Vector3 position, float height)
        {
            if (assets == null || assets.glowMaterial == null) return;
            var flame = WorldFactory.Group("Flame", root, position);
            var cone = WorldFactory.Mountain(rng, topFrom: 2f, sides: 7, rings: 1);     // un singur inel = con simplu
            AddGlow(flame, cone, new Color(1f, 0.42f, 0.08f), new Vector3(height * 0.34f, height, height * 0.34f));
            AddGlow(flame, cone, new Color(1f, 0.86f, 0.30f), new Vector3(height * 0.19f, height * 0.68f, height * 0.19f));
            flame.gameObject.AddComponent<FlameDance>();
        }

        void AddGlow(Transform parent, Mesh mesh, Color color, Vector3 scale)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var material = new Material(assets.glowMaterial) { color = color };
            renderer.sharedMaterials = new[] { material, material };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Licurici: sfere mici luminoase, imprastiate intr-un inel, care plutesc si palpaie (<see cref="Firefly"/>).</summary>
        void Fireflies(int count, Vector3 center, float minRadius, float maxRadius)
        {
            if (assets == null || assets.glowMaterial == null) return;
            var material = new Material(assets.glowMaterial) { color = new Color(0.85f, 1f, 0.45f) };
            var swarm = WorldFactory.Group("Fireflies", root, Vector3.zero);
            for (int i = 0; i < count; i++)
            {
                var position = center + Polar(Range(0f, 360f), Range(minRadius, maxRadius));
                position.y = Range(0.3f, 2.4f);
                var fly = WorldFactory.Primitive(PrimitiveType.Sphere, "Firefly", swarm, position, Vector3.one * 0.04f, Color.white);
                fly.GetComponent<Renderer>().sharedMaterial = material;
                fly.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                fly.AddComponent<Firefly>();
            }
        }

        /// <summary>
        /// Pune un model din biblioteca la <paramref name="position"/>, rotit cu <paramref name="yaw"/> grade si
        /// scalat astfel incat cea mai mare dimensiune a lui sa fie <paramref name="size"/> metri. Modelele au
        /// marimi oarecare in fisier, asa ca masuram „cutia” lor (bounds) si scalam dupa ea.
        /// </summary>
        Transform Prop(string model, Vector3 position, float size, float yaw)
        {
            var holder = Spawn(model, position, out var bounds);
            if (holder == null) return null;

            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest > 0.0001f) holder.localScale = Vector3.one * (size / largest);
            SitOnGround(holder, position.y);
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return holder;
        }

        /// <summary>
        /// Ca <see cref="Prop"/>, dar modelul e intins pe fiecare axa pana ocupa exact cutia <paramref name="size"/>
        /// (latime, inaltime, adancime), cu baza pe sol.
        /// </summary>
        Transform FittedProp(string model, Vector3 position, Vector3 size, float yaw)
        {
            var holder = Spawn(model, position, out var bounds);
            if (holder == null) return null;

            holder.localScale = new Vector3(
                size.x / Mathf.Max(bounds.size.x, 0.0001f),
                size.y / Mathf.Max(bounds.size.y, 0.0001f),
                size.z / Mathf.Max(bounds.size.z, 0.0001f));
            SitOnGround(holder, position.y);
            // Centram cutia pe pozitia ceruta (pivotul modelului nu e mereu in mijloc).
            var fitted = BoundsOf(holder);
            holder.position += new Vector3(position.x - fitted.center.x, 0f, position.z - fitted.center.z);
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return holder;
        }

        Transform Spawn(string model, Vector3 position, out Bounds bounds)
        {
            bounds = default;
            var prefab = assets != null ? assets.Model(model) : null;
            if (prefab == null) return null;

            var holder = WorldFactory.Group(model, root, position);
            holder.gameObject.AddComponent<PropHolder>();   // un click pe model in Scene selecteaza acest suport
            Object.Instantiate(prefab, holder);
            if (holder.GetComponentsInChildren<Renderer>().Length == 0) return null;
            bounds = BoundsOf(holder);
            return holder;
        }

        static Bounds BoundsOf(Transform holder)
        {
            var renderers = holder.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        /// <summary>Dupa scalare, urca sau coboara modelul pana are baza la inaltimea <paramref name="y"/>.</summary>
        static void SitOnGround(Transform holder, float y)
        {
            holder.position += Vector3.up * (y - BoundsOf(holder).min.y);
        }

        /// <summary>
        /// Imprastie <paramref name="count"/> modele intr-un inel in jurul pacientului.
        /// <paramref name="clearRadius"/> = raza zonei de joc ocolite; plantele mici (<paramref name="smallPlants"/>)
        /// ocolesc doar culoarul pe care merge personajul. <paramref name="maxZ"/> = limita spre fata (ex. malul marii).
        /// </summary>
        void Scatter(string[] models, int count, float minRadius, float maxRadius, float minSize, float maxSize,
            float clearRadius, bool smallPlants = false, float maxZ = float.MaxValue)
        {
            int placed = 0;
            for (int attempt = 0; attempt < count * 20 && placed < count; attempt++)
            {
                // Radacina patrata face distributia uniforma pe suprafata (altfel s-ar inghesui spre centru).
                float radius = Mathf.Sqrt(Mathf.Lerp(minRadius * minRadius, maxRadius * maxRadius, (float)rng.NextDouble()));
                var position = Polar(Range(0f, 360f), radius);
                if (position.z > maxZ || IsBlocked(position)) continue;
                if (smallPlants ? InWalkLane(position) : InPlayArea(position, clearRadius)) continue;

                Prop(models[rng.Next(models.Length)], position, Range(minSize, maxSize), Range(0f, 360f));
                placed++;
            }
        }

        /// <summary>Rezerva un cerc in care <see cref="Scatter"/> nu mai pune nimic (ex. iazul).</summary>
        void Block(Vector3 center, float radius) => blocked.Add(new Vector4(center.x, 0f, center.z, radius));

        bool IsBlocked(Vector3 p)
        {
            foreach (var b in blocked)
                if (new Vector2(p.x - b.x, p.z - b.z).sqrMagnitude < b.w * b.w) return true;
            return false;
        }

        /// <summary>Zona de joc: un cerc in jurul tablei (centrul la 1 m in fata pacientului).</summary>
        static bool InPlayArea(Vector3 p, float radius) =>
            new Vector2(p.x, p.z - 1f).sqrMagnitude < radius * radius;

        /// <summary>Culoarul pe care merge personajul (intre locul lui si tabla) si locul pacientului.</summary>
        static bool InWalkLane(Vector3 p) =>
            (Mathf.Abs(p.x) < 3.4f && p.z > 0.6f && p.z < 2.4f) || new Vector2(p.x, p.z).sqrMagnitude < 0.8f;

        /// <summary>
        /// Munte sau insula: un mesh neregulat generat din cod. Fetele aflate mai sus de
        /// <paramref name="topFrom"/> (0–1 din inaltime) primesc a doua culoare (zapada, vegetatie).
        /// </summary>
        void Peak(Vector3 position, float height, float radius, Color body, Color top, float topFrom)
        {
            var peak = new GameObject("Peak");
            peak.transform.SetParent(root, false);
            peak.transform.localPosition = position;
            peak.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            peak.transform.localScale = new Vector3(radius, height, radius);
            peak.AddComponent<MeshFilter>().sharedMesh = WorldFactory.Mountain(rng, topFrom);
            var renderer = peak.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { WorldFactory.Material(body), WorldFactory.Material(top) };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Unghi (0° = in fata, +Z; creste spre dreapta) si distanta → pozitie pe sol.</summary>
        static Vector3 Polar(float angleDegrees, float distance)
        {
            float a = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * distance, 0f, Mathf.Cos(a) * distance);
        }

        float Range(float min, float max) => Mathf.Lerp(min, max, (float)rng.NextDouble());
    }
}
