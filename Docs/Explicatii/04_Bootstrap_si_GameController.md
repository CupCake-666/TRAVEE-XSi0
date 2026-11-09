# 04 – TicTacToeBootstrap.cs și GameController.cs

Primele fișiere din partea **Unity** (`Assets/Scripts/Game/`). Aici apar `MonoBehaviour`, `Awake`, corutine etc.
(vezi `00_Introducere`, secțiunea 3). Convenție: comentariile `// ←` sunt explicațiile mele.

---

## Cele două interfețe care leagă totul

`GameController` nu știe dacă jocul e 2D sau 3D, dacă AI-ul are corp sau nu, ori dacă pacientul folosește
mouse-ul sau mâna. Lucrează doar cu trei interfețe:

| Interfață | Ce înseamnă | Cine o implementează |
|---|---|---|
| `ICellInputSource` | „pacientul a ales căsuța *i*” | `Board3DView` (mouse acum; mâna în VR) |
| `IBoardView` | „afișează tabla” | `Board3DView` (tabla 3D) |
| `IOpponentPresenter` | „cum arată AI-ul cât joacă” | `OpponentCharacter` (personajul 3D) |

### IBoardView.cs
```csharp
public interface IBoardView
{
    void ResetBoard();                  // ← golește tabla la un meci nou
    void SetMark(int cell, Mark mark);  // ← afișează X sau 0 în căsuță
    void HighlightLine(int[] line);     // ← colorează linia câștigătoare
}
```

### IOpponentPresenter.cs
```csharp
public interface IOpponentPresenter
{
    void OnMenu();                                  // ← jucătorul e în meniu
    void OnAffectedSideChanged(AffectedSide side);  // ← s-a schimbat mâna afectată (personajul își poate muta locul)
    void OnMatchStart();                            // ← începe meciul (după monedă)
    void OnPatientTurn();                           // ← e rândul pacientului
    IEnumerator Think(float seconds);               // ← AI-ul „se gândește” (corutină: controller-ul o așteaptă)
    IEnumerator PlacePiece(int cell, Action place); // ← AI-ul pune piesa; `place()` = momentul în care apare pe tablă
    void OnMatchEnd(MatchOutcome outcome, bool celebrateDraw);  // ← reacția de la final
}
```
`Think` și `PlacePiece` întorc `IEnumerator`: controller-ul le așteaptă cu `yield return`, deci meciul
continuă abia după ce personajul a terminat de mers și de pus piesa.

`PlacePiece` primește o funcție, `place`, în loc să pună piesa singur. Personajul decide **când** apare piesa
(în momentul în care mâna atinge căsuța), dar logica mutării rămâne în controller.

```csharp
public sealed class NoOpponent : IOpponentPresenter
{
    public void OnMenu() { }                        // ← nu face nimic
    ...
    public IEnumerator Think(float seconds) { yield return new WaitForSeconds(seconds); }  // ← doar pauza
    public IEnumerator PlacePiece(int cell, Action place) { place(); yield break; }        // ← piesa apare imediat
}
```
Un AI „fără corp”. E folosit când nu există personaj (varianta 2D sau dacă modelul lipsește). Așa controller-ul
nu are nevoie de verificări `if (opponent != null)` peste tot (tiparul *Null Object*).

---

## TicTacToeBootstrap.cs – punctul de pornire

**Singura componentă pusă în scenă** (pe obiectul `XSi0`). Construiește tot jocul din cod.
Echivalent Unreal: un `GameMode` care la `BeginPlay` creează widget-urile și actorii.

