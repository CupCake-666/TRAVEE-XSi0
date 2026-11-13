using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Travee.TicTacToe.Editor
{
    /// <summary>
    /// Pregătește asset-urile peisajelor: materialele modelelor din Assets/Art/Nature și setările de import ale
    /// panoramelor de cer. Modelele Quaternius vin fără materiale gata făcute pentru URP: în fișierul FBX e doar
    /// NUMELE materialului (ex. „Leaves_NormalTree”). Aici creăm câte un material URP cu acel nume, îi punem
    /// textura potrivită și îi spunem importatorului să-l folosească.
    /// </summary>
    public static class NatureImport
    {
        public const string NatureFolder = "Assets/Art/Nature";
        public const string SkyFolder = "Assets/Art/Sky";
        const string TexturesFolder = NatureFolder + "/Textures";
        const string MaterialsFolder = NatureFolder + "/Materials";

        /// <summary>Cum se face un material: numele din FBX, textura, (opțional) normal map și dacă e „decupat”.</summary>
        struct Recipe
        {
            public string Name, Texture, Normal;
            public bool Cutout;         // frunze: pixelii transparenți ai texturii nu se desenează
            public bool DoubleSided;    // plante subțiri: se văd din ambele părți
        }

        static readonly Recipe[] Recipes =
        {
            new Recipe { Name = "Bark_NormalTree", Texture = "Bark_NormalTree", Normal = "Bark_NormalTree_Normal" },
            new Recipe { Name = "Bark_TwistedTree", Texture = "Bark_TwistedTree", Normal = "Bark_TwistedTree_Normal" },
            new Recipe { Name = "Leaves_NormalTree", Texture = "Leaves_NormalTree_C", Cutout = true, DoubleSided = true },
            new Recipe { Name = "Leaves_TwistedTree", Texture = "Leaves_TwistedTree_C", Cutout = true, DoubleSided = true },
            new Recipe { Name = "Leaves_Pine", Texture = "Leaf_Pine_C", Cutout = true, DoubleSided = true },
            new Recipe { Name = "Leaves", Texture = "Leaves", Cutout = true, DoubleSided = true },
            new Recipe { Name = "Flowers", Texture = "Flowers", Cutout = true, DoubleSided = true },
            new Recipe { Name = "Grass", Texture = "Grass", DoubleSided = true },
            new Recipe { Name = "Mushrooms", Texture = "Mushrooms" },
            new Recipe { Name = "PathRocks", Texture = "PathRocks_Diffuse" },
            new Recipe { Name = "Rocks", Texture = "Rocks_Diffuse" },
        };

        /// <summary>Rulează toți pașii. Se poate apela de oricâte ori: ce există deja nu se reface.</summary>
        public static void Setup()
        {
            ConfigureNormalMaps();
            ConfigureSkyTextures();
            bool created = CreateMaterials();
            RemapModels(force: created);
        }

        /// <summary>Texturile „_Normal” trebuie importate ca Normal Map, altfel relieful scoarței iese greșit.</summary>
        static void ConfigureNormalMaps()
        {
            if (!AssetDatabase.IsValidFolder(TexturesFolder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileNameWithoutExtension(path).EndsWith("_Normal", StringComparison.Ordinal)) continue;
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.textureType == TextureImporterType.NormalMap) continue;
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// Panoramele de cer sunt imagini mari (până la 8192 px). Unity le-ar micșora implicit la 2048 px și
        /// cerul ar ieși neclar, așa că ridicăm limita și oprim mipmap-urile (cerul e mereu „la aceeași distanță”).
        /// </summary>
        static void ConfigureSkyTextures()
        {
            if (!AssetDatabase.IsValidFolder(SkyFolder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SkyFolder }))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (importer.maxTextureSize == 8192 && !importer.mipmapEnabled) continue;
                importer.maxTextureSize = 8192;
                importer.mipmapEnabled = false;
                importer.wrapModeU = TextureWrapMode.Repeat;    // panorama se închide în cerc
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        static bool CreateMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || !AssetDatabase.IsValidFolder(TexturesFolder)) return false;
            if (!AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.CreateFolder(NatureFolder, "Materials");

            bool created = false;
            foreach (var recipe in Recipes)
            {
                string path = $"{MaterialsFolder}/{recipe.Name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) continue;

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/{recipe.Texture}.png");
                if (texture == null)
                {
                    Debug.LogWarning($"[TRAVEE] Lipsește textura {recipe.Texture}.png pentru materialul {recipe.Name}.");
                    continue;
                }

                var material = new Material(shader) { name = recipe.Name };
                material.SetTexture("_BaseMap", texture);
                material.SetFloat("_Smoothness", 0.05f);            // natură mată, fără luciu
                if (!string.IsNullOrEmpty(recipe.Normal))
                {
                    var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/{recipe.Normal}.png");
                    if (normal != null)
                    {
                        material.SetTexture("_BumpMap", normal);
                        material.EnableKeyword("_NORMALMAP");
                    }
                }
                if (recipe.Cutout)
                {
                    material.SetFloat("_AlphaClip", 1f);            // „Alpha Clipping” din Inspector
                    material.SetFloat("_Cutoff", 0.4f);             // pixelii cu alpha sub 0,4 nu se desenează
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                    material.renderQueue = (int)RenderQueue.AlphaTest;
                }
                if (recipe.DoubleSided) material.SetFloat("_Cull", (float)CullMode.Off);   // „Render Face: Both”

                AssetDatabase.CreateAsset(material, path);
                created = true;
            }
            if (created) AssetDatabase.SaveAssets();
            return created;
        }

        /// <summary>
        /// Spune fiecărui model să-și caute materialele după nume în folderul „Materials” de lângă el.
        /// Materialele negăsite (ex. culorile simple ale palmierilor) rămân cele din fișierul FBX.
        /// </summary>
        static void RemapModels(bool force)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { NatureFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
                if (!force && importer.GetExternalObjectMap().Count > 0) continue;     // deja legat
                if (importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName,
                        ModelImporterMaterialSearch.Local))
                    importer.SaveAndReimport();
            }
        }
    }
}
