using System;
using System.Collections.Generic;

namespace Travee.TicTacToe.Core.AI
{
    public interface IAIStrategy
    {
        DifficultyLevel Level { get; }

        /// <summary>Apelat la începutul fiecărui meci (resetează starea internă, dacă există).</summary>
        void OnNewGame();

        /// <summary>Alege celula în care mută AI-ul. Tabla poate fi modificată temporar, dar e lăsată neschimbată.</summary>
        int ChooseMove(Board board, Mark aiMark);
    }

    public static class AIFactory
    {
        public static IAIStrategy Create(DifficultyLevel level, DifficultySettings settings, AffectedSide side, Random rng)
        {
            switch (level)
            {
                case DifficultyLevel.Level1: return new GentleAI(settings, side, rng);
                case DifficultyLevel.Level2: return new HelperAI(settings, side, rng);
                case DifficultyLevel.Level3: return new BalancedAI(settings, rng);
                default: return new ChallengerAI(settings, rng);
            }
        }
    }

    static class AIUtil
    {
        public static T Pick<T>(IList<T> items, Random rng) => items[rng.Next(items.Count)];

        /// <summary>
        /// +1 pentru coloana de pe partea sănătoasă, -1 pentru coloana de pe partea afectată, 0 pentru mijloc.
        /// Coloanele sunt văzute din perspectiva pacientului (coloana 0 = stânga lui).
        /// </summary>
        public static int HealthySideScore(int cell, AffectedSide side)
        {
            int col = Board.Column(cell);
            if (col == 1) return 0;
            bool isLeft = col == 0;
            bool onAffected = side == AffectedSide.Left ? isLeft : !isLeft;
            return onAffected ? -1 : 1;
        }

        public static bool IsCorner(int cell) => cell == 0 || cell == 2 || cell == 6 || cell == 8;
    }
}