### Câmpurile din Inspector
```csharp
public enum ViewMode { TwoD, ThreeD }          // ← modul de afișare; scena 3D are ThreeD

[DisallowMultipleComponent]                    // ← nu poți pune componenta de 2 ori pe același obiect
public sealed class TicTacToeBootstrap : MonoBehaviour
{
    [Header("Afișare")]                        // ← titlu de secțiune în Inspector
    [SerializeField, Tooltip("...")] ViewMode viewMode = ViewMode.TwoD;
    // ← câmp privat, dar vizibil în Inspector (ca UPROPERTY(EditAnywhere)). Meniul TRAVEE îl pune pe ThreeD.
    [SerializeField] GameObject opponentModel;                 // ← modelul FBX al personajului (Ch19_nonPBR)
    [SerializeField] RuntimeAnimatorController opponentAnimator; // ← Animator Controller-ul generat
    [SerializeField] OpponentPlacement opponentPlacement = OpponentPlacement.AffectedSide;
    // ← unde stă personajul: pe partea afectată (implicit), pe cea sănătoasă, mereu stânga sau mereu dreapta
    [SerializeField] Material boardSeeThroughMaterial;         // ← materialul transparent al tablei

    [Header("Pacient")]
    [SerializeField] AffectedSide affectedSide = AffectedSide.Left;   // ← mâna afectată la pornire

    [Header("Dificultate")]
    [SerializeField] DifficultySettings difficulty = new DifficultySettings();
    // ← clasa [Serializable] apare în Inspector ca un grup cu toate procentele (vezi 01)
    [SerializeField, Min(0f)] float aiMoveDelay = 0.8f;       // ← cât „se gândește” AI-ul (secunde)

    [Header("Date")]
    [SerializeField] bool logData = true;      // ← salvează CSV-urile
    [SerializeField] int randomSeed;           // ← 0 = aleator; altă valoare = meciuri reproductibile
```
Modelul, controller-ul și materialul sunt puse automat de meniul **TRAVEE → Creează scena X și 0 (3D)**
(vezi `09`). Valorile scrise în cod (`= 0.8f`) sunt doar cele *implicite*: după ce componenta e în scenă,
Unity păstrează valorile din Inspector.

### `Awake()` – construcția jocului
```csharp
void Awake()                                   // ← apelată automat de Unity când pornește scena
{
    bool is3D = viewMode == ViewMode.ThreeD;

    EnsureEventSystem();                       // ← fără EventSystem, butoanele UI nu primesc click-uri
    var canvas = CreateCanvas();               // ← „pânza” pe care se desenează UI-ul

    // Ordinea contează: ultimul creat e desenat deasupra.
    var game = GameScreen.Create(canvas, withBoard: !is3D);   // ← în 3D: doar textele (tabla e în scenă)
    var coinFlip = CoinFlipScreen.Create(canvas);             // ← moneda
    var result = ResultScreen.Create(canvas, overScene: is3D);// ← în 3D: un card jos, se vede personajul
    var menu = MenuScreen.Create(canvas, overScene: is3D);    // ← în 3D: un card în stânga

    var controller = gameObject.AddComponent<GameController>();
    // ← componentele Unity nu se creează cu `new`, ci cu AddComponent; de aceea există Init(...) separat
    if (is3D)
    {
        var room = Room3D.Build(transform);    // ← podea, perete, lumină, cameră (vezi 07)
        room.Camera.gameObject.AddComponent<DesktopHeadLook>(); // ← click dreapta = privește în jur (în VR: ochelarii)
        var board = Board3DView.Create(room.Root, room.BoardPosition, room.BoardRotation, room.Camera,
            boardSeeThroughMaterial);          // ← tabla 3D verticală, în fața pacientului
        board.transform.localScale = Vector3.one * Room3D.BoardScale;   // ← mărită de 1,3 ori
        var opponent = CreateOpponent(room, board);   // ← personajul (sau null dacă lipsește modelul)

        controller.Init(difficulty, affectedSide, aiMoveDelay, logData, randomSeed,
            menu, coinFlip, game, result, board, opponent);
        // ← `board` e dat ca IBoardView; `opponent` ca IOpponentPresenter (null → NoOpponent)
        controller.AddInputSource(board);      // ← tabla e și sursa de selecție (mouse)
        if (opponent != null) opponent.Enter();// ← personajul intră în scenă mergând
    }
    else
    {
        // ← varianta 2D veche: tabla e un element de UI (BoardView), fără personaj
        EnsureCamera2D();
        controller.Init(..., game.Board);
        controller.AddInputSource(game.Board);
    }
}
```
`if (opponent != null)` și nu `opponent?.Enter()`: `opponent` e o componentă Unity, iar pentru obiectele Unity
operatorul `?.` nu e sigur (vezi 00, §3.8).

