# 01 – Board.cs, Match.cs, DifficultySettings.cs

Fișierele din `Assets/Scripts/Core/`. Sunt **C# pur**: nu folosesc nimic din Unity, deci ar merge la fel
într-o aplicație de consolă (chiar așa am făcut simulările de 20.000 de meciuri).

Convenție: în blocurile de cod, comentariile `// ←` sunt explicațiile mele (nu există în fișierul real).

---

## Board.cs

### Antetul fișierului
```csharp
using System;                       // ← aduce tipuri de bază: Array, ArgumentException, InvalidOperationException
using System.Collections.Generic;   // ← aduce List<T>

namespace Travee.TicTacToe.Core     // ← tot codul de logică stă în acest namespace
{
```

### Enum-urile
```csharp
public enum Mark { None = 0, X = 1, O = 2 }
// ← ce poate fi într-o căsuță. Valorile numerice contează: le folosim la codificarea tablei (baza 3).

public enum BoardState { InProgress, XWins, OWins, Draw }
// ← starea tablei: încă se joacă / a câștigat X / a câștigat O / remiză.

/// <summary>Partea corpului afectată de AVC (mâna paralizată).</summary>
public enum AffectedSide { Left, Right }
// ← `/// <summary>` e un comentariu de documentare: apare ca tooltip în Visual Studio / Rider.
```

### `MarkExtensions` – funcții ajutătoare pentru `Mark`
```csharp
public static class MarkExtensions          // ← clasă statică = doar funcții, fără obiecte
{
    public static Mark Opponent(this Mark mark)   // ← metodă de extensie (vezi 00, §2.11): se scrie mark.Opponent()
    {
        switch (mark)                             // ← switch ca în C++
        {
            case Mark.X: return Mark.O;           // ← adversarul lui X e O
            case Mark.O: return Mark.X;           // ← adversarul lui O e X
            default: return Mark.None;            // ← None nu are adversar
        }
    }

    public static string Symbol(this Mark mark)   // ← textul care se afișează pe tablă
    {
        switch (mark)
        {
            case Mark.X: return "X";
            case Mark.O: return "0";              // ← cifra zero, cum se scrie „X și 0” în română
            default: return "";                   // ← căsuță goală → text gol
        }
    }
}
```
**Pe scurt:** `Opponent()` îți dă celălalt jucător, `Symbol()` îți dă ce se scrie în căsuță.

### Clasa `Board` – câmpuri și constructori
```csharp
public sealed class Board                    // ← sealed = nu se poate moșteni
{
    public const int Size = 3;               // ← latura tablei
    public const int CellCount = Size * Size;// ← 9 căsuțe (calculat la compilare)

    public static readonly int[][] Lines =   // ← cele 8 linii câștigătoare; `static` = una singură pentru toate tablele
    {
        new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, // rânduri
        new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 }, // coloane
        new[] { 0, 4, 8 }, new[] { 2, 4, 6 },                     // diagonale
    };
    // ← `new[] { 0, 1, 2 }` = un int[] cu 3 elemente (tipul e dedus)

    readonly Mark[] cells = new Mark[CellCount];
    // ← tabla propriu-zisă: 9 valori Mark. Fără modificator = private.
    // ← `readonly` = variabila `cells` arată mereu spre ACELAȘI tablou (conținutul se poate schimba).
    // ← un tablou nou de enum-uri e umplut automat cu 0 = Mark.None, deci tabla începe goală.

    public Board() { }                       // ← constructor gol: tablă goală

    public Board(Board other)                // ← constructor de copiere (ca în C++)
    {
        Array.Copy(other.cells, cells, CellCount);  // ← copiază cele 9 valori din cealaltă tablă
    }

    public Board Clone() => new Board(this); // ← o copie independentă; modificările ei nu afectează originalul
