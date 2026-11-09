# 09 – Editor: SceneSetup.cs și CharacterImportSettings.cs

Fișierele din `Assets/Scripts/Editor/`. Rulează **doar în editorul Unity**, nu ajung în aplicația finală
(asmdef-ul lor are `includePlatforms: ["Editor"]`). Ele pregătesc proiectul ca să nu fie nevoie de setări
manuale în Inspector.

---

## Meniul TRAVEE (SceneSetup.cs)

| Comandă | Ce face |
|---|---|
| **Creează scena X și 0 (3D)** | trece rendererul pe 3D, extrage texturile personajului, corectează importul animațiilor, generează Animator Controller-ul și materialul transparent, creează scena `XSi0_3D` |
| **Regenerează animațiile personajului** | corectează importul animațiilor și regenerează doar Animator Controller-ul (după ce schimbi o animație) |
| **Creează scena X și 0 (2D)** | varianta 2D veche (scena `XSi0`) |

### Constante
```csharp
const string ScenePath2D = "Assets/Scenes/XSi0.unity";
const string ScenePath3D = "Assets/Scenes/XSi0_3D.unity";
const string ControllerPath = CharacterImportSettings.CharacterFolder + "OpponentAnimator.controller";
const string RendererPath = "Assets/Settings/UniversalRenderer3D.asset";
const string SeeThroughMaterialPath = "Assets/Art/Materials/BoardSeeThrough.mat";
```

### `CreateScene3D()`
```csharp
[MenuItem("TRAVEE/Creează scena X și 0 (3D)")]   // ← apare în bara de meniu a editorului
static void CreateScene3D()
{
    EnsureUniversalRenderer();                    // ← 1. renderer care luminează obiecte 3D
    CharacterImportSettings.EnsureModelTextures();// ← 2. texturile personajului (altfel apare alb)
    CharacterImportSettings.FixExtraTakes();      // ← 3. scoate take-ul ascuns, actualizează Loop-urile

    var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterImportSettings.ModelPath);
    // ← AssetDatabase = acces la fișierele proiectului din editor (ca Content Browser-ul, din cod)
    if (model == null) Debug.LogWarning("...");   // ← fără model, scena merge fără personaj

    var controller = BuildAnimatorController();   // ← 4. mașina de stări
    var seeThrough = EnsureSeeThroughMaterial();  // ← 5. materialul transparent
    CreateScene(ScenePath3D, ViewMode.ThreeD, model, controller, seeThrough);   // ← 6. scena
}

public static void BatchCreateScene3D() => CreateScene3D();
// ← aceeași comandă, apelabilă din linia de comandă (Unity.exe -batchmode -executeMethod ...)

[MenuItem("TRAVEE/Regenerează animațiile personajului")]
static void RebuildAnimator()
{
    CharacterImportSettings.FixExtraTakes();
    BuildAnimatorController();
}
```

### `CreateScene(...)`
```csharp
static void CreateScene(string path, ViewMode mode, GameObject model, RuntimeAnimatorController controller,
    Material seeThrough)
{
    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
    // ← „Vrei să salvezi scena curentă?”; Cancel → ne oprim

    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // ← scenă goală

    var cam = new GameObject("Main Camera").AddComponent<Camera>();
    cam.tag = "MainCamera";
    cam.orthographic = mode == ViewMode.TwoD;   // ← în 3D, Room3D o reconfigurează oricum
    cam.transform.position = new Vector3(0, 0, -10);

    var bootstrap = new GameObject("XSi0").AddComponent<TicTacToeBootstrap>();
    var so = new SerializedObject(bootstrap);
    // ← câmpurile bootstrap-ului sunt private ([SerializeField]); SerializedObject le scrie exact ca Inspector-ul
    so.FindProperty("viewMode").enumValueIndex = (int)mode;
    so.FindProperty("opponentModel").objectReferenceValue = model;
    so.FindProperty("opponentAnimator").objectReferenceValue = controller;
    so.FindProperty("boardSeeThroughMaterial").objectReferenceValue = seeThrough;
    so.ApplyModifiedPropertiesWithoutUndo();

    EditorSceneManager.SaveScene(scene, path);

    var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
    scenes.Insert(0, new EditorBuildSettingsScene(path, true));   // ← prima scenă din build = cea care pornește
    EditorBuildSettings.scenes = scenes.ToArray();
}
```

