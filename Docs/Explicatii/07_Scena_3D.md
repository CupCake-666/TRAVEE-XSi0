# 07 – Scena 3D: Room3D, WorldFactory, Board3DView, DesktopHeadLook

Fișierele din `Assets/Scripts/Game/World/` și `Game/Input/`. Recitește înainte secțiunea 4 din `00_Introducere`
(poziții, rotații, materiale, raycast). Toate distanțele sunt în **metri**.

Cum e așezată scena (privită de sus):

```
                 perete (z = 2,6)

   personaj ●                              ● (sau aici, dacă stă pe dreapta)
   (x = -1,1, z = 1,45)
                     ● personajul când pune piesa (z = 1,25)
                 ┌─────────┐
                 │  tabla  │  verticală, centrul la (0; 1,12; 0,9)
                 └─────────┘

                     ▲  pacientul / camera: (0; 1,2; 0), privește spre +Z
```

---

## ICellInputSource.cs – „pacientul a ales o căsuță”

```csharp
public interface ICellInputSource
{
    event Action<int> CellSelected;   // ← emis cu indexul căsuței (0..8) când pacientul o selectează
    bool InputEnabled { get; set; }   // ← GameController o pornește doar pe rândul pacientului
}
```
Acum o implementează `Board3DView` (mouse). În VR se adaugă o clasă nouă, de exemplu una care detectează
atingerea degetului pe collider-ul căsuței, și se înregistrează cu `controller.AddInputSource(...)`.
`GameController` nu trebuie modificat.

---

## WorldFactory.cs – construirea obiectelor 3D din cod

### `Material(color, smoothness)` – materiale colorate
```csharp
static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
// ← un material per culoare, refolosit (mai puține materiale = randare mai rapidă)

public static Material Material(Color color, float smoothness = 0.25f)
{
    if (materials.TryGetValue(color, out var cached) && cached != null) return cached;   // ← există deja

    var source = GraphicsSettings.currentRenderPipeline != null
        ? GraphicsSettings.currentRenderPipeline.defaultMaterial
        : null;
    // ← materialul implicit al pipeline-ului URP (shader-ul „Lit”). Pornim de la el ca să fim siguri
    //   că shader-ul ajunge și în build (un shader căutat doar după nume poate lipsi din build).
    var material = source != null ? new Material(source) : new Material(Shader.Find("Standard"));
    material.color = color;                    // ← culoarea de bază
    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);   // ← cât de lucios
    materials[color] = material;
    return material;
}
```

### `Primitive`, `Box`, `Group`
```csharp
public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
    Vector3 localScale, Color color, bool keepCollider = false)
{
    var go = GameObject.CreatePrimitive(type); // ← cub/cilindru gata făcut, cu mesh, renderer și collider
    go.name = name;
    go.transform.SetParent(parent, false);     // ← copil al lui `parent`, coordonate locale
    go.transform.localPosition = localPosition;
    go.transform.localScale = localScale;      // ← un cub are 1 m; scala îi dă dimensiunile
    go.GetComponent<Renderer>().sharedMaterial = Material(color);
    if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
    // ← colliderul rămâne doar unde avem nevoie de click (căsuțele); restul nu trebuie să „prindă” raze
    return go;
}

public static GameObject Box(...) => Primitive(PrimitiveType.Cube, ...);   // ← scurtătură pentru cuburi

public static Transform Group(string name, Transform parent, Vector3 localPosition)
{
    var t = new GameObject(name).transform;    // ← obiect gol, folosit ca părinte (ca un Scene Component gol)
    t.SetParent(parent, false);
    t.localPosition = localPosition;
    return t;
}
```