### `CreateOpponent(room, board)`
```csharp
OpponentCharacter CreateOpponent(Room3D room, Board3DView board)
{
    if (opponentModel == null || opponentAnimator == null)
    {
        Debug.LogWarning("[TRAVEE] Lipsește modelul sau Animator Controller-ul personajului. ...");
        return null;                           // ← jocul merge și fără personaj (NoOpponent)
    }

    var model = Instantiate(opponentModel, room.Root);   // ← o copie a modelului în scenă (ca SpawnActor)
    model.name = "Opponent";
    return OpponentCharacter.Create(model, opponentAnimator, room, board, opponentPlacement);
    // ← pune pe model componenta care îl animă (vezi 08)
}
```

### `EnsureEventSystem()` și `CreateCanvas()`
```csharp
static void EnsureEventSystem()
{
    if (FindAnyObjectByType<EventSystem>() != null) return;   // ← există deja? nu facem nimic
    var go = new GameObject("EventSystem", typeof(EventSystem));
    go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    // ← modulul care citește mouse/touch prin noul Input System și îl transformă în click-uri pe UI
}

Transform CreateCanvas()
{
    var go = new GameObject("Canvas", typeof(RectTransform));
    go.transform.SetParent(transform, false);  // ← copil al lui XSi0
    var canvas = go.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;   // ← UI desenat peste imaginea camerei 3D
    var scaler = go.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1920, 1080);// ← pozițiile din cod sunt gândite pentru 1920x1080
    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
    scaler.matchWidthOrHeight = 1f;            // ← se scalează după înălțimea ecranului
    go.AddComponent<GraphicRaycaster>();       // ← detectează pe ce element de UI s-a dat click
    return go.transform;
}
```
`EnsureCamera2D()` e folosită doar de varianta 2D (cameră ortografică, fundal simplu).

---

## GameController.cs – fluxul jocului

Leagă ecranele, AI-ul, logica și logger-ul. E o **mașină de stări** scrisă ca o corutină:
Meniu → Monedă → Meci → Rezultat.

### Câmpuri
```csharp
readonly List<ICellInputSource> inputSources = new List<ICellInputSource>();  // ← sursele de input (tabla; în VR și mâna)

DifficultySettings settings;   AffectedSide affectedSide;   float aiMoveDelay;
System.Random rng;             // ← scris complet ca să nu se confunde cu UnityEngine.Random
SessionLogger logger;          // ← null dacă logarea e dezactivată

MenuScreen menu; CoinFlipScreen coinFlip; GameScreen game; ResultScreen result;   // ← ecranele
IBoardView board;              // ← tabla (3D)
IOpponentPresenter opponent;   // ← personajul (sau NoOpponent)

Match match;                   // ← meciul curent (null când nu se joacă)
DifficultyLevel currentLevel;  // ← pentru „Joacă din nou”
bool awaitingPatient;          // ← așteptăm acum o mutare de la pacient?
int selectedCell = -1;         // ← căsuța aleasă; -1 = încă nimic
```

### `Init(...)`
```csharp
public void Init(DifficultySettings settings, AffectedSide affectedSide, float aiMoveDelay, bool logData,
    int randomSeed, MenuScreen menu, CoinFlipScreen coinFlip, GameScreen game, ResultScreen result,
    IBoardView board, IOpponentPresenter opponent = null)   // ← `= null`: parametru opțional
{
    this.settings = settings; this.affectedSide = affectedSide; this.aiMoveDelay = aiMoveDelay;
    rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
    logger = logData ? new SessionLogger() : null;

    this.menu = menu; this.coinFlip = coinFlip; this.game = game; this.result = result;
    this.board = board;
    this.opponent = opponent ?? new NoOpponent();   // ← fără personaj → AI fără corp
    this.opponent.OnAffectedSideChanged(affectedSide);   // ← personajul își ia locul pe partea corectă

    menu.LevelChosen += StartGame;                  // ← click pe un nivel → StartGame(nivel)
    menu.SideToggleRequested += ToggleAffectedSide; // ← click pe „Mâna afectată”
    game.MenuRequested += ShowMenu;                 // ← butonul „Meniu” din joc
    result.PlayAgainRequested += () => StartGame(currentLevel);   // ← lambda: îi dăm nivelul curent
    result.MenuRequested += ShowMenu;               // ← „Alege alt nivel”

    ShowMenu();                                     // ← pornim cu meniul
}
```
`+=` pe un eveniment = „când se întâmplă asta, apelează funcția mea” (ca `AddDynamic` în Unreal).