### `BuildAnimatorController()`
```csharp
static RuntimeAnimatorController BuildAnimatorController()
{
    AssetDatabase.DeleteAsset(ControllerPath);    // ← îl refacem de la zero de fiecare dată
    var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

    var layers = controller.layers;               // ← `layers` întoarce o COPIE a tabloului...
    layers[0].iKPass = true;                      // ← ...o modificăm...
    controller.layers = layers;                   // ← ...și o punem la loc (altfel modificarea se pierde)
    // ← IK Pass: fără el, Unity nu apelează OnAnimatorIK (mâna n-ar ajunge la căsuță)
    var machine = controller.layers[0].stateMachine;

    int position = 0;
    foreach (var pair in OpponentAnimations.Files)   // ← pentru fiecare rol (Idle, Walk, ...)
    {
        var clip = CharacterImportSettings.LoadClip(pair.Value);   // ← clipul din fișierul FBX
        if (clip == null) { Debug.LogWarning(...); continue; }    // ← fișier lipsă: sărim rolul

        var state = machine.AddState(pair.Key, new Vector3(300 + (position % 3) * 220, (position / 3) * 70, 0));
        // ← o stare cu numele rolului; poziția e doar pentru aranjarea în fereastra Animator
        state.motion = clip;
        if (pair.Key == OpponentAnimations.Idle) machine.defaultState = state;   // ← starea de pornire
        position++;
    }
    AssetDatabase.SaveAssets();
    return controller;
}
```
Controller-ul nu are tranziții desenate: `OpponentCharacter` trece între stări din cod, cu `CrossFadeInFixedTime`.
Îl poți deschide totuși în fereastra **Animator** ca să vezi stările.

### `EnsureSeeThroughMaterial()`
```csharp
var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BoardSeeThrough" };
material.SetFloat("_Surface", 1f);            // ← tipul suprafeței: Transparent (0 = Opaque)
material.SetFloat("_Blend", 0f);              // ← modul de amestec: Alpha
material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);          // ← formula de amestec a culorilor:
material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);  //   culoare = sursă·α + fundal·(1−α)
material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
material.SetFloat("_ZWrite", 0f);             // ← nu scrie adâncime (standard pentru transparență)
material.SetOverrideTag("RenderType", "Transparent");
material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");   // ← activează varianta transparentă a shader-ului
material.renderQueue = (int)RenderQueue.Transparent;  // ← 3000: se desenează după obiectele opace
AssetDatabase.CreateAsset(material, SeeThroughMaterialPath);
```
Acestea sunt exact setările pe care Unity le face când alegi „Surface Type: Transparent” în Inspector.
Materialul e salvat ca **fișier** (nu creat doar la rulare) pentru că Unity include în build doar variantele de
shader folosite de materialele din proiect. Un material transparent creat doar din cod ar apărea opac în build.

### `EnsureUniversalRenderer()`
```csharp
if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)) return;
// ← `is ... pipeline` = verifică tipul și, dacă se potrivește, îl pune în variabila `pipeline` (ca dynamic_cast)

var so = new SerializedObject(pipeline);
var list = so.FindProperty("m_RendererDataList");   // ← lista de renderere a pipeline-ului (câmp intern)
if (list.arraySize > 0 && list.GetArrayElementAtIndex(0).objectReferenceValue is UniversalRendererData)
    return;                                     // ← deja 3D

var existing = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
if (existing == null)
{
    pipeline.LoadBuiltinRendererData(RendererType.UniversalRenderer);   // ← funcția URP care creează un renderer
    AssetDatabase.SaveAssets();
    AssetDatabase.MoveAsset("Assets/UniversalRenderer.asset", RendererPath);   // ← îl mutăm în Settings
    existing = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
    so.Update();
}
list.GetArrayElementAtIndex(0).objectReferenceValue = existing;   // ← primul renderer = cel 3D
so.FindProperty("m_DefaultRendererIndex").intValue = 0;
so.ApplyModifiedPropertiesWithoutUndo();
```
Proiectul a pornit din șablonul **2D**, al cărui renderer (Renderer2D) nu luminează obiecte 3D: totul ar apărea
negru sau neluminat. Funcția îl înlocuiește o singură dată cu un Universal Renderer. UI-ul merge la fel.

