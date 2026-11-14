# 10 – Peisaje și sunete: GameAssets, Sky, Environments, Sfx

> **Actualizare:** peisajele pot fi salvate în scenă și aranjate de mână; atunci `Environments.cs` e folosit doar la reconstruire, iar cerul, ceața și sunetul vin din componenta `ThemeSettings`. `Flicker`, `Tide`, `FlameDance` și `Firefly` au acum fișierele lor (un `MonoBehaviour` se salvează în scenă doar dacă fișierul poartă numele clasei). Vezi `Docs/Cum_modific_scena.md`.

Jocul nu se mai întâmplă într-o cameră goală, ci într-un **peisaj** ales din meniu (pădure, munte noaptea,
plajă), și are **sunete**: ambianță, efecte la fiecare acțiune și pașii personajului.

| Fișier | Rol |
|---|---|
| `Game/GameAssets.cs` | „biblioteca”: lista modelelor 3D și a sunetelor, căutate după nume |
| `Game/World/Environments.cs` | construiește obiectele fiecărui peisaj |
| `Game/World/Sky.cs` | cerul: o fotografie 360° sau, în lipsa ei, unul desenat din cod |
| `Editor/NatureImport.cs` | creează materialele modelelor și setează importul panoramelor de cer |
| `Game/World/Room3D.cs` | aplică un peisaj: decor + lumină + ceață + cer + ambianță |
| `Game/Audio/Sfx.cs` | toate sunetele |

De unde vin modelele și sunetele și ce licență au: vezi `CREDITS.md` din rădăcina proiectului. Toate sunt
**CC0** (domeniu public).

---

## De ce o „bibliotecă” (GameAssets.cs)

Până acum tot ce se vedea era construit din cod, din cuburi. Copacii și sunetele sunt însă **fișiere**
(`.fbx`, `.ogg`), iar codul are nevoie de o cale spre ele. În Unity, un fișier ajunge în aplicația finală
doar dacă e **referit** de ceva din scenă. Soluția: un singur obiect care le ține pe toate.

```csharp
public sealed class GameAssets : ScriptableObject
// ← ScriptableObject = un obiect de date salvat ca fișier .asset (echivalentul unui Data Asset din Unreal).
//   Nu stă pe un GameObject; există ca fișier în proiect: Assets/Art/GameAssets.asset
{
    public GameObject[] models = new GameObject[0];   // ← modelele din Assets/Art/Nature
    public AudioClip[] clips = new AudioClip[0];      // ← sunetele din Assets/Audio
    public Material skyMaterial;                      // ← materialul cerului
    public Texture2D[] skies = new Texture2D[0];      // ← panoramele de cer din Assets/Art/Sky

    Dictionary<string, GameObject> modelsByName;      // ← index după nume, construit la prima căutare

    public GameObject Model(string name)
    {
        if (modelsByName == null)                     // ← prima căutare: construim dicționarul
        {
            modelsByName = new Dictionary<string, GameObject>();
            foreach (var model in models)
                if (model != null) modelsByName[model.name] = model;   // ← numele = numele fișierului
        }
        return modelsByName.TryGetValue(name, out var found) ? found : null;   // ← null dacă lipsește
    }
    // Clip(name) face același lucru pentru sunete.
}
```
Listele nu se completează de mână: meniul **TRAVEE → Creează scena X și 0 (3D)** adună toate fișierele din
cele două foldere (vezi `09_Editor_si_Importul.md`). Bootstrap-ul are un singur câmp, `assets`, care arată
spre această bibliotecă.

**Ca să adaugi un model sau un sunet:** pui fișierul în `Assets/Art/Nature` sau `Assets/Audio`, rulezi din
nou meniul TRAVEE și îl folosești în cod după nume.

---

## Environments.cs – cele trei peisaje

```csharp
public enum WorldTheme { Forest, Mountain, Beach }
// ← camera simplă de la început a fost scoasă din meniu; mai există doar ca decor de rezervă (Plain),
//   folosit automat dacă lipsește biblioteca de asset-uri
```

