using System;
using System.Collections.Generic;
using NUnit.Framework;
using Travee.TicTacToe.Core;
using Travee.TicTacToe.Core.AI;

namespace Travee.TicTacToe.Tests
{
    public class AITests
    {
        const int Games = 3000;
        static readonly DifficultySettings Settings = new DifficultySettings();

        // Modele simplificate de pacient folosite în simulări.
        static int RandomPatient(Board b, Mark m, Random r)
        {
            var e = b.EmptyCells();
            return e[r.Next(e.Count)];
        }

        /// <summary>Câștigă dacă poate, blochează dacă trebuie, altfel mută aleator.</summary>
        static int BeginnerPatient(Board b, Mark m, Random r)
        {
            var e = b.EmptyCells();
            foreach (int c in e) if (b.IsWinningMove(c, m)) return c;
            foreach (int c in e) if (b.IsWinningMove(c, m.Opponent())) return c;
            return e[r.Next(e.Count)];
        }

        static int PerfectPatient(Board b, Mark m, Random r)
        {
            var e = b.EmptyCells();
            int best = int.MinValue;
            var bestMoves = new List<int>();
            foreach (int c in e)
            {
                int s = Search.ScoreAfter(b, c, m);
                if (s > best) { best = s; bestMoves.Clear(); }
                if (s == best) bestMoves.Add(c);
            }
            return bestMoves[r.Next(bestMoves.Count)];
        }

        static (int win, int draw, int loss) Simulate(DifficultyLevel level, Func<Board, Mark, Random, int> patient, int seed)
        {
            var rng = new Random(seed);
            var ai = AIFactory.Create(level, Settings, AffectedSide.Left, rng);
            int win = 0, draw = 0, loss = 0;
            for (int g = 0; g < Games; g++)
            {
                var match = new Match(level, CoinFlip.PatientStarts(level, Settings, rng));
                ai.OnNewGame();
                while (!match.IsOver)
                {
                    int cell = match.IsPatientTurn
                        ? patient(match.Board.Clone(), match.PatientMark, rng)
                        : ai.ChooseMove(match.Board.Clone(), match.AIMark);
                    match.Play(cell);
                }
                switch (match.Outcome)
                {
                    case MatchOutcome.PatientWin: win++; break;
                    case MatchOutcome.AIWin: loss++; break;
                    default: draw++; break;
                }
            }
            return (win, draw, loss);
        }

        [Test]
        public void Board_DetectsWinsAndDraws()
        {
            var b = new Board();
            foreach (int c in new[] { 0, 3, 1, 4, 2 }) b.Place(c, b.NextToMove);
            Assert.AreEqual(BoardState.XWins, b.State);

            var d = new Board();
            foreach (int c in new[] { 0, 1, 2, 4, 3, 5, 7, 6, 8 }) d.Place(c, d.NextToMove);
            Assert.AreEqual(BoardState.Draw, d.State);
        }

        [Test]
        public void Minimax_EmptyBoardIsDraw()
        {
            Assert.AreEqual(0, Search.ScoreForX(new Board()));
        }

        [Test]
        public void Level1_PatientAlwaysStarts()
        {
            var rng = new Random(1);
            for (int i = 0; i < 1000; i++)
                Assert.IsTrue(CoinFlip.PatientStarts(DifficultyLevel.Level1, Settings, rng));
        }

        [TestCase(DifficultyLevel.Level1)]
        [TestCase(DifficultyLevel.Level2)]
        public void GentleLevels_PatientNeverLoses(DifficultyLevel level)
        {
            foreach (var patient in new Func<Board, Mark, Random, int>[] { RandomPatient, BeginnerPatient, PerfectPatient })
                Assert.AreEqual(0, Simulate(level, patient, 7).loss, $"{level} a câștigat contra pacientului {patient.Method.Name}");
        }

        [Test]
        public void Level3_BeginnerWinsAroundTwoThirds()
        {
            var (win, _, _) = Simulate(DifficultyLevel.Level3, BeginnerPatient, 11);
            double rate = (double)win / Games;
            Assert.That(rate, Is.InRange(0.55, 0.80), $"Rata de victorie a pacientului: {rate:P1}");
        }

        [Test]
        public void Level4_PerfectPatientNeverLoses_AndSometimesWins()
        {
            var (win, _, loss) = Simulate(DifficultyLevel.Level4, PerfectPatient, 13);
            Assert.AreEqual(0, loss);
            Assert.Greater(win, 0);
        }

        [Test]
        public void Level4_WithoutMistakes_IsUnbeatable()
        {
            var noMistakes = new DifficultySettings { level4MistakeChance = 0f };
            var rng = new Random(5);
            var ai = AIFactory.Create(DifficultyLevel.Level4, noMistakes, AffectedSide.Left, rng);
            for (int g = 0; g < 500; g++)
            {
                var match = new Match(DifficultyLevel.Level4, g % 2 == 0);
                ai.OnNewGame();
                while (!match.IsOver)
                    match.Play(match.IsPatientTurn
                        ? PerfectPatient(match.Board.Clone(), match.PatientMark, rng)
                        : ai.ChooseMove(match.Board.Clone(), match.AIMark));
                Assert.AreNotEqual(MatchOutcome.PatientWin, match.Outcome);
            }
        }
    }
}
