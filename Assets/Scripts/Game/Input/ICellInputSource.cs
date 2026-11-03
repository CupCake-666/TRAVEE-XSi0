using System;

namespace Travee.TicTacToe
{
    /// <summary>
    /// Orice sursă care poate selecta o căsuță de pe tablă: click de mouse / touch acum,
    /// atingerea cu mâna în VR mai târziu. GameController ascultă toate sursele înregistrate.
    /// </summary>
    public interface ICellInputSource
    {
        /// <summary>Emis cu indexul căsuței (0..8) atunci când pacientul o selectează.</summary>
        event Action<int> CellSelected;

        /// <summary>Activat de GameController doar cât timp e rândul pacientului.</summary>
        bool InputEnabled { get; set; }
    }
}
