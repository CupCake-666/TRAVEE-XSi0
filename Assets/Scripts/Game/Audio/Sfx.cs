using System.Collections.Generic;
using UnityEngine;

namespace Travee.TicTacToe.Audio
{
    /// <summary>Evenimentele din joc care au sunet. Fisierele din spatele lor sunt in <see cref="Sfx"/>.</summary>
    public enum Sound
    {
        Click,          // buton de UI
        Hover,          // cursorul (mai tarziu: mana) ajunge pe o casuta libera
        PlacePatient,   // pacientul pune piesa
        PlaceAI,        // AI-ul pune piesa
        Target,         // AI-ul isi arata casuta aleasa (glow rosu)
        CoinTick,       // moneda se intoarce
        CoinResult,     // moneda s-a oprit
        Win,            // pacientul a castigat (sau remiza la nivelurile blande)
        Draw,
        Lose,
        Footstep,       // pasii personajului
        Applause,       // personajul aplauda victoria pacientului
    }

    /// <summary>
    /// Toate sunetele jocului. Se apeleaza static, de oriunde: <c>Sfx.Play(Sound.Click)</c>.
    /// Daca nu a fost creat (ex. scena 2D, fara biblioteca de asset-uri), apelurile nu fac nimic.
    /// Are o sursa audio pentru efecte scurte, una pentru efecte lungi (aplauze) si doua pentru ambianta (ca sa poata trece lin de la una la alta).
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        /// <summary>Sunet → fisierele din Assets/Audio/Sfx (fara extensie). Mai multe = se alege unul la intamplare.</summary>
        static readonly Dictionary<Sound, string[]> Files = new Dictionary<Sound, string[]>
        {
            { Sound.Click, new[] { "click_001" } },
            { Sound.Hover, new[] { "tick_001" } },
            { Sound.PlacePatient, new[] { "drop_002" } },
            { Sound.PlaceAI, new[] { "drop_004" } },
            { Sound.Target, new[] { "question_003" } },
            { Sound.CoinTick, new[] { "tick_004" } },
            { Sound.CoinResult, new[] { "confirmation_002" } },
            { Sound.Win, new[] { "jingles_STEEL07" } },
            { Sound.Draw, new[] { "jingles_STEEL00" } },
            { Sound.Lose, new[] { "jingles_STEEL09" } },
            { Sound.Footstep, new[] { "footstep00", "footstep01", "footstep02", "footstep03", "footstep04" } },
            { Sound.Applause, new[] { "applause" } },
        };

        const float AmbientFade = 2f;       // secunde pentru trecerea de la o ambianta la alta si pentru reluare

        static Sfx instance;

        GameAssets assets;
        AudioSource effects, longEffect;
        readonly AudioSource[] ambient = new AudioSource[2];
        readonly float[] ambientTarget = new float[2];     // volumul spre care se indreapta fiecare sursa
        int current;                                        // sursa de ambianta „activa”
        float effectsVolume, ambientVolume;

