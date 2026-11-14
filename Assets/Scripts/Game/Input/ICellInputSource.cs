using System;

namespace Travee.TicTacToe
{
    /// <summary>
    /// Orice sursa care poate selecta o casuta de pe tabla: click de mouse / touch acum,
    /// atingerea cu mana in VR mai tarziu. GameController asculta toate sursele inregistrate.
    /// </summary>
    public interface ICellInputSource
    {
        /// <summary>Emis cu indexul casutei (0..8) atunci cand pacientul o selecteaza.</summary>
        event Action<int> CellSelected;

        /// <summary>Activat de GameController doar cat timp e randul pacientului.</summary>
        bool InputEnabled { get; set; }
    }
}
