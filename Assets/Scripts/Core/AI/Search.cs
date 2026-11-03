using System.Collections.Generic;

namespace Travee.TicTacToe.Core.AI
{
    /// <summary>
    /// Algoritmii de căutare folosiți de AI. Spațiul X și 0 e mic (sub 6000 de poziții legale),
    /// deci totul se rezolvă exact, cu memoizare.
    /// </summary>
    public static class Search
    {
        /// <summary>Penalizarea pentru o victorie a AI-ului în evaluarea "ajutătoare" (nivelurile 1-2).</summary>
        public const double AIWinPenalty = 10.0;

        static readonly Dictionary<int, int> minimaxMemo = new Dictionary<int, int>();
        static readonly Dictionary<int, double> helpMemo = new Dictionary<int, double>();

        // ---------------------------------------------------------------- Minimax (joc perfect)

        /// <summary>
        /// Scorul poziției cu joc perfect de ambele părți, din perspectiva lui X.
        /// &gt; 0 = X câștigă, &lt; 0 = O câștigă, 0 = remiză. Victoriile rapide au valoare absolută mai mare.
        /// </summary>
        public static int ScoreForX(Board board)
        {
            var winner = board.Winner();
            int empty = board.EmptyCount;
            if (winner == Mark.X) return 10 + empty;
            if (winner == Mark.O) return -(10 + empty);
            if (empty == 0) return 0;

            int key = board.Key;
            if (minimaxMemo.TryGetValue(key, out int cached)) return cached;

            var toMove = board.NextToMove;
            int best = toMove == Mark.X ? int.MinValue : int.MaxValue;
            for (int cell = 0; cell < Board.CellCount; cell++)
            {
                if (!board.IsEmpty(cell)) continue;
                board.Place(cell, toMove);
                int score = ScoreForX(board);
                board.Clear(cell);
                best = toMove == Mark.X ? System.Math.Max(best, score) : System.Math.Min(best, score);
            }

            minimaxMemo[key] = best;
            return best;
        }

        public static int Score(Board board, Mark perspective) =>
            perspective == Mark.X ? ScoreForX(board) : -ScoreForX(board);

        /// <summary>Scorul (din perspectiva celui care mută) după ce <paramref name="mover"/> pune în <paramref name="cell"/>.</summary>
        public static int ScoreAfter(Board board, int cell, Mark mover)
        {
            board.Place(cell, mover);
            int score = Score(board, mover);
            board.Clear(cell);
            return score;
        }

        /// <summary>Câte răspunsuri ale adversarului (aflat la mutare) duc la victoria lui, cu joc perfect.</summary>
        public static int CountWinningReplies(Board board, Mark replier)
        {
            int n = 0;
            foreach (int cell in board.EmptyCells())
                if (ScoreAfter(board, cell, replier) > 0) n++;
            return n;
        }

        /// <summary>Câte răspunsuri ale adversarului (aflat la mutare) duc la pierderea lui, cu joc perfect.</summary>
        public static int CountLosingReplies(Board board, Mark replier)
        {
            int n = 0;
            foreach (int cell in board.EmptyCells())
                if (ScoreAfter(board, cell, replier) < 0) n++;
            return n;
        }

        public static bool HasImmediateWin(Board board, Mark mark)
        {
            for (int cell = 0; cell < Board.CellCount; cell++)
                if (board.IsWinningMove(cell, mark)) return true;
            return false;
        }

        // ---------------------------------------------------------------- Expectimax (AI ajutător)

        /// <summary>
        /// Probabilitatea ca pacientul să câștige, presupunând că pacientul mută ALEATOR (cel mai slab caz,
        /// de ex. mișcări imprecise) și AI-ul joacă cât se poate de ajutător de aici înainte.
        /// O victorie a AI-ului valorează -<see cref="AIWinPenalty"/>, deci AI-ul o evită cu orice preț.
        /// </summary>
        public static double HelpValue(Board board, Mark aiMark)
        {
            var patientMark = aiMark.Opponent();
            var winner = board.Winner();
            if (winner == patientMark) return 1.0;
            if (winner == aiMark) return -AIWinPenalty;
            if (board.IsFull) return 0.0;

            int key = board.Key * 3 + (int)aiMark;
            if (helpMemo.TryGetValue(key, out double cached)) return cached;

            var toMove = board.NextToMove;
            double result;
            if (toMove == aiMark)
            {
                result = double.NegativeInfinity;
                for (int cell = 0; cell < Board.CellCount; cell++)
                {
                    if (!board.IsEmpty(cell)) continue;
                    result = System.Math.Max(result, HelpValueAfter(board, cell, aiMark));
                }
            }
            else
            {
                double sum = 0;
                int count = 0;
                for (int cell = 0; cell < Board.CellCount; cell++)
                {
                    if (!board.IsEmpty(cell)) continue;
                    board.Place(cell, toMove);
                    sum += HelpValue(board, aiMark);
                    board.Clear(cell);
                    count++;
                }
                result = sum / count;
            }

            helpMemo[key] = result;
            return result;
        }

        /// <summary>Valoarea ajutătoare după ce AI-ul pune în <paramref name="cell"/>.</summary>
        public static double HelpValueAfter(Board board, int cell, Mark aiMark)
        {
            board.Place(cell, aiMark);
            double value = HelpValue(board, aiMark);
            board.Clear(cell);
            return value;
        }
    }
}