### `Torus(radius, thickness)` – inelul pentru piesa 0
Un tor (o „gogoașă”) se obține rotind un cerc mic (tubul) de-a lungul unui cerc mare (inelul).
```csharp
for (int i = 0; i <= segments; i++)            // ← pași de-a lungul inelului mare
{
    float u = i / (float)segments * Mathf.PI * 2f;          // ← unghiul pe inel (0 .. 2π)
    var ringDir = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u)); // ← direcția din centru spre inel, în planul XZ
    for (int j = 0; j <= sides; j++)           // ← pași în jurul tubului
    {
        float v = j / (float)sides * Mathf.PI * 2f;         // ← unghiul în jurul tubului
        var normal = ringDir * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);   // ← direcția spre exteriorul tubului
        vertices.Add(ringDir * radius + normal * thickness); // ← punctul de pe suprafață
        normals.Add(normal);                   // ← normala (pentru lumină)
    }
}
// apoi, pentru fiecare „pătrățel” de pe suprafață, două triunghiuri:
int a = i * stride + j, b = a + stride, c = a + 1, d = b + 1;
triangles.AddRange(new[] { a, c, b, b, c, d });
// ← ordinea vârfurilor contează: Unity desenează doar fața ale cărei vârfuri apar în sensul acelor de ceasornic
```
La final, `new Mesh`, `SetVertices`, `SetNormals`, `SetTriangles`, `RecalculateBounds`: mesh-ul e gata de folosit.

---

## Room3D.cs – camera de joc

Clasă C# obișnuită (nu MonoBehaviour): construiește scena și ține minte pozițiile importante.

### Pozițiile
```csharp
public const float EyeHeight = 1.2f;           // ← înălțimea ochilor unui pacient așezat

public Vector3 BoardPosition => new Vector3(0f, 1.12f, 0.9f);
// ← centrul tablei: drept în față, la 0,9 m, puțin sub nivelul ochilor
public const float BoardScale = 1.3f;
// ← tabla are 0,48 m; mărită de 1,3 ori ≈ 0,62 m, ca să ocupe cam cât ocupa în 2D
public Quaternion BoardRotation => Quaternion.Euler(-90f, 0f, 0f);
// ← tabla e construită culcată (fața în sus, +Y). Rotită cu -90° în jurul axei X,
//   fața ajunge să privească spre pacient (-Z), iar rândul 0 ajunge sus

public Vector3 OpponentHome(AffectedSide side) =>
    new Vector3(side == AffectedSide.Left ? -1.1f : 1.1f, 0f, 1.45f);
// ← unde așteaptă personajul: 1,1 m lateral, 1,45 m în față (în câmpul vizual, mai în spate decât tabla)
public Vector3 OpponentAtBoard => new Vector3(0f, 0f, 1.25f);
// ← unde stă când pune piesa: în spatele tablei, cât să ajungă cu mâna la căsuțe
```
Pozițiile sunt proprietăți (`=>`), deci se calculează la fiecare citire. Toate valorile de așezare sunt adunate
aici, ca să poată fi ajustate dintr-un singur loc.

### `Build` și geometria
```csharp
public static Room3D Build(Transform parent)
{
    var room = new Room3D { Root = WorldFactory.Group("Room3D", parent, Vector3.zero) };
    // ← `{ Root = ... }` = setează proprietatea imediat după construcție („object initializer”)
    room.BuildGeometry();
    room.BuildLighting();
    room.SetupCamera();
    return room;
}

void BuildGeometry()
{
    WorldFactory.Box("Floor", Root, new Vector3(0, -0.05f, 1f), new Vector3(8f, 0.1f, 8f), Floor);
    // ← podeaua: o placă de 8×8 m, cu fața de sus exact la y = 0
    WorldFactory.Box("Wall", Root, new Vector3(0, 1.5f, 2.6f), new Vector3(8f, 3f, 0.1f), Wall);
    // ← peretele din spate, la 2,6 m
}
```

### Lumina
```csharp
RenderSettings.ambientMode = AmbientMode.Flat;           // ← lumină ambientală uniformă (fără skybox)
RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.62f);   // ← umbrele nu sunt complet negre

var sun = new GameObject("Light").AddComponent<Light>();
sun.type = LightType.Directional;              // ← ca un „soare” (DirectionalLight în Unreal)
sun.intensity = 1.1f;
sun.color = new Color(1f, 0.96f, 0.9f);        // ← ușor caldă
sun.shadows = LightShadows.Soft;               // ← umbre cu margini moi
sun.transform.rotation = Quaternion.Euler(50f, -25f, 0f);   // ← vine de sus, din stânga-spate
```

