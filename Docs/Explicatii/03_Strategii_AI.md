# 03 – Strategiile AI (IAIStrategy, GentleAI, HelperAI, BalancedAI, ChallengerAI)

Toate fișierele din `Assets/Scripts/Core/AI/` în afară de `Search.cs`.

---

## IAIStrategy.cs

### Interfața
```csharp
public interface IAIStrategy
{
    DifficultyLevel Level { get; }              // ← fiecare strategie spune ce nivel e
    void OnNewGame();                           // ← apelat la începutul fiecărui meci
    int ChooseMove(Board board, Mark aiMark);   // ← „AI, ești `aiMark`, unde muți pe tabla asta?” → index 0..8
}
```
O **interfață** e ca o clasă C++ cu doar funcții virtuale pure (`virtual ... = 0`). Fiecare nivel e o clasă
care o implementează (`: IAIStrategy`). `GameController` lucrează doar cu `IAIStrategy` și nu știe ce nivel
are în față – asta e **Strategy pattern**. Ca să adaugi un nivel 5, scrii o clasă nouă și nu modifici nimic altceva.

### Fabrica
```csharp
public static class AIFactory
{
    public static IAIStrategy Create(DifficultyLevel level, DifficultySettings settings, AffectedSide side, Random rng)
    {
        switch (level)
        {
            case DifficultyLevel.Level1: return new GentleAI(settings, side, rng);
            case DifficultyLevel.Level2: return new HelperAI(settings, side, rng);
            case DifficultyLevel.Level3: return new BalancedAI(settings, rng);
            default: return new ChallengerAI(settings, rng);
        }
    }
}
```
Un singur loc care știe ce clasă corespunde fiecărui nivel. Primește din exterior setările, partea afectată și
generatorul `rng` (același pentru tot jocul → cu un seed fix, totul e reproductibil).

### Utilitare comune
```csharp
static class AIUtil                              // ← fără `public` = internal: vizibilă doar în Core
{
    public static T Pick<T>(IList<T> items, Random rng) => items[rng.Next(items.Count)];
    // ← funcție GENERICĂ (ca template<typename T>): alege un element la întâmplare din listă

    public static int HealthySideScore(int cell, AffectedSide side)
    {
        int col = Board.Column(cell);
        if (col == 1) return 0;                  // ← coloana din mijloc: neutră
        bool isLeft = col == 0;                  // ← coloana 0 = stânga pacientului
        bool onAffected = side == AffectedSide.Left ? isLeft : !isLeft;
        // ← dacă mâna stângă e afectată, stânga e partea afectată; altfel dreapta
        return onAffected ? -1 : 1;              // ← -1 = partea afectată, +1 = partea sănătoasă
    }

    public static bool IsCorner(int cell) => cell == 0 || cell == 2 || cell == 6 || cell == 8;
}
```

---

## GentleAI.cs – Nivel 1 (Foarte ușor)

```csharp
public sealed class GentleAI : IAIStrategy
{
    const double Tolerance = 0.02;              // ← mutările la cel mult 2% de cea mai bună sunt considerate „la fel de bune”

    readonly DifficultySettings settings;
    readonly AffectedSide side;
    readonly Random rng;

    public GentleAI(DifficultySettings settings, AffectedSide side, Random rng)
    {
        this.settings = settings;               // ← `this.` deosebește câmpul de parametrul cu același nume
        this.side = side;
        this.rng = rng;
    }

    public DifficultyLevel Level => DifficultyLevel.Level1;

    public void OnNewGame() { }                 // ← n-are stare internă, deci nu are ce reseta
```

### `ChooseMove`
```csharp
    public int ChooseMove(Board board, Mark aiMark)
    {
        var moves = board.EmptyCells();         // ← toate căsuțele libere
        var values = new double[moves.Count];   // ← aici punem „cât ajută” fiecare mutare
        double best = double.NegativeInfinity;
        for (int i = 0; i < moves.Count; i++)
        {
            values[i] = Search.HelpValueAfter(board, moves[i], aiMark);  // ← șansa pacientului dacă AI-ul pune aici
            best = Math.Max(best, values[i]);   // ← cea mai mare șansă găsită
        }

        var candidates = new List<int>();
        for (int i = 0; i < moves.Count; i++)
            if (values[i] >= best - Tolerance) candidates.Add(moves[i]);
        // ← păstrăm mutările aproape la fel de bune ca cea mai bună (ca jocul să nu fie identic de fiecare dată)

        if (settings.steerTowardAffectedSide)
            candidates = KeepHealthiestSide(candidates);
        // ← dacă opțiunea e activă, dintre ele le păstrăm pe cele de pe partea sănătoasă

        return AIUtil.Pick(candidates, rng);    // ← alegem una la întâmplare
    }
```

