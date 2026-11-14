using Travee.TicTacToe.Core;

namespace Travee.TicTacToe
{
    /// <summary>
    /// Orice afisare a tablei: 2D (UI) sau 3D (pe masa). GameController nu stie care e folosita.
    /// </summary>
    public interface IBoardView
    {
        void ResetBoard();
        void SetMark(int cell, Mark mark);
        void HighlightLine(int[] line);
    }
}
