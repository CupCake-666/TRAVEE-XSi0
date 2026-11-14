using UnityEngine;

namespace Travee.TicTacToe.World
{
    /// <summary>Palpairea focului: intensitatea luminii variaza lin, cu zgomot Perlin.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class Flicker : MonoBehaviour
    {
        [SerializeField, Tooltip("Cât de repede pâlpâie.")] float speed = 4f;
        [SerializeField, Tooltip("Intensitatea minimă și maximă, ca fracțiune din intensitatea luminii.")]
        Vector2 range = new Vector2(0.75f, 1.15f);

        Light source;
        float baseIntensity;

        void Awake()
        {
            source = GetComponent<Light>();
            baseIntensity = source.intensity;
        }

        void Update()
        {
            source.intensity = baseIntensity * Mathf.Lerp(range.x, range.y, Mathf.PerlinNoise(Time.time * speed, 0.5f));
        }
    }
}
