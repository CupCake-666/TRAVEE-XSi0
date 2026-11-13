using System.Collections.Generic;
using UnityEngine;

namespace Travee.TicTacToe.Audio
{
    /// <summary>Evenimentele din joc care au sunet. Fișierele din spatele lor sunt în <see cref="Sfx"/>.</summary>
    public enum Sound
    {
        Click,          // buton de UI
        Hover,          // cursorul (mai târziu: mâna) ajunge pe o căsuță liberă
        PlacePatient,   // pacientul pune piesa
        PlaceAI,        // AI-ul pune piesa
        Target,         // AI-ul își arată căsuța aleasă (glow roșu)
        CoinTick,       // moneda se întoarce
        CoinResult,     // moneda s-a oprit
        Win,            // pacientul a câștigat (sau remiză la nivelurile blânde)
        Draw,
        Lose,
        Footstep,       // pașii personajului
        Applause,       // personajul aplaudă victoria pacientului
    }

    /// <summary>
    /// Toate sunetele jocului. Se apelează static, de oriunde: <c>Sfx.Play(Sound.Click)</c>.
    /// Dacă nu a fost creat (ex. scena 2D, fără bibliotecă de asset-uri), apelurile nu fac nimic.
    /// Are o sursă audio pentru efecte scurte, una pentru efecte lungi (aplauze) și două pentru ambianță (ca să poată trece lin de la una la alta).
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        /// <summary>Sunet → fișierele din Assets/Audio/Sfx (fără extensie). Mai multe = se alege unul la întâmplare.</summary>
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

        const float AmbientFade = 2f;       // secunde pentru trecerea de la o ambianță la alta și pentru reluare

        static Sfx instance;

        GameAssets assets;
        AudioSource effects, longEffect;
        readonly AudioSource[] ambient = new AudioSource[2];
        readonly float[] ambientTarget = new float[2];     // volumul spre care se îndreaptă fiecare sursă
        int current;                                        // sursa de ambianță „activă”
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

            // Sunetul are nevoie de „urechi”: un AudioListener, de obicei pe cameră (în VR: capul pacientului).
            if (FindAnyObjectByType<AudioListener>() == null && Camera.main != null)
                Camera.main.gameObject.AddComponent<AudioListener>();

            instance = sfx;
            return sfx;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Efect 2D (se aude la fel indiferent unde te uiți): UI, piese, rezultat.</summary>
        public static void Play(Sound sound, float volume = 1f)
        {
            if (instance == null) return;
            var clip = instance.Pick(sound);
            if (clip != null) instance.effects.PlayOneShot(clip, volume * instance.effectsVolume);
        }

        /// <summary>
        /// Efect lung (aplauzele), pe o sursă separată, ca să poată fi oprit cu <see cref="StopLong"/> când
        /// jucătorul pleacă de pe ecranul de rezultat. (PlayOneShot nu poate fi oprit la jumătate.)
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

        /// <summary>Efect 3D: se aude din direcția <paramref name="position"/> (în VR, din partea personajului).</summary>
        public static void PlayAt(Sound sound, Vector3 position, float volume = 1f)
        {
            if (instance == null) return;
            var clip = instance.Pick(sound);
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume * instance.effectsVolume);
        }

        /// <summary>Schimbă ambianța (păsări, greieri, valuri), cu trecere lină. <c>null</c> = liniște.</summary>
        public static void SetAmbient(string clipName)
        {
            if (instance == null) return;
            var clip = string.IsNullOrEmpty(clipName) ? null : instance.assets.Clip(clipName);
            if (clip == instance.ambient[instance.current].clip && instance.ambientTarget[instance.current] > 0f) return;
            instance.StartAmbient(clip);
        }

        AudioClip Pick(Sound sound)
        {
            if (assets == null || !Files.TryGetValue(sound, out var names)) return null;
            return assets.Clip(names[Random.Range(0, names.Length)]);
        }

        /// <summary>Sursa activă se stinge, iar cealaltă pornește clipul de la început și crește în volum.</summary>
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
            // Volumele se apropie lin de țintele lor; o sursă ajunsă la zero se oprește.
            float step = ambientVolume / AmbientFade * Time.unscaledDeltaTime;
            for (int i = 0; i < ambient.Length; i++)
            {
                ambient[i].volume = Mathf.MoveTowards(ambient[i].volume, ambientTarget[i], step);
                if (ambient[i].isPlaying && ambient[i].volume <= 0f && ambientTarget[i] <= 0f) ambient[i].Stop();
            }

            // Reluare fără „cusătură”: înregistrările nu sunt bucle perfecte, deci nu le lăsăm să ajungă la capăt.
            // Cu AmbientFade secunde înainte de final, pornim același clip pe a doua sursă și le încrucișăm.
            var active = ambient[current];
            if (ambientTarget[current] > 0f && active.clip != null && active.clip.length > AmbientFade * 3f &&
                active.time >= active.clip.length - AmbientFade)
                StartAmbient(active.clip);
        }
    }
}
