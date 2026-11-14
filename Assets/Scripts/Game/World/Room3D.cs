using Travee.TicTacToe.Audio;
using Travee.TicTacToe.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Travee.TicTacToe.World
{
    /// <summary>
    /// „Regizorul” scenei de joc: stie unde sunt camera (capul pacientului), tabla si locurile personajului si
    /// aplica peisajul ales (decor, lumina, cer, ceata, sunet de fundal).
    /// Unitati: metri. Pacientul sta in origine si priveste spre +Z.
    ///
    /// Tot ce se poate tine in scena e luat din scena, daca exista acolo: camera, tabla, locurile personajului
    /// (<see cref="OpponentSpot"/>), obiectele peisajelor si setarile lor (<see cref="ThemeSettings"/>).
    /// Ce lipseste din scena e construit din cod, cu valorile implicite de aici – asa ca jocul porneste si
    /// dintr-o scena goala.
    /// </summary>
    public sealed class Room3D
    {
        public const float EyeHeight = 1.2f;    // pacient asezat

        /// <summary>Inaltimea implicita a centrului tablei si distanta ei fata de pacient (metri).</summary>
        public const float DefaultBoardHeight = EyeHeight, DefaultBoardDistance = 0.9f;

        /// <summary>Tabla e marita (0,48 m × 1,3 ≈ 0,62 m), ca sa ocupe cam cat ocupa in varianta 2D.</summary>
        public const float BoardScale = 1.3f;

        /// <summary>Prefixul obiectelor-radacina din scena care tin decorul: Decor_All, Decor_Forest…</summary>
        public const string DecorPrefix = "Decor_";

        /// <summary>Numele obiectului (copil al lui Decor_...) care tine obiectele peisajului puse in scena din editor.</summary>
        public const string EditableName = "Peisaj";

        /// <summary>Numele luminii principale a unui peisaj (copil al lui Decor_...).</summary>
        public const string SunName = "Soare";

        // Locul pe care urca personajul in spatele tablei (vezi BuildStep): jumatatile de latime/adancime, in metri.
        const float StepHalfWidth = 0.6f, StepHalfDepth = 0.27f, StepRamp = 0.3f;
        float StepCenterZ => BoardPosition.z + 0.35f;

        GameAssets assets;
        Light sun;                  // lumina creata din cod, folosita doar daca peisajul nu are una in scena
        Transform environment;
        Transform homeLeft, homeRight;

        public Transform Root { get; private set; }
        public Camera Camera { get; private set; }
        public WorldTheme Theme { get; private set; }

        /// <summary>Inaltimea treptei pe care urca personajul la tabla (0 = nu exista treapta).</summary>
        public float StepHeight { get; private set; }

        /// <summary>Centrul tablei. Vine din pozitia obiectului „Tabla” din scena sau, daca lipseste, din Inspector.</summary>
        public Vector3 BoardPosition { get; private set; } = new Vector3(0f, DefaultBoardHeight, DefaultBoardDistance);

        /// <summary>Rotatia implicita a tablei: vertical, cu fata spre pacient (tabla e construita culcata).</summary>
        public static Quaternion DefaultBoardRotation => Quaternion.Euler(-90f, 0f, 0f);

        /// <summary>Locul implicit al personajului cand asteapta: lateral, cu 80 cm mai in spate decat tabla.</summary>
        public static Vector3 DefaultHome(AffectedSide side, Vector3 boardPosition) =>
            new Vector3(side == AffectedSide.Left ? -1.3f : 1.3f, 0f, boardPosition.z + 0.8f);

        /// <summary>Locul personajului cand asteapta: obiectul <see cref="OpponentSpot"/> din scena, daca exista.</summary>
        public Vector3 OpponentHome(AffectedSide side)
        {
            var spot = side == AffectedSide.Left ? homeLeft : homeRight;
            return spot != null ? spot.position : DefaultHome(side, BoardPosition);
        }

        /// <param name="assets">Biblioteca de modele/sunete. Fara ea, peisajele din cod nu pot fi construite.</param>
        /// <param name="boardPosition">Centrul tablei (din scena sau din Inspector).</param>
        /// <param name="cameraFromScene">true = camera a fost asezata de mana in scena: nu o mutam.</param>
        public static Room3D Build(Transform parent, GameAssets assets, WorldTheme theme, Vector3 boardPosition,
            bool cameraFromScene)
        {
            var room = new Room3D
            {
                Root = WorldFactory.Group("Room3D", parent, Vector3.zero),
                assets = assets,
                BoardPosition = boardPosition,
            };
            foreach (var spot in Object.FindObjectsByType<OpponentSpot>(FindObjectsSortMode.None))
            {
                if (spot.side == AffectedSide.Left) room.homeLeft = spot.transform;
                else room.homeRight = spot.transform;
            }
            room.BuildLighting();
            room.SetupCamera(cameraFromScene);
            room.SetTheme(theme);
            return room;
        }

        /// <summary>Trece la alt peisaj: arata decorul lui, ii aplica lumina, cerul, ceata si sunetul de fundal.</summary>
        public void SetTheme(WorldTheme theme)
        {
            Theme = theme;

            if (environment != null) Object.Destroy(environment.gameObject);
            environment = WorldFactory.Group("Environment_" + theme, Root, Vector3.zero);
            var decor = ShowDecor(theme);
            var settings = decor != null ? decor.GetComponent<ThemeSettings>() : null;

            // Obiectele peisajului: daca sunt in scena (Decor_... → Peisaj), le folosim asa cum au fost aranjate.
            // Altfel le construim acum, din cod.
            bool inScene = decor != null && decor.transform.Find(EditableName) != null;
            var look = inScene ? EnvironmentBuilder.Look(theme) : EnvironmentBuilder.Build(theme, assets, environment);
            if (settings != null) look = settings.look;         // setarile din Inspector au prioritate
            BuildStand();

            // Lumina principala: cea din scena a peisajului, daca are una; altfel cea creata din cod.
            var sceneSun = settings != null ? settings.sun : null;
            sun.enabled = sceneSun == null;
            if (sceneSun == null)
            {
                sun.color = look.SunColor;
                sun.intensity = look.SunIntensity;
                sun.transform.rotation = Quaternion.Euler(look.SunAngles);
            }

            ApplyLook(look, assets, Camera, settings != null ? settings.skyPanorama : null);

            if (settings != null && settings.ambientSound != null) Sfx.SetAmbient(settings.ambientSound);
            else Sfx.SetAmbient(look.AmbientSound);
        }

        /// <summary>
        /// Aplica partea de „atmosfera” a unui peisaj: lumina ambienta, ceata si cerul. E statica si nu depinde
        /// de joc, ca sa poata fi apelata si din editor, pentru previzualizare.
        /// </summary>
        /// <param name="panorama">Fotografia de cer aleasa explicit; null = cea cu numele din <paramref name="look"/>.</param>
        public static void ApplyLook(ThemeLook look, GameAssets assets, Camera camera, Texture2D panorama = null)
        {
            // Lumina ambienta in trei culori (cer / orizont / sol): lumineaza si partile aflate in umbra.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = look.AmbientSky;
            RenderSettings.ambientEquatorColor = look.AmbientEquator;
            RenderSettings.ambientGroundColor = look.AmbientGround;

            // Ceata are culoarea orizontului: obiectele indepartate se „topesc” in cer.
            RenderSettings.fog = look.FogEnd > 0f;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = look.Sky.Horizon;
            RenderSettings.fogStartDistance = look.FogStart;
            RenderSettings.fogEndDistance = look.FogEnd;

            if (camera == null) return;
            bool hasSky = assets != null;
            if (panorama == null && hasSky) panorama = assets.SkyTexture(look.Sky.Panorama);
            Sky.Apply(hasSky ? assets.skyMaterial : null, camera, look.Sky, panorama);
        }

        /// <summary>
        /// Activeaza decorul peisajului dat (si Decor_All) si le dezactiveaza pe celelalte. Intoarce obiectul
        /// Decor_... al peisajului, daca exista in scena.
        /// </summary>
        GameObject ShowDecor(WorldTheme theme)
        {
            GameObject current = null;
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())    // include si obiectele dezactivate
            {
                if (!go.name.StartsWith(DecorPrefix, System.StringComparison.Ordinal)) continue;
                string owner = go.name.Substring(DecorPrefix.Length);
                bool mine = owner == theme.ToString();
                go.SetActive(mine || owner == "All");
                if (mine) current = go;
            }
            return current;
        }

        /// <summary>
        /// Un personaj prea scund nu ajunge cu mana la randul de sus al tablei. Daca se intampla (in mod normal
        /// nu: personajul e marit automat), ii punem in spatele tablei ceva pe care sa urce, potrivit peisajului.
        /// </summary>
        public void BuildStep(float height)
        {
            if (height <= 0.01f || StepHeight > 0f) return;
            StepHeight = height;
            BuildStand();
        }

        void BuildStand()
        {
            if (StepHeight <= 0f || environment == null) return;
            EnvironmentBuilder.BuildStand(Theme, assets, environment, new Vector3(BoardPosition.x, 0f, StepCenterZ),
                StepHalfWidth, StepHalfDepth, StepHeight);
        }

        /// <summary>
        /// Inaltimea „podelei” intr-un punct: 0 peste tot, <see cref="StepHeight"/> pe treapta, cu o trecere lina
        /// pe o latime de <see cref="StepRamp"/> in jurul ei (personajul urca si coboara treptat).
        /// </summary>
        public float FloorHeight(Vector3 point)
        {
            if (StepHeight <= 0f) return 0f;
            float dx = Mathf.Max(0f, Mathf.Abs(point.x - BoardPosition.x) - StepHalfWidth);
            float dz = Mathf.Max(0f, Mathf.Abs(point.z - StepCenterZ) - StepHalfDepth);
            float outside = Mathf.Sqrt(dx * dx + dz * dz);
            return StepHeight * (1f - Mathf.SmoothStep(0f, 1f, outside / StepRamp));
        }

        void BuildLighting()
        {
            sun = new GameObject("Light").AddComponent<Light>();
            sun.transform.SetParent(Root, false);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
        }

        void SetupCamera(bool fromScene)
        {
            Camera = UnityEngine.Camera.main;
            if (Camera == null)
            {
                Camera = new GameObject("Main Camera").AddComponent<Camera>();
                Camera.tag = "MainCamera";
                fromScene = false;
            }
            if (fromScene) return;      // asezata de mana in scena: o lasam exact cum e

            ConfigureCamera(Camera, BoardPosition);
        }

        /// <summary>Setarile implicite ale camerei (capul pacientului). Folosite si de editor cand pregateste scena.</summary>
        public static void ConfigureCamera(Camera camera, Vector3 boardPosition)
        {
            camera.orthographic = false;
            camera.fieldOfView = 65f;            // destul de larg cat sa se vada tabla si personajul
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1500f;         // muntii si insulele sunt departe
            camera.transform.position = new Vector3(0f, EyeHeight, 0f);
            // Privirea „de baza” e la jumatatea dintre orizont si tabla. In VR directia o da capul pacientului.
            camera.transform.LookAt(new Vector3(boardPosition.x, (EyeHeight + boardPosition.y) / 2f + 0.03f, boardPosition.z));
        }
    }
}