### `ThemeLook` – ce diferă în afară de obiecte
```csharp
public struct ThemeLook
{
    public SkySettings Sky;             // ← culorile cerului, stele, lună
    public Color SunColor;              // ← lumina principală („soarele” sau „luna”)
    public float SunIntensity;
    public Vector3 SunAngles;           // ← din ce direcție vine
    public Color AmbientSky, AmbientEquator, AmbientGround;   // ← lumina ambientă, pe trei culori
    public float FogStart, FogEnd;      // ← ceața, în metri
    public string AmbientSound;         // ← numele sunetului de fundal
}
```
Fiecare peisaj e o metodă care **creează obiectele** și **întoarce** un `ThemeLook`. `Room3D` aplică apoi
aspectul. Așa, un peisaj nou înseamnă o singură metodă nouă.

### `Build` – punctul de intrare
```csharp
public static ThemeLook Build(WorldTheme theme, GameAssets assets, Transform root)
{
    var builder = new EnvironmentBuilder(assets, root) { rng = new System.Random(100 + (int)theme) };
    // ← generator de numere aleatoare cu SĂMÂNȚĂ FIXĂ: copacii „împrăștiați la întâmplare” ajung
    //   în aceleași locuri la fiecare rulare (peisajul e reproductibil, nu se schimbă între ședințe)
    if (assets == null) return builder.Plain();     // ← fără modele: doar o podea și un perete
    switch (theme)
    {
        case WorldTheme.Mountain: return builder.Mountain();
        case WorldTheme.Beach: return builder.Beach();
        default: return builder.Forest();
    }
}
```

### `Prop` – pune un model în scenă
```csharp
Transform Prop(string model, Vector3 position, float size, float yaw)
{
    var prefab = assets != null ? assets.Model(model) : null;
    if (prefab == null) return null;                       // ← model lipsă: îl sărim, jocul merge mai departe

    var holder = WorldFactory.Group(model, root, position);   // ← obiect gol la poziția dorită
    var instance = Object.Instantiate(prefab, holder);        // ← copia modelului, copil al lui
    var renderers = instance.GetComponentsInChildren<Renderer>();

    var bounds = renderers[0].bounds;                      // ← „cutia” care încadrează modelul (AABB)
    foreach (var r in renderers) bounds.Encapsulate(r.bounds);
    float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
    holder.localScale = Vector3.one * (size / largest);
    // ← modelele au în fișier mărimi oarecare. Nu ne bazăm pe ele: măsurăm cutia și scalăm astfel încât
    //   cea mai mare dimensiune să fie exact `size` metri (un copac de 6 m, o tufă de 1 m)

    // după scalare, recalculăm cutia și urcăm/coborâm modelul până are baza pe sol
    holder.position += Vector3.up * (position.y - bounds.min.y);
    holder.localRotation = Quaternion.Euler(0f, yaw, 0f);  // ← rotit în jurul axei verticale
    return holder;
}
```
E același truc folosit la personaj (`NormalizeHeight`): măsori, apoi scalezi.

### `Scatter` – împrăștie multe modele
```csharp
void Scatter(string[] models, int count, float minRadius, float maxRadius, float minSize, float maxSize,
    float clearRadius, bool smallPlants = false, float maxZ = float.MaxValue)
{
    int placed = 0;
    for (int attempt = 0; attempt < count * 20 && placed < count; attempt++)   // ← cu limită de încercări
    {
        float radius = Mathf.Sqrt(Mathf.Lerp(minRadius * minRadius, maxRadius * maxRadius, (float)rng.NextDouble()));
        // ← distanță aleatoare față de pacient. Rădăcina pătrată face distribuția uniformă pe SUPRAFAȚĂ:
        //   fără ea, obiectele s-ar înghesui spre centru (un inel îndepărtat are arie mai mare)
        var position = Polar(Range(0f, 360f), radius);     // ← unghi aleator în jur
        if (position.z > maxZ) continue;                   // ← ex. pe plajă: nu punem palmieri în mare
        if (smallPlants ? InWalkLane(position) : InPlayArea(position, clearRadius)) continue;
        // ← zona de joc rămâne liberă: copacii ocolesc un cerc în jurul tablei, iar plantele mici
        //   ocolesc doar culoarul pe care merge personajul

        Prop(models[rng.Next(models.Length)], position, Range(minSize, maxSize), Range(0f, 360f));
        placed++;
    }
}
```
Este „rejection sampling”: alegi un punct la întâmplare și îl arunci dacă pică într-o zonă interzisă.

