using Travee.TicTacToe.Core;

namespace Travee.TicTacToe
{
    /// <summary>
    /// Orice afișare a tablei: 2D (UI) sau 3D (pe masă). GameController nu știe care e folosită.
    /// </summary>
    public interface IBoardView
    {
        void ResetBoard();
        void SetMark(int cell, Mark mark);
        void HighlightLine(int[] line);
    }
}
