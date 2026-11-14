using System;
using System.Collections.Generic;

namespace Travee.TicTacToe.Core
{
    public enum Mark { None = 0, X = 1, O = 2 }

    public enum BoardState { InProgress, XWins, OWins, Draw }

    /// <summary>Partea corpului afectata de AVC (mana paralizata).</summary>
    public enum AffectedSide { Left, Right }

    public static class MarkExtensions
    {
        public static Mark Opponent(this Mark mark)
        {
            switch (mark)
            {
                case Mark.X: return Mark.O;
                case Mark.O: return Mark.X;
                default: return Mark.None;
            }
        }

        public static string Symbol(this Mark mark)
        {
            switch (mark)
            {
                case Mark.X: return "X";
                case Mark.O: return "0";
                default: return "";
            }
        }
    }

    /// <summary>
    /// Tabla 3x3. Celulele sunt indexate 0..8, rand cu rand:
    ///  0 | 1 | 2
    ///  3 | 4 | 5
    ///  6 | 7 | 8
    /// X muta intotdeauna primul.
    /// </summary>
    public sealed class Board
    {
        public const int Size = 3;
        public const int CellCount = Size * Size;

        public static readonly int[][] Lines =
        {
            new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, // randuri
            new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 }, // coloane
            new[] { 0, 4, 8 }, new[] { 2, 4, 6 },                     // diagonale
        };

        readonly Mark[] cells = new Mark[CellCount];

        public Board() { }

        public Board(Board other)
        {
            Array.Copy(other.cells, cells, CellCount);
        }

        public Board Clone() => new Board(this);

        public Mark this[int cell] => cells[cell];

        public static int Row(int cell) => cell / Size;
        public static int Column(int cell) => cell % Size;

        public bool IsEmpty(int cell) => cells[cell] == Mark.None;

        public int Count(Mark mark)
        {
            int n = 0;
            for (int i = 0; i < CellCount; i++)
                if (cells[i] == mark) n++;
            return n;
        }

        public int EmptyCount => Count(Mark.None);

        public bool IsFull => EmptyCount == 0;

        /// <summary>Cine urmeaza la mutare (X incepe mereu).</summary>
        public Mark NextToMove => Count(Mark.X) == Count(Mark.O) ? Mark.X : Mark.O;

        public List<int> EmptyCells()
        {
            var result = new List<int>(CellCount);
            for (int i = 0; i < CellCount; i++)
                if (cells[i] == Mark.None) result.Add(i);
            return result;
        }

        public void Place(int cell, Mark mark)
        {
            if (cell < 0 || cell >= CellCount) throw new ArgumentOutOfRangeException(nameof(cell));
            if (mark == Mark.None) throw new ArgumentException("Nu se poate plasa Mark.None.", nameof(mark));
            if (cells[cell] != Mark.None) throw new InvalidOperationException($"Celula {cell} este deja ocupată.");
            cells[cell] = mark;
        }

        /// <summary>Folosit de algoritmii de cautare pentru a anula o mutare.</summary>
        public void Clear(int cell) => cells[cell] = Mark.None;

        public Mark Winner() => Winner(out _);

        public Mark Winner(out int[] winningLine)
        {
            foreach (var line in Lines)
            {
                var m = cells[line[0]];
                if (m != Mark.None && m == cells[line[1]] && m == cells[line[2]])
                {
                    winningLine = line;
                    return m;
                }
            }
            winningLine = null;
            return Mark.None;
        }

        public BoardState State
        {
            get
            {
                var w = Winner();
                if (w == Mark.X) return BoardState.XWins;
                if (w == Mark.O) return BoardState.OWins;
                return IsFull ? BoardState.Draw : BoardState.InProgress;
            }
        }

        public bool IsGameOver => State != BoardState.InProgress;

        /// <summary>Ar castiga <paramref name="mark"/> imediat daca ar pune in <paramref name="cell"/>?</summary>
        public bool IsWinningMove(int cell, Mark mark)
        {
            if (!IsEmpty(cell)) return false;
            cells[cell] = mark;
            bool wins = Winner() == mark;
            cells[cell] = Mark.None;
            return wins;
        }

        /// <summary>Codificare in baza 3, unica pentru fiecare configuratie (folosita pentru memoizare).</summary>
        public int Key
        {
            get
            {
                int key = 0;
                for (int i = 0; i < CellCount; i++) key = key * 3 + (int)cells[i];
                return key;
            }
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    var m = cells[r * Size + c];
                    sb.Append(m == Mark.None ? "." : m.Symbol());
                }
                if (r < Size - 1) sb.Append('/');
            }
            return sb.ToString();
        }
    }
}
