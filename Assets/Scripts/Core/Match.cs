using System;
using System.Collections.Generic;

namespace Travee.TicTacToe.Core
{
    public enum DifficultyLevel { Level1 = 1, Level2 = 2, Level3 = 3, Level4 = 4 }

    public enum MatchOutcome { InProgress, PatientWin, AIWin, Draw }

    public readonly struct MoveRecord
    {
        public readonly int Number;     // 1-based
        public readonly int Cell;
        public readonly Mark Mark;
        public readonly bool ByPatient;

        public MoveRecord(int number, int cell, Mark mark, bool byPatient)
        {
            Number = number;
            Cell = cell;
            Mark = mark;
            ByPatient = byPatient;
        }
    }

    /// <summary>
    /// Un meci pacient vs AI. Cine castiga coin flip-ul incepe si primeste X.
    /// </summary>
    public sealed class Match
    {
        readonly List<MoveRecord> moves = new List<MoveRecord>();

        public DifficultyLevel Level { get; }
        public bool PatientStarts { get; }
        public Mark PatientMark { get; }
        public Mark AIMark { get; }
        public Board Board { get; } = new Board();
        public IReadOnlyList<MoveRecord> Moves => moves;

        public Match(DifficultyLevel level, bool patientStarts)
        {
            Level = level;
            PatientStarts = patientStarts;
            PatientMark = patientStarts ? Mark.X : Mark.O;
            AIMark = PatientMark.Opponent();
        }

        public bool IsOver => Board.IsGameOver;

        public bool IsPatientTurn => !IsOver && Board.NextToMove == PatientMark;

        public MatchOutcome Outcome
        {
            get
            {
                switch (Board.State)
                {
                    case BoardState.InProgress: return MatchOutcome.InProgress;
                    case BoardState.Draw: return MatchOutcome.Draw;
                    case BoardState.XWins: return PatientMark == Mark.X ? MatchOutcome.PatientWin : MatchOutcome.AIWin;
                    default: return PatientMark == Mark.O ? MatchOutcome.PatientWin : MatchOutcome.AIWin;
                }
            }
        }

        /// <summary>Aplica mutarea jucatorului aflat la rand. Returneaza false daca mutarea nu e valida.</summary>
        public bool TryPlay(int cell)
        {
            if (IsOver || cell < 0 || cell >= Board.CellCount || !Board.IsEmpty(cell)) return false;
            var mark = Board.NextToMove;
            Board.Place(cell, mark);
            moves.Add(new MoveRecord(moves.Count + 1, cell, mark, mark == PatientMark));
            return true;
        }

        public void Play(int cell)
        {
            if (!TryPlay(cell)) throw new InvalidOperationException($"Mutare invalidă: {cell} pe {Board}");
        }
    }
}
