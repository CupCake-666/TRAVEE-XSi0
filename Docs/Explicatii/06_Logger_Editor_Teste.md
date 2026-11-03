# 06 – SessionLogger.cs, SceneSetup.cs, AITests.cs și fișierele .asmdef

---

## SessionLogger.cs – salvarea datelor

Scrie două fișiere CSV (le poți deschide în Excel sau le poți încărca în Python/R pentru analiză):
- `games.csv` – un rând pe meci;
- `moves.csv` – un rând pe mutare.

### Antet și constante
```csharp
using System;                       // ← DateTime, Environment
using System.Collections.Generic;   // ← List
using System.Globalization;         // ← CultureInfo
using System.IO;                    // ← File, Path, Directory (lucrul cu fișiere)
using System.Text;                  // ← Encoding
using Travee.TicTacToe.Core;
using UnityEngine;                  // ← Application, Time, Debug

public sealed class SessionLogger      // ← clasă C# obișnuită (NU MonoBehaviour) → se creează cu `new`
{
    const string GamesHeader =
        "session_id,game_id,start_time,difficulty,affected_side,patient_started,patient_mark,outcome," +
        "total_moves,patient_moves,duration_s,avg_patient_reaction_ms,patient_moves_on_affected_side";
    // ← capul de tabel pentru games.csv (`+` lipește două string-uri)
    const string MovesHeader =
        "session_id,game_id,move_no,actor,mark,cell,row,col,column_side,on_affected_side,reaction_ms,time_since_start_ms";

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    // ← „cultura invariantă” = formatare internațională: punct zecimal (1.5), nu virgulă (1,5).
    //   Pe un Windows setat în română, fără asta 1.5 ar deveni "1,5" și ar strica separatorul CSV.
```

### Câmpuri
```csharp
    readonly string gamesPath;          // ← calea completă spre games.csv
    readonly string movesPath;          // ← calea completă spre moves.csv
    readonly string sessionId;          // ← ex. "20261031_143005" (data și ora pornirii)
    readonly List<string> pendingMoves = new List<string>();   // ← rândurile mutărilor, ținute în memorie până la final

    int gameCounter;                    // ← al câtelea meci din sesiune
    string gameId;                      // ← ex. "20261031_143005_003"
    DateTime gameStartTime;             // ← data/ora reală a începerii meciului (pentru CSV)
    float gameStartRealtime;            // ← momentul în secunde (pentru durate precise)
    Match match;                        // ← meciul logat acum (null = niciunul)
    AffectedSide side;                  // ← partea afectată din acest meci
    int patientMoves;                   // ← contoare pentru rândul din games.csv
    int patientMovesOnAffectedSide;
    double totalReactionMs;

    public string Directory { get; }    // ← folderul unde se salvează (doar-citire)
```

### Constructorul
```csharp
    public SessionLogger()
    {
        sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv);
        // ← data și ora curentă ca text: an luna zi _ oră minut secundă
        Directory = Path.Combine(Application.persistentDataPath, "TRAVEE", "XSi0");
        // ← Path.Combine lipește bucăți de cale cu separatorul corect (\ pe Windows, / în rest)
        System.IO.Directory.CreateDirectory(Directory);
        // ← creează folderul (și părinții lui) dacă nu există. Scriem `System.IO.Directory` complet
        //   pentru că proprietatea noastră se numește tot `Directory` și altfel s-ar confunda.
        gamesPath = Path.Combine(Directory, "games.csv");
        movesPath = Path.Combine(Directory, "moves.csv");
        EnsureHeader(gamesPath, GamesHeader);  // ← scrie capul de tabel dacă fișierul e nou
        EnsureHeader(movesPath, MovesHeader);
        Debug.Log($"[TRAVEE] Datele sesiunii {sessionId} se salvează în: {Directory}");   // ← calea apare în Console
    }
```