```
De ce `Clone()`? În C#, `var b2 = b1;` **nu copiază** tabla (ambele variabile arată spre același obiect).
Când îi dăm tabla AI-ului, îi dăm o copie, ca să nu poată strica tabla reală.

### Acces la celule și coordonate
```csharp
    public Mark this[int cell] => cells[cell];
    // ← INDEXER: îți permite să scrii board[4] ca la un tablou (ca operator[] în C++).
    //   Are doar `get` (=>), deci din afară poți CITI, dar nu poți scrie board[4] = X.

    public static int Row(int cell) => cell / Size;     // ← rândul: 0..2 (împărțire întreagă)
    public static int Column(int cell) => cell % Size;  // ← coloana: 0..2 (restul împărțirii)
    // ← ex: celula 5 → rând 5/3 = 1, coloană 5%3 = 2

    public bool IsEmpty(int cell) => cells[cell] == Mark.None;  // ← căsuța e liberă?
```

### Numărare
```csharp
    public int Count(Mark mark)              // ← câte căsuțe conțin `mark`
    {
        int n = 0;
        for (int i = 0; i < CellCount; i++)
            if (cells[i] == mark) n++;
        return n;
    }

    public int EmptyCount => Count(Mark.None);   // ← proprietate: câte căsuțe libere
    public bool IsFull => EmptyCount == 0;       // ← tabla e plină?

    /// <summary>Cine urmează la mutare (X începe mereu).</summary>
    public Mark NextToMove => Count(Mark.X) == Count(Mark.O) ? Mark.X : Mark.O;
    // ← dacă sunt tot atâtea X cât O → mută X; altfel (X are una în plus) → mută O.
    //   Așa NU ținem o variabilă separată „cine e la rând” care s-ar putea desincroniza.
```

### Lista căsuțelor libere
```csharp
    public List<int> EmptyCells()
    {
        var result = new List<int>(CellCount);   // ← listă goală; (9) = capacitate rezervată, ca vector::reserve
        for (int i = 0; i < CellCount; i++)
            if (cells[i] == Mark.None) result.Add(i);  // ← adaugă indexul fiecărei căsuțe libere
        return result;
    }
```

### Punerea și ștergerea unei piese
```csharp
    public void Place(int cell, Mark mark)
    {
        if (cell < 0 || cell >= CellCount) throw new ArgumentOutOfRangeException(nameof(cell));
        // ← index greșit → eroare. `nameof(cell)` = textul "cell" (rămâne corect dacă redenumești variabila)
        if (mark == Mark.None) throw new ArgumentException("Nu se poate plasa Mark.None.", nameof(mark));
        if (cells[cell] != Mark.None) throw new InvalidOperationException($"Celula {cell} este deja ocupată.");
        cells[cell] = mark;                      // ← mutarea propriu-zisă
    }

    /// <summary>Folosit de algoritmii de căutare pentru a anula o mutare.</summary>
    public void Clear(int cell) => cells[cell] = Mark.None;
```
Excepțiile sunt doar „plase de siguranță” pentru greșeli de programare. În joc, validarea normală se face
înainte (în `Match.TryPlay` și `GameController.OnCellSelected`), deci nu se ajunge niciodată aici cu date greșite.

### Câștigătorul
```csharp
    public Mark Winner() => Winner(out _);
    // ← variantă scurtă: vrem doar câștigătorul, linia nu ne interesează (`out _` o aruncă)

    public Mark Winner(out int[] winningLine)
    {
        foreach (var line in Lines)              // ← verificăm cele 8 linii
        {
            var m = cells[line[0]];              // ← ce e în prima căsuță a liniei
            if (m != Mark.None && m == cells[line[1]] && m == cells[line[2]])
            {                                    // ← nu e goală și toate 3 sunt la fel → linie completă
                winningLine = line;              // ← întoarcem și linia (pentru colorarea ei pe ecran)
                return m;                        // ← X sau O
            }
        }
        winningLine = null;                      // ← un parametru `out` TREBUIE setat în orice caz
        return Mark.None;                        // ← nimeni n-a câștigat
    }
```

### Starea tablei
```csharp
    public BoardState State
    {
        get                                      // ← proprietate cu getter pe mai multe linii
        {
            var w = Winner();
            if (w == Mark.X) return BoardState.XWins;
            if (w == Mark.O) return BoardState.OWins;
            return IsFull ? BoardState.Draw : BoardState.InProgress;  // ← plină fără câștigător = remiză
        }
    }

    public bool IsGameOver => State != BoardState.InProgress;   // ← s-a terminat jocul?