### Cele trei peisaje, pe scurt
- **`Forest()`** – sol verde; cinci copaci puși de mână, care încadrează tabla; 85 de copaci împrăștiați
  între 8 și 40 m (cei mai mulți verzi, câțiva cu frunze roșii ca accent); tufe cu flori, ferigi, pietre,
  iarbă, trifoi, flori, ciuperci; un cort cu foc de tabără în spate.
- **`Mountain()`** – noapte. 26 de **munți** generați din cod (`Peak`) în cerc, la 70–150 m; brazi și
  bolovani; un **foc de tabără** cu o lumină portocalie care pâlpâie (`Flicker`); cer fotografic cu Calea Lactee.
- **`Beach()`** – nisip; **marea** începe la 10 m în față: o placă albastră lucioasă, o fâșie de apă mică
  mai deschisă și o dungă albă de spumă, care înaintează și se retrag lent (`Tide`); palmieri, plante, o
  canoe, stânci în apă, trei insule în larg.

Modelele sunt din **Quaternius Stylized Nature MegaKit** (copaci, tufe, plante, pietre – texturate) și
**Ultimate Nature Pack** (palmieri, buturugi, bușteni); cortul, focul și canoea au rămas din Kenney Nature Kit.

### Solul – `Ground` și `WorldFactory.MottledMaterial`
Solul nu mai e o culoare uniformă: primește o textură de „pete” în două nuanțe, generată din cod.
```csharp
float noise = 0.65f * Mathf.PerlinNoise(u * 5f + 3.1f, v * 5f + 7.7f)       // ← pete mari
            + 0.35f * Mathf.PerlinNoise(u * 19f + 11.3f, v * 19f + 2.9f);   // ← detaliu fin
pixels[...] = Color.Lerp(dark, light, Mathf.SmoothStep(0.25f, 0.75f, noise));
...
wrapMode = TextureWrapMode.Mirror,
// ← textura se repetă în oglindă: marginile se potrivesc mereu, deci nu se văd cusături între repetări
material.mainTextureScale = Vector2.one * (size / metersPerTile);   // ← o repetare la 9 metri
```

### `Peak` și `WorldFactory.Mountain` – munți din cod
```csharp
peak.transform.localScale = new Vector3(radius, height, radius);    // ← mesh-ul are raza 1 și înălțimea 1
peak.AddComponent<MeshFilter>().sharedMesh = WorldFactory.Mountain(rng, topFrom);
renderer.sharedMaterials = new[] { WorldFactory.Material(body), WorldFactory.Material(top) };
// ← două materiale: stâncă și zăpadă (la insule: două nuanțe de verde)
```
`Mountain` construiește un con din **5 inele** a câte 13 vârfuri. Fiecare vârf e împins aleator spre
interior/exterior (±28%) și în sus/jos, iar inelele sunt decalate între ele: rezultă creste neregulate, nu un
con neted. Fiecare față are vârfurile ei (nu le împarte cu vecinele), deci are propria normală și se vede
**plată** – aspect „low-poly”. Mesh-ul are două **sub-mesh-uri**: fețele a căror înălțime medie depășește
pragul `topFrom` intră în al doilea, care primește materialul de zăpadă. Linia zăpezii iese astfel neregulată,
ca la un munte real.

### Detaliile: iaz, flăcări, licurici
Pacientul se poate întoarce de jur împrejur, așa că peisajele au lucruri de văzut și în spate:
- **pădure** – un iaz cu nuferi, pietre cu mușchi pe mal și două sălcii; un pâlc de mesteceni; o cărare de
  pietre spre tabără; tufe cu fructe; petale pe jos; flacără la focul de tabără;
- **munte** – brazi și stânci înzăpezite mai departe; cerc de pietre în jurul focului; flacără; 55 de licurici;
- **plajă** – un loc de foc cu bușteni; o a doua canoe, pe apă; în spate, interiorul insulei: dealuri verzi,
  stânci cu mușchi, iarbă de dună.