---

## Importul automat al personajului (CharacterImportSettings.cs)

Clasa moștenește `AssetPostprocessor`: Unity îi apelează automat metodele **la fiecare import** de fișier.
Orice FBX pus în `Assets/Art/Character/` e configurat fără nicio setare manuală.

### Constante
```csharp
public const string CharacterFolder = "Assets/Art/Character/";
public const string AnimationsFolder = CharacterFolder + "Animations/";
public const string ModelPath = CharacterFolder + "Ch19_nonPBR.fbx";   // ← modelul (T-pose, cu skin)
public const string TexturesFolder = CharacterFolder + "Textures";

const string MixamoTake = "mixamo.com";
// ← numele take-ului cu animația aleasă pe Mixamo. Fișierele descărcate „With Skin” mai conțin un take
//   ascuns, „Take 001” (260 de cadre, o poză cu brațul întins), care NU trebuie importat

static readonly HashSet<string> Looping = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "Happy Idle", "Thinking", "Look Around", "Happy Walk", "Clapping", "Victory Idle", "Waving",
};
// ← animațiile care se repetă (Loop Time); restul rulează o dată. HashSet = mulțime (ca std::unordered_set)
```

### `OnPreprocessModel()` – rigul
```csharp
void OnPreprocessModel()                        // ← apelată înainte de importul unui model
{
    if (!IsCharacterFbx) return;                // ← doar FBX-urile din folderul personajului
    var importer = (ModelImporter)assetImporter;   // ← setările de import (cele din Inspector, tab-ul Rig)
    bool isModel = assetPath == ModelPath;

    if (!importer.importSettingsMissing &&
        (isModel || importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther)) return;
    // ← configurăm doar la primul import, sau cât timp o animație nu are încă avatarul modelului

    importer.animationType = ModelImporterAnimationType.Human;   // ← rig Humanoid

    var modelAvatar = isModel ? null : LoadModelAvatar();
    if (modelAvatar != null)
    {
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;   // ← animațiile folosesc avatarul modelului
        importer.sourceAvatar = modelAvatar;
    }
    else
    {
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;   // ← modelul își creează avatarul
    }

    if (!isModel) importer.materialImportMode = ModelImporterMaterialImportMode.None;
    // ← fișierele de animație nu au nevoie de materiale
}
```
Avatarul copiat garantează că toate animațiile se potrivesc exact pe scheletul modelului.

### `OnPreprocessTexture()` – normal map
```csharp
void OnPreprocessTexture()
{
    if (!assetPath.StartsWith(TexturesFolder + "/", StringComparison.Ordinal)) return;
    if (assetPath.IndexOf("_Normal", StringComparison.OrdinalIgnoreCase) >= 0)
        ((TextureImporter)assetImporter).textureType = TextureImporterType.NormalMap;
    // ← textura „…_Normal.png” conține relieful, nu culori: trebuie marcată ca normal map
}
```

### `OnPreprocessAnimation()` – clipurile
```csharp
void OnPreprocessAnimation()
{
    if (!IsCharacterFbx) return;
    var importer = (ModelImporter)assetImporter;
    if (!importer.importSettingsMissing && importer.clipAnimations.Length > 0 && !HasExtraTakes(importer)) return;
    // ← deja configurat corect: nu atingem nimic

    string clipName = Path.GetFileNameWithoutExtension(assetPath);   // ← ex. "Clapping"
    var defaults = importer.defaultClipAnimations;                   // ← toate take-urile din fișier
    var clips = defaults.Any(c => c.takeName == MixamoTake)
        ? defaults.Where(c => c.takeName == MixamoTake).ToArray()    // ← păstrăm DOAR take-ul Mixamo
        : defaults;
    // ← Any / Where = LINQ: „există vreun element care...” / „doar elementele care...” (ca std::any_of / copy_if)
    foreach (var clip in clips)
    {
        clip.name = clipName;                   // ← Mixamo numește toate clipurile "mixamo.com"; le dăm numele fișierului
        clip.loopTime = Looping.Contains(clipName);
        clip.lockRootRotation = true;           // ← „Bake Into Pose”: rădăcina nu se rotește...
        clip.lockRootHeightY = true;            // ← ...nu urcă/coboară...
        clip.lockRootPositionXZ = true;         // ← ...și nu alunecă (animațiile sunt In Place)
        clip.keepOriginalOrientation = true;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = true;
    }
    importer.clipAnimations = clips;
}
```
**Problema rezolvată aici:** fișierul `Happy Idle.fbx` (descărcat cu skin) conținea și take-ul ascuns
„Take 001”. Fără filtru, acela ajungea animația Idle, iar personajul stătea tot timpul cu brațul întins lateral.

