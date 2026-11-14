using UnityEngine;

namespace Travee.TicTacToe.World
{
    /// <summary>
    /// Marcheaza „suportul” unui obiect de decor (obiectul gol care tine modelul). Atributul [SelectionBase] ii
    /// spune editorului ca, la un click pe model in fereastra Scene, sa selecteze acest suport – adica obiectul
    /// pe care vrei de fapt sa-l muti, rotesti sau scalezi – nu bucata de mesh de sub el.
    /// </summary>
    [SelectionBase]
    public sealed class PropHolder : MonoBehaviour
    {
    }
}
