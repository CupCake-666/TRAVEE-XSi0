using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Travee.TicTacToe.Editor
{
    /// <summary>
    /// Configurează automat orice FBX pus în Assets/Art/Character/ (personaj + animații Mixamo):
    /// rig Humanoid, avatar copiat de pe model, clipuri redenumite după fișier, loop pentru animațiile continue.
    /// </summary>
    public sealed class CharacterImportSettings : AssetPostprocessor
    {
        public const string CharacterFolder = "Assets/Art/Character/";
        public const string AnimationsFolder = CharacterFolder + "Animations/";
        public const string ModelPath = CharacterFolder + "Ch19_nonPBR.fbx";
        public const string TexturesFolder = CharacterFolder + "Textures";

        // Take-ul cu animația aleasă pe Mixamo. Fișierele descărcate „With Skin” mai conțin un take ascuns,
        // „Take 001” (260 de cadre, o poză ciudată cu brațul întins), care nu trebuie importat.
        const string MixamoTake = "mixamo.com";

        // Animațiile care se repetă cât timp personajul rămâne în starea respectivă.
        static readonly HashSet<string> Looping = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Happy Idle", "Thinking", "Look Around", "Happy Walk", "Clapping", "Victory Idle", "Waving",
        };

        bool IsCharacterFbx =>
            assetPath.StartsWith(CharacterFolder, StringComparison.Ordinal) &&
            assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!IsCharacterFbx) return;
            var importer = (ModelImporter)assetImporter;
            bool isModel = assetPath == ModelPath;

            // Configurăm doar la primul import sau cât timp animația nu are încă avatarul modelului.
            if (!importer.importSettingsMissing &&
                (isModel || importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther)) return;

            importer.animationType = ModelImporterAnimationType.Human;

            var modelAvatar = isModel ? null : LoadModelAvatar();
            if (modelAvatar != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = modelAvatar;
            }
            else
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }

            if (!isModel) importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        /// <summary>Texturile extrase din FBX: cele "_Normal" trebuie marcate ca normal map.</summary>
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TexturesFolder + "/", StringComparison.Ordinal)) return;
            if (assetPath.IndexOf("_Normal", StringComparison.OrdinalIgnoreCase) >= 0)
                ((TextureImporter)assetImporter).textureType = TextureImporterType.NormalMap;
        }

        void OnPreprocessAnimation()
        {
            if (!IsCharacterFbx) return;
            var importer = (ModelImporter)assetImporter;
            if (!importer.importSettingsMissing && importer.clipAnimations.Length > 0 && !HasExtraTakes(importer)) return;

            string clipName = Path.GetFileNameWithoutExtension(assetPath);
            var defaults = importer.defaultClipAnimations;
            var clips = defaults.Any(c => c.takeName == MixamoTake)
                ? defaults.Where(c => c.takeName == MixamoTake).ToArray()
                : defaults;
            foreach (var clip in clips)
            {
                clip.name = clipName;                       // Mixamo numește toate clipurile "mixamo.com"
                clip.loopTime = Looping.Contains(clipName);
                // Animațiile sunt "In Place": blocăm rădăcina ca personajul să nu alunece sau să se rotească.
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }

        /// <summary>
        /// Animațiile pot fi importate înaintea modelului (ordine alfabetică). După ce modelul are avatar,
        /// le reimportăm ca să-l copieze.
        /// </summary>
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!imported.Contains(ModelPath)) return;
            EditorApplication.delayCall += () => EnsureModelTextures();
            if (LoadModelAvatar() == null) return;

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { CharacterFolder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == ModelPath) continue;
                if (AssetImporter.GetAtPath(path) is ModelImporter importer &&
                    importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = LoadModelAvatar();
                    importer.SaveAndReimport();
                }
            }
        }

        /// <summary>
        /// Texturile modelului Mixamo sunt încorporate în FBX. Le extragem în Textures/ și reimportăm modelul,
        /// ca materialele (generate automat pentru URP) să le găsească. Altfel personajul apare alb.
        /// </summary>
        public static bool EnsureModelTextures()
        {
            if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter importer)) return false;
            if (AssetDatabase.IsValidFolder(TexturesFolder) &&
                AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesFolder }).Length > 0) return true;

            if (!AssetDatabase.IsValidFolder(TexturesFolder))
                AssetDatabase.CreateFolder(CharacterFolder.TrimEnd('/'), "Textures");
            if (!importer.ExtractTextures(TexturesFolder)) return false;

            AssetDatabase.Refresh();
            importer.materialSearch = ModelImporterMaterialSearch.Local;
            importer.SaveAndReimport();
            Debug.Log($"[TRAVEE] Texturile personajului au fost extrase în {TexturesFolder}.");
            return true;
        }

        /// <summary>Importerul are clipuri din alte take-uri decât cel Mixamo (ex. „Take 001”)?</summary>
        static bool HasExtraTakes(ModelImporter importer) =>
            importer.defaultClipAnimations.Any(c => c.takeName == MixamoTake) &&
            importer.clipAnimations.Any(c => c.takeName != MixamoTake);

        /// <summary>
        /// Aduce fișierele deja importate la regulile curente: scoate take-ul ascuns („Take 001”)
        /// și actualizează care animații se repetă (Loop Time).
        /// </summary>
        public static void FixExtraTakes()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { CharacterFolder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer) || importer.clipAnimations.Length == 0) continue;

                bool changed = false;
                var clips = importer.clipAnimations;
                if (HasExtraTakes(importer))
                {
                    clips = clips.Where(c => c.takeName == MixamoTake).ToArray();
                    changed = true;
                    Debug.Log($"[TRAVEE] Am scos take-ul ascuns din {path}.");
                }

                bool loop = Looping.Contains(Path.GetFileNameWithoutExtension(path));
                foreach (var clip in clips)
                {
                    if (clip.loopTime == loop) continue;
                    clip.loopTime = loop;
                    changed = true;
                }

                if (!changed) continue;
                importer.clipAnimations = clips;   // modificarea marchează importerul ca schimbat
                importer.SaveAndReimport();
            }
        }

        static Avatar LoadModelAvatar() =>
            AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault(a => a.isValid);

        /// <summary>Clipul de animație dintr-un FBX Mixamo (ignoră clipurile "__preview__").</summary>
        public static AnimationClip LoadClip(string fileName)
        {
            foreach (string folder in new[] { AnimationsFolder, CharacterFolder })
            {
                var clip = AssetDatabase.LoadAllAssetRepresentationsAtPath($"{folder}{fileName}.fbx")
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
                if (clip != null) return clip;
            }
            return null;
        }
    }
}
