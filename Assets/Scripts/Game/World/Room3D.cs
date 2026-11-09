using Travee.TicTacToe.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Travee.TicTacToe.World
{
    /// <summary>
    /// Camera de joc: podea, perete, lumină și camera (capul pacientului, așezat).
    /// Unități: metri. Pacientul stă în origine și privește spre +Z. Tabla plutește în fața lui (ca în VR),
    /// iar personajul AI stă lateral, în stânga sau în dreapta.
    /// </summary>
    public sealed class Room3D
    {
        public const float EyeHeight = 1.2f;    // pacient așezat

        static readonly Color Floor = new Color(0.78f, 0.72f, 0.64f);
        static readonly Color Wall = new Color(0.86f, 0.90f, 0.93f);

        public Transform Root { get; private set; }
        public Camera Camera { get; private set; }

        /// <summary>Centrul tablei: drept în fața pacientului, puțin sub nivelul ochilor.</summary>
        public Vector3 BoardPosition => new Vector3(0f, 1.12f, 0.9f);

        /// <summary>Tabla e mărită (0,48 m × 1,3 ≈ 0,62 m), ca să ocupe cam cât ocupa în varianta 2D.</summary>
        public const float BoardScale = 1.3f;

        /// <summary>Tabla stă vertical, cu fața spre pacient (se vede ca în varianta 2D).</summary>
        public Quaternion BoardRotation => Quaternion.Euler(-90f, 0f, 0f);

        /// <summary>Locul personajului când așteaptă: lateral, în câmpul vizual, puțin mai în spate decât tabla.</summary>
        public Vector3 OpponentHome(AffectedSide side) =>
            new Vector3(side == AffectedSide.Left ? -1.1f : 1.1f, 0f, 1.45f);

        /// <summary>Locul de unde personajul pune piesa: în spatele tablei, față în față cu pacientul.</summary>
        public Vector3 OpponentAtBoard => new Vector3(0f, 0f, 1.25f);

        public static Room3D Build(Transform parent)
        {
            var room = new Room3D { Root = WorldFactory.Group("Room3D", parent, Vector3.zero) };
            room.BuildGeometry();
            room.BuildLighting();
            room.SetupCamera();
            return room;
        }

        void BuildGeometry()
        {
            WorldFactory.Box("Floor", Root, new Vector3(0, -0.05f, 1f), new Vector3(8f, 0.1f, 8f), Floor);
            WorldFactory.Box("Wall", Root, new Vector3(0, 1.5f, 2.6f), new Vector3(8f, 3f, 0.1f), Wall);
        }

        void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.62f);

            var sun = new GameObject("Light").AddComponent<Light>();
            sun.transform.SetParent(Root, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
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
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Wall;
            Camera.transform.position = new Vector3(0f, EyeHeight, 0f);
            Camera.transform.LookAt(BoardPosition + Vector3.up * 0.05f);
        }
    }
}