### `BeginGame` – început de meci
```csharp
    public void BeginGame(Match newMatch, AffectedSide affectedSide)
    {
        match = newMatch;
        side = affectedSide;
        gameCounter++;
        gameId = $"{sessionId}_{gameCounter:D3}";   // ← :D3 = număr pe 3 cifre: 001, 002...
        gameStartTime = DateTime.Now;
        gameStartRealtime = Time.realtimeSinceStartup;
        pendingMoves.Clear();                  // ← resetăm tot ce ține de meciul anterior
        patientMoves = 0;
        patientMovesOnAffectedSide = 0;
        totalReactionMs = 0;
    }
```

### `LogMove` – o mutare
```csharp
    public void LogMove(MoveRecord move, double? reactionMs)
    {
        if (match == null) return;             // ← siguranță: nu logăm nimic în afara unui meci

        int row = Board.Row(move.Cell);
        int col = Board.Column(move.Cell);
        string columnSide = col == 0 ? "Left" : col == 2 ? "Right" : "Center";
        // ← ternari înlănțuiți: coloana 0 = stânga, 2 = dreapta, altfel mijloc
        bool onAffected = (side == AffectedSide.Left && col == 0) || (side == AffectedSide.Right && col == 2);
        // ← mutarea e pe coloana părții afectate?

        if (move.ByPatient)                    // ← statistici doar pentru mutările pacientului
        {
            patientMoves++;
            if (onAffected) patientMovesOnAffectedSide++;
            totalReactionMs += reactionMs ?? 0;   // ← `?? 0`: dacă e null, adunăm 0
        }

        double sinceStart = (Time.realtimeSinceStartup - gameStartRealtime) * 1000.0;  // ← ms de la începutul meciului
        pendingMoves.Add(string.Join(",",      // ← lipește valorile cu virgulă între ele → un rând CSV
            sessionId, gameId, move.Number.ToString(Inv),
            move.ByPatient ? "Patient" : "AI", move.Mark.ToString(),
            move.Cell.ToString(Inv), row.ToString(Inv), col.ToString(Inv),
            columnSide, onAffected ? "1" : "0",            // ← bool scris ca 1/0 (mai ușor de analizat)
            reactionMs.HasValue ? reactionMs.Value.ToString("F0", Inv) : "",
            // ← "F0" = fără zecimale. Pentru AI (null) lăsăm celula goală.
            sinceStart.ToString("F0", Inv)));
    }
```

### `EndGame` – finalul meciului (sau abandon)
```csharp
    public void EndGame()
    {
        if (match == null) return;             // ← apelat de două ori? a doua oară nu face nimic

        string outcome = match.Outcome == MatchOutcome.InProgress ? "Abandoned" : match.Outcome.ToString();
        // ← dacă meciul nu s-a terminat, înseamnă că pacientul a ieșit → "Abandoned"
        double duration = Time.realtimeSinceStartup - gameStartRealtime;     // ← durata în secunde
        string avgReaction = patientMoves > 0 ? (totalReactionMs / patientMoves).ToString("F0", Inv) : "";
        // ← media timpilor de reacție (evităm împărțirea la 0)

        string gameRow = string.Join(",",
            sessionId, gameId, gameStartTime.ToString("yyyy-MM-ddTHH:mm:ss", Inv),   // ← dată în format standard ISO
            ((int)match.Level).ToString(Inv), side.ToString(),
            match.PatientStarts ? "1" : "0", match.PatientMark.ToString(), outcome,
            match.Moves.Count.ToString(Inv), patientMoves.ToString(Inv),
            duration.ToString("F2", Inv), avgReaction, patientMovesOnAffectedSide.ToString(Inv));
            // ← "F2" = două zecimale

        try                                    // ← ca try/catch din C++
        {
            File.AppendAllText(gamesPath, gameRow + Environment.NewLine, Encoding.UTF8);
            // ← ADAUGĂ la sfârșitul fișierului (nu suprascrie). UTF8 = diacriticele sunt păstrate.
            if (pendingMoves.Count > 0)
                File.AppendAllText(movesPath, string.Join(Environment.NewLine, pendingMoves) + Environment.NewLine, Encoding.UTF8);
            // ← toate mutările odată, câte una pe rând
        }
        catch (IOException e)                  // ← eroare de fișier (cel mai des: e deschis în Excel)
        {
            Debug.LogError($"[TRAVEE] Nu s-au putut salva datele (fișierul e deschis în Excel?): {e.Message}");
        }

        match = null;                          // ← nu mai logăm nimic până la următorul BeginGame
        pendingMoves.Clear();
    }
```