### Camera = capul pacientului
```csharp
Camera = UnityEngine.Camera.main;              // ← camera din scenă (sau una nouă, dacă lipsește)
Camera.orthographic = false;                   // ← perspectivă 3D
Camera.fieldOfView = 65f;                      // ← unghi larg cât să încapă tabla și personajul
Camera.nearClipPlane = 0.05f;                  // ← obiectele foarte apropiate (în VR, mâinile) nu dispar
Camera.clearFlags = CameraClearFlags.SolidColor;
Camera.backgroundColor = Wall;
Camera.transform.position = new Vector3(0f, EyeHeight, 0f);
Camera.transform.LookAt(BoardPosition + Vector3.up * 0.05f);   // ← privește spre centrul tablei
```
În VR, poziția și rotația camerei vor fi date de ochelari; restul scenei rămâne la fel.

---

## Board3DView.cs – tabla 3D

Implementează **două** interfețe: `IBoardView` (afișează tabla) și `ICellInputSource` (citește mouse-ul).

### `BoardCell` – eticheta căsuței
```csharp
public sealed class BoardCell : MonoBehaviour { public int Index; }
// ← o componentă minusculă pusă pe fiecare căsuță: când raza lovește un collider, aflăm ce căsuță e
```

### Constante și câmpuri
```csharp
const float BoardSize = 0.48f;    // ← latura tablei (înainte de scalarea cu 1,3)
const float Gap = 0.012f;         // ← spațiul dintre căsuțe = liniile grilei
const float TileHeight = 0.012f;  // ← grosimea unei căsuțe
const float SeeThroughAlpha = 0.3f;   // ← cât de opacă e tabla când personajul e în spatele ei
const float FadeSpeed = 3f;       // ← cât de repede se schimbă opacitatea (unități de alpha pe secundă)

readonly Renderer[] tiles = new Renderer[Board.CellCount];      // ← cele 9 căsuțe
readonly Transform[] pieces = new Transform[Board.CellCount];   // ← piesa din fiecare căsuță (null = goală)
readonly bool[] highlighted = new bool[Board.CellCount];        // ← face parte din linia câștigătoare?
readonly List<Material> fadeMaterials = new List<Material>();   // ← materialele care devin transparente
Material tileMaterial, hoverMaterial, winMaterial;              // ← normal / mouse deasupra / linie câștigătoare
Camera cam; Mesh torus;
int hovered = -1;                 // ← căsuța de sub mouse (-1 = niciuna)
float alpha = 1f, targetAlpha = 1f;   // ← opacitatea curentă și cea spre care se îndreaptă
```

### `Create` și `Build`
```csharp
public static Board3DView Create(Transform parent, Vector3 position, Quaternion rotation, Camera camera,
    Material seeThroughTemplate)
{
    var root = WorldFactory.Group("Board3D", parent, position);
    root.localRotation = rotation;             // ← verticală, cu fața spre pacient
    var view = root.gameObject.AddComponent<Board3DView>();
    view.cam = camera;
    view.Build(seeThroughTemplate);
    return view;
}

void Build(Material template)
{
    torus = WorldFactory.Torus(0.045f, 0.013f);   // ← mesh-ul pentru 0, creat o singură dată

    var baseMaterial = BoardMaterial(template, BaseColor, 3000);   // ← placa: render queue 3000
    tileMaterial = BoardMaterial(template, TileColor, 3001);       // ← căsuțele: 3001
    hoverMaterial = BoardMaterial(template, HoverColor, 3001);
    winMaterial = BoardMaterial(template, Theme.CellWin, 3001);
    // ← obiectele transparente nu scriu adâncime; ordinea fixă (placa înainte, căsuțele după)
    //   împiedică placa să fie desenată peste căsuțe

    var plate = WorldFactory.Box("Base", transform, new Vector3(0, 0.01f, 0), new Vector3(BoardSize, 0.02f, BoardSize), BaseColor);
    plate.GetComponent<Renderer>().sharedMaterial = baseMaterial;
    // ← placa închisă la culoare: spațiile dintre căsuțe lasă să se vadă și arată ca liniile grilei

    float cell = (BoardSize - Gap * 4f) / Board.Size;   // ← latura unei căsuțe
    for (int i = 0; i < Board.CellCount; i++)
    {
        float x = -BoardSize / 2f + Gap + cell / 2f + Board.Column(i) * (cell + Gap);
        float z = BoardSize / 2f - Gap - cell / 2f - Board.Row(i) * (cell + Gap);
        // ← coloana 0 = stânga pacientului (-X); rândul 0 = sus (după rotire), ca în 2D

        var tile = WorldFactory.Box($"Cell_{i}", transform, new Vector3(x, 0.02f + TileHeight / 2f, z),
            new Vector3(cell, TileHeight, cell), TileColor, keepCollider: true);   // ← păstrează colliderul
        tile.AddComponent<BoardCell>().Index = i;         // ← eticheta pentru raycast
        tiles[i] = tile.GetComponent<Renderer>();
        tiles[i].sharedMaterial = tileMaterial;
    }
}
```