```csharp
void Block(Vector3 center, float radius)    // ← rezervă un cerc: Scatter nu mai pune nimic acolo (ex. iazul)
void Pond(Vector3 center, float radius, Color color)
// ← un cilindru Unity turtit până devine un disc de 2 cm, cu material lucios (smoothness 0,92)
void Flame(Vector3 position, float height)
// ← două conuri (portocaliu și galben) cu material „Unlit”, plus componenta FlameDance care le face să tremure
void Fireflies(int count, Vector3 center, float minRadius, float maxRadius)
// ← sfere de 4 cm cu material Unlit, fiecare cu o componentă Firefly: plutește și pâlpâie cu zgomot Perlin
```
Materialul **Unlit** („neluminat”) își arată culoarea așa cum e, fără lumini și umbre, deci obiectul pare că
luminează singur – potrivit pentru flăcări și licurici. E un asset (`Assets/Art/Materials/Glow.mat`, creat de
meniul TRAVEE și ținut în `GameAssets.glowMaterial`), ca shader-ul lui să ajungă în build.

### `BuildStand` – pe ce urcă personajul (rezervă)
La înălțimea implicită a tablei personajul ajunge de pe sol și această parte nu se folosește. Dacă tabla e
ridicată din Inspector și el nu mai ajunge (vezi `08_Personajul_AI.md`), urcă pe ceva potrivit peisajului:
```csharp
if (theme == WorldTheme.Forest) stand = builder.FittedProp("TreeStump_Moss", center, size, 0f);   // ← buturugă
else stand = builder.FittedProp("Rock_Medium_2", center, size + Vector3.up * 0.05f, 0f);
// ← lespede de stâncă la munte și pe plajă; dacă modelul lipsește, un podium simplu de lemn
```
`FittedProp` e ca `Prop`, dar scalează modelul **separat pe fiecare axă** până ocupă exact cutia cerută
(lățime, înălțime, adâncime). Așa, același bolovan devine o lespede lată și joasă, cu fața de sus exact la
înălțimea de care are nevoie personajul. Obiectul e ceva mai mare decât zona pe care se stă și e mutat cu
17 cm spre spate, ca marginea lui să nu treacă prin tablă.

### `Flicker` și `Tide` – două componente mici
```csharp
public sealed class Flicker : MonoBehaviour      // ← pe lumina focului
{
    void Update()
    {
        source.intensity = baseIntensity * Mathf.Lerp(0.75f, 1.15f, Mathf.PerlinNoise(Time.time * 4f, 0.5f));
        // ← PerlinNoise = zgomot „lin” (valori aleatoare care se schimbă treptat): pâlpâire naturală,
        //   nu sacadată ca la Random
    }
}

public sealed class Tide : MonoBehaviour         // ← pe mare
{
    void Update()
    {
        transform.localPosition = start + Vector3.back * (0.6f * Mathf.Sin(Time.time * 0.7f));
        // ← marea se mișcă ±60 cm spre pacient și înapoi, cu o perioadă de ~9 secunde
    }
}
```

---

## Sky.cs – cerul

Cerul este o **imagine panoramică**: o singură imagine în care lățimea reprezintă 360° în jurul pacientului,
iar înălțimea merge de la „drept în jos” la „drept în sus” (ca o hartă a lumii). Shader-ul `Skybox/Panoramic`
o înfășoară în jurul scenei. Există două surse pentru imagine:

1. **o fotografie 360°** din `Assets/Art/Sky` (de pe Poly Haven, CC0): nori de zi pentru pădure și plajă,
   Calea Lactee pentru munte;
2. dacă fotografia lipsește, o imagine **desenată din cod** pixel cu pixel (gradient, stele, lună).