### `KeepHealthiestSide`
```csharp
    List<int> KeepHealthiestSide(List<int> candidates)
    {
        int bestScore = int.MinValue;
        foreach (int c in candidates) bestScore = Math.Max(bestScore, AIUtil.HealthySideScore(c, side));
        // ← cel mai bun scor de „parte sănătoasă” dintre candidați (+1 dacă există vreunul pe partea sănătoasă)
        return candidates.FindAll(c => AIUtil.HealthySideScore(c, side) == bestScore);
        // ← FindAll + lambda = filtrare (ca std::copy_if): păstrăm doar candidații cu acel scor
    }
}
```
**Efectul:** AI-ul ocupă partea sănătoasă și lasă căsuțele de pe partea afectată libere → pacientul e „împins”
să întindă mâna afectată. Ordinea contează: întâi „ajută pacientul”, abia apoi „partea”, deci preferința de
parte nu scade niciodată șansele pacientului.

---

## HelperAI.cs – Nivel 2 (Ușor)

Constructorul, câmpurile, `Level` și `OnNewGame` sunt identice ca structură cu `GentleAI`.
În plus are `const double Epsilon = 1e-6;` – o valoare foarte mică folosită la compararea numerelor reale
(două valori `double` „egale” pot diferi la a 15-a zecimală).

### `ChooseMove`
```csharp
    public int ChooseMove(Board board, Mark aiMark)
    {
        var moves = board.EmptyCells();
        var values = new double[moves.Count];
        double best = double.NegativeInfinity;
        for (int i = 0; i < moves.Count; i++)
        {
            values[i] = Search.HelpValueAfter(board, moves[i], aiMark);   // ← la fel ca la nivelul 1
            best = Math.Max(best, values[i]);
        }

        double threshold = best - settings.level2DefenseSlack;
        // ← pragul: acceptăm mutări cu până la 0.25 mai slabe decât cea mai ajutătoare
        threshold = best > 0 ? Math.Max(threshold, Epsilon) : best - Epsilon;
        // ← dacă pacientul încă poate câștiga (best > 0), pragul nu coboară sub ~0 → îi lăsăm mereu o șansă.
        //   dacă nu mai poate câștiga oricum (best ≤ 0), acceptăm doar cea mai bună mutare.

        var candidates = new List<int>();
        for (int i = 0; i < moves.Count; i++)
            if (values[i] >= threshold) candidates.Add(moves[i]);   // ← mutările „permise”

        double bestPlausibility = double.NegativeInfinity;
        var chosen = new List<int>();
        foreach (int cell in candidates)
        {
            double p = Plausibility(board, cell, aiMark);          // ← cât de „inteligentă” pare mutarea
            if (p > bestPlausibility + Epsilon)                    // ← strict mai bună → o luăm de la zero
            {
                bestPlausibility = p;
                chosen.Clear();
                chosen.Add(cell);
            }
            else if (Math.Abs(p - bestPlausibility) <= Epsilon)    // ← la egalitate → o adăugăm
            {
                chosen.Add(cell);
            }
        }

        return AIUtil.Pick(chosen, rng);        // ← dintre cele mai „inteligente”, una la întâmplare
    }
```

### `Plausibility` – cât de inteligentă „pare” o mutare
```csharp
    double Plausibility(Board board, int cell, Mark aiMark)
    {
        double score = 0;
        if (board.IsWinningMove(cell, aiMark.Opponent())) score += 4;
        // ← dacă PACIENTUL ar câștiga punând aici, înseamnă că mutarea AI-ului îl blochează → +4
        if (CreatesOpenTwo(board, cell, aiMark)) score += 1;     // ← AI-ul își face o pereche → +1
        if (cell == 4) score += 0.6;                             // ← centrul e poziția cea mai „serioasă”
        else if (AIUtil.IsCorner(cell)) score += 0.3;            // ← colțurile, pe locul doi
        if (settings.steerTowardAffectedSide) score += 0.2 * AIUtil.HealthySideScore(cell, side);
        // ← mică preferință pentru partea sănătoasă (±0.2)
        return score;
    }
```