### `EnsureHeader`
```csharp
    static void EnsureHeader(string path, string header)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)    // ← fișierul nu există sau e gol?
            File.WriteAllText(path, header + Environment.NewLine, Encoding.UTF8);   // ← scriem doar capul de tabel
    }
}
```
Datorită acestei funcții, fiecare sesiune nouă **adaugă** la aceleași fișiere, deci toate datele stau într-un singur tabel.

---

## SceneSetup.cs – meniul „TRAVEE” din editor

```csharp
using System.Linq;                     // ← LINQ: Where, ToList (operații pe colecții)
using UnityEditor;                     // ← API-ul editorului (există DOAR în editor, nu în jocul compilat)
using UnityEditor.SceneManagement;     // ← EditorSceneManager
using UnityEngine;

public static class SceneSetup
{
    const string ScenePath = "Assets/Scenes/XSi0.unity";

    [MenuItem("TRAVEE/Creează scena X și 0")]   // ← apare în bara de meniu a editorului
    static void CreateScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        // ← dialogul „Vrei să salvezi scena curentă?”. Dacă apeși Cancel → oprim.

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // ← scenă nouă, complet goală, care o înlocuiește pe cea deschisă

        var cam = new GameObject("Main Camera").AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.transform.position = new Vector3(0, 0, -10);   // ← camera puțin în spate (convenția Unity 2D)

        new GameObject("XSi0").AddComponent<TicTacToeBootstrap>();   // ← obiectul cu scriptul de pornire

        EditorSceneManager.SaveScene(scene, ScenePath);    // ← salvăm fișierul .unity

        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        // ← lista de scene din Build Settings, fără scena noastră (dacă exista deja). Where = filtrare cu lambda.
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        // ← o punem PRIMA (indexul 0 = scena care pornește în build); true = activă
        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.Log($"[TRAVEE] Scena a fost creată: {ScenePath}. Apasă Play.");
    }
}
```
Fișierul e în assembly-ul **Editor** (vezi `.asmdef` mai jos), deci nu ajunge niciodată în aplicația finală.
Fără această separare, `using UnityEditor` ar da eroare la build.

---

## AITests.cs – testele automate

Rulează din **Window → General → Test Runner → EditMode → Run All**. Folosesc **NUnit**
(similar cu Google Test din C++ sau Automation Tests din Unreal).

### Pregătire
```csharp
public class AITests
{
    const int Games = 3000;            // ← câte meciuri simulăm per test
    static readonly DifficultySettings Settings = new DifficultySettings();   // ← setările implicite
```

### Pacienții simulați
```csharp
    static int RandomPatient(Board b, Mark m, Random r)
    {
        var e = b.EmptyCells();
        return e[r.Next(e.Count)];     // ← o căsuță liberă la întâmplare
    }

    static int BeginnerPatient(Board b, Mark m, Random r)
    {
        var e = b.EmptyCells();
        foreach (int c in e) if (b.IsWinningMove(c, m)) return c;              // ← 1. câștigă dacă poate
        foreach (int c in e) if (b.IsWinningMove(c, m.Opponent())) return c;   // ← 2. blochează dacă trebuie
        return e[r.Next(e.Count)];                                              // ← 3. altfel la întâmplare
    }

    static int PerfectPatient(Board b, Mark m, Random r)
    {
        var e = b.EmptyCells();
        int best = int.MinValue;
        var bestMoves = new List<int>();
        foreach (int c in e)
        {
            int s = Search.ScoreAfter(b, c, m);          // ← scorul minimax al mutării
            if (s > best) { best = s; bestMoves.Clear(); }   // ← mai bună → resetăm lista
            if (s == best) bestMoves.Add(c);              // ← egală cu cea mai bună → o adăugăm
        }
        return bestMoves[r.Next(bestMoves.Count)];       // ← una dintre cele mai bune
    }
```

