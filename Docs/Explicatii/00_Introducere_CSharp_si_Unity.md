# 00 – C# și Unity pentru cineva care vine din C++ / Unreal

Citește asta înainte de celelalte fișiere: aici sunt explicate **o singură dată** toate noțiunile de limbaj
și de Unity care apar în cod. În restul explicațiilor o să trimit înapoi aici.

Ordinea recomandată de citire:

| Fișier | Ce explică |
|---|---|
| `00_Introducere_CSharp_si_Unity.md` | limbajul C# și conceptele Unity (acest fișier) |
| `01_Board_si_Match.md` | `Board.cs`, `Match.cs`, `DifficultySettings.cs` |
| `02_Search.md` | `Search.cs` – minimax și expectimax |
| `03_Strategii_AI.md` | `IAIStrategy.cs` + cele 4 niveluri |
| `04_Bootstrap_si_GameController.md` | pornirea jocului și fluxul unui meci |
| `05_UI.md` | `UIFactory.cs`, `BoardView.cs`, `Screens.cs`, `ICellInputSource.cs` |
| `06_Logger_Editor_Teste.md` | `SessionLogger.cs`, `SceneSetup.cs`, `AITests.cs` |

---

## 1. Dicționar Unreal → Unity

| Unreal | Unity | Observații |
|---|---|---|
| `AActor` | `GameObject` | „obiectul” din scenă. Singur nu face nimic. |
| `UActorComponent` | `Component` / `MonoBehaviour` | **Tot comportamentul** se pune în componente atașate pe GameObject. |
| clasa ta derivată din `AActor` | clasa ta derivată din `MonoBehaviour` | scriptul tău e o componentă. |
| `BeginPlay()` | `Awake()` / `Start()` | `Awake` = imediat ce obiectul e creat; `Start` = înainte de primul frame. |
| `Tick(DeltaTime)` | `Update()` + `Time.deltaTime` | nu am folosit `Update` deloc – am folosit corutine (vezi mai jos). |
| `UPROPERTY(EditAnywhere)` | `[SerializeField]` sau câmp `public` | apare în Inspector (echivalentul Details panel). |
| `UFUNCTION(CallInEditor)` / meniuri editor | `[MenuItem("...")]` | adaugă o comandă în meniul editorului. |
| Level / Map | Scene (`.unity`) | |
| UMG Widget | uGUI: `Canvas` + `Image` + `Text` + `Button` | noi construim UI-ul din cod. |
| Module (`.Build.cs`) | Assembly Definition (`.asmdef`) | fiecare „modul” devine un DLL separat. |
| Delegates (`DECLARE_MULTICAST_DELEGATE`) | `event Action<...>` | |
| Timere / Latent actions | Corutine (`IEnumerator` + `yield return`) | |
| `TArray<T>` | `List<T>` | |
| `TMap<K,V>` | `Dictionary<K,V>` | |
| `FMath` | `Mathf` (Unity) / `Math` (C#) | |
| `UE_LOG` | `Debug.Log` / `Debug.LogError` | apare în fereastra **Console**. |
| `SpawnActor` | `new GameObject(...)` + `AddComponent<T>()` | |

**Ideea cea mai importantă din Unity:** un `GameObject` e doar un container cu un `Transform`
(poziție/rotație/scală). Ce face el depinde **doar** de componentele puse pe el. Un buton = GameObject +
`RectTransform` + `Image` (desenul) + `Button` (logica de click).

---

## 2. C# pentru programatori C++

### 2.1 Fișiere, namespace, using
```csharp
using System;                       // ca #include + using namespace std; – aduce tipuri dintr-un namespace
namespace Travee.TicTacToe.Core     // exact ca namespace în C++, punctele = namespace-uri imbricate
{
    ...
}
```
- În C# **nu există headere**. Declarația și implementarea sunt în același loc.
- `using X;` nu copiază text ca `#include`, doar îți permite să scrii `List` în loc de `System.Collections.Generic.List`.

### 2.2 Clase și struct-uri
```csharp
public sealed class Board { ... }    // sealed = nu poate fi moștenită (ca `final` în C++)
public static class Search { ... }   // static class = doar funcții statice, nu poți face `new Search()`
public readonly struct MoveRecord    // struct = tip valoare; readonly = câmpurile nu se mai schimbă după constructor
```
**Diferența cheie față de C++:**
- `class` este **tip referință**: variabila e de fapt un *pointer* (cu garbage collector). `var b2 = b1;` → ambele arată spre **același** obiect.
- `struct` este **tip valoare**: se copiază la atribuire, ca un struct C++ obișnuit.
- Nu există `delete`. Memoria e eliberată automat de **garbage collector** când nimeni nu mai referă obiectul.
- Nu există pointeri `*` sau referințe `&` (în cod normal). Accesezi totul cu `.`.

### 2.3 Modificatori de acces
- `public` – vizibil peste tot. `private` – doar în clasă (**implicit**, dacă nu scrii nimic).
- `internal` – vizibil doar în același assembly (DLL). O clasă fără modificator (ex. `static class AIUtil`) e `internal`.
- `readonly` pe câmp – poate fi setat doar în declarație sau în constructor (ca un `const` membru inițializat în constructor).
- `const` – constantă la compilare (ca `constexpr`).

### 2.4 Proprietăți (getter/setter elegant)
```csharp
public int EmptyCount => Count(Mark.None);        // proprietate doar-citire, calculată la fiecare acces
public bool InputEnabled { get; set; }            // "auto-property": compilatorul creează un câmp ascuns
public BoardView Board { get; private set; }      // oricine o citește, doar clasa o poate scrie
public Board Board { get; } = new Board();        // doar-citire, inițializată o singură dată
```
Se folosesc ca niște câmpuri: `board.EmptyCount` (fără paranteze), dar în spate rulează o funcție.

### 2.5 `=>` (expression-bodied)
```csharp
public static int Row(int cell) => cell / Size;
// este exact:
public static int Row(int cell) { return cell / Size; }
```

### 2.6 `var`
`var x = new List<int>();` = `auto x = ...` din C++. Tipul e dedus la compilare; tot static e.

### 2.7 Interpolare de string-uri
```csharp
$"Celula {cell} este ocupată"     // ca std::format("Celula {} este ocupată", cell)
$"{gameCounter:D3}"               // format: 3 cifre cu zerouri în față → 007
$"{rate:P1}"                      // procent cu o zecimală → 66.7%
```

### 2.8 `out` și tuple
```csharp
public Mark Winner(out int[] winningLine)   // `out` = parametru prin referință care TREBUIE setat de funcție
board.Winner(out int[] line);               // la apel declari variabila direct acolo
board.Winner(out _);                        // `_` = "nu mă interesează valoarea"
(int win, int draw, int loss) Simulate(...) // funcție care returnează un tuplu (ca std::tuple)
var (win, _, _) = Simulate(...);            // destructurare (ca structured bindings: auto [w, d, l] = ...)
```

### 2.9 Tablouri și colecții
```csharp
int[] a = new int[9];                 // tablou de mărime fixă, pe heap, inițializat cu 0
int[][] lines = { new[]{0,1,2}, ...};  // tablou de tablouri ("jagged array")
var list = new List<int>();           // ca std::vector<int>
list.Add(5); list.Count; list[0]; list.Clear();
var dict = new Dictionary<int,int>(); // ca std::unordered_map<int,int>
dict.TryGetValue(key, out int v)      // caută: returnează bool, iar valoarea vine prin `out`
dict[key] = value;                    // inserează sau suprascrie
foreach (int x in list) { ... }       // ca for (int x : list)
Array.IndexOf(arr, value)             // poziția valorii sau -1
Array.Copy(src, dst, n)               // ca std::copy / memcpy
```
`IReadOnlyList<T>` = o „vedere” doar-citire peste o listă (ca `const std::vector<T>&`).

### 2.10 Enum-uri
```csharp
public enum Mark { None = 0, X = 1, O = 2 }
(int)Mark.X        // = 1, conversie explicită la int
Mark.X.ToString()  // = "X" – numele valorii ca text (în C++ n-ai asta gratis)
Enum.GetValues(typeof(DifficultyLevel))  // toate valorile enum-ului
```

### 2.11 Metode de extensie
```csharp
public static class MarkExtensions
{
    public static Mark Opponent(this Mark mark) { ... }   // `this` în fața primului parametru
}
// se apelează ca și cum ar fi o metodă a lui Mark:
Mark.X.Opponent()   // == MarkExtensions.Opponent(Mark.X)
```
Îți permite să „adaugi” metode la un tip pe care nu-l poți modifica (aici unui enum).

### 2.12 Lambda, `Action`, `Func`, evenimente
```csharp
Action                  // pointer la funcție void fără parametri      ~ std::function<void()>
Action<int>             // void f(int)                                  ~ std::function<void(int)>
Func<Board,Mark,int>    // int f(Board, Mark) – ultimul tip e cel returnat
() => DoSomething()     // lambda; capturează automat variabilele folosite (prin referință!)
c => c > 3              // lambda cu un parametru
```
**Evenimente** (echivalentul multicast delegates din Unreal):
```csharp
public event Action<int> CellSelected;   // declarație
x.CellSelected += OnCellSelected;        // abonare (AddDynamic / AddUObject în Unreal)
x.CellSelected -= OnCellSelected;        // dezabonare
CellSelected?.Invoke(4);                 // declanșare (Broadcast). `?.` = doar dacă cineva e abonat
```
Doar clasa care declară evenimentul îl poate declanșa; ceilalți doar se pot abona.

### 2.13 Operatori cu `?`
- `a?.B()` – apelează `B` doar dacă `a` nu e null (altfel nu face nimic). Ex: `logger?.EndGame()`.
- `a ?? b` – „`a`, iar dacă e null, atunci `b`”. Ex: `reactionMs ?? 0`.
- `double?` – **nullable**: un `double` care poate fi și `null` (ca `std::optional<double>`).
  `.HasValue` = are valoare? `.Value` = valoarea.
- `cond ? a : b` – operatorul ternar, ca în C++.

### 2.14 Excepții
`throw new InvalidOperationException("mesaj");` – ca `throw std::runtime_error(...)`. Le folosim doar
pentru erori de programare (o mutare imposibilă), nu pentru fluxul normal.

### 2.15 Atribute `[...]`
Etichete puse pe clase/câmpuri/metode, citite de Unity sau de alte unelte (ca macro-urile `UPROPERTY`/`UCLASS`):
- `[Serializable]` – clasa poate fi salvată/afișată în Inspector.
- `[SerializeField]` – câmp privat, dar vizibil și salvat în Inspector.
- `[Header("Text")]`, `[Tooltip("Text")]`, `[Min(0f)]` – doar aspectul în Inspector.
- `[DisallowMultipleComponent]` – nu poți pune componenta de două ori pe același obiect.
- `[MenuItem("TRAVEE/...")]` – comandă în meniul editorului.
- `[Test]`, `[TestCase(...)]` – funcții de test NUnit.

### 2.16 `System.Random`
```csharp
var rng = new System.Random(seed);  // generator; același seed → aceeași secvență (reproductibil)
rng.NextDouble()                     // număr real uniform în [0, 1)
rng.Next(n)                          // întreg uniform în [0, n)
```
Atenție: Unity are și el o clasă `UnityEngine.Random` (statică). De aceea uneori scriu explicit
`System.Random` sau `UnityEngine.Random`, ca să nu se încurce.

---

## 3. Concepte Unity folosite în proiect

### 3.1 MonoBehaviour
Orice clasă `: MonoBehaviour` e o componentă. **Nu o creezi cu `new`**, ci cu:
```csharp
var c = gameObject.AddComponent<GameController>();
```
Unity apelează automat anumite metode după nume (fără `override`!): `Awake()`, `Start()`, `Update()`,
`OnEnable()` etc. Noi folosim doar `Awake()`.

În interiorul unui MonoBehaviour ai acces direct la:
- `gameObject` – obiectul pe care stă componenta;
- `transform` – poziția/ierarhia lui.

### 3.2 Ierarhia (părinte–copil)
```csharp
go.transform.SetParent(parent, false);
```
Pune obiectul ca **copil** al altuia (ca `AttachToComponent`). `false` = „păstrează coordonatele locale”,
adică poziția e relativă la părinte. Pentru UI aproape mereu vrei `false`.

### 3.3 `gameObject.SetActive(true/false)`
Ascunde/afișează un obiect **cu tot cu copiii lui** și îi oprește componentele. Așa schimbăm ecranele
(meniu, joc, rezultat): toate există mereu, doar unul e activ.

### 3.4 UI în Unity (uGUI)
- **Canvas** – „pânza” pe care se desenează UI-ul. `ScreenSpaceOverlay` = desenat direct peste ecran.
- **CanvasScaler** – scalează UI-ul pentru diverse rezoluții. Noi proiectăm totul la 1920×1080.
- **GraphicRaycaster** – detectează pe ce element de UI ai dat click.
- **EventSystem** + **InputSystemUIInputModule** – transformă mouse/touch-ul în evenimente de click pentru UI.
  Fără ele butoanele nu reacționează.
- **RectTransform** – `Transform`-ul elementelor de UI. Are:
  - `anchorMin` / `anchorMax` – de ce punct al părintelui e „agățat” elementul (0,0 = stânga-jos, 1,1 = dreapta-sus, 0.5,0.5 = centru);
  - `pivot` – punctul propriu care e poziționat (0.5,0.5 = centrul elementului);
  - `anchoredPosition` – deplasarea față de ancoră, în pixeli de referință;
  - `sizeDelta` – lățime/înălțime (când ancorele min și max coincid);
  - `offsetMin` / `offsetMax` – marginile, când elementul se întinde pe tot părintele.
- **Image** – dreptunghi colorat (sau un sprite). **Text** – text. **Button** – face click-abil un element.
- Ordinea desenării: copiii creați **mai târziu** sunt desenați **deasupra**.

### 3.5 Corutine (foarte important – înlocuiesc Tick + stări)
O corutină e o funcție care se poate „opri” și continua în frame-urile următoare:
```csharp
IEnumerator MatchRoutine()
{
    Debug.Log("acum");
    yield return new WaitForSeconds(1f);  // pauză 1 secundă, jocul continuă normal între timp
    Debug.Log("după o secundă");
    yield return null;                    // așteaptă exact un frame
    yield return AltaCorutina();          // așteaptă până se termină altă corutină
}
StartCoroutine(MatchRoutine());   // pornire
StopAllCoroutines();              // oprește toate corutinele acestei componente
```
`IEnumerator` + `yield return` e un mecanism C# (generator). Unity îl folosește ca să-ți dea cod
„secvențial” care durează mai multe frame-uri – fără state machine manuală și fără timere.
În Unreal ai face asta cu `FTimerHandle` sau cu latent actions din Blueprint.

### 3.6 Timp
- `Time.deltaTime` – secunde de la frame-ul anterior (ca `DeltaTime` din Tick).
- `Time.realtimeSinceStartup` – secunde reale de la pornirea aplicației (nu e afectat de pauză/time scale).
  Îl folosim pentru timpul de reacție.

### 3.7 Altele
- `Debug.Log(...)` – mesaj în Console.
- `Application.persistentDataPath` – un folder unde aplicația are mereu drept de scriere (pe Windows:
  `AppData\LocalLow\<Companie>\<Produs>`; pe Quest/Android: memoria aplicației).
- `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` – fontul inclus în Unity.
- `FindAnyObjectByType<T>()` – caută în scenă orice obiect cu componenta T (ca `UGameplayStatics::GetActorOfClass`).
- `Camera.main` – camera cu tag-ul „MainCamera”.
- `Color`, `Vector2`, `Vector3` – struct-uri (tipuri valoare). `new Vector2(x, y)`.
- `Mathf.Lerp(a, b, t)` – interpolare liniară: `a + (b - a) * t`. `Mathf.Clamp01(x)` – limitează la [0,1].

### 3.8 Unity și `null`
Obiectele Unity (`GameObject`, componente, `Font`, `Sprite`) au o comparație cu `null` „specială”
(un obiect distrus *pare* null). De aceea pentru ele folosesc `if (x == null)` și **nu** `x ??= ...`
sau `x?.`. Pentru clasele noastre C# obișnuite (ex. `SessionLogger`) `?.` e în regulă.
