# Cum modifici peisajele de mână

Un lucru de știut de la început: **peisajele nu sunt salvate în scenă, sunt construite din cod când apeși
Play** (`Assets/Scripts/Game/World/Environments.cs`). De aceea, în editor, înainte de Play, scena `XSi0_3D`
pare aproape goală: are doar camera, obiectul `XSi0` și obiectele `Decor_...`. Copacii apar abia la rulare.

Ai trei feluri de a modifica ce se vede. Le poți combina.

| Vrei să… | Cel mai simplu |
|---|---|
| adaugi obiecte noi, așezate cu mouse-ul | **A.** le pui în scenă, sub `Decor_...` |
| muți / ștergi un obiect care există deja | **B.** îi schimbi linia din `Environments.cs` |
| afli o poziție bună, vizual | **C.** muți obiectul în timpul Play-ului și copiezi numerele |
| schimbi tabla, personajul, volumele | Inspector, pe obiectul `XSi0` |

---

## A. Adaugi obiecte de mână, în editor

Rulează o dată **TRAVEE → Creează scena X și 0 (3D)**. În Hierarchy apar patru obiecte goale:

- `Decor_All` – ce pui sub el apare în **toate** peisajele;
- `Decor_Forest`, `Decor_Mountain`, `Decor_Beach` – apare doar în peisajul respectiv.

Pași:
1. În fereastra **Project**, deschide `Assets/Art/Nature`. Acolo sunt toate modelele (copaci, pietre, tufe…).
2. Trage un model în fereastra **Scene** sau direct în **Hierarchy**, peste `Decor_Forest` (de exemplu).
   Trebuie să ajungă **copil** al obiectului `Decor_...`; dacă a ajuns afară, trage-l peste el în Hierarchy.
3. Așază-l cu uneltele din Scene: **W** = mutare, **E** = rotire, **R** = scalare. **F** = centrează
   vederea pe obiectul selectat. Valorile exacte se pot scrie în Inspector, la **Transform**.
4. **Ctrl+S** ca să salvezi scena.
5. Apasă Play: obiectul tău apare împreună cu decorul construit din cod.

Repere ca să știi unde pui:
- pacientul stă în **origine** (X = 0, Z = 0), cu ochii la 1,2 m, și privește spre **+Z**;
- **+X** e în dreapta lui, **−X** în stânga, **−Z** în spatele lui; solul e la **Y = 0**;
- tabla e la Z = 0,9; personajul așteaptă la X = ±1,3, Z = 1,7 și merge până în spatele tablei;
- **nu pune nimic** în dreptunghiul X între −3,4 și 3,4, Z între 0,6 și 2,4: pe acolo merge personajul;
- pe plajă, marea începe la Z = 10.

Bine de știut:
- în editor, toate obiectele `Decor_...` sunt vizibile deodată. La Play rămâne doar cel al peisajului ales.
  Dacă te încurcă, debifează temporar căsuța din stânga numelui, în Inspector (la Play se setează singură);
- modelele au în fișier mărimi diferite, deci probabil va trebui să le scalezi (**R**);
- ca să vezi unde vine față de restul decorului, folosește metoda **C** de mai jos pentru poziție, apoi
  pune obiectul definitiv aici;
- obiectele puse așa **se păstrează** când rulezi din nou meniul TRAVEE (scena nu mai e recreată de la zero)
  și ajung și în build.

Poți pune și alte lucruri decât modele: lumini (**GameObject → Light**), forme simple
(**GameObject → 3D Object**), orice.

---

## B. Schimbi ce e construit din cod

Deschide `Assets/Scripts/Game/World/Environments.cs`. Fiecare peisaj e o metodă: `Forest()`, `Mountain()`,
`Beach()`. Un obiect pus de mână în cod arată așa:

```csharp
Prop("tent_detailedOpen", new Vector3(-5.8f, 0, 9.0f), 2.8f, 140);
//    ↑ numele modelului          ↑ X     ↑ Y  ↑ Z     ↑ mărime (m)  ↑ rotire (grade)
```
- **numele** = numele fișierului din `Assets/Art/Nature`, fără `.fbx`;
- **poziția** = metri, cu aceleași repere ca mai sus;
- **mărimea** = cât să aibă cea mai mare dimensiune a obiectului (un copac de 8 m, o tufă de 1 m);
- **rotirea** = în jurul axei verticale.

