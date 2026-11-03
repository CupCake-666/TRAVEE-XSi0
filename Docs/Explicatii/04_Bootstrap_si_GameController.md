# 04 – TicTacToeBootstrap.cs și GameController.cs

Acestea sunt primele fișiere din partea **Unity** (`Assets/Scripts/Game/`). Aici apar `MonoBehaviour`,
`Awake`, corutine etc. – vezi `00_Introducere`, secțiunea 3.

---

## TicTacToeBootstrap.cs – punctul de pornire

E **singura componentă pusă în scenă** (pe obiectul `XSi0`). Restul jocului îl construiește ea din cod.
Echivalent Unreal: un `GameMode` care la `BeginPlay` creează widget-urile și actorii.

### Antet
```csharp
using Travee.TicTacToe.Core;          // ← logica (AffectedSide, DifficultySettings)
using Travee.TicTacToe.UI;            // ← ecranele și Theme
using UnityEngine;                    // ← MonoBehaviour, GameObject, Camera, Vector2...
using UnityEngine.EventSystems;       // ← EventSystem
using UnityEngine.InputSystem.UI;     // ← InputSystemUIInputModule (din pachetul Input System)
using UnityEngine.UI;                 // ← CanvasScaler, GraphicRaycaster
```

### Câmpurile din Inspector
```csharp
[DisallowMultipleComponent]                    // ← nu poți pune componenta de 2 ori pe același obiect
public sealed class TicTacToeBootstrap : MonoBehaviour   // ← e o componentă Unity
{
    [Header("Pacient")]                        // ← titlu de secțiune în Inspector
    [SerializeField] AffectedSide affectedSide = AffectedSide.Left;
    // ← câmp privat, dar vizibil în Inspector (dropdown Left/Right) – ca UPROPERTY(EditAnywhere)

    [Header("Dificultate")]
    [SerializeField] DifficultySettings difficulty = new DifficultySettings();
    // ← clasa [Serializable] apare în Inspector ca un grup care se poate desfășura, cu toate câmpurile ei
    [SerializeField, Min(0f), Tooltip("Pauza (secunde) înainte ca AI-ul să mute, ca pacientul să urmărească mutarea.")]
    float aiMoveDelay = 0.8f;
    // ← mai multe atribute pe același câmp. Min(0) = Inspector-ul nu acceptă valori negative

    [Header("Date")]
    [SerializeField, Tooltip("Salvează games.csv și moves.csv în persistentDataPath/TRAVEE/XSi0.")]
    bool logData = true;
    [SerializeField, Tooltip("0 = aleator. O valoare fixă face meciurile reproductibile.")]
    int randomSeed;                            // ← fără valoare explicită = 0
```
**Important:** valorile scrise aici (`= 0.8f`) sunt doar cele *implicite*. După ce componenta e pusă în scenă,
Unity salvează valorile din Inspector în fișierul scenei – dacă schimbi valoarea în cod, obiectul existent
păstrează valoarea veche (poți da click dreapta pe componentă → **Reset**).

### `Awake()` – construcția jocului
```csharp
    void Awake()                               // ← Unity o apelează automat când obiectul apare (la Play)
    {
        EnsureCamera();                        // ← avem cameră?
        EnsureEventSystem();                   // ← avem sistem de evenimente pentru click-uri?
        var canvas = CreateCanvas();           // ← „pânza” pe care desenăm UI-ul

        // Ordinea contează: ultimul creat e desenat deasupra.
        var game = GameScreen.Create(canvas);      // ← jos: ecranul cu tabla
        var coinFlip = CoinFlipScreen.Create(canvas); // ← peste el: moneda (semi-transparent)
        var result = ResultScreen.Create(canvas);  // ← peste el: rezultatul (semi-transparent)
        var menu = MenuScreen.Create(canvas);      // ← deasupra tuturor: meniul

        var controller = gameObject.AddComponent<GameController>();
        // ← adaugă componenta GameController pe ACELAȘI obiect (XSi0). Așa „creezi” un MonoBehaviour.
        controller.Init(difficulty, affectedSide, aiMoveDelay, logData, randomSeed, menu, coinFlip, game, result);
        // ← îi dăm tot ce are nevoie (dependency injection manual)
    }
```
De ce nu `new GameController()`? Componentele Unity **nu** se creează cu `new`; Unity trebuie să le
înregistreze ca să le apeleze metodele (Awake, corutine etc.). De aceea există o metodă separată `Init(...)`
în loc de constructor cu parametri.

