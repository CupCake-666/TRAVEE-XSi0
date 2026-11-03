using System;
using System.Collections.Generic;

namespace Travee.TicTacToe.Core.AI
{
    /// <summary>
    /// Nivel 2 – Ușor. Pacientul tot nu poate pierde, dar AI-ul "pare" că se apără:
    /// blochează amenințările și ocupă poziții bune atâta timp cât asta nu reduce prea mult
    /// șansa pacientului de a câștiga (<see cref="DifficultySettings.level2DefenseSlack"/>).
    /// </summary>
    public sealed class HelperAI : IAIStrategy
    {
        const double Epsilon = 1e-6;

        readonly DifficultySettings settings;
        readonly AffectedSide side;
        readonly Random rng;

        public HelperAI(DifficultySettings settings, AffectedSide side, Random rng)
        {
            this.settings = settings;
            this.side = side;
            this.rng = rng;
        }

        public DifficultyLevel Level => DifficultyLevel.Level2;

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

            // Mutări "permise": nu scad prea mult șansa pacientului și nu îi iau orice șansă de victorie.
            double threshold = best - settings.level2DefenseSlack;
            threshold = best > 0 ? Math.Max(threshold, Epsilon) : best - Epsilon;

            var candidates = new List<int>();
            for (int i = 0; i < moves.Count; i++)
                if (values[i] >= threshold) candidates.Add(moves[i]);

            // Dintre ele, alege mutarea care arată cel mai "inteligent".
            double bestPlausibility = double.NegativeInfinity;
            var chosen = new List<int>();
            foreach (int cell in candidates)
            {
                double p = Plausibility(board, cell, aiMark);
                if (p > bestPlausibility + Epsilon)
                {
                    bestPlausibility = p;
                    chosen.Clear();
                    chosen.Add(cell);
                }
                else if (Math.Abs(p - bestPlausibility) <= Epsilon)
                {
                    chosen.Add(cell);
                }
            }

            return AIUtil.Pick(chosen, rng);
        }

        double Plausibility(Board board, int cell, Mark aiMark)
        {
            double score = 0;
            if (board.IsWinningMove(cell, aiMark.Opponent())) score += 4;   // blochează pacientul
            if (CreatesOpenTwo(board, cell, aiMark)) score += 1;              // își face o "amenințare"
            if (cell == 4) score += 0.6;
            else if (AIUtil.IsCorner(cell)) score += 0.3;
            if (settings.steerTowardAffectedSide) score += 0.2 * AIUtil.HealthySideScore(cell, side);
            return score;
        }

        static bool CreatesOpenTwo(Board board, int cell, Mark mark)
        {
            foreach (var line in Board.Lines)
            {
                if (Array.IndexOf(line, cell) < 0) continue;
                int own = 0, empty = 0;
                foreach (int c in line)
                {
                    if (c == cell) continue;
                    if (board[c] == mark) own++;
                    else if (board[c] == Mark.None) empty++;
                }
                if (own == 1 && empty == 1) return true;
            }
            return false;
        }
    }
}
