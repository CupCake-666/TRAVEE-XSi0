using UnityEngine;

namespace Travee.TicTacToe.World
{
    /// <summary>
    /// Setarile unui peisaj, editabile din Inspector: cerul, lumina ambienta, ceata, sunetul de fundal si
    /// lumina principala. Sta pe obiectul Decor_Forest / Decor_Mountain / Decor_Beach. Daca exista, jocul
    /// foloseste valorile de aici; daca lipseste, le ia pe cele scrise in cod (EnvironmentBuilder.Look).
    /// </summary>
    public sealed class ThemeSettings : MonoBehaviour
    {
        [Tooltip("Peisajul căruia îi aparțin aceste setări.")]
        public WorldTheme theme;

        [Tooltip("Cerul, lumina ambientă și ceața.")]
        public ThemeLook look;

        [Tooltip("Fotografia 360° folosită ca cer. Gol = cerul desenat din cod (culorile de mai sus).")]
        public Texture2D skyPanorama;

        [Tooltip("Sunetul de fundal al peisajului (se repetă).")]
        public AudioClip ambientSound;

        [Tooltip("Lumina principală (soarele / luna). Orice lumină din scenă; de obicei copilul „Soare”.")]
        public Light sun;
    }
}