### `EnsureCamera()`
```csharp
    static void EnsureCamera()                 // ← static: nu folosește câmpurile obiectului
    {
        var cam = Camera.main;                 // ← caută camera cu tag-ul "MainCamera"
        if (cam == null)                       // ← nu există? o creăm
        {
            cam = new GameObject("Main Camera").AddComponent<Camera>();
            // ← obiect nou cu numele "Main Camera" + componenta Camera; AddComponent returnează componenta
            cam.tag = "MainCamera";            // ← ca să fie găsită de Camera.main
            cam.orthographic = true;           // ← cameră 2D (fără perspectivă)
        }
        cam.clearFlags = CameraClearFlags.SolidColor;  // ← fundal de o singură culoare (nu skybox)
        cam.backgroundColor = Theme.Background;        // ← gri-albăstrui deschis
    }
```

### `EnsureEventSystem()`
```csharp
    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;   // ← există deja unul în scenă? nu facem nimic
        var go = new GameObject("EventSystem", typeof(EventSystem));
        // ← obiect nou care are deja componenta EventSystem (al doilea parametru = lista de componente)
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        // ← modulul care citește mouse/touch prin noul Input System și îl transformă în click-uri pe UI.
        //   AssignDefaultActions() îi dă acțiunile standard (click stânga, touch, scroll, navigare cu tastatura).
    }
```
Fără EventSystem, butoanele UI se desenează, dar **nu reacționează** la click.

### `CreateCanvas()`
```csharp
    Transform CreateCanvas()
    {
        var go = new GameObject("Canvas", typeof(RectTransform));   // ← obiect UI (are RectTransform, nu Transform simplu)
        go.transform.SetParent(transform, false);                    // ← îl punem copil al lui XSi0 (ordine în Hierarchy)

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;           // ← UI desenat direct peste ecran

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;  // ← scalează după rezoluția ecranului
        scaler.referenceResolution = new Vector2(1920, 1080);        // ← toate pozițiile din cod sunt gândite pentru 1920x1080
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;        // ← 1 = se potrivește după ÎNĂLȚIME (tabla încape mereu pe verticală)

        go.AddComponent<GraphicRaycaster>();   // ← detectează pe ce element s-a dat click
        return go.transform;                   // ← ecranele vor fi create ca și copii ai acestui transform
    }
}
```

---

## GameController.cs – fluxul jocului

Leagă totul: ecranele, AI-ul, logica și logger-ul. **Nu știe nimic despre mouse sau VR** – primește doar
„pacientul a ales căsuța X” prin `ICellInputSource`.

### Câmpuri
```csharp
public sealed class GameController : MonoBehaviour
{
    readonly List<ICellInputSource> inputSources = new List<ICellInputSource>();
    // ← toate sursele de input (acum doar tabla; în VR și mâna)

    DifficultySettings settings;               // ← setările primite din Bootstrap
    AffectedSide affectedSide;                 // ← partea afectată curentă (se poate schimba din meniu)
    float aiMoveDelay;
    System.Random rng;                         // ← generatorul de numere aleatoare (scris complet ca să nu se confunde cu UnityEngine.Random)
    SessionLogger logger;                      // ← null dacă logarea e dezactivată

    MenuScreen menu;                           // ← referințe la ecrane
    CoinFlipScreen coinFlip;
    GameScreen game;
    ResultScreen result;

    Match match;                               // ← meciul curent (null când nu se joacă)
    DifficultyLevel currentLevel;              // ← ținut minte pentru „Joacă din nou”
    bool awaitingPatient;                      // ← așteptăm acum o mutare de la pacient?
    int selectedCell = -1;                     // ← căsuța aleasă de pacient; -1 = încă nimic
```

### `Init(...)`
```csharp
    public void Init(DifficultySettings settings, AffectedSide affectedSide, float aiMoveDelay, bool logData,
        int randomSeed, MenuScreen menu, CoinFlipScreen coinFlip, GameScreen game, ResultScreen result)
    {
        this.settings = settings;              // ← salvăm parametrii în câmpuri
        this.affectedSide = affectedSide;
        this.aiMoveDelay = aiMoveDelay;
        rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
        // ← seed fix → aceleași „aruncări” de fiecare dată; fără seed → diferit la fiecare pornire
        logger = logData ? new SessionLogger() : null;   // ← logger doar dacă e activat

        this.menu = menu;
        this.coinFlip = coinFlip;
        this.game = game;
        this.result = result;

        menu.LevelChosen += StartGame;                   // ← click pe un nivel → StartGame(nivel)
        menu.SideToggleRequested += ToggleAffectedSide;  // ← click pe „Mâna afectată” → comutăm partea
        game.MenuRequested += ShowMenu;                  // ← butonul „Meniu” din joc
        result.PlayAgainRequested += () => StartGame(currentLevel);
        // ← lambda: evenimentul nu are parametri, dar StartGame vrea un nivel → îi dăm nivelul curent
        result.MenuRequested += ShowMenu;                // ← „Alege alt nivel”

        AddInputSource(game.Board);            // ← tabla e prima sursă de input (mouse/touch)
        ShowMenu();                            // ← pornim cu meniul
    }
```
`+=` pe un eveniment = „când se întâmplă asta, apelează funcția mea” (ca `OnClicked.AddDynamic` în Unreal).
Ecranele doar anunță evenimente; controller-ul decide ce se întâmplă.