        public static Sfx Create(Transform parent, GameAssets assets, float effectsVolume, float ambientVolume)
        {
            var go = new GameObject("Sfx");
            go.transform.SetParent(parent, false);
            var sfx = go.AddComponent<Sfx>();
            sfx.assets = assets;
            sfx.effectsVolume = effectsVolume;
            sfx.ambientVolume = ambientVolume;

            sfx.effects = go.AddComponent<AudioSource>();
            sfx.effects.playOnAwake = false;
            sfx.longEffect = go.AddComponent<AudioSource>();
            sfx.longEffect.playOnAwake = false;
            for (int i = 0; i < sfx.ambient.Length; i++)
            {
                sfx.ambient[i] = go.AddComponent<AudioSource>();
                sfx.ambient[i].playOnAwake = false;
                sfx.ambient[i].volume = 0f;
            }

            // Sunetul are nevoie de „urechi”: un AudioListener, de obicei pe camera (in VR: capul pacientului).
            if (FindAnyObjectByType<AudioListener>() == null && Camera.main != null)
                Camera.main.gameObject.AddComponent<AudioListener>();

            instance = sfx;
            return sfx;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Efect 2D (se aude la fel indiferent unde te uiti): UI, piese, rezultat.</summary>
        public static void Play(Sound sound, float volume = 1f)
        {
            if (instance == null) return;
            var clip = instance.Pick(sound);
            if (clip != null) instance.effects.PlayOneShot(clip, volume * instance.effectsVolume);
        }

        /// <summary>
        /// Efect lung (aplauzele), pe o sursa separata, ca sa poata fi oprit cu <see cref="StopLong"/> cand
        /// jucatorul pleaca de pe ecranul de rezultat. (PlayOneShot nu poate fi oprit la jumatate.)
        /// </summary>
        public static void PlayLong(Sound sound, float volume = 1f)
        {
            if (instance == null) return;
            var clip = instance.Pick(sound);
            if (clip == null) return;
            instance.longEffect.clip = clip;
            instance.longEffect.volume = volume * instance.effectsVolume;
            instance.longEffect.Play();
        }

        public static void StopLong()
        {
            if (instance != null) instance.longEffect.Stop();
        }

        /// <summary>Efect 3D: se aude din directia <paramref name="position"/> (in VR, din partea personajului).</summary>
        public static void PlayAt(Sound sound, Vector3 position, float volume = 1f)
        {
            if (instance == null) return;
            var clip = instance.Pick(sound);
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume * instance.effectsVolume);
        }

        /// <summary>Schimba ambianta (pasari, greieri, valuri), cu trecere lina. <c>null</c> = liniste.</summary>
        public static void SetAmbient(string clipName)
        {
            if (instance == null) return;
            SetAmbient(string.IsNullOrEmpty(clipName) ? null : instance.assets.Clip(clipName));
        }

        /// <summary>La fel, dar cu sunetul dat direct (cel ales in Inspector, in ThemeSettings).</summary>
        public static void SetAmbient(AudioClip clip)
        {
            if (instance == null) return;
            if (clip == instance.ambient[instance.current].clip && instance.ambientTarget[instance.current] > 0f) return;
            instance.StartAmbient(clip);
        }

        AudioClip Pick(Sound sound)
        {
            if (assets == null || !Files.TryGetValue(sound, out var names)) return null;
            return assets.Clip(names[Random.Range(0, names.Length)]);
        }

        /// <summary>Sursa activa se stinge, iar cealalta porneste clipul de la inceput si creste in volum.</summary>
        void StartAmbient(AudioClip clip)
        {
            ambientTarget[current] = 0f;
            if (clip == null) return;

            current = 1 - current;
            ambient[current].clip = clip;
            ambient[current].time = 0f;
            ambient[current].Play();
            ambientTarget[current] = ambientVolume;
        }

        void Update()
        {
            // Volumele se apropie lin de tintele lor; o sursa ajunsa la zero se opreste.
            float step = ambientVolume / AmbientFade * Time.unscaledDeltaTime;
            for (int i = 0; i < ambient.Length; i++)
            {
                ambient[i].volume = Mathf.MoveTowards(ambient[i].volume, ambientTarget[i], step);
                if (ambient[i].isPlaying && ambient[i].volume <= 0f && ambientTarget[i] <= 0f) ambient[i].Stop();
            }

            // Reluare fara „cusatura”: inregistrarile nu sunt bucle perfecte, deci nu le lasam sa ajunga la capat.
            // Cu AmbientFade secunde inainte de final, pornim acelasi clip pe a doua sursa si le incrucisam.
            var active = ambient[current];
            if (ambientTarget[current] > 0f && active.clip != null && active.clip.length > AmbientFade * 3f &&
                active.time >= active.clip.length - AmbientFade)
                StartAmbient(active.clip);
        }
    }
}