```csharp
public static void Apply(Material skyTemplate, Camera camera, SkySettings sky, Texture2D panorama = null)
{
    if (skyTemplate == null) { ... return; }       // ← fără material de cer: fundal într-o singură culoare

    if (material == null) material = new Material(skyTemplate) { name = "Sky" };

    if (panorama != null)
    {
        material.mainTexture = panorama;                                         // ← fotografia
        material.SetFloat("_Exposure", sky.Exposure > 0f ? sky.Exposure : 1f);   // ← luminozitate
        material.SetFloat("_Rotation", sky.Rotation);                            // ← o rotim în jurul pacientului
        material.SetColor("_Tint", sky.Tint.maxColorComponent > 0f ? sky.Tint : NeutralTint);   // ← nuanță
    }
    else
    {
        texture.SetPixels32(Paint(sky));           // ← desenăm cerul din cod
        texture.Apply();
        material.mainTexture = texture;
        ...
    }

    RenderSettings.skybox = material;              // ← „cerul” scenei
    camera.clearFlags = CameraClearFlags.Skybox;   // ← camera umple fundalul cu el
    DynamicGI.UpdateEnvironment();                 // ← reflexiile (marea) iau culorile noului cer
}
```
Aceeași fotografie de zi e folosită în două peisaje, cu `Rotation` diferit (0° în pădure, 140° pe plajă),
deci norii din față sunt alții. La munte, `Exposure = 0,55` și o nuanță albăstruie întunecă fotografia
(varianta descărcată e luminată artificial, ca să se vadă stelele pe un ecran).

### `Paint` – gradientul și luna
```csharp
for (int y = 0; y < Height; y++)
{
    float v = (y + 0.5f) / Height;                 // ← 0 = jos, 0,5 = orizont, 1 = sus
    Color row = v >= 0.5f
        ? Color.Lerp(sky.Horizon, sky.Zenith, Mathf.Pow((v - 0.5f) * 2f, 0.55f))
        // ← deasupra orizontului: de la culoarea orizontului la cea de sus. Puterea 0,55 face
        //   trecerea mai rapidă lângă orizont (cum arată un cer real)
        : Color.Lerp(sky.Horizon, sky.Ground, Mathf.Clamp01((0.5f - v) * 12f));
        // ← sub orizont: culoarea solului
    ...
    // luna: pentru fiecare pixel calculăm direcția lui în spațiu și unghiul față de direcția lunii
    float dot = cosLon[x] * cosLat * moonDir.x + sinLat * moonDir.y + sinLon[x] * cosLat * moonDir.z;
    // ← produsul scalar a doi vectori de lungime 1 = cosinusul unghiului dintre ei
    if (dot >= cosDisc) pixel = moonColor;         // ← în interiorul discului (2,4°)
    else if (dot >= cosHalo) pixel = amestec;      // ← în halou (până la 9°), tot mai slab
}
```
Sinusurile și cosinusurile pe coloane sunt calculate o singură dată, înaintea buclei, ca desenarea celor
2 milioane de pixeli să fie rapidă.

### `PaintStars`
1500 de puncte la poziții aleatoare (sămânță fixă: același cer mereu), între 3° și 80° deasupra
orizontului. Cam o stea din cinci e „mare”: primește și patru pixeli vecini mai palizi, în cruce.

---

## Room3D.cs – aplicarea unui peisaj

Pozițiile (tabla, locul personajului) au rămas la fel; vezi `07_Scena_3D.md`. Nou este `SetTheme`:

```csharp
public void SetTheme(WorldTheme theme)
{
    Theme = theme;

    if (environment != null) Object.Destroy(environment.gameObject);   // ← ștergem decorul vechi
    environment = WorldFactory.Group("Environment_" + theme, Root, Vector3.zero);
    var look = EnvironmentBuilder.Build(theme, assets, environment);   // ← îl construim pe cel nou
    // ← tot decorul e sub un singur obiect-părinte: îl putem șterge dintr-o mișcare.
    //   Tabla și personajul NU sunt sub el, deci rămân neatinse când schimbi peisajul.

    sun.color = look.SunColor;                     // ← lumina principală
    sun.intensity = look.SunIntensity;
    sun.transform.rotation = Quaternion.Euler(look.SunAngles);

    RenderSettings.ambientMode = AmbientMode.Trilight;   // ← ambient pe trei culori: cer / orizont / sol
    RenderSettings.ambientSkyColor = look.AmbientSky;
    ...
    RenderSettings.fog = look.FogEnd > 0f;         // ← ceață liniară, de culoarea orizontului:
    RenderSettings.fogColor = look.Sky.Horizon;    //   obiectele îndepărtate se „topesc” în cer
    ...
    BuildStand();                                  // ← obiectul pe care urcă personajul, în stilul peisajului
    ...
    Sky.Apply(hasSky ? assets.skyMaterial : null, Camera, look.Sky,
        hasSky ? assets.SkyTexture(look.Sky.Panorama) : null);   // ← fotografia de cer, dacă există
    Sfx.SetAmbient(look.AmbientSound);             // ← păsări / greieri / valuri
}
```
În toate peisajele lumina vine **din spatele pacientului**, ca fața tablei să fie luminată. La munte luna
se vede în față, dar „lumina lunii” vine tot din spate: e o abatere de la realitate, făcută pentru ca tabla
să rămână ușor de citit.