### Sursele de input
```csharp
public void AddInputSource(ICellInputSource source)
{
    if (inputSources.Contains(source)) return;     // ← nu o adăugăm de două ori
    inputSources.Add(source);
    source.CellSelected += OnCellSelected;         // ← ascultăm selecțiile ei
    source.InputEnabled = awaitingPatient;         // ← activă doar pe rândul pacientului
}

public void RemoveInputSource(ICellInputSource source)   // ← opusul (util în VR, la schimbarea controlerelor)
```
În VR, adăugarea mâinii va fi o singură linie: `controller.AddInputSource(handTouchInput);`.

### `ShowMenu()` și `ToggleAffectedSide()`
```csharp
void ShowMenu()
{
    AbortCurrentMatch();                           // ← oprim meciul în curs (logat ca abandonat)
    menu.SetAffectedSide(affectedSide);
    menu.gameObject.SetActive(true);               // ← afișăm meniul, ascundem restul
    coinFlip.gameObject.SetActive(false);
    game.gameObject.SetActive(false);
    result.gameObject.SetActive(false);
    opponent.OnMenu();                             // ← personajul se întoarce la locul lui
}

void ToggleAffectedSide()
{
    affectedSide = affectedSide == AffectedSide.Left ? AffectedSide.Right : AffectedSide.Left;
    menu.SetAffectedSide(affectedSide);
    opponent.OnAffectedSideChanged(affectedSide);  // ← dacă stă pe partea afectată, merge pe cealaltă parte
}
```

### `StartGame(level)` și `MatchRoutine(level)` – inima jocului
```csharp
void StartGame(DifficultyLevel level)
{
    AbortCurrentMatch();
    currentLevel = level;
    StartCoroutine(MatchRoutine(level));           // ← tot meciul e o singură corutină
}

IEnumerator MatchRoutine(DifficultyLevel level)
{
    // --- pregătire ---
    menu.gameObject.SetActive(false);
    result.gameObject.SetActive(false);
    game.gameObject.SetActive(true);
    game.Setup(level);                             // ← titlul nivelului, texte goale
    board.ResetBoard();                            // ← tabla goală
    opponent.OnMenu();                             // ← personajul revine la loc (tabla redevine opacă)

    // --- monedă ---
    bool patientStarts = CoinFlip.PatientStarts(level, settings, rng);   // ← AICI se decide cine începe
    coinFlip.gameObject.SetActive(true);
    yield return coinFlip.Play(patientStarts);     // ← așteaptă animația monedei
    coinFlip.gameObject.SetActive(false);

    // --- meciul ---
    match = new Match(level, patientStarts);       // ← cine începe e X
    var ai = AIFactory.Create(level, settings, affectedSide, rng);   // ← AI-ul nivelului (vezi 03)
    ai.OnNewGame();
    logger?.BeginGame(match, affectedSide);
    game.SetPlayers(match.PatientMark);            // ← „Tu: X   AI: 0”
    opponent.OnMatchStart();                       // ← personajul face cu mâna

    while (!match.IsOver)
    {
        if (match.IsPatientTurn)                   // ===== RÂNDUL PACIENTULUI =====
        {
            game.SetStatus("Rândul tău – alege o căsuță");
            selectedCell = -1;
            opponent.OnPatientTurn();              // ← personajul revine la loc și răspunde la privire
            float turnStart = Time.realtimeSinceStartup;   // ← începutul rândului (timpul de reacție)
            SetAwaitingPatient(true);              // ← activăm inputul
            while (selectedCell < 0) yield return null;
            // ← AȘTEAPTĂ cadru cu cadru până când OnCellSelected pune o valoare. Jocul nu e blocat.
            SetAwaitingPatient(false);

            double reactionMs = (Time.realtimeSinceStartup - turnStart) * 1000.0;
            Apply(selectedCell, reactionMs);
        }
        else                                       // ===== RÂNDUL AI-ULUI =====
        {
            game.SetStatus("AI-ul se gândește…");
            int cell = ai.ChooseMove(match.Board.Clone(), match.AIMark);   // ← decizia e instantanee
            yield return opponent.Think(aiMoveDelay);                       // ← personajul „se gândește”
            yield return opponent.PlacePiece(cell, () => Apply(cell, null));
            // ← personajul vine la tablă; când mâna ajunge la căsuță apelează lambda → piesa apare
        }
    }

    // --- final ---
    logger?.EndGame();                             // ← scriem meciul în CSV
    match.Board.Winner(out int[] line);
    board.HighlightLine(line);                     // ← linia câștigătoare devine verde
    game.SetStatus("");
    opponent.OnMatchEnd(match.Outcome, IsGentle(level));   // ← aplauze / ridicat din umeri / victorie
    yield return new WaitForSeconds(1.2f);

    ShowResult(match.Outcome, level);
    match = null;
}
```
AI-ul hotărăște mutarea **înainte** de animație: personajul doar o „joacă” vizual.

