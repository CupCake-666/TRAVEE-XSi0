using System.Collections.Generic;
using UnityEngine;

namespace Travee.TicTacToe
{
    /// <summary>
    /// „Biblioteca” de asset-uri a jocului: modelele 3D ale peisajelor, sunetele si materialele de cer.
    /// E un ScriptableObject (un fisier .asset cu date, ca un Data Asset din Unreal), completat automat de
    /// meniul TRAVEE. Codul cere un asset dupa nume: <c>assets.Model("tree_oak")</c>.
    /// Fiind referit din scena, tot ce e in el ajunge sigur si in build.
    /// </summary>
    public sealed class GameAssets : ScriptableObject
    {
        [Tooltip("Modelele din Assets/Art/Nature (Kenney Nature Kit, CC0).")]
        public GameObject[] models = new GameObject[0];
        [Tooltip("Sunetele din Assets/Audio (efecte și ambianțe, CC0).")]
        public AudioClip[] clips = new AudioClip[0];
        [Tooltip("Material cu shader-ul Skybox/Panoramic: cerul (imaginea lui e generată din cod).")]
        public Material skyMaterial;
        [Tooltip("Material URP Unlit: obiecte care „luminează” singure, neafectate de umbre (flăcări, licurici).")]
        public Material glowMaterial;
        [Tooltip("Panoramele de cer din Assets/Art/Sky (fotografii 360°, Poly Haven, CC0).")]
        public Texture2D[] skies = new Texture2D[0];

        Dictionary<string, GameObject> modelsByName;
        Dictionary<string, AudioClip> clipsByName;

        public GameObject Model(string name)
        {
            if (modelsByName == null)
            {
                modelsByName = new Dictionary<string, GameObject>();
                foreach (var model in models)
                    if (model != null) modelsByName[model.name] = model;
            }
            return modelsByName.TryGetValue(name, out var found) ? found : null;
        }

        /// <summary>Panorama de cer cu numele dat, sau null (atunci cerul se deseneaza din cod).</summary>
        public Texture2D SkyTexture(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var sky in skies)
                if (sky != null && sky.name == name) return sky;
            return null;
        }

        public AudioClip Clip(string name)
        {
            if (clipsByName == null)
            {
                clipsByName = new Dictionary<string, AudioClip>();
                foreach (var clip in clips)
                    if (clip != null) clipsByName[clip.name] = clip;
            }
            return clipsByName.TryGetValue(name, out var found) ? found : null;
        }
    }
}