```

### „Dacă pun aici, câștig?”
```csharp
    public bool IsWinningMove(int cell, Mark mark)
    {
        if (!IsEmpty(cell)) return false;        // ← căsuță ocupată → nu se poate
        cells[cell] = mark;                      // ← pune TEMPORAR piesa
        bool wins = Winner() == mark;            // ← verifică dacă acum `mark` are o linie
        cells[cell] = Mark.None;                 // ← scoate piesa la loc (tabla rămâne neschimbată)
        return wins;
    }
```
Truc des folosit în AI: „încerci” o mutare, verifici, apoi o anulezi. E mai rapid decât să copiezi tabla.

### Codificarea tablei într-un număr
```csharp
    public int Key
    {
        get
        {
            int key = 0;
            for (int i = 0; i < CellCount; i++) key = key * 3 + (int)cells[i];
            return key;
        }
    }
```
Fiecare căsuță e o cifră în **baza 3** (0 = gol, 1 = X, 2 = O). Exact cum `"472"` în baza 10 se calculează
`((4)*10 + 7)*10 + 2`, aici facem `key = key*3 + cifră` pentru cele 9 căsuțe.
Rezultă un număr între 0 și 3⁹−1 = 19682, **diferit pentru fiecare tablă**. Îl folosim în `Search.cs`
ca cheie în dicționar („am mai calculat poziția asta?”).

### Afișare pentru debug
```csharp
    public override string ToString()           // ← `override`: suprascrie metoda virtuală din `object`
    {
        var sb = new System.Text.StringBuilder(); // ← construiește un string eficient (ca std::ostringstream)
        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++)
            {
                var m = cells[r * Size + c];
                sb.Append(m == Mark.None ? "." : m.Symbol());   // ← "." pentru gol
            }
            if (r < Size - 1) sb.Append('/');    // ← separator între rânduri
        }
        return sb.ToString();                    // ← ex: "X.0/.X./..."
    }
}
```
Toate obiectele C# au `ToString()`; suprascriind-o, `Debug.Log(board)` afișează ceva lizibil.

---

## Match.cs

### Tipurile ajutătoare
```csharp
public enum DifficultyLevel { Level1 = 1, Level2 = 2, Level3 = 3, Level4 = 4 }
// ← valorile numerice = numărul nivelului; (int)level dă 1..4 (folosit în CSV)

public enum MatchOutcome { InProgress, PatientWin, AIWin, Draw }
// ← rezultatul văzut DIN PERSPECTIVA PACIENTULUI (mai util decât XWins/OWins)

public readonly struct MoveRecord               // ← struct = tip valoare, mic, copiat ușor
{
    public readonly int Number;     // 1-based  ← a câta mutare din meci (1, 2, 3...)
    public readonly int Cell;                   // ← căsuța 0..8
    public readonly Mark Mark;                  // ← X sau O
    public readonly bool ByPatient;             // ← a fost a pacientului?

    public MoveRecord(int number, int cell, Mark mark, bool byPatient)
    {
        Number = number;                        // ← câmpurile readonly se pot seta doar aici
        Cell = cell;
        Mark = mark;
        ByPatient = byPatient;
    }
}
```

### Clasa `Match` – câmpuri și proprietăți
```csharp
public sealed class Match
{
    readonly List<MoveRecord> moves = new List<MoveRecord>();   // ← istoricul mutărilor (privat)

    public DifficultyLevel Level { get; }        // ← proprietăți doar-citire: setate o dată în constructor
    public bool PatientStarts { get; }
    public Mark PatientMark { get; }
    public Mark AIMark { get; }
    public Board Board { get; } = new Board();   // ← tabla meciului, creată odată cu meciul
    public IReadOnlyList<MoveRecord> Moves => moves;
    // ← expunem lista ca DOAR-CITIRE: din afară o poți parcurge, dar nu poți adăuga/șterge mutări
```

### Constructorul
```csharp
    public Match(DifficultyLevel level, bool patientStarts)
    {
        Level = level;
        PatientStarts = patientStarts;
        PatientMark = patientStarts ? Mark.X : Mark.O;  // ← regula ta: cine începe e X
        AIMark = PatientMark.Opponent();                // ← AI-ul primește celălalt simbol
    }
```

### Proprietăți de stare
```csharp
    public bool IsOver => Board.IsGameOver;              // ← meciul s-a terminat?

