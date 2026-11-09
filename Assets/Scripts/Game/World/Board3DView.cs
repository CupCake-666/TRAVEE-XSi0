using System;
using System.Collections;
using System.Collections.Generic;
using Travee.TicTacToe.Core;
using Travee.TicTacToe.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Travee.TicTacToe.World
{
    /// <summary>Marchează un collider ca fiind căsuța cu indexul dat.</summary>
    public sealed class BoardCell : MonoBehaviour
    {
        public int Index;
    }

    /// <summary>
    /// Tabla 3D care plutește în fața pacientului. Este și sursa de input pentru mouse: o rază din cameră
    /// prin cursor (raycast) găsește căsuța. În VR, aceleași collidere vor fi atinse de mână.
    /// Când personajul vine în spatele ei, tabla devine semi-transparentă (<see cref="SetSeeThrough"/>).
    /// </summary>
    public sealed class Board3DView : MonoBehaviour, IBoardView, ICellInputSource
    {
        const float BoardSize = 0.48f;    // metri
        const float Gap = 0.012f;
        const float TileHeight = 0.012f;
        const float SeeThroughAlpha = 0.3f;
        const float FadeSpeed = 3f;       // cât de repede se schimbă transparența (alpha pe secundă)

        static readonly Color BaseColor = new Color(0.24f, 0.20f, 0.17f);
        static readonly Color TileColor = new Color(0.96f, 0.93f, 0.86f);
        static readonly Color HoverColor = new Color(1.00f, 0.86f, 0.55f);

        readonly Renderer[] tiles = new Renderer[Board.CellCount];
        readonly Transform[] pieces = new Transform[Board.CellCount];
        readonly bool[] highlighted = new bool[Board.CellCount];
        readonly List<Material> fadeMaterials = new List<Material>();
        Material tileMaterial, hoverMaterial, winMaterial;
        Camera cam;
        Mesh torus;
        int hovered = -1;
        float alpha = 1f, targetAlpha = 1f;

        public event Action<int> CellSelected;

        public bool InputEnabled { get; set; }

        /// <summary>Poziția din lume a centrului căsuței (personajul își duce mâna acolo).</summary>
        public Vector3 CellWorldPosition(int cell) =>
            tiles[cell].transform.position + transform.up * (TileHeight / 2f);

        /// <param name="seeThroughTemplate">
        /// Material URP Lit transparent (creat de meniul TRAVEE). Fără el, tabla rămâne opacă.
        /// </param>
        public static Board3DView Create(Transform parent, Vector3 position, Quaternion rotation, Camera camera,
            Material seeThroughTemplate)
        {
            var root = WorldFactory.Group("Board3D", parent, position);
            root.localRotation = rotation;
            var view = root.gameObject.AddComponent<Board3DView>();
            view.cam = camera;
            view.Build(seeThroughTemplate);
            return view;
        }

        void Build(Material template)
        {
            torus = WorldFactory.Torus(0.045f, 0.013f);

            // Placa (render queue 3000) se desenează înaintea căsuțelor (3001), ca să nu le acopere când e transparentă.
            var baseMaterial = BoardMaterial(template, BaseColor, 3000);
            tileMaterial = BoardMaterial(template, TileColor, 3001);
            hoverMaterial = BoardMaterial(template, HoverColor, 3001);
            winMaterial = BoardMaterial(template, Theme.CellWin, 3001);

            // Spațiile dintre căsuțe lasă să se vadă placa închisă la culoare: arată ca liniile grilei.
            var plate = WorldFactory.Box("Base", transform, new Vector3(0, 0.01f, 0), new Vector3(BoardSize, 0.02f, BoardSize), BaseColor);
            plate.GetComponent<Renderer>().sharedMaterial = baseMaterial;

            float cell = (BoardSize - Gap * 4f) / Board.Size;
            for (int i = 0; i < Board.CellCount; i++)
            {
                // Coloana 0 = stânga pacientului (-X); rândul 0 = sus / cel mai departe de pacient (+Z), ca în 2D.
                float x = -BoardSize / 2f + Gap + cell / 2f + Board.Column(i) * (cell + Gap);
                float z = BoardSize / 2f - Gap - cell / 2f - Board.Row(i) * (cell + Gap);

                var tile = WorldFactory.Box($"Cell_{i}", transform, new Vector3(x, 0.02f + TileHeight / 2f, z),
                    new Vector3(cell, TileHeight, cell), TileColor, keepCollider: true);
                tile.AddComponent<BoardCell>().Index = i;
                tiles[i] = tile.GetComponent<Renderer>();
                tiles[i].sharedMaterial = tileMaterial;
            }
        }

        /// <summary>Material propriu al tablei (nu cel partajat din WorldFactory), ca să-i putem schimba transparența.</summary>
        Material BoardMaterial(Material template, Color color, int renderQueue)
        {
            var material = new Material(template != null ? template : WorldFactory.Material(color)) { color = color };
            if (template != null)
            {
                material.renderQueue = renderQueue;
                fadeMaterials.Add(material);
            }
            return material;
        }

        /// <summary>Tabla devine semi-transparentă (true) sau revine opacă (false), treptat.</summary>
        public void SetSeeThrough(bool seeThrough) => targetAlpha = seeThrough ? SeeThroughAlpha : 1f;

        void Update()
        {
            UpdateFade();

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

        int CellUnderPointer()
        {
            if (cam == null || Mouse.current == null) return -1;
            // Dacă mouse-ul e peste un element de UI (butonul "Meniu" etc.), nu selectăm prin el.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return -1;

            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 10f)) return -1;
            var boardCell = hit.collider.GetComponent<BoardCell>();
            if (boardCell == null || pieces[boardCell.Index] != null) return -1; // ocupată: fără hover
            return boardCell.Index;
        }

        void SetHovered(int cell)
        {
            if (cell == hovered) return;
            if (hovered >= 0) RefreshTile(hovered);
            hovered = cell;
            if (hovered >= 0) tiles[hovered].sharedMaterial = hoverMaterial;
        }

        void RefreshTile(int cell)
        {
            tiles[cell].sharedMaterial = highlighted[cell] ? winMaterial : tileMaterial;
        }

        public void ResetBoard()
        {
            hovered = -1;
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

        /// <summary>Piesa cade puțin de deasupra tablei și „sare” la mărimea finală (ease-out-back, ca în 2D).</summary>
        static IEnumerator DropIn(Transform piece)
        {
            const float duration = 0.3f;
            var end = piece.localPosition;
            var start = end + Vector3.up * 0.08f;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                if (piece == null) yield break;     // tabla a fost resetată între timp
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