### Alegerea din meniu (TicTacToeBootstrap.cs)
```csharp
var theme = (WorldTheme)PlayerPrefs.GetInt(ThemeKey, (int)worldTheme);
// ← PlayerPrefs = setări mici păstrate între rulări (în registry pe Windows). Prima dată nu există cheia,
//   deci se ia valoarea din Inspector (worldTheme); după aceea, ultimul peisaj ales.
var room = Room3D.Build(transform, assets, theme);
...
menu.SetTheme(EnvironmentBuilder.DisplayName(room.Theme));    // ← textul butonului: „Peisaj: Pădure”
menu.ThemeToggleRequested += () => NextTheme(room, menu);     // ← click pe buton

static void NextTheme(Room3D room, MenuScreen menu)
{
    int count = System.Enum.GetValues(typeof(WorldTheme)).Length;       // ← 4 peisaje
    room.SetTheme((WorldTheme)(((int)room.Theme + 1) % count));         // ← următorul; după ultimul, primul
    menu.SetTheme(EnvironmentBuilder.DisplayName(room.Theme));
    PlayerPrefs.SetInt(ThemeKey, (int)room.Theme);                      // ← ținem minte alegerea
}
```
Butonul „Peisaj” din meniu (`MenuScreen`, în `Screens.cs`) funcționează exact ca butonul „Mâna afectată”:
ridică un eveniment, iar cine ascultă decide ce se întâmplă.

---

## Sfx.cs – sunetele

### Lista de sunete
```csharp
public enum Sound { Click, Hover, PlacePatient, PlaceAI, Target, CoinTick, CoinResult, Win, Draw, Lose, Footstep, Applause }

static readonly Dictionary<Sound, string[]> Files = new Dictionary<Sound, string[]>
{
    { Sound.Click, new[] { "click_001" } },
    ...
    { Sound.Footstep, new[] { "footstep00", "footstep01", "footstep02", "footstep03", "footstep04" } },
    // ← mai multe fișiere pentru același sunet: se alege unul la întâmplare, ca pașii să nu sune identic
};
```
Restul codului nu știe de fișiere: cere `Sfx.Play(Sound.Click)`. **Ca să schimbi un sunet**, pui alt fișier
în `Assets/Audio/Sfx`, îi scrii numele în tabelul de mai sus și rulezi din nou meniul TRAVEE.

### Crearea și apelul static
```csharp
static Sfx instance;                               // ← singurul obiect Sfx din scenă (tiparul „singleton”)

public static Sfx Create(Transform parent, GameAssets assets, float effectsVolume, float ambientVolume)
{
    ...
    sfx.effects = go.AddComponent<AudioSource>();  // ← AudioSource = „difuzorul” (AudioComponent în Unreal)
    // + două surse pentru ambianță (vezi mai jos)

    if (FindAnyObjectByType<AudioListener>() == null && Camera.main != null)
        Camera.main.gameObject.AddComponent<AudioListener>();
    // ← AudioListener = „urechile”. Trebuie să existe exact unul; stă pe cameră (în VR: capul pacientului)

    instance = sfx;
    return sfx;
}

public static void Play(Sound sound, float volume = 1f)
{
    if (instance == null) return;                  // ← fără Sfx (ex. scena 2D): apelul nu face nimic
    var clip = instance.Pick(sound);
    if (clip != null) instance.effects.PlayOneShot(clip, volume * instance.effectsVolume);
    // ← PlayOneShot: redă peste ce se aude deja, fără să oprească sunetul anterior
}

public static void PlayAt(Sound sound, Vector3 position, float volume = 1f)
{
    ...
    AudioSource.PlayClipAtPoint(clip, position, volume * instance.effectsVolume);
    // ← sunet 3D: se aude din direcția acelui punct. Pașii și salutul vin dinspre personaj,
    //   ceea ce în VR îl ajută pe pacient să-l găsească cu privirea
}
```
Metodele sunt `static`, deci se pot apela din orice clasă fără o referință la obiect. Verificarea
`instance == null` face ca jocul să meargă și fără sunet, fără nicio eroare.