### `CreatesOpenTwo` – își face AI-ul o „amenințare”?
```csharp
    static bool CreatesOpenTwo(Board board, int cell, Mark mark)
    {
        foreach (var line in Board.Lines)
        {
            if (Array.IndexOf(line, cell) < 0) continue;   // ← linia nu conține căsuța asta → sărim
            int own = 0, empty = 0;
            foreach (int c in line)
            {
                if (c == cell) continue;                    // ← nu numărăm căsuța în care tocmai punem
                if (board[c] == mark) own++;                // ← piese proprii pe linie
                else if (board[c] == Mark.None) empty++;    // ← căsuțe libere pe linie
            }
            if (own == 1 && empty == 1) return true;        // ← după mutare: 2 piese proprii + 1 liberă = pereche deschisă
        }
        return false;
    }
```
**Pe scurt:** nivelul 2 alege dintre mutările care încă îi lasă pacientului șanse bune pe cea care *arată*
cel mai mult a apărare. Pacientul simte că trebuie să se străduiască, dar penalizarea din `HelpValue`
împiedică orice victorie a AI-ului.

---

## BalancedAI.cs – Nivel 3 (Mediu)

```csharp
public sealed class BalancedAI : IAIStrategy
{
    readonly DifficultySettings settings;
    readonly Random rng;

    public BalancedAI(DifficultySettings settings, Random rng) { this.settings = settings; this.rng = rng; }
    public DifficultyLevel Level => DifficultyLevel.Level3;
    public void OnNewGame() { }

    public int ChooseMove(Board board, Mark aiMark)
    {
        var moves = board.EmptyCells();
        var scores = new int[moves.Count];
        int bestSign = int.MinValue;
        for (int i = 0; i < moves.Count; i++)
        {
            scores[i] = Search.ScoreAfter(board, moves[i], aiMark);   // ← rezultatul cu joc perfect după mutare
            bestSign = Math.Max(bestSign, Math.Sign(scores[i]));
            // ← Math.Sign dă -1, 0 sau +1 (pierdere / remiză / câștig). Păstrăm cel mai bun rezultat posibil.
        }

        var optimal = new List<int>();
        var mistakes = new List<int>();
        for (int i = 0; i < moves.Count; i++)
            (Math.Sign(scores[i]) == bestSign ? optimal : mistakes).Add(moves[i]);
        // ← sintaxă compactă: ternarul alege LISTA, apoi .Add pune mutarea în ea.
        //   Optimă = duce la același rezultat ca cea mai bună; altfel e o greșeală.

        if (mistakes.Count > 0 && rng.NextDouble() < settings.level3MistakeChance)
            return AIUtil.Pick(mistakes, rng);  // ← cu 45% șansă: o greșeală oarecare (dacă există vreuna)

        return AIUtil.Pick(optimal, rng);       // ← altfel: o mutare optimă la întâmplare
    }
}
```
Probabilitatea e **constantă**, cum ai cerut. Valoarea 0.45 a fost reglată prin simulare astfel încât un pacient
„începător” (câștigă dacă poate, blochează dacă trebuie) să câștige ~67% din meciuri.

---

## ChallengerAI.cs – Nivel 4 (Provocare)

### Câmpuri și resetare
```csharp
public sealed class ChallengerAI : IAIStrategy
{
    readonly DifficultySettings settings;
    readonly Random rng;
    bool mistakeMade;                           // ← a făcut deja greșeala în meciul curent? (singura stare internă)

    public ChallengerAI(DifficultySettings settings, Random rng) { this.settings = settings; this.rng = rng; }
    public DifficultyLevel Level => DifficultyLevel.Level4;
    public bool MistakeMade => mistakeMade;     // ← expus pentru eventuală logare
    public void OnNewGame() => mistakeMade = false;   // ← meci nou → are din nou „dreptul” la o greșeală
```

