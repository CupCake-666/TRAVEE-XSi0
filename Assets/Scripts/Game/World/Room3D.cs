using Travee.TicTacToe.Audio;
using Travee.TicTacToe.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Travee.TicTacToe.World
{
    /// <summary>
    /// Scena de joc: camera (capul pacientului, așezat), lumina și peisajul din jur.
    /// Unități: metri. Pacientul stă în origine și privește spre +Z. Tabla plutește în fața lui (ca în VR),
    /// iar personajul AI stă lateral, în stânga sau în dreapta. Aceste poziții sunt aceleași în orice peisaj;
    /// <see cref="SetTheme"/> schimbă doar decorul, cerul, lumina și sunetul de fundal.
    /// </summary>
    public sealed class Room3D
    {
        public const float EyeHeight = 1.2f;    // pacient așezat

        /// <summary>Înălțimea implicită a centrului tablei și distanța ei față de pacient (metri).</summary>
        public const float DefaultBoardHeight = EyeHeight, DefaultBoardDistance = 0.9f;

        // Locul pe care urcă personajul în spatele tablei (vezi BuildStep): centrul și jumătățile de lățime/adâncime, în metri.
        const float StepHalfWidth = 0.6f, StepHalfDepth = 0.27f, StepRamp = 0.3f;
        float StepCenterZ => BoardPosition.z + 0.35f;

        GameAssets assets;
        Light sun;
        Transform environment;

        public Transform Root { get; private set; }
        public Camera Camera { get; private set; }
        public WorldTheme Theme { get; private set; }

        /// <summary>Înălțimea treptei pe care urcă personajul la tablă (0 = nu există treaptă).</summary>
        public float StepHeight { get; private set; }

        /// <summary>
        /// Centrul tablei: drept în fața pacientului, implicit la nivelul ochilor (privește drept înainte).
        /// Personajul își potrivește singur înălțimea ca să ajungă la rândul de sus. Se reglează din Inspector
        /// (TicTacToeBootstrap).
        /// </summary>
        public Vector3 BoardPosition { get; private set; } = new Vector3(0f, DefaultBoardHeight, DefaultBoardDistance);

        /// <summary>Tabla e mărită (0,48 m × 1,3 ≈ 0,62 m), ca să ocupe cam cât ocupa în varianta 2D.</summary>
        public const float BoardScale = 1.3f;

        /// <summary>Tabla stă vertical, cu fața spre pacient (se vede ca în varianta 2D).</summary>
        public Quaternion BoardRotation => Quaternion.Euler(-90f, 0f, 0f);

        /// <summary>Locul personajului când așteaptă: lateral, în câmpul vizual, cu 80 cm mai în spate decât tabla.</summary>
        public Vector3 OpponentHome(AffectedSide side) =>
            new Vector3(side == AffectedSide.Left ? -1.3f : 1.3f, 0f, BoardPosition.z + 0.8f);

        /// <param name="assets">Biblioteca de modele/sunete. Fără ea rămâne doar peisajul „Cameră”.</param>
        public static Room3D Build(Transform parent, GameAssets assets, WorldTheme theme,
            float boardHeight = DefaultBoardHeight, float boardDistance = DefaultBoardDistance)
        {
            var room = new Room3D
            {
                Root = WorldFactory.Group("Room3D", parent, Vector3.zero),
                assets = assets,
                BoardPosition = new Vector3(0f, boardHeight, boardDistance),
            };
            room.BuildLighting();
            room.SetupCamera();
            room.SetTheme(theme);
            return room;
        }

        /// <summary>Înlocuiește peisajul: șterge obiectele celui vechi, le construiește pe ale celui nou și aplică aspectul.</summary>
        public void SetTheme(WorldTheme theme)
        {
            Theme = theme;

            if (environment != null) Object.Destroy(environment.gameObject);
            environment = WorldFactory.Group("Environment_" + theme, Root, Vector3.zero);
            var look = EnvironmentBuilder.Build(theme, assets, environment);
            BuildStand();
            ShowHandPlacedDecor();

            sun.color = look.SunColor;
            sun.intensity = look.SunIntensity;
            sun.transform.rotation = Quaternion.Euler(look.SunAngles);

            // Lumina ambientă în trei culori (cer / orizont / sol): luminează și părțile aflate în umbră.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = look.AmbientSky;
            RenderSettings.ambientEquatorColor = look.AmbientEquator;
            RenderSettings.ambientGroundColor = look.AmbientGround;

            // Ceața are culoarea orizontului: obiectele îndepărtate se „topesc” în cer.
            RenderSettings.fog = look.FogEnd > 0f;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = look.Sky.Horizon;
            RenderSettings.fogStartDistance = look.FogStart;
            RenderSettings.fogEndDistance = look.FogEnd;

            bool hasSky = assets != null;
            Sky.Apply(hasSky ? assets.skyMaterial : null, Camera, look.Sky,
                hasSky ? assets.SkyTexture(look.Sky.Panorama) : null);
            Sfx.SetAmbient(look.AmbientSound);
        }

        /// <summary>
        /// Un personaj scund nu ajunge cu mâna la rândul de sus al tablei. Îi punem în spatele tablei ceva pe care
        /// să urce, potrivit peisajului (buturugă, lespede de stâncă). Înălțimea o cere personajul, după ce își
        /// măsoară brațul; obiectul se reface la fiecare schimbare de peisaj.
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
            EnvironmentBuilder.BuildStand(Theme, assets, environment, new Vector3(0f, 0f, StepCenterZ),
                StepHalfWidth, StepHalfDepth, StepHeight);
        }

        /// <summary>
        /// Înălțimea „podelei” într-un punct: 0 peste tot, <see cref="StepHeight"/> pe podium, cu o trecere lină
        /// pe o lățime de <see cref="StepRamp"/> în jurul lui (personajul urcă și coboară treptat).
        /// </summary>
        public float FloorHeight(Vector3 point)
        {
            if (StepHeight <= 0f) return 0f;
            float dx = Mathf.Max(0f, Mathf.Abs(point.x) - StepHalfWidth);
            float dz = Mathf.Max(0f, Mathf.Abs(point.z - StepCenterZ) - StepHalfDepth);
            float outside = Mathf.Sqrt(dx * dx + dz * dz);
            return StepHeight * (1f - Mathf.SmoothStep(0f, 1f, outside / StepRamp));
        }

        /// <summary>Prefixul obiectelor din scenă care țin decorul pus de mână în editor.</summary>
        public const string DecorPrefix = "Decor_";

        /// <summary>
        /// Pe lângă decorul construit din cod, scena poate avea decor așezat de mână în editor. El stă sub
        /// obiecte-rădăcină numite Decor_Forest, Decor_Mountain, Decor_Beach (apar doar în peisajul respectiv)
        /// și Decor_All (apare în toate). Aici îl activăm pe cel al peisajului curent și le ascundem pe celelalte.
        /// </summary>
        void ShowHandPlacedDecor()
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())    // include și obiectele dezactivate
            {
                if (!go.name.StartsWith(DecorPrefix, System.StringComparison.Ordinal)) continue;
                string owner = go.name.Substring(DecorPrefix.Length);
                go.SetActive(owner == "All" || owner == Theme.ToString());
            }
        }

        void BuildLighting()
        {
            sun = new GameObject("Light").AddComponent<Light>();
            sun.transform.SetParent(Root, false);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
        }

        void SetupCamera()
        {
            Camera = UnityEngine.Camera.main;
            if (Camera == null)
            {
                Camera = new GameObject("Main Camera").AddComponent<Camera>();
                Camera.tag = "MainCamera";
            }
            Camera.orthographic = false;
            Camera.fieldOfView = 65f;            // destul de larg cât să se vadă tabla și personajul
            Camera.nearClipPlane = 0.05f;
            Camera.farClipPlane = 1500f;         // munții și insulele sunt departe
            Camera.transform.position = new Vector3(0f, EyeHeight, 0f);
            // Privirea „de bază” e la jumătatea dintre orizont și tablă: pe ecran se văd și tabla (jos), și
            // peisajul cu personajul (sus). În VR direcția o dă capul pacientului.
            Camera.transform.LookAt(new Vector3(0f, (EyeHeight + BoardPosition.y) / 2f + 0.03f, BoardPosition.z));
        }
    }
}