### Ambianța: două surse care se încrucișează
```csharp
readonly AudioSource[] ambient = new AudioSource[2];    // ← două „difuzoare” de fundal
readonly float[] ambientTarget = new float[2];          // ← volumul spre care se îndreaptă fiecare
int current;                                            // ← care e cel activ

void StartAmbient(AudioClip clip)
{
    ambientTarget[current] = 0f;        // ← cel activ începe să se stingă
    if (clip == null) return;
    current = 1 - current;              // ← trecem pe celălalt
    ambient[current].clip = clip;
    ambient[current].time = 0f;
    ambient[current].Play();
    ambientTarget[current] = ambientVolume;   // ← și acesta crește
}

void Update()
{
    // 1. volumele se apropie lin de ținte (MoveTowards); o sursă ajunsă la zero se oprește
    // 2. reluarea: cu 2 secunde înainte de finalul clipului, pornim ACELAȘI clip pe a doua sursă
    if (... active.time >= active.clip.length - AmbientFade) StartAmbient(active.clip);
}
```
Același mecanism rezolvă două lucruri. La schimbarea peisajului, ambianța veche se stinge în timp ce cea
nouă crește (crossfade). Iar la reluare: înregistrările nu sunt bucle perfecte, deci un simplu `loop` s-ar
auzi ca o „cusătură” la fiecare repetare; încrucișând sfârșitul cu începutul, repetarea nu se mai aude.

### Unde se aude fiecare sunet

| Sunet | Unde e apelat |
|---|---|
| `Click` | `UIFactory.Button` – orice buton, dintr-un singur loc |
| `Hover` | `Board3DView.SetHovered` – cursorul ajunge pe o căsuță liberă |
| `PlacePatient` / `PlaceAI` | `GameController.Apply` – apare o piesă |
| `Target` | `OpponentCharacter.PlacePiece` – căsuța AI-ului se aprinde roșu |
| `CoinTick` / `CoinResult` | `CoinFlipScreen.Play` – la fiecare întoarcere a monedei și la final |
| `Win` / `Draw` / `Lose` | `GameController.ShowResult` |
| `Footstep` | `OpponentCharacter.WalkTo` – la fiecare 0,45 s de mers |
| `Applause` | `OpponentCharacter.MatchEnd` – când aplaudă victoria pacientului (o singură persoană care aplaudă) |
| ambianța | `Room3D.SetTheme` |

La nivelurile 1–2, remiza folosește sunetul de victorie, la fel cum mesajul o prezintă ca pe un succes.

Aplauzele sunt un efect lung (16 secunde), deci merg pe o sursă separată: `Sfx.PlayLong(Sound.Applause)`
le pornește, iar `Sfx.StopLong()` le oprește când personajul trece la altceva (de exemplu, apeși „Joacă din
nou”). `PlayOneShot`, folosit la efectele scurte, nu poate fi oprit la jumătate.

Volumele se reglează din Inspector, pe obiectul `XSi0`: **Effects Volume** și **Ambient Volume**.

---

## Trei capcane de build (și cum sunt evitate)

Editorul „iartă” lucruri pe care aplicația finală nu le iartă. Toate trei au aceeași cauză: Unity pune în
build doar ce **vede că e folosit** de scene.

1. **Materialul obiectelor 3D.** Materialul implicit al pipeline-ului URP există doar în editor. În build,
   `WorldFactory.Material` nu avea de la ce porni și jocul se oprea la pornire (ecranele de UI apăreau
   suprapuse). Acum pornește de la `WorldLit.mat`, un fișier referit de scenă (`WorldFactory.Template`).
2. **Ceața.** O pornim din cod, la rulare. Dacă scena salvată nu are ceață, Unity scoate din build
   variantele de shader care o desenează. De aceea meniul TRAVEE salvează scena cu ceața activată.
3. **Cerul.** Shader-ul `Skybox/Panoramic` ajunge în build pentru că `SkyPanoramic.mat` e în `GameAssets`,
   iar `GameAssets` e referit de scenă.