### `BoardMaterial` – materialele proprii ale tablei
```csharp
Material BoardMaterial(Material template, Color color, int renderQueue)
{
    var material = new Material(template != null ? template : WorldFactory.Material(color)) { color = color };
    // ← o copie a materialului transparent (sau, dacă lipsește, a unuia opac)
    if (template != null)
    {
        material.renderQueue = renderQueue;
        fadeMaterials.Add(material);           // ← doar acestea își schimbă opacitatea
    }
    return material;
}
```
Materialul transparent (`BoardSeeThrough.mat`) e creat de meniul TRAVEE (vezi `09`). Fără el, tabla merge,
dar rămâne opacă.

### Transparența
```csharp
public void SetSeeThrough(bool seeThrough) => targetAlpha = seeThrough ? SeeThroughAlpha : 1f;
// ← apelată de personaj: true când vine în spatele tablei, false când pleacă

void UpdateFade()
{
    if (Mathf.Approximately(alpha, targetAlpha)) return;   // ← a ajuns deja
    alpha = Mathf.MoveTowards(alpha, targetAlpha, FadeSpeed * Time.deltaTime);   // ← un pas spre țintă
    foreach (var material in fadeMaterials)
    {
        var c = material.color;
        c.a = alpha;                           // ← schimbăm doar canalul alpha
        material.color = c;                    // ← `Color` e struct: îl modificăm în copie și îl punem la loc
    }
}
```
Tranziția e treptată (~0,25 s), nu bruscă.

### `Update` – mouse-ul pe tablă
```csharp
void Update()                                  // ← apelată de Unity în fiecare frame
{
    UpdateFade();

    int cell = InputEnabled ? CellUnderPointer() : -1;     // ← căutăm căsuța doar pe rândul pacientului
    SetHovered(cell);                                      // ← o luminăm

    if (cell >= 0 && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        CellSelected?.Invoke(cell);            // ← click stânga → anunțăm GameController
}

int CellUnderPointer()
{
    if (cam == null || Mouse.current == null) return -1;
    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return -1;
    // ← mouse-ul e peste un buton de UI (ex. „Meniu”)? atunci nu selectăm prin el

    var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());   // ← raza din cameră prin cursor
    if (!Physics.Raycast(ray, out var hit, 10f)) return -1;               // ← n-a lovit nimic în 10 m
    var boardCell = hit.collider.GetComponent<BoardCell>();
    if (boardCell == null || pieces[boardCell.Index] != null) return -1;  // ← nu e căsuță / e ocupată
    return boardCell.Index;
}
```
`Mouse.current` vine din noul Input System: `leftButton.wasPressedThisFrame` = s-a apăsat exact în acest frame.

### Hover, resetare, piese, linia câștigătoare
```csharp
void SetHovered(int cell)
{
    if (cell == hovered) return;               // ← nimic nou
    if (hovered >= 0) RefreshTile(hovered);    // ← căsuța veche revine la culoarea ei
    hovered = cell;
    if (hovered >= 0) tiles[hovered].sharedMaterial = hoverMaterial;   // ← cea nouă devine galbenă
}

void RefreshTile(int cell) =>
    tiles[cell].sharedMaterial = highlighted[cell] ? winMaterial : tileMaterial;

public void ResetBoard()                       // ← meci nou: scoatem piesele și culorile
{
    hovered = -1;
    for (int i = 0; i < Board.CellCount; i++)
    {
        if (pieces[i] != null) Destroy(pieces[i].gameObject);   // ← Destroy = șterge obiectul din scenă
        pieces[i] = null;
        highlighted[i] = false;
        RefreshTile(i);
    }
}

public void SetMark(int cell, Mark mark)
{
    SetHovered(-1);
    var piece = mark == Mark.X ? CreateX() : CreateO();
    piece.SetParent(transform, false);
    piece.localPosition = tiles[cell].transform.localPosition + Vector3.up * (TileHeight / 2f + 0.012f);
    // ← chiar deasupra căsuței (în coordonatele tablei, „sus” = spre pacient)
    pieces[cell] = piece;
    StartCoroutine(DropIn(piece));             // ← animația de apariție
}

public void HighlightLine(int[] line)          // ← cele 3 căsuțe câștigătoare devin verzi
```