### `Simulate` – joacă multe meciuri
```csharp
    static (int win, int draw, int loss) Simulate(DifficultyLevel level, Func<Board, Mark, Random, int> patient, int seed)
    // ← `patient` e o FUNCȚIE primită ca parametru (una dintre cele 3 de mai sus)
    {
        var rng = new Random(seed);    // ← seed fix → testul dă mereu același rezultat
        var ai = AIFactory.Create(level, Settings, AffectedSide.Left, rng);
        int win = 0, draw = 0, loss = 0;
        for (int g = 0; g < Games; g++)
        {
            var match = new Match(level, CoinFlip.PatientStarts(level, Settings, rng));   // ← coin flip real
            ai.OnNewGame();
            while (!match.IsOver)
            {
                int cell = match.IsPatientTurn
                    ? patient(match.Board.Clone(), match.PatientMark, rng)    // ← mută pacientul simulat
                    : ai.ChooseMove(match.Board.Clone(), match.AIMark);       // ← mută AI-ul
                match.Play(cell);
            }
            switch (match.Outcome)
            {
                case MatchOutcome.PatientWin: win++; break;
                case MatchOutcome.AIWin: loss++; break;
                default: draw++; break;
            }
        }
        return (win, draw, loss);      // ← tuplu cu cele 3 numere
    }
```
Exact același cod ca în joc (`Match`, `AIFactory`, `CoinFlip`) – doar că în loc de click avem o funcție.

### Testele
```csharp
    [Test]                             // ← marchează funcția ca test
    public void Board_DetectsWinsAndDraws()
    {
        var b = new Board();
        foreach (int c in new[] { 0, 3, 1, 4, 2 }) b.Place(c, b.NextToMove);   // ← X: 0,1,2 (rândul de sus)
        Assert.AreEqual(BoardState.XWins, b.State);    // ← verificare: dacă nu e adevărat, testul pică

        var d = new Board();
        foreach (int c in new[] { 0, 1, 2, 4, 3, 5, 7, 6, 8 }) d.Place(c, d.NextToMove);   // ← o partidă terminată remiză
        Assert.AreEqual(BoardState.Draw, d.State);
    }

    [Test]
    public void Minimax_EmptyBoardIsDraw()
    {
        Assert.AreEqual(0, Search.ScoreForX(new Board()));   // ← jucat perfect, X și 0 e remiză
    }

    [Test]
    public void Level1_PatientAlwaysStarts()
    {
        var rng = new Random(1);
        for (int i = 0; i < 1000; i++)
            Assert.IsTrue(CoinFlip.PatientStarts(DifficultyLevel.Level1, Settings, rng));   // ← 1000 aruncări, toate pentru pacient
    }

    [TestCase(DifficultyLevel.Level1)] // ← același test rulat de 2 ori, cu parametri diferiți
    [TestCase(DifficultyLevel.Level2)]
    public void GentleLevels_PatientNeverLoses(DifficultyLevel level)
    {
        foreach (var patient in new Func<Board, Mark, Random, int>[] { RandomPatient, BeginnerPatient, PerfectPatient })
            Assert.AreEqual(0, Simulate(level, patient, 7).loss, $"{level} a câștigat contra pacientului {patient.Method.Name}");
        // ← contra fiecărui tip de pacient: 0 înfrângeri. Al 3-lea parametru = mesajul afișat dacă pică
    }

    [Test]
    public void Level3_BeginnerWinsAroundTwoThirds()
    {
        var (win, _, _) = Simulate(DifficultyLevel.Level3, BeginnerPatient, 11);   // ← ne interesează doar victoriile
        double rate = (double)win / Games;     // ← (double) ca împărțirea să nu fie întreagă
        Assert.That(rate, Is.InRange(0.55, 0.80), $"Rata de victorie a pacientului: {rate:P1}");
    }

    [Test]
    public void Level4_PerfectPatientNeverLoses_AndSometimesWins()
    {
        var (win, _, loss) = Simulate(DifficultyLevel.Level4, PerfectPatient, 13);
        Assert.AreEqual(0, loss);              // ← jocul perfect nu pierde niciodată (corectitudine)
        Assert.Greater(win, 0);                // ← dar uneori câștigă (nivelul e „fair”)
    }

    [Test]
    public void Level4_WithoutMistakes_IsUnbeatable()
    {
        var noMistakes = new DifficultySettings { level4MistakeChance = 0f };   // ← setări cu 0% greșeli
        var rng = new Random(5);
        var ai = AIFactory.Create(DifficultyLevel.Level4, noMistakes, AffectedSide.Left, rng);
        for (int g = 0; g < 500; g++)
        {
            var match = new Match(DifficultyLevel.Level4, g % 2 == 0);   // ← alternăm cine începe
            ai.OnNewGame();
            while (!match.IsOver)
                match.Play(match.IsPatientTurn
                    ? PerfectPatient(match.Board.Clone(), match.PatientMark, rng)
                    : ai.ChooseMove(match.Board.Clone(), match.AIMark));
            Assert.AreNotEqual(MatchOutcome.PatientWin, match.Outcome);   // ← fără greșeli, AI-ul nu poate fi învins
        }
    }
}
```

