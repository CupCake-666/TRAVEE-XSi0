# 02 – Search.cs (minimax și expectimax)

`Assets/Scripts/Core/AI/Search.cs` este „creierul” comun al tuturor nivelurilor. Conține doi algoritmi:

| Algoritm | Întrebarea la care răspunde | Folosit de |
|---|---|---|
| **Minimax** | „Dacă de aici ambii joacă perfect, cine câștigă?” | nivelurile 3 și 4 |
| **Expectimax** | „Dacă pacientul mută la întâmplare și AI-ul îl ajută, ce șanse are pacientul să câștige?” | nivelurile 1 și 2 |

---

## Ideea de bază: arborele de joc

Din orice poziție poți încerca fiecare mutare posibilă, apoi din fiecare poziție nouă toate răspunsurile,
și tot așa până la final. Se formează un **arbore**. Valorile de la frunze (câștig/pierdere/remiză) „urcă”
spre rădăcină după o regulă:

- **Minimax:** fiecare jucător alege ce e mai bun pentru el (X ia maximul, O ia minimul).
- **Expectimax:** AI-ul ia maximul, iar pentru pacient se face **media** (pentru că mută aleator).

**Memoizare:** aceeași poziție se poate obține pe drumuri diferite (ex. X în 0, O în 4, X în 8 dă aceeași tablă ca
X în 8, O în 4, X în 0). O calculăm o singură dată și ținem minte rezultatul într-un `Dictionary`.

---

## Câmpurile clasei
```csharp
public static class Search                      // ← doar funcții statice, nu creezi obiecte Search
{
    public const double AIWinPenalty = 10.0;    // ← cât de „rău” e ca AI-ul să câștige la nivelurile 1-2

    static readonly Dictionary<int, int> minimaxMemo = new Dictionary<int, int>();
    // ← memoria minimax: cheie = board.Key, valoare = scorul calculat
    static readonly Dictionary<int, double> helpMemo = new Dictionary<int, double>();
    // ← memoria expectimax: cheie = board.Key*3 + aiMark, valoare = probabilitatea
```
Fiind `static`, memoria se păstrează între meciuri – a doua oară totul e instant.

---

## `ScoreForX(board)` – minimax
```csharp
public static int ScoreForX(Board board)
{
    var winner = board.Winner();                // ← a câștigat cineva deja?
    int empty = board.EmptyCount;               // ← câte căsuțe libere au mai rămas
    if (winner == Mark.X) return 10 + empty;    // ← X a câștigat: pozitiv. Cu cât mai repede (mai multe goale), cu atât mai mare
    if (winner == Mark.O) return -(10 + empty); // ← O a câștigat: negativ
    if (empty == 0) return 0;                   // ← tabla plină, nimeni n-a câștigat: remiză

    int key = board.Key;
    if (minimaxMemo.TryGetValue(key, out int cached)) return cached;
    // ← am mai calculat poziția asta? atunci returnăm direct rezultatul salvat

    var toMove = board.NextToMove;              // ← cine e la mutare în poziția asta
    int best = toMove == Mark.X ? int.MinValue : int.MaxValue;
    // ← X caută maximul → pornim de la cel mai mic număr posibil; O caută minimul → pornim de la cel mai mare

    for (int cell = 0; cell < Board.CellCount; cell++)
    {
        if (!board.IsEmpty(cell)) continue;     // ← sărim peste căsuțele ocupate
        board.Place(cell, toMove);              // ← încercăm mutarea
        int score = ScoreForX(board);           // ← RECURSIV: cât valorează poziția rezultată
        board.Clear(cell);                      // ← anulăm mutarea (tabla revine cum era)
        best = toMove == Mark.X ? System.Math.Max(best, score) : System.Math.Min(best, score);
        // ← X păstrează cel mai mare scor, O pe cel mai mic
    }

    minimaxMemo[key] = best;                    // ← salvăm rezultatul pentru data viitoare
    return best;
}
```
**De ce `10 + empty`?** Fără el, o victorie în 3 mutări și una în 7 ar valora la fel, iar AI-ul ar putea
„tărăgăna” o victorie sigură. Cu el, victoria imediată are scor mai mare → AI-ul o ia pe ea.
Pentru pierderi e invers: o pierdere mai târzie e „mai puțin rea” (−11 e mai bun decât −14).

**Semnul e tot ce contează de obicei:** `> 0` = X câștigă forțat, `< 0` = O câștigă forțat, `0` = remiză.
Pe tabla goală rezultatul e 0 – faimosul fapt că X și 0 jucat perfect se termină remiză.

---