    public bool IsPatientTurn => !IsOver && Board.NextToMove == PatientMark;
    // ← e rândul pacientului dacă jocul nu s-a terminat și urmează simbolul lui

    public MatchOutcome Outcome
    {
        get
        {
            switch (Board.State)
            {
                case BoardState.InProgress: return MatchOutcome.InProgress;
                case BoardState.Draw: return MatchOutcome.Draw;
                case BoardState.XWins: return PatientMark == Mark.X ? MatchOutcome.PatientWin : MatchOutcome.AIWin;
                // ← a câștigat X: dacă pacientul e X → victoria pacientului, altfel a AI-ului
                default: return PatientMark == Mark.O ? MatchOutcome.PatientWin : MatchOutcome.AIWin;
                // ← singurul caz rămas: OWins
            }
        }
    }
```

### Aplicarea unei mutări
```csharp
    public bool TryPlay(int cell)
    {
        if (IsOver || cell < 0 || cell >= Board.CellCount || !Board.IsEmpty(cell)) return false;
        // ← refuză: joc terminat / index invalid / căsuță ocupată. Nu aruncă excepție, doar returnează false.
        var mark = Board.NextToMove;             // ← simbolul celui aflat la rând
        Board.Place(cell, mark);                 // ← pune piesa
        moves.Add(new MoveRecord(moves.Count + 1, cell, mark, mark == PatientMark));
        // ← salvează mutarea: numărul ei (Count+1), căsuța, simbolul, dacă e a pacientului
        return true;
    }

    public void Play(int cell)
    {
        if (!TryPlay(cell)) throw new InvalidOperationException($"Mutare invalidă: {cell} pe {Board}");
        // ← varianta „strictă”: dacă mutarea e invalidă e un bug → excepție cu tabla afișată (ToString)
    }
}
```
**Pe scurt:** `Match` leagă tabla de cei doi jucători: știe cine e pacientul, cine e la rând, cine a câștigat
și ține istoricul (folosit apoi de logger pentru CSV).

---

## DifficultySettings.cs

### Clasa de setări
```csharp
[Serializable]                                  // ← Unity o poate afișa și salva în Inspector
public class DifficultySettings
{
    public float level1PatientStartChance = 1.00f;  // ← `f` = literal float (ca în C++)
    public float level2PatientStartChance = 0.75f;
    public float level3PatientStartChance = 0.60f;
    public float level4PatientStartChance = 0.50f;
    public float level2DefenseSlack = 0.25f;
    public float level3MistakeChance = 0.45f;
    public float level4MistakeChance = 0.20f;
    public int level4MaxPunishingReplies = 1;
    public bool steerTowardAffectedSide = true;
```
Câmpurile sunt `public` (nu proprietăți) pentru că Unity serializează **câmpuri**, nu proprietăți.
Valorile de după `=` sunt cele implicite; în Inspector le vezi și le poți schimba
(Unity transformă `level3MistakeChance` în eticheta „Level 3 Mistake Chance”).

### Alegerea șansei după nivel
```csharp
    public float PatientStartChance(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Level1: return level1PatientStartChance;
            case DifficultyLevel.Level2: return level2PatientStartChance;
            case DifficultyLevel.Level3: return level3PatientStartChance;
            default: return level4PatientStartChance;
        }
    }
}
```

### Coin flip-ul
```csharp
public static class CoinFlip
{
    public static bool PatientStarts(DifficultyLevel level, DifficultySettings settings, Random rng)
    {
        return rng.NextDouble() < settings.PatientStartChance(level);
    }
}
```
`NextDouble()` dă un număr la întâmplare între 0 și 1. Dacă șansa e 0.75, numărul e sub 0.75 în 75% din
cazuri → pacientul începe în 75% din meciuri. Cu 1.00 condiția e mereu adevărată → nivelul 1 = mereu pacientul.

Animația monedei de pe ecran **nu decide nimic**: primește rezultatul calculat aici și doar îl arată.
