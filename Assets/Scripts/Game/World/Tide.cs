using UnityEngine;

namespace Travee.TicTacToe.World
{
    /// <summary>Mareea: marea inainteaza si se retrage cativa zeci de centimetri, intr-un ritm lent.</summary>
    public sealed class Tide : MonoBehaviour
    {
        [SerializeField, Tooltip("Cât înaintează și se retrage marea (metri).")] float distance = 0.6f;
        [SerializeField, Tooltip("Cât de repede (mai mare = mai rapid).")] float speed = 0.7f;

        Vector3 start;

        void Awake() => start = transform.localPosition;

        void Update()
        {
            transform.localPosition = start + Vector3.back * (distance * Mathf.Sin(Time.time * speed));
        }
    }
}
