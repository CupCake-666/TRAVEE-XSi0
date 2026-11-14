using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Travee.TicTacToe.Editor
{
    /// <summary>
    /// Configureaza automat orice FBX pus in Assets/Art/Character/ (personaj + animatii Mixamo):
    /// rig Humanoid, avatar copiat de pe model, clipuri redenumite dupa fisier, loop pentru animatiile continue.
    /// </summary>
    public sealed class CharacterImportSettings : AssetPostprocessor
    {
        public const string CharacterFolder = "Assets/Art/Character/";
        public const string AnimationsFolder = CharacterFolder + "Animations/";
        public const string ModelPath = CharacterFolder + "Ch19_nonPBR.fbx";
        public const string TexturesFolder = CharacterFolder + "Textures";

        // Take-ul cu animatia aleasa pe Mixamo. Fisierele descarcate „With Skin” mai contin un take ascuns,
        // „Take 001” (260 de cadre, o poza ciudata cu bratul intins), care nu trebuie importat.
        const string MixamoTake = "mixamo.com";

        // Animatiile care se repeta cat timp personajul ramane in starea respectiva.
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

            // Configuram doar la primul import sau cat timp animatia nu are inca avatarul modelului.
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
                clip.name = clipName;                       // Mixamo numeste toate clipurile "mixamo.com"
                clip.loopTime = Looping.Contains(clipName);
                // Animatiile sunt "In Place": blocam radacina ca personajul sa nu alunece sau sa se roteasca.
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
        /// Animatiile pot fi importate inaintea modelului (ordine alfabetica). Dupa ce modelul are avatar,
        /// le reimportam ca sa-l copieze.
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
        /// Texturile modelului Mixamo sunt incorporate in FBX. Le extragem in Textures/ si reimportam modelul,
        /// ca materialele (generate automat pentru URP) sa le gaseasca. Altfel personajul apare alb.
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

        /// <summary>Importerul are clipuri din alte take-uri decat cel Mixamo (ex. „Take 001”)?</summary>
        static bool HasExtraTakes(ModelImporter importer) =>
            importer.defaultClipAnimations.Any(c => c.takeName == MixamoTake) &&
            importer.clipAnimations.Any(c => c.takeName != MixamoTake);

        /// <summary>
        /// Aduce fisierele deja importate la regulile curente: scoate take-ul ascuns („Take 001”)
        /// si actualizeaza care animatii se repeta (Loop Time).
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
                importer.clipAnimations = clips;   // modificarea marcheaza importerul ca schimbat
                importer.SaveAndReimport();
            }
        }

        static Avatar LoadModelAvatar() =>
            AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault(a => a.isValid);

        /// <summary>Clipul de animatie dintr-un FBX Mixamo (ignora clipurile "__preview__").</summary>
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