### Gestionarea surselor de input
```csharp
    public void AddInputSource(ICellInputSource source)
    {
        if (inputSources.Contains(source)) return;   // ← nu o adăugăm de două ori
        inputSources.Add(source);
        source.CellSelected += OnCellSelected;       // ← ascultăm selecțiile ei
        source.InputEnabled = awaitingPatient;       // ← o aducem în aceeași stare ca restul (activă doar pe rândul pacientului)
    }

    public void RemoveInputSource(ICellInputSource source)
    {
        if (!inputSources.Remove(source)) return;    // ← Remove returnează false dacă nu era în listă
        source.CellSelected -= OnCellSelected;       // ← nu mai ascultăm
        source.InputEnabled = false;
    }
```
În VR: `controller.AddInputSource(handTouchInput);` – și gata.

### `ShowMenu()` și `ToggleAffectedSide()`
```csharp
    void ShowMenu()
    {
        AbortCurrentMatch();                   // ← dacă era un meci în curs, îl oprim (și îl logăm ca abandonat)
        menu.SetAffectedSide(affectedSide);    // ← textul butonului „Mâna afectată: ...”
        menu.gameObject.SetActive(true);       // ← afișăm meniul...
        coinFlip.gameObject.SetActive(false);  // ← ...și ascundem restul
        game.gameObject.SetActive(false);
        result.gameObject.SetActive(false);
    }

    void ToggleAffectedSide()
    {
        affectedSide = affectedSide == AffectedSide.Left ? AffectedSide.Right : AffectedSide.Left;  // ← comutare
        menu.SetAffectedSide(affectedSide);    // ← actualizăm textul
    }
```

### `StartGame(level)`
```csharp
    void StartGame(DifficultyLevel level)
    {
        AbortCurrentMatch();                   // ← siguranță: oprim orice meci anterior
        currentLevel = level;                  // ← ținem minte pentru „Joacă din nou”
        StartCoroutine(MatchRoutine(level));   // ← pornim corutina care rulează tot meciul
    }
```

### `MatchRoutine(level)` – inima jocului
O corutină (vezi 00, §3.5): o funcție care se poate opri cu `yield return` și continua în frame-urile următoare.
Așa scriem tot meciul **de sus în jos, ca pe un program obișnuit**, deși durează minute.

```csharp
    IEnumerator MatchRoutine(DifficultyLevel level)
    {
        // --- pregătire ecran ---
        menu.gameObject.SetActive(false);
        result.gameObject.SetActive(false);
        game.gameObject.SetActive(true);
        game.Setup(level);                     // ← titlul nivelului + tablă goală

        // --- coin flip ---
        bool patientStarts = CoinFlip.PatientStarts(level, settings, rng);  // ← AICI se decide cine începe
        coinFlip.gameObject.SetActive(true);
        yield return coinFlip.Play(patientStarts);
        // ← așteaptă până se termină animația monedei (o corutină poate aștepta altă corutină)
        coinFlip.gameObject.SetActive(false);

        // --- meciul ---
        match = new Match(level, patientStarts);           // ← meci nou: tablă goală, simboluri stabilite
        var ai = AIFactory.Create(level, settings, affectedSide, rng);  // ← AI-ul pentru nivelul ales
        ai.OnNewGame();
        logger?.BeginGame(match, affectedSide);            // ← `?.`: doar dacă logger nu e null
        game.SetPlayers(match.PatientMark);                // ← „Tu: X   AI: 0”

        while (!match.IsOver)                  // ← cât timp nu s-a terminat
        {
            if (match.IsPatientTurn)           // ===== RÂNDUL PACIENTULUI =====
            {
                game.SetStatus("Rândul tău – alege o căsuță");
                selectedCell = -1;             // ← încă n-a ales nimic
                float turnStart = Time.realtimeSinceStartup;   // ← momentul începerii rândului (pentru timpul de reacție)
                SetAwaitingPatient(true);      // ← activăm inputul
                while (selectedCell < 0) yield return null;
                // ← AȘTEAPTĂ cadru cu cadru până când OnCellSelected pune o valoare în selectedCell.
                //   Jocul nu e blocat: Unity desenează normal, doar corutina „stă” aici.
                SetAwaitingPatient(false);     // ← dezactivăm inputul (nu mai acceptăm click-uri)

                double reactionMs = (Time.realtimeSinceStartup - turnStart) * 1000.0;  // ← secunde → milisecunde
                Apply(selectedCell, reactionMs);   // ← aplicăm mutarea
            }
            else                               // ===== RÂNDUL AI-ULUI =====
            {
                game.SetStatus("AI-ul se gândește…");
                yield return new WaitForSeconds(aiMoveDelay);  // ← pauză ca pacientul să urmărească
                Apply(ai.ChooseMove(match.Board.Clone(), match.AIMark), null);
                // ← AI-ul primește o COPIE a tablei; null = AI-ul nu are timp de reacție
            }
        }

        // --- final ---
        logger?.EndGame();                     // ← scriem meciul în CSV
        match.Board.Winner(out int[] line);    // ← linia câștigătoare (sau null la remiză)
        game.Board.HighlightLine(line);        // ← o colorăm în verde
        game.SetStatus("");
        yield return new WaitForSeconds(1.2f); // ← lăsăm pacientul să vadă linia

        ShowResult(match.Outcome, level);      // ← ecranul de rezultat
        match = null;                          // ← nu mai e niciun meci activ
    }
```