### `Apply`, `OnCellSelected`, `SetAwaitingPatient`, `AbortCurrentMatch`
```csharp
void Apply(int cell, double? reactionMs)           // ← double? = poate fi null (AI-ul nu are timp de reacție)
{
    match.Play(cell);                              // ← mutarea în logică
    var move = match.Moves[match.Moves.Count - 1]; // ← ultima mutare din istoric
    board.SetMark(cell, move.Mark);                // ← piesa 3D apare pe tablă
    logger?.LogMove(move, reactionMs);
}

void OnCellSelected(int cell)                      // ← apelată de orice sursă de input
{
    if (!awaitingPatient || match == null || !match.IsPatientTurn) return;   // ← nu e momentul
    if (!match.Board.IsEmpty(cell)) return;        // ← căsuță ocupată: pacientul mai încearcă
    selectedCell = cell;                           // ← „trezește” corutina
}

void SetAwaitingPatient(bool value)
{
    awaitingPatient = value;
    foreach (var source in inputSources) source.InputEnabled = value;
}

void AbortCurrentMatch()
{
    StopAllCoroutines();                           // ← oprește MatchRoutine (și moneda din interiorul ei)
    SetAwaitingPatient(false);
    if (match != null) { logger?.EndGame(); match = null; }   // ← meci neterminat → "Abandoned"
}
```

### `ShowResult` și `IsGentle`
```csharp
void ShowResult(MatchOutcome outcome, DifficultyLevel level)
{
    bool gentleLevel = IsGentle(level);
    switch (outcome)
    {
        case MatchOutcome.PatientWin: result.Show("Felicitări, ai câștigat!", "Ai jucat excelent.", Theme.X); break;
        case MatchOutcome.Draw when gentleLevel:   // ← remiză la nivelurile 1-2 → prezentată ca succes
            result.Show("Bravo, ai jucat foarte bine!", "...", Theme.X); break;
        case MatchOutcome.Draw:                    // ← remiză la nivelurile 3-4
            result.Show("Remiză", ..., Theme.Text); break;
        default:                                   // ← AI-ul a câștigat
            result.Show("AI-ul a câștigat de data aceasta", "Nu-i nimic – ...", Theme.O); break;
    }
}

static bool IsGentle(DifficultyLevel level) =>
    level == DifficultyLevel.Level1 || level == DifficultyLevel.Level2;
// ← folosită și pentru mesaj, și pentru reacția personajului (aplaudă și la remiză)
```
`case ... when condiție` = cazul se potrivește doar dacă e îndeplinită și condiția. În C#, fiecare `case` se
termină cu `break`/`return` (nu există „fall-through” accidental ca în C++).
