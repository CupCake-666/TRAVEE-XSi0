using UnityEngine;

namespace Travee.TicTacToe.World
{
    /// <summary>Flacara tremura: se lungeste/scurteaza si se apleaca usor, cu zgomot Perlin.</summary>
    public sealed class FlameDance : MonoBehaviour
    {
        [SerializeField, Tooltip("Cât de repede tremură.")] float speed = 5f;
        [SerializeField, Tooltip("Cât se apleacă, în grade.")] float sway = 14f;

        float seed;

        void Awake() => seed = Random.value * 100f;

        void Update()
        {
            float t = Time.time * speed + seed;
            transform.localScale = new Vector3(1f, Mathf.Lerp(0.8f, 1.25f, Mathf.PerlinNoise(t, seed)), 1f);
            transform.localRotation = Quaternion.Euler(
                (Mathf.PerlinNoise(seed, t) - 0.5f) * sway, 0f, (Mathf.PerlinNoise(t, t * 0.5f) - 0.5f) * sway);
        }
    }
}