### `OnPostprocessAllAssets(...)` – ordinea importului
```csharp
static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
{
    if (!imported.Contains(ModelPath)) return;  // ← ne interesează doar momentul în care s-a importat modelul
    EditorApplication.delayCall += () => EnsureModelTextures();   // ← extragem texturile după ce se termină importul
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
            importer.SaveAndReimport();          // ← reimportă animația cu avatarul modelului
        }
    }
}
```
Unity importă fișierele în ordine alfabetică: folderul `Animations/` vine înaintea lui `Ch19_nonPBR.fbx`, deci
animațiile se importă **înaintea** modelului și încă nu au avatarul lui. Funcția le reimportă după ce apare.

### `EnsureModelTextures()` – texturile
```csharp
public static bool EnsureModelTextures()
{
    if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter importer)) return false;   // ← modelul lipsește
    if (AssetDatabase.IsValidFolder(TexturesFolder) &&
        AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesFolder }).Length > 0) return true;   // ← deja extrase

    if (!AssetDatabase.IsValidFolder(TexturesFolder))
        AssetDatabase.CreateFolder(CharacterFolder.TrimEnd('/'), "Textures");
    if (!importer.ExtractTextures(TexturesFolder)) return false;
    // ← texturile sunt încorporate în FBX; le scoatem ca fișiere .png separate

    AssetDatabase.Refresh();
    importer.materialSearch = ModelImporterMaterialSearch.Local;   // ← materialele caută texturile lângă model
    importer.SaveAndReimport();
    return true;
}
```
Fără acest pas, personajul apare **alb**.

### `FixExtraTakes()` – corectarea fișierelor deja importate
```csharp
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
            clips = clips.Where(c => c.takeName == MixamoTake).ToArray();   // ← scoatem take-ul ascuns
            changed = true;
        }

        bool loop = Looping.Contains(Path.GetFileNameWithoutExtension(path));
        foreach (var clip in clips)
        {
            if (clip.loopTime == loop) continue;
            clip.loopTime = loop;               // ← ex. Waving a devenit animație în buclă
            changed = true;
        }

        if (!changed) continue;
        importer.clipAnimations = clips;        // ← modificarea marchează importerul ca schimbat
        importer.SaveAndReimport();
    }
}
```
Regulile din `OnPreprocessAnimation` se aplică la primul import. Când regulile se schimbă (filtrul de take,
lista de animații în buclă), această funcție aduce la zi și fișierele importate deja. O apelează meniul TRAVEE.

### Utilitare
```csharp
static bool HasExtraTakes(ModelImporter importer) =>
    importer.defaultClipAnimations.Any(c => c.takeName == MixamoTake) &&
    importer.clipAnimations.Any(c => c.takeName != MixamoTake);
// ← fișierul are take-ul Mixamo, dar importă și altceva → trebuie corectat

static Avatar LoadModelAvatar() =>
    AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault(a => a.isValid);
// ← avatarul generat din model (un FBX importat conține mai multe „sub-asset”-uri: mesh, avatar, clipuri)

public static AnimationClip LoadClip(string fileName)   // ← folosită la generarea Animator Controller-ului
{
    foreach (string folder in new[] { AnimationsFolder, CharacterFolder })   // ← caută în ambele foldere
    {
        var clip = AssetDatabase.LoadAllAssetRepresentationsAtPath($"{folder}{fileName}.fbx")
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        // ← ignorăm clipurile "__preview__" (create de Unity doar pentru previzualizare)
        if (clip != null) return clip;
    }
    return null;
}
```