### Piesele X și 0
```csharp
Transform CreateX()
{
    var x = WorldFactory.Group("X", transform, Vector3.zero);
    var size = new Vector3(0.12f, 0.022f, 0.028f);    // ← o bară lungă și subțire
    WorldFactory.Box("Bar1", x, Vector3.zero, size, Theme.X).transform.localRotation = Quaternion.Euler(0, 45, 0);
    WorldFactory.Box("Bar2", x, Vector3.zero, size, Theme.X).transform.localRotation = Quaternion.Euler(0, -45, 0);
    // ← două bare încrucișate la ±45° = X
    return x;
}

Transform CreateO()
{
    var o = new GameObject("O");
    o.AddComponent<MeshFilter>().sharedMesh = torus;    // ← geometria: inelul generat din cod
    o.AddComponent<MeshRenderer>().sharedMaterial = WorldFactory.Material(Theme.O);   // ← portocaliu
    return o.transform;
}
```

### `DropIn` – animația piesei
```csharp
static IEnumerator DropIn(Transform piece)
{
    const float duration = 0.3f;
    var end = piece.localPosition;
    var start = end + Vector3.up * 0.08f;      // ← pornește cu 8 cm mai aproape de pacient
    for (float t = 0; t < duration; t += Time.deltaTime)
    {
        if (piece == null) yield break;        // ← tabla a fost resetată între timp
        float k = t / duration;                // ← progres 0 → 1
        float scale = Mathf.LerpUnclamped(0.3f, 1f, 1f + 2.7f * Mathf.Pow(k - 1f, 3) + 1.7f * Mathf.Pow(k - 1f, 2));
        // ← „ease-out-back”: crește puțin peste mărimea finală, apoi revine (efect de săritură)
        piece.localScale = Vector3.one * scale;
        piece.localPosition = Vector3.Lerp(start, end, 1f - (1f - k) * (1f - k));   // ← se „lipește” de tablă
        yield return null;
    }
    if (piece == null) yield break;
    piece.localScale = Vector3.one;
    piece.localPosition = end;
}
```

---

## DesktopHeadLook.cs – „capul” pacientului pe calculator

```csharp
public sealed class DesktopHeadLook : MonoBehaviour
{
    const float Sensitivity = 0.15f;   // ← grade pe pixel de mișcare a mouse-ului
    const float MaxYaw = 75f;          // ← cât se poate întoarce stânga / dreapta
    const float MaxPitch = 40f;        // ← cât se poate uita sus / jos

    Vector3 baseAngles;                // ← orientarea de pornire (spre tablă)
    float yaw, pitch;                  // ← cât s-a întors față de ea

    void Start() => baseAngles = transform.eulerAngles;   // ← Start = înainte de primul frame

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)       // ← doar cât ții apăsat click dreapta
        {
            var delta = mouse.delta.ReadValue() * Sensitivity;  // ← cât s-a mișcat mouse-ul în acest frame
            yaw = Mathf.Clamp(yaw + delta.x, -MaxYaw, MaxYaw);       // ← stânga-dreapta
            pitch = Mathf.Clamp(pitch - delta.y, -MaxPitch, MaxPitch); // ← sus-jos (mouse în sus = privire în sus)
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            yaw = pitch = 0f;                  // ← Space: privește din nou înainte

        transform.rotation = Quaternion.Euler(baseAngles.x + pitch, baseAngles.y + yaw, 0f);
    }
}
```
Pe calculator simulează mișcarea capului, ca personajul să poată reacționa când „pacientul” se uită la el.
În VR componenta nu se mai adaugă: camera e rotită de ochelari, iar codul personajului care verifică
privirea rămâne același.