## Funcții ajutătoare pentru minimax
```csharp
public static int Score(Board board, Mark perspective) =>
    perspective == Mark.X ? ScoreForX(board) : -ScoreForX(board);
// ← același scor, dar „din perspectiva” lui X sau O: pentru O inversăm semnul (ce e bun pentru X e rău pentru O)

public static int ScoreAfter(Board board, int cell, Mark mover)
{
    board.Place(cell, mover);                   // ← încearcă mutarea
    int score = Score(board, mover);            // ← cât e de bună poziția pentru cel care a mutat
    board.Clear(cell);                          // ← anulează
    return score;
}
// ← „Dacă `mover` pune în `cell`, ce rezultat obține cu joc perfect?” – funcția folosită cel mai des de AI
```

```csharp
public static int CountWinningReplies(Board board, Mark replier)
{
    int n = 0;
    foreach (int cell in board.EmptyCells())
        if (ScoreAfter(board, cell, replier) > 0) n++;   // ← mutarea asta îl duce pe replier la câștig forțat
    return n;
}
// ← câte mutări ale lui `replier` câștigă (folosit la nivelul 4: „câte răspunsuri pedepsesc greșeala mea?”)

public static int CountLosingReplies(Board board, Mark replier)
{
    int n = 0;
    foreach (int cell in board.EmptyCells())
        if (ScoreAfter(board, cell, replier) < 0) n++;   // ← mutarea asta îl duce pe replier la pierdere
    return n;
}
// ← câte capcane are poziția (nivelul 4 alege poziția cu cele mai multe capcane pentru pacient)

public static bool HasImmediateWin(Board board, Mark mark)
{
    for (int cell = 0; cell < Board.CellCount; cell++)
        if (board.IsWinningMove(cell, mark)) return true; // ← există o căsuță care îi dă linia ACUM?
    return false;
}
```

---

## `HelpValue(board, aiMark)` – expectimax (AI ajutător)
```csharp
public static double HelpValue(Board board, Mark aiMark)
{
    var patientMark = aiMark.Opponent();        // ← simbolul pacientului
    var winner = board.Winner();
    if (winner == patientMark) return 1.0;      // ← pacientul a câștigat: 100%
    if (winner == aiMark) return -AIWinPenalty; // ← AI-ul a câștigat: -10 (foarte rău!)
    if (board.IsFull) return 0.0;               // ← remiză: 0%

    int key = board.Key * 3 + (int)aiMark;
    // ← cheia include și simbolul AI-ului: aceeași tablă are altă valoare dacă AI-ul e X sau O
    if (helpMemo.TryGetValue(key, out double cached)) return cached;

    var toMove = board.NextToMove;
    double result;
    if (toMove == aiMark)                       // ← E RÂNDUL AI-ULUI: alege mutarea cea mai ajutătoare
    {
        result = double.NegativeInfinity;       // ← pornim de la -∞ ca orice valoare să fie mai mare
        for (int cell = 0; cell < Board.CellCount; cell++)
        {
            if (!board.IsEmpty(cell)) continue;
            result = System.Math.Max(result, HelpValueAfter(board, cell, aiMark));  // ← păstrăm maximul
        }
    }
    else                                        // ← E RÂNDUL PACIENTULUI: presupunem că mută la întâmplare
    {
        double sum = 0;
        int count = 0;
        for (int cell = 0; cell < Board.CellCount; cell++)
        {
            if (!board.IsEmpty(cell)) continue;
            board.Place(cell, toMove);          // ← încercăm mutarea pacientului
            sum += HelpValue(board, aiMark);    // ← RECURSIV
            board.Clear(cell);
            count++;
        }
        result = sum / count;                   // ← MEDIA: fiecare mutare are aceeași probabilitate
    }

    helpMemo[key] = result;
    return result;
}
```

**De ce pacient „aleator”?** E cel mai pesimist model: un pacient cu mâna afectată poate atinge din greșeală
o altă căsuță. Dacă AI-ul îl ajută să câștige *chiar și așa*, cu un pacient care gândește va merge și mai bine.

**De ce −10 pentru victoria AI-ului?** Valorile normale sunt între 0 și 1. Dacă o variantă are chiar și 5%
șansă ca AI-ul să câștige, contribuția e 0.05 × (−10) = −0.5 – mai mult decât orice câștig posibil.
Deci AI-ul evită **orice** drum în care ar putea câștiga. Asta garantează că la nivelurile 1–2 pacientul nu pierde
(testat pe zeci de mii de meciuri).

```csharp
public static double HelpValueAfter(Board board, int cell, Mark aiMark)
{
    board.Place(cell, aiMark);                  // ← AI-ul încearcă să pună în `cell`
    double value = HelpValue(board, aiMark);    // ← cât de bine îi merge pacientului de aici
    board.Clear(cell);
    return value;
}
// ← „Dacă AI-ul pune aici, ce șanse are pacientul?” – folosit de nivelurile 1 și 2 pentru fiecare căsuță
```