Ca să **muți** un obiect, schimbi numerele. Ca să-l **ștergi**, ștergi linia (sau pui `//` în fața ei).
Ca să **adaugi** unul, copiezi o linie și îi schimbi numele și numerele.

Obiectele „împrăștiate” vin din linii `Scatter`:
```csharp
Scatter(new[] { "Bush_Common_Flowers", "Bush_Common" }, 34, 4f, 18f, 0.9f, 1.8f, 3.7f);
//      ↑ din ce modele alege                           ↑ câte
//                                                          ↑ între ce distanțe față de pacient (m)
//                                                                    ↑ mărimea, între … și …
//                                                                                ↑ raza zonei de joc ocolite
```
Aici nu muți obiecte unul câte unul: schimbi **câte** sunt, **cât de departe** și **cât de mari**. Pozițiile
sunt „la întâmplare”, dar aceleași la fiecare rulare.

Tot aici, la sfârșitul fiecărei metode, sunt culorile cerului, lumina, ceața și sunetul de fundal.

După ce salvezi fișierul, Unity recompilează singur; apeși Play și vezi rezultatul.

---

## C. Găsești poziția potrivită în timpul Play-ului

1. Apasă **Play**. În Hierarchy, sub `XSi0 → Room3D → Environment_Forest` (sau `_Mountain`, `_Beach`),
   apar toate obiectele peisajului, cu numele modelelor.
2. Treci pe fereastra **Scene** (jocul continuă să ruleze) și selectează un obiect: click pe el, sau pe
   numele lui în Hierarchy.
3. Mută-l, rotește-l, scalează-l (**W / E / R**) până arată bine.
4. Notează valorile din Inspector, de la **Transform** (Position, Rotation Y, Scale).
5. Oprește Play-ul.

**Atenție:** tot ce modifici în timpul Play-ului **se pierde** când oprești. De aceea notezi numerele și apoi:
- le scrii în linia `Prop(...)` din `Environments.cs` (metoda B), dacă obiectul era construit din cod, sau
- le scrii la obiectul tău de sub `Decor_...` (metoda A).

Truc: click dreapta pe componenta **Transform → Copy → Component** în timpul Play-ului, iar după ce
oprești, pe obiectul din `Decor_...`: click dreapta pe Transform → **Paste → Component Values**.

---

## Setările din Inspector (obiectul `XSi0`)

Selectează `XSi0` în Hierarchy (cu Play oprit). Valorile schimbate aici se păstrează în scenă.

| Câmp | Ce face |
|---|---|
| Opponent Placement | pe ce parte stă personajul |
| World Theme | peisajul de la prima pornire (apoi se ține minte cel ales din meniu) |
| Opponent Reach | cât de întins e brațul personajului când atinge căsuța |
| Board Height / Board Distance | înălțimea centrului tablei și distanța ei față de pacient |
| Effects Volume / Ambient Volume | volumul efectelor și al sunetului de fundal |
| Affected Side, Difficulty, Ai Move Delay | setările de terapie și de dificultate |

---

## Adaugi modele sau sunete noi

1. Pune fișierul în `Assets/Art/Nature` (modele `.fbx`), `Assets/Art/Sky` (panorame de cer) sau
   `Assets/Audio/Sfx` și `Assets/Audio/Ambient` (sunete).
2. Rulează **TRAVEE → Creează scena X și 0 (3D)**: biblioteca de asset-uri se reface și include fișierul nou.
3. Modelul poate fi folosit apoi în ambele feluri: tras de mână sub `Decor_...` sau, în cod, cu
   `Prop("NumeleFisierului", ...)`.

Regula de licență rămâne: doar asset-uri gratuite, cu licență care permite folosirea în TRAVEE (ideal CC0),
iar sursa se trece în `CREDITS.md`.

---

## Dacă ceva nu merge

- **Obiectul meu nu apare la Play.** Verifică să fie copil al unui `Decor_...` și că acel `Decor_` e al
  peisajului în care te uiți (sau pune-l sub `Decor_All`).
- **Obiectul apare roz.** Materialul lui nu e pentru URP. Rulează din nou meniul TRAVEE; dacă rămâne roz,
  selectează materialul și alege shader-ul **Universal Render Pipeline → Lit**.
- **Am stricat ceva în cod.** Erorile apar în fereastra **Console**, cu fișierul și linia. Varianta bună e
  mereu în git: `git checkout -- Assets/Scripts/Game/World/Environments.cs` readuce fișierul la ultimul commit.
