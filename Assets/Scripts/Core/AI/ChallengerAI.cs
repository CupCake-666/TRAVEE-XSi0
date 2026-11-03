using System;
using System.Collections.Generic;

namespace Travee.TicTacToe.Core.AI
{
    /// <summary>
    /// Nivel 4 – Provocare (fair). Joacă perfect și, dintre mutările optime, o alege pe cea care întinde
    /// cele mai multe capcane. X și 0 e un joc rezolvat (cu joc perfect iese remiză), așa că, pentru ca victoria
    /// să fie posibilă, AI-ul face rar (maximum o dată pe meci) o greșeală SUBTILĂ: o mutare pierzătoare care
    /// nu oferă o victorie imediată și pe care pacientul o poate pedepsi doar printr-un răspuns precis.
    /// </summary>
    public sealed class ChallengerAI : IAIStrategy
    {
        readonly DifficultySettings settings;
        readonly Random rng;
        bool mistakeMade;

        public ChallengerAI(DifficultySettings settings, Random rng)
        {
            this.settings = settings;
            this.rng = rng;
        }

        public DifficultyLevel Level => DifficultyLevel.Level4;

        /// <summary>True dacă AI-ul a făcut deja greșeala subtilă în meciul curent (util pentru log).</summary>
        public bool MistakeMade => mistakeMade;

        public void OnNewGame() => mistakeMade = false;

        public int ChooseMove(Board board, Mark aiMark)
        {
            var patientMark = aiMark.Opponent();
            var moves = board.EmptyCells();
            var scores = new int[moves.Count];
            int bestScore = int.MinValue;
            for (int i = 0; i < moves.Count; i++)
            {
                scores[i] = Search.ScoreAfter(board, moves[i], aiMark);
                bestScore = Math.Max(bestScore, scores[i]);
            }

            if (!mistakeMade && rng.NextDouble() < settings.level4MistakeChance)
            {
                var subtle = FindSubtleMistakes(board, moves, scores, aiMark, patientMark);
                if (subtle.Count > 0)
                {
                    mistakeMade = true;
                    return AIUtil.Pick(subtle, rng);
                }
            }

            // Câștigă cât mai repede dacă poate.
            if (bestScore > 0)
            {
                var winning = new List<int>();
                for (int i = 0; i < moves.Count; i++)
                    if (scores[i] == bestScore) winning.Add(moves[i]);
                return AIUtil.Pick(winning, rng);
            }

            // Altfel: dintre mutările cu același rezultat optim, cea cu cele mai multe capcane.
            int bestSign = Math.Sign(bestScore);
            int bestTraps = -1;
            var chosen = new List<int>();
            for (int i = 0; i < moves.Count; i++)
            {
                if (Math.Sign(scores[i]) != bestSign) continue;
                board.Place(moves[i], aiMark);
                int traps = Search.CountLosingReplies(board, patientMark);
                board.Clear(moves[i]);

                if (traps > bestTraps)
                {
                    bestTraps = traps;
                    chosen.Clear();
                }
                if (traps == bestTraps) chosen.Add(moves[i]);
            }
            return AIUtil.Pick(chosen, rng);
        }

        List<int> FindSubtleMistakes(Board board, List<int> moves, int[] scores, Mark aiMark, Mark patientMark)
        {
            var result = new List<int>();
            int fewestPunishers = int.MaxValue;
            for (int i = 0; i < moves.Count; i++)
            {
                if (scores[i] >= 0) continue; // doar mutări care pierd cu joc perfect

                board.Place(moves[i], aiMark);
                bool obvious = Search.HasImmediateWin(board, patientMark);
                int punishers = Search.CountWinningReplies(board, patientMark);
                board.Clear(moves[i]);

                if (obvious || punishers > settings.level4MaxPunishingReplies) continue;
                if (punishers < fewestPunishers)
                {
                    fewestPunishers = punishers;
                    result.Clear();
                }
                if (punishers == fewestPunishers) result.Add(moves[i]);
            }
            return result;
        }
    }
}
