using System;
using System.Collections.Generic;

namespace Travee.TicTacToe.Core.AI
{
    /// <summary>
    /// Nivel 1 – Foarte ușor. Pacientul nu poate pierde.
    /// AI-ul alege mutarea care maximizează șansa pacientului de a câștiga chiar dacă acesta mută aleator:
    /// nu închide niciodată o linie proprie, nu blochează și lasă liniile pacientului deschise.
    /// </summary>
    public sealed class GentleAI : IAIStrategy
    {
        const double Tolerance = 0.02;

        readonly DifficultySettings settings;
        readonly AffectedSide side;
        readonly Random rng;

        public GentleAI(DifficultySettings settings, AffectedSide side, Random rng)
        {
            this.settings = settings;
            this.side = side;
            this.rng = rng;
        }

        public DifficultyLevel Level => DifficultyLevel.Level1;

        public void OnNewGame() { }

        public int ChooseMove(Board board, Mark aiMark)
        {
            var moves = board.EmptyCells();
            var values = new double[moves.Count];
            double best = double.NegativeInfinity;
            for (int i = 0; i < moves.Count; i++)
            {
                values[i] = Search.HelpValueAfter(board, moves[i], aiMark);
                best = Math.Max(best, values[i]);
            }

            var candidates = new List<int>();
            for (int i = 0; i < moves.Count; i++)
                if (values[i] >= best - Tolerance) candidates.Add(moves[i]);

            if (settings.steerTowardAffectedSide)
                candidates = KeepHealthiestSide(candidates);

            return AIUtil.Pick(candidates, rng);
        }

        List<int> KeepHealthiestSide(List<int> candidates)
        {
            int bestScore = int.MinValue;
            foreach (int c in candidates) bestScore = Math.Max(bestScore, AIUtil.HealthySideScore(c, side));
            return candidates.FindAll(c => AIUtil.HealthySideScore(c, side) == bestScore);
        }
    }
}
