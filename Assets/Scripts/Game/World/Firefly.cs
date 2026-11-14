using UnityEngine;

namespace Travee.TicTacToe.World
{
    /// <summary>Un licurici: pluteste incet in jurul locului lui si palpaie (ii variem marimea).</summary>
    public sealed class Firefly : MonoBehaviour
    {
        [SerializeField, Tooltip("Cât se îndepărtează de locul lui (metri).")] float wander = 1.6f;
        [SerializeField, Tooltip("Cât de repede plutește.")] float speed = 0.22f;

        Vector3 origin;
        float seed, size;

        void Awake()
        {
            origin = transform.localPosition;
            size = transform.localScale.x;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float t = Time.time * speed + seed;
            transform.localPosition = origin + new Vector3(
                Mathf.PerlinNoise(t, seed) - 0.5f,
                (Mathf.PerlinNoise(seed, t) - 0.5f) * 0.5f,
                Mathf.PerlinNoise(t + 50f, seed + 50f) - 0.5f) * wander;
            float glow = Mathf.SmoothStep(0.15f, 1f, Mathf.PerlinNoise(t * 4f, seed * 2f));
            transform.localScale = Vector3.one * (size * glow);
        }
    }
}
