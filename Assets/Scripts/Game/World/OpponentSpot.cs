using Travee.TicTacToe.Core;
using UnityEngine;

namespace Travee.TicTacToe.World
{
    /// <summary>
    /// Locul in care asteapta personajul, pe stanga sau pe dreapta pacientului. E un obiect gol din scena:
    /// il muti cu mouse-ul si personajul va astepta acolo. In editor se vede ca o sfera cu o sageata.
    /// </summary>
    public sealed class OpponentSpot : MonoBehaviour
    {
        [Tooltip("Pe ce parte a pacientului e acest loc.")]
        public AffectedSide side = AffectedSide.Left;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.8f);
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.05f, 0.08f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f, new Vector3(0.5f, 1.8f, 0.5f));
        }
    }
}