---

## Fișierele `.asmdef` (Assembly Definition)

Echivalentul modulelor din Unreal (`.Build.cs`). Sunt fișiere JSON. Câmpurile importante:

```json
{
    "name": "Travee.TicTacToe.Core",        // ← numele DLL-ului generat
    "rootNamespace": "Travee.TicTacToe.Core",
    "references": [],                        // ← alte assembly-uri de care depinde (ca PublicDependencyModuleNames)
    "includePlatforms": [],                  // ← gol = toate platformele; ["Editor"] = doar în editor
    "autoReferenced": true,                  // ← scripturile „libere” din proiect îl pot folosi automat
    "noEngineReferences": true               // ← INTERZIS să folosească UnityEngine → Core rămâne C# pur
}
```

| Fișier | Dependențe | Particularități |
|---|---|---|
| `Scripts/Core/Travee.TicTacToe.Core.asmdef` | – | `noEngineReferences: true` |
| `Scripts/Game/Travee.TicTacToe.asmdef` | Core, `Unity.InputSystem`, `UnityEngine.UI` | |
| `Scripts/Editor/Travee.TicTacToe.Editor.asmdef` | Game | `includePlatforms: ["Editor"]` → nu intră în build |
| `Tests/EditMode/Travee.TicTacToe.Tests.asmdef` | Core, TestRunner | `overrideReferences` + `nunit.framework.dll`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, `autoReferenced: false` |

Fiecare folder cu `.asmdef` (și subfolderele lui) devine un DLL separat. Unity recompilează doar DLL-urile
afectate de o modificare, deci și compilarea e mai rapidă.

---

## Rezumat: drumul unui click

```
Click pe căsuța 4
  → Button.onClick                       (UIFactory.Button a legat lambda-ul)
  → BoardView.OnCellClicked(4)           (doar dacă InputEnabled)
  → CellSelected?.Invoke(4)              (evenimentul interfeței ICellInputSource)
  → GameController.OnCellSelected(4)     (verifică: e rândul pacientului? căsuța e liberă?)
  → selectedCell = 4                     (corutina MatchRoutine iese din bucla de așteptare)
  → reactionMs = acum − turnStart
  → Apply(4): Match.Play → Board.Place → MoveRecord
              BoardView.SetMark (animație Pop)
              SessionLogger.LogMove (buffer)
  → rândul AI-ului: WaitForSeconds(0.8) → ai.ChooseMove(copie) → Apply(...)
  → la final: SessionLogger.EndGame → scrie în games.csv și moves.csv
```
