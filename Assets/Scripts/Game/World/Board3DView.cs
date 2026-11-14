using System;
using System.Collections;
using System.Collections.Generic;
using Travee.TicTacToe.Audio;
using Travee.TicTacToe.Core;
using Travee.TicTacToe.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Travee.TicTacToe.World
{
    /// <summary>Marcheaza un collider ca fiind casuta cu indexul dat.</summary>
    public sealed class BoardCell : MonoBehaviour
    {
        public int Index;
    }

    /// <summary>
    /// Tabla 3D care pluteste in fata pacientului. Este si sursa de input pentru mouse: o raza din camera
    /// prin cursor (raycast) gaseste casuta. In VR, aceleasi collidere vor fi atinse de mana.
    /// Cand personajul vine in spatele ei, tabla devine semi-transparenta (<see cref="SetSeeThrough"/>).
    /// </summary>
    public sealed class Board3DView : MonoBehaviour, IBoardView, ICellInputSource
    {
        const float BoardSize = 0.48f;    // metri
        const float Gap = 0.012f;
        const float TileHeight = 0.012f;
        const float SeeThroughAlpha = 0.3f;
        const float FadeSpeed = 3f;       // cat de repede se schimba transparenta (alpha pe secunda)
        const float PlateHeight = 0.02f;  // grosimea placii de sub casute
        const float TargetPulse = 6f;     // viteza pulsatiei casutei alese de AI

        static readonly Color BaseColor = new Color(0.24f, 0.20f, 0.17f);
        static readonly Color TileColor = new Color(0.96f, 0.93f, 0.86f);
        static readonly Color HoverColor = new Color(1.00f, 0.86f, 0.55f);
        static readonly Color TargetColor = new Color(0.93f, 0.20f, 0.18f);   // casuta aleasa de AI

        readonly Renderer[] tiles = new Renderer[Board.CellCount];
        readonly Transform[] pieces = new Transform[Board.CellCount];
        readonly bool[] highlighted = new bool[Board.CellCount];
        readonly List<Material> fadeMaterials = new List<Material>();
        Material tileMaterial, hoverMaterial, winMaterial, targetMaterial;
        Camera cam;
        Mesh torus;
        int hovered = -1;
        int aiTarget = -1;
        float alpha = 1f, targetAlpha = 1f;

        public event Action<int> CellSelected;

        public bool InputEnabled { get; set; }

        /// <summary>Pozitia din lume a centrului casutei (personajul isi duce mana acolo).</summary>
        public Vector3 CellWorldPosition(int cell) =>
            tiles[cell].transform.position + transform.up * (TileHeight / 2f);

        /// <summary>
        /// Punctul de pe SPATELE tablei din dreptul casutei: acolo atinge personajul, care sta in spatele ei.
        /// (lossyScale = scala din lume; tabla e marita cu Room3D.BoardScale.)
        /// </summary>
        public Vector3 CellBackPosition(int cell) =>
            tiles[cell].transform.position - transform.up * ((PlateHeight + TileHeight / 2f) * transform.lossyScale.y);

        /// <param name="seeThroughTemplate">
        /// Material URP Lit transparent (creat de meniul TRAVEE). Fara el, tabla ramane opaca.
        /// </param>
        public static Board3DView Create(Transform parent, Vector3 position, Quaternion rotation, Camera camera,
            Material seeThroughTemplate)
        {
            var root = WorldFactory.Group("Board3D", parent, position);
            root.localRotation = rotation;
            var view = root.gameObject.AddComponent<Board3DView>();
            view.Init(camera, seeThroughTemplate);
            return view;
        }

        /// <summary>
        /// Construieste placa si casutele sub acest obiect. Se apeleaza o data, la pornire. Daca tabla e un
        /// obiect pus in scena („Tabla”), pozitia, rotatia si marimea ei sunt cele din Transform-ul lui.
        /// </summary>
        public void Init(Camera camera, Material seeThroughTemplate)
        {
            cam = camera;
            Build(seeThroughTemplate);
        }

        /// <summary>
        /// In editor tabla nu are inca obiecte (se construiesc la pornire), asa ca o desenam ca „gizmo”: placa
        /// si cele noua casute, exact unde vor fi in joc. Gizmo-urile se vad doar in fereastra Scene.
        /// </summary>
        void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            Gizmos.matrix = transform.localToWorldMatrix;       // desenam in coordonatele tablei
            Gizmos.color = BaseColor;
            Gizmos.DrawCube(new Vector3(0f, PlateHeight / 2f, 0f), new Vector3(BoardSize, PlateHeight, BoardSize));
            Gizmos.color = TileColor;
            float cell = (BoardSize - Gap * 4f) / Board.Size;
            for (int i = 0; i < Board.CellCount; i++)
            {
                float x = -BoardSize / 2f + Gap + cell / 2f + Board.Column(i) * (cell + Gap);
                float z = BoardSize / 2f - Gap - cell / 2f - Board.Row(i) * (cell + Gap);
                Gizmos.DrawCube(new Vector3(x, PlateHeight + TileHeight / 2f, z), new Vector3(cell, TileHeight, cell));
            }
        }

        void Build(Material template)
        {
            torus = WorldFactory.Torus(0.045f, 0.013f);

            // Placa (render queue 3000) se deseneaza inaintea casutelor (3001), ca sa nu le acopere cand e transparenta.
            var baseMaterial = BoardMaterial(template, BaseColor, 3000);
            tileMaterial = BoardMaterial(template, TileColor, 3001);
            hoverMaterial = BoardMaterial(template, HoverColor, 3001);
            winMaterial = BoardMaterial(template, Theme.CellWin, 3001);
            // Casuta AI-ului nu urmeaza transparenta tablei: culoarea si alpha ei pulseaza separat (UpdateTarget).
            targetMaterial = BoardMaterial(template, TargetColor, 3001, fades: false);

            // Spatiile dintre casute lasa sa se vada placa inchisa la culoare: arata ca liniile grilei.
            var plate = WorldFactory.Box("Base", transform, new Vector3(0, PlateHeight / 2f, 0), new Vector3(BoardSize, PlateHeight, BoardSize), BaseColor);
            plate.GetComponent<Renderer>().sharedMaterial = baseMaterial;

            float cell = (BoardSize - Gap * 4f) / Board.Size;
            for (int i = 0; i < Board.CellCount; i++)
            {
                // Coloana 0 = stanga pacientului (-X); randul 0 = sus / cel mai departe de pacient (+Z), ca in 2D.
                float x = -BoardSize / 2f + Gap + cell / 2f + Board.Column(i) * (cell + Gap);
                float z = BoardSize / 2f - Gap - cell / 2f - Board.Row(i) * (cell + Gap);

                var tile = WorldFactory.Box($"Cell_{i}", transform, new Vector3(x, PlateHeight + TileHeight / 2f, z),
                    new Vector3(cell, TileHeight, cell), TileColor, keepCollider: true);
                tile.AddComponent<BoardCell>().Index = i;
                tiles[i] = tile.GetComponent<Renderer>();
                tiles[i].sharedMaterial = tileMaterial;
            }
        }

        /// <summary>Material propriu al tablei (nu cel partajat din WorldFactory), ca sa-i putem schimba transparenta.</summary>
        Material BoardMaterial(Material template, Color color, int renderQueue, bool fades = true)
        {
            var material = new Material(template != null ? template : WorldFactory.Material(color)) { color = color };
            if (template != null)
            {
                material.renderQueue = renderQueue;
                if (fades) fadeMaterials.Add(material);
            }
            return material;
        }

        /// <summary>Tabla devine semi-transparenta (true) sau revine opaca (false), treptat.</summary>
        public void SetSeeThrough(bool seeThrough) => targetAlpha = seeThrough ? SeeThroughAlpha : 1f;

        /// <summary>
        /// Marcheaza casuta pe care urmeaza sa o aleaga AI-ul cu un glow rosu care pulseaza
        /// (echivalentul hover-ului galben al pacientului). -1 = niciuna. Se sterge singur cand apare piesa.
        /// </summary>
        public void SetAITarget(int cell)
        {
            if (cell == aiTarget) return;
            int previous = aiTarget;
            aiTarget = cell;
            if (previous >= 0) RefreshTile(previous);
            if (aiTarget >= 0) RefreshTile(aiTarget);
        }

        void Update()
        {
            UpdateFade();
            UpdateTarget();

            int cell = InputEnabled ? CellUnderPointer() : -1;
            SetHovered(cell);

            if (cell >= 0 && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                CellSelected?.Invoke(cell);
        }

        void UpdateFade()
        {
            if (Mathf.Approximately(alpha, targetAlpha)) return;
            alpha = Mathf.MoveTowards(alpha, targetAlpha, FadeSpeed * Time.deltaTime);
            foreach (var material in fadeMaterials)
            {
                var c = material.color;
                c.a = alpha;
                material.color = c;
            }
        }

        /// <summary>Pulsatia casutei AI-ului: culoarea trece intre rosu deschis si rosu, iar alpha intre 0,55 si 0,95.</summary>
        void UpdateTarget()
        {
            if (aiTarget < 0) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * TargetPulse);
            var color = Color.Lerp(Color.Lerp(TileColor, TargetColor, 0.6f), TargetColor, pulse);
            color.a = Mathf.Lerp(0.55f, 0.95f, pulse);
            targetMaterial.color = color;
        }

        int CellUnderPointer()
        {
            if (cam == null || Mouse.current == null) return -1;
            // Daca mouse-ul e peste un element de UI (butonul "Meniu" etc.), nu selectam prin el.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return -1;

            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 10f)) return -1;
            var boardCell = hit.collider.GetComponent<BoardCell>();
            if (boardCell == null || pieces[boardCell.Index] != null) return -1; // ocupata: fara hover
            return boardCell.Index;
        }

        void SetHovered(int cell)
        {
            if (cell == hovered) return;
            if (hovered >= 0) RefreshTile(hovered);
            hovered = cell;
            if (hovered < 0) return;
            tiles[hovered].sharedMaterial = hoverMaterial;
            Sfx.Play(Sound.Hover, 0.5f);
        }

        void RefreshTile(int cell)
        {
            if (cell == aiTarget) tiles[cell].sharedMaterial = targetMaterial;
            else tiles[cell].sharedMaterial = highlighted[cell] ? winMaterial : tileMaterial;
        }

        public void ResetBoard()
        {
            hovered = -1;
            aiTarget = -1;
            for (int i = 0; i < Board.CellCount; i++)
            {
                if (pieces[i] != null) Destroy(pieces[i].gameObject);
                pieces[i] = null;
                highlighted[i] = false;
                RefreshTile(i);
            }
        }

        public void SetMark(int cell, Mark mark)
        {
            SetHovered(-1);
            if (cell == aiTarget) SetAITarget(-1);
            var piece = mark == Mark.X ? CreateX() : CreateO();
            piece.SetParent(transform, false);
            piece.localPosition = tiles[cell].transform.localPosition + Vector3.up * (TileHeight / 2f + 0.012f);
            pieces[cell] = piece;
            StartCoroutine(DropIn(piece));
        }

        public void HighlightLine(int[] line)
        {
            if (line == null) return;
            foreach (int cell in line)
            {
                highlighted[cell] = true;
                RefreshTile(cell);
            }
        }

        Transform CreateX()
        {
            var x = WorldFactory.Group("X", transform, Vector3.zero);
            var size = new Vector3(0.12f, 0.022f, 0.028f);
            WorldFactory.Box("Bar1", x, Vector3.zero, size, Theme.X).transform.localRotation = Quaternion.Euler(0, 45, 0);
            WorldFactory.Box("Bar2", x, Vector3.zero, size, Theme.X).transform.localRotation = Quaternion.Euler(0, -45, 0);
            return x;
        }

        Transform CreateO()
        {
            var o = new GameObject("O");
            o.AddComponent<MeshFilter>().sharedMesh = torus;
            o.AddComponent<MeshRenderer>().sharedMaterial = WorldFactory.Material(Theme.O);
            return o.transform;
        }

        /// <summary>Piesa cade putin de deasupra tablei si „sare” la marimea finala (ease-out-back, ca in 2D).</summary>
        static IEnumerator DropIn(Transform piece)
        {
            const float duration = 0.3f;
            var end = piece.localPosition;
            var start = end + Vector3.up * 0.08f;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                if (piece == null) yield break;     // tabla a fost resetata intre timp
                float k = t / duration;
                float scale = Mathf.LerpUnclamped(0.3f, 1f, 1f + 2.7f * Mathf.Pow(k - 1f, 3) + 1.7f * Mathf.Pow(k - 1f, 2));
                piece.localScale = Vector3.one * scale;
                piece.localPosition = Vector3.Lerp(start, end, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            if (piece == null) yield break;
            piece.localScale = Vector3.one;
            piece.localPosition = end;
        }
    }
}
