using System;
using System.Collections.Generic;

namespace Travee.TicTacToe.Core.AI
{
    /// <summary>
    /// Nivel 3 – Mediu. Joacă optim (minimax), dar cu probabilitatea constantă
    /// <see cref="DifficultySettings.level3MistakeChance"/> face o mutare greșită aleasă la întâmplare.
    /// Ținta: pacientul câștigă ~60-70% din meciuri; AI-ul poate câștiga, remiza e posibilă.
    /// </summary>
    public sealed class BalancedAI : IAIStrategy
    {
        readonly DifficultySettings settings;
        readonly Random rng;

        public BalancedAI(DifficultySettings settings, Random rng)
        {
            this.settings = settings;
            this.rng = rng;
        }

        public DifficultyLevel Level => DifficultyLevel.Level3;

        public void OnNewGame() { }

        public int ChooseMove(Board board, Mark aiMark)
        {
            var moves = board.EmptyCells();
            var scores = new int[moves.Count];
            int bestSign = int.MinValue;
            for (int i = 0; i < moves.Count; i++)
            {
                scores[i] = Search.ScoreAfter(board, moves[i], aiMark);
                bestSign = Math.Max(bestSign, Math.Sign(scores[i]));
            }

            var optimal = new List<int>();
            var mistakes = new List<int>();
            for (int i = 0; i < moves.Count; i++)
                (Math.Sign(scores[i]) == bestSign ? optimal : mistakes).Add(moves[i]);

            if (mistakes.Count > 0 && rng.NextDouble() < settings.level3MistakeChance)
                return AIUtil.Pick(mistakes, rng);

            return AIUtil.Pick(optimal, rng);
        }
    }
}