### `Apply(cell, reactionMs)`
```csharp
    void Apply(int cell, double? reactionMs)   // ← double? = poate fi null (pentru AI)
    {
        match.Play(cell);                      // ← mutarea în logică
        var move = match.Moves[match.Moves.Count - 1];   // ← ultima mutare din istoric
        game.Board.SetMark(cell, move.Mark);   // ← o afișăm pe ecran (cu animație)
        logger?.LogMove(move, reactionMs);     // ← o trimitem logger-ului
    }
```

### `OnCellSelected(cell)` – apelată de orice sursă de input
```csharp
    void OnCellSelected(int cell)
    {
        if (!awaitingPatient || match == null || !match.IsPatientTurn) return;
        // ← ignorăm selecția dacă nu așteptăm o mutare (ex. click în timpul rândului AI-ului)
        if (!match.Board.IsEmpty(cell)) return; // căsuță ocupată: ignorăm, pacientul mai încearcă
        selectedCell = cell;                   // ← corutina vede valoarea și iese din bucla de așteptare
    }
```
Observă că funcția **nu** face mutarea direct – doar „anunță” corutina. Toată logica de ordine stă într-un loc (`MatchRoutine`).

### `SetAwaitingPatient(value)`
```csharp
    void SetAwaitingPatient(bool value)
    {
        awaitingPatient = value;
        foreach (var source in inputSources) source.InputEnabled = value;   // ← activăm/dezactivăm toate sursele
    }
```

### `AbortCurrentMatch()`
```csharp
    void AbortCurrentMatch()
    {
        StopAllCoroutines();                   // ← oprește MatchRoutine (și animația monedei din interiorul ei)
        SetAwaitingPatient(false);
        if (match != null)                     // ← era un meci început?
        {
            logger?.EndGame(); // meci neterminat → "Abandoned"
            match = null;
        }
    }
```

### `ShowResult(outcome, level)` – mesajele
```csharp
    void ShowResult(MatchOutcome outcome, DifficultyLevel level)
    {
        bool gentleLevel = level == DifficultyLevel.Level1 || level == DifficultyLevel.Level2;
        switch (outcome)
        {
            case MatchOutcome.PatientWin:
                result.Show("Felicitări, ai câștigat!", "Ai jucat excelent.", Theme.X);
                break;
            case MatchOutcome.Draw when gentleLevel:
            // ← `case ... when condiție`: cazul se potrivește DOAR dacă e remiză ȘI nivel 1-2
                result.Show("Bravo, ai jucat foarte bine!", "Ai fost foarte aproape de victorie – mai încearcă o dată!", Theme.X);
                break;
            case MatchOutcome.Draw:            // ← remiză la nivelurile 3-4
                result.Show("Remiză", level == DifficultyLevel.Level4
                    ? "Ai ținut piept AI-ului! Pentru victorie e nevoie de o strategie aproape perfectă."
                    : "Un meci echilibrat. Mai încerci?", Theme.Text);
                break;
            default:                           // ← singurul caz rămas: AIWin
                result.Show("AI-ul a câștigat de data aceasta", "Nu-i nimic – fiecare meci este un exercițiu bun!", Theme.O);
                break;
        }
    }
}
```
Spre deosebire de C++, în C# fiecare `case` trebuie să se termine cu `break`/`return` (nu există „fall-through” accidental).