### `ChooseMove`
```csharp
    public int ChooseMove(Board board, Mark aiMark)
    {
        var patientMark = aiMark.Opponent();
        var moves = board.EmptyCells();
        var scores = new int[moves.Count];
        int bestScore = int.MinValue;
        for (int i = 0; i < moves.Count; i++)
        {
            scores[i] = Search.ScoreAfter(board, moves[i], aiMark);   // ← scorul exact (nu doar semnul)
            bestScore = Math.Max(bestScore, scores[i]);
        }

        // PASUL 1: poate o greșeală subtilă?
        if (!mistakeMade && rng.NextDouble() < settings.level4MistakeChance)
        {                                       // ← doar dacă n-a greșit încă și „iese” probabilitatea de 20%
            var subtle = FindSubtleMistakes(board, moves, scores, aiMark, patientMark);
            if (subtle.Count > 0)               // ← există o greșeală potrivită?
            {
                mistakeMade = true;             // ← o singură greșeală pe meci
                return AIUtil.Pick(subtle, rng);
            }
        }                                       // ← dacă nu există, joacă normal (nu face o greșeală grosolană)

        // PASUL 2: dacă poate câștiga, câștigă cât mai repede
        if (bestScore > 0)
        {
            var winning = new List<int>();
            for (int i = 0; i < moves.Count; i++)
                if (scores[i] == bestScore) winning.Add(moves[i]);    // ← scor maxim exact = victoria cea mai rapidă
            return AIUtil.Pick(winning, rng);
        }

        // PASUL 3: altfel, mutarea optimă cu cele mai multe capcane
        int bestSign = Math.Sign(bestScore);    // ← 0 (remiză) sau -1 (pierde oricum)
        int bestTraps = -1;
        var chosen = new List<int>();
        for (int i = 0; i < moves.Count; i++)
        {
            if (Math.Sign(scores[i]) != bestSign) continue;   // ← doar mutările cu rezultat optim
            board.Place(moves[i], aiMark);                     // ← încearcă mutarea
            int traps = Search.CountLosingReplies(board, patientMark);
            // ← câte răspunsuri ale pacientului ar duce la pierderea lui = câte capcane întinde mutarea
            board.Clear(moves[i]);

            if (traps > bestTraps)              // ← mai multe capcane decât tot ce am văzut → resetăm lista
            {
                bestTraps = traps;
                chosen.Clear();
            }
            if (traps == bestTraps) chosen.Add(moves[i]);     // ← adăugăm (și când tocmai am resetat)
        }
        return AIUtil.Pick(chosen, rng);
    }
```

### `FindSubtleMistakes` – ce înseamnă o greșeală „subtilă”
```csharp
    List<int> FindSubtleMistakes(Board board, List<int> moves, int[] scores, Mark aiMark, Mark patientMark)
    {
        var result = new List<int>();
        int fewestPunishers = int.MaxValue;
        for (int i = 0; i < moves.Count; i++)
        {
            if (scores[i] >= 0) continue;       // ← doar mutări care PIERD cu joc perfect (altfel n-ar fi greșeli)

            board.Place(moves[i], aiMark);
            bool obvious = Search.HasImmediateWin(board, patientMark);
            // ← pacientul ar câștiga chiar la mutarea următoare? → greșeală prea evidentă
            int punishers = Search.CountWinningReplies(board, patientMark);
            // ← câte răspunsuri ale pacientului câștigă forțat
            board.Clear(moves[i]);

            if (obvious || punishers > settings.level4MaxPunishingReplies) continue;
            // ← respinsă: prea evidentă sau prea ușor de pedepsit (implicit: maxim 1 răspuns câștigător)
            if (punishers < fewestPunishers)    // ← păstrăm greșelile cele mai greu de pedepsit
            {
                fewestPunishers = punishers;
                result.Clear();
            }
            if (punishers == fewestPunishers) result.Add(moves[i]);
        }
        return result;
    }
}
```
**Pe scurt:** nivelul 4 e un jucător perfect care întinde capcane. Ca victoria să fie totuși posibilă, are voie
la o singură greșeală pe meci, și doar una pe care o poți pedepsi printr-un singur răspuns corect.
