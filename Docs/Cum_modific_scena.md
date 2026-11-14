# Cum modifici scena de mână

Scena `Assets/Scenes/XSi0_3D.unity` conține acum, ca obiecte reale, tot ce ține de Unity: camera, tabla,
personajul cu Animator-ul lui, locurile în care așteaptă, cele trei peisaje cu fiecare copac și piatră,
luminile, setările de cer și ceață și interfața (meniul, textele, ecranul de rezultat). Le selectezi în fereastra **Scene** sau în **Hierarchy** și le schimbi
din **Inspector**. Jocul le folosește exact cum le găsește.

În cod au rămas doar lucrurile care **sunt** logică: regulile jocului, AI-ul, mișcarea personajului și ce
fac butoanele (vezi „Ce a rămas în cod” la sfârșit).

---

## Prima dată

1. Deschide scena `Assets/Scenes/XSi0_3D`.
2. Dacă în Hierarchy nu vezi `Tabla`, `Personaj` și `Decor_Forest → Peisaj`, rulează
   **TRAVEE → Scena editabilă → 1. Pune totul în scenă**. Comanda creează doar ce lipsește; ce există deja
   (inclusiv ce ai aranjat tu) rămâne neatins.
3. Fereastra Scene trebuie să fie în **3D**. Proiectul a pornit din șablonul 2D, de aceea se deschidea în
   2D și totul părea plat, văzut din față. Editorul o trece acum singur pe 3D, o dată, când încarcă
   scripturile. Dacă tot pare plat: **TRAVEE → Scena editabilă → Treci fereastra Scene pe 3D**, sau
   dezactivează butonul **2D** din bara de sus a ferestrei Scene.

Navigarea în Scene: click dreapta ținut + **W A S D** = zbori prin scenă; rotița = zoom; **F** = centrează
pe obiectul selectat; **Alt** + click stânga = te rotești în jurul lui.

---

## Ce e în Hierarchy

| Obiect | Ce este | Ce poți schimba |
|---|---|---|
| `Main Camera` | capul pacientului | poziția (înălțimea ochilor), direcția de bază, Field of View |
| `Tabla` | tabla de joc | poziția, rotația, mărimea (Transform) |
| `Personaj` | personajul AI | Animator, setările lui (componenta **Opponent Character**), materialele |
| `Loc_Stanga`, `Loc_Dreapta` | unde așteaptă personajul | poziția |
| `Decor_Forest`, `Decor_Mountain`, `Decor_Beach` | câte un peisaj | tot: obiecte, lumină, cer, ceață, sunet |
| `Decor_All` | obiecte care apar în toate peisajele | ce pui tu în el |
| `Interfata` | meniul, moneda, textele din joc, ecranul de rezultat | poziții, mărimi, culori, fonturi, texte fixe |
| `XSi0` | pornirea jocului și setările de terapie | dificultate, mâna afectată, volume |

### Un peisaj (`Decor_...`)
- **`Peisaj`** – toate obiectele: copaci, tufe, pietre, munți, mare, foc. Un click pe un obiect în Scene îl
  selectează pe el (suportul cu numele modelului, de exemplu `CommonTree_3`), nu peisajul întreg.
  Îl muți cu **W**, îl rotești cu **E**, îl scalezi cu **R**, îl ștergi cu **Delete**, îl dublezi cu **Ctrl+D**.
- **`Soare`** – lumina principală. Rotația ei dă direcția luminii și a umbrelor; culoarea și intensitatea
  sunt în componenta **Light**.
- componenta **Theme Settings** (pe `Decor_...` însuși) – cerul, lumina ambientă, ceața, sunetul de fundal:

| Câmp | Ce face |
|---|---|
| Look → Sky → Zenith / Horizon / Ground | culorile cerului desenat din cod; **Horizon** e și culoarea ceții |
| Look → Sky → Stars, Moon Direction | stelele și luna cerului desenat din cod |
| Look → Sky → Exposure, Rotation, Tint | luminozitatea, rotirea și nuanța fotografiei de cer |
| Look → Ambient Sky / Equator / Ground | lumina care ajunge și în umbră, pe trei culori |
| Look → Fog Start / Fog End | de unde începe și unde devine completă ceața (metri); Fog End = 0 o oprește |
| Sky Panorama | fotografia 360° folosită ca cer (din `Assets/Art/Sky`); goală = cerul desenat din cod |
| Ambient Sound | sunetul de fundal (din `Assets/Audio/Ambient`) |
| Sun | lumina principală a peisajului (de obicei copilul `Soare`) |

  Câmpurile *Sun Color / Sun Intensity / Sun Angles* din Look nu se folosesc cât timp **Sun** e setat:
  atunci contează obiectul `Soare`.

În editor se vede **un singur peisaj** odată. Îl schimbi din
**TRAVEE → Scena editabilă → Arată în editor: Pădure / Munte / Plajă**, care aplică și cerul și ceața lui.
După ce schimbi valori în Theme Settings, rulează din nou aceeași comandă ca să vezi rezultatul în editor
(în joc se aplică oricum la pornire).

### Tabla
În editor e desenată ca un contur (gizmo) cu cele nouă căsuțe; plăcuțele și piesele reale apar la Play.
Poziția = centrul tablei. Rotația `X = -90` înseamnă „vertical, cu fața spre pacient” (tabla e construită
culcată); schimbă **Y** ca s-o întorci stânga-dreapta și **X** ca s-o înclini. **Scale** îi dă mărimea
(1,3 = 62 cm latura). Personajul își calculează singur unde să stea ca s-o atingă.

### Personajul
- **Animator**: selectează `Personaj` și deschide **Window → Animation → Animator**. Vezi stările (Idle,
  Walk, Wave, Think, PlaceA…), poți schimba clipul fiecăreia (câmpul **Motion**) sau viteza lui. Numele
  stărilor trebuie să rămână aceleași: codul le pornește după nume. Controller-ul e
  `Assets/Art/Character/OpponentAnimator.controller` și nu mai e regenerat automat.
- **Opponent Character** (Inspector): pe ce parte stă, înălțimea de bază, mărirea automată până la tablă,
  cât de întins e brațul, viteza de mers, cât face cu mâna, cât stă aprinsă căsuța roșie, unghiul la care
  observă că pacientul se uită la el.
- **Poziția lui din scenă nu contează**: la pornire merge la `Loc_Stanga` sau `Loc_Dreapta`. Mută acele
  obiecte ca să-i schimbi locul.
- **Mărimea**: o dă câmpul **Height** (și mărirea automată), nu Scale-ul din Transform. Pune Height = 0
  dacă vrei să decidă Scale-ul.

### Interfața (`Interfata`)
E un **Canvas** cu patru ecrane, fiecare un obiect: `GameScreen` (textele din timpul meciului și butonul
Meniu), `CoinFlipScreen` (moneda), `ResultScreen` (rezultatul) și `MenuScreen` (meniul).

- În editor e activ doar `MenuScreen`. Ca să aranjezi alt ecran, selectează-l și bifează căsuța din stânga
  numelui, în Inspector; debifeaz-o la loc când termini. La pornire, jocul decide singur care se vede.
- Interfața se editează cel mai ușor în **fereastra Game** sau în Scene cu butonul **2D** apăsat (aici 2D
  chiar ajută) și cu unealta **T** (Rect Tool). Dublu-click pe `Interfata` în Hierarchy o încadrează.
  În vederea 3D, Canvas-ul apare ca un dreptunghi uriaș peste lume: e normal, e desenat „pe ecran”, nu în
  lume. Îl poți ascunde în Scene cu iconița-ochi de lângă numele lui, în Hierarchy.
- Poți schimba orice ține de aspect: poziția și mărimea (Rect Transform), culoarea (Image), fontul, mărimea
  și culoarea textului (Text), textele fixe („X și 0”, „Joacă din nou”…).
- **Textele care se schimbă în joc** (nivelul, al cui e rândul, mesajul de rezultat, „Mâna afectată: …”) sunt
  scrise de cod la rulare; ce vezi în editor sunt exemple. Ca să le schimbi conținutul: `GameController.cs`,
  `Screens.cs` (numele nivelurilor) și scripturile ecranelor.
- **Ce fac butoanele rămâne în cod.** Fiecare ecran are în Inspector o secțiune „Legături spre obiectele din
  scenă”: acolo scrie care obiect e butonul de nivel, care e textul de stare etc. Nu șterge acele obiecte și
  nu goli legăturile; dacă înlocuiești un buton cu altul, trage-l pe cel nou în câmpul respectiv.
- Canvas-ul se scalează după înălțimea ecranului, față de 1920 × 1080 (componenta **Canvas Scaler**).

### Camera
Poziția = ochii pacientului; rotația = direcția în care privește la început. Componenta
**Desktop Head Look** e privirea cu mouse-ul (în VR se scoate). Cât timp această componentă e pe cameră,
jocul nu mai mută camera de la locul ei.

---

## Adaugi obiecte

Trage un model din `Assets/Art/Nature` (fereastra Project) în Scene, apoi, în Hierarchy, peste `Decor_...`
al peisajului dorit – sau peste `Decor_All`, ca să apară în toate trei. Așază-l și salvează cu **Ctrl+S**.

Pune obiectele tale **lângă** `Peisaj` (direct sub `Decor_...`), nu în el: dacă refaci vreodată peisajul
din cod, ce e lângă el rămâne.

Poți adăuga orice obiect Unity: lumini (**GameObject → Light**), forme (**GameObject → 3D Object**),
sisteme de particule, surse de sunet.

Repere:
- pacientul stă în **origine** și privește spre **+Z**; **+X** e în dreapta lui; solul e la **Y = 0**;
- nu pune nimic pe drumul personajului, între locurile lui și spatele tablei;
- pe plajă, marea începe la Z = 10.

Modele sau sunete noi: pune fișierul în `Assets/Art/Nature`, `Assets/Art/Sky` sau `Assets/Audio/...`, apoi
rulează **TRAVEE → Creează scena X și 0 (3D)** (reface lista de asset-uri; scena nu e ștearsă). Regula de
licență rămâne: doar asset-uri gratuite, ideal CC0, cu sursa trecută în `CREDITS.md`.

---

## Comenzile din meniul TRAVEE

| Comandă | Ce face |
|---|---|
| Scena editabilă → 1. Pune totul în scenă | creează ce lipsește; nu atinge ce există |
| Scena editabilă → Arată în editor: … | afișează un peisaj în editor, cu cerul și ceața lui |
| Scena editabilă → Reconstruiește din cod peisajul afișat | șterge `Peisaj` și îl reface din `Environments.cs` (pierzi aranjarea lui) |
| Scena editabilă → Readu setările din cod pentru peisajul afișat | reface Theme Settings și `Soare` cu valorile din cod |
| Scena editabilă → Reconstruiește interfața din cod | șterge `Interfata` și o reface (pierzi aranjarea ei) |
| Scena editabilă → Treci fereastra Scene pe 3D | scoate fereastra Scene din modul 2D |
| Creează scena X și 0 (3D) | reface lista de asset-uri și legăturile; **păstrează** scena |
| Regenerează animațiile personajului | reface stările Animator Controller-ului din fișierele de animație |

Dacă ștergi un obiect din greșeală (`Tabla`, `Personaj`, un `Soare`), comanda **1. Pune totul în scenă**
îl pune la loc cu valorile implicite. Dacă obiectul lipsește și nu rulezi comanda, jocul îl creează singur
la pornire, ca înainte.

---

## Ce a rămas în cod

| Ce | Unde | De ce |
|---|---|---|
| regulile, AI-ul, desfășurarea unui meci | `Scripts/Core`, `GameController.cs` | logică |
| mișcarea personajului, IK-ul, atingerea | `OpponentCharacter.cs` | logică |
| plăcuțele tablei și piesele X / 0 | `Board3DView.cs` | apar și dispar în timpul jocului |
| cerul desenat din cod | `Sky.cs` | folosit doar dacă nu e aleasă o fotografie |
| „rețeta” peisajelor | `Environments.cs` | folosită doar la *Reconstruiește din cod* |
| ce fac butoanele; textele care se schimbă în joc; animația monedei | `Scripts/Game/UI`, `GameController.cs` | logică |
| „rețeta” interfeței | metodele `Create` / `Build` ale ecranelor | folosită doar la *Reconstruiește interfața din cod* |

Pentru schimbări „în masă” (de exemplu de două ori mai mulți copaci), e mai simplu să schimbi numerele din
`Environments.cs` și să rulezi *Reconstruiește din cod peisajul afișat*, decât să pui obiectele unul câte unul.

---

## Dacă ceva nu merge

- **Totul pare plat, văzut din față.** Fereastra Scene e în modul 2D: rulează **TRAVEE → Scena editabilă →
  Treci fereastra Scene pe 3D** sau dezactivează butonul **2D** din bara ei.
- **Un dreptunghi alb uriaș acoperă scena.** E Canvas-ul interfeței; ascunde-l cu iconița-ochi de lângă
  `Interfata`, în Hierarchy (se ascunde doar în editor, nu și în joc).
- **Un buton nu mai face nimic.** I s-a pierdut legătura: selectează ecranul lui și verifică secțiunea
  „Legături spre obiectele din scenă”.
- **Nu pot selecta obiecte în Scene.** Verifică în Hierarchy să nu fie activată iconița cu degetul tăiat
  de lângă numele obiectului (blochează selectarea) și, sus în Scene, să fie aleasă unealta de mutare (**W**),
  nu mâna (**Q**).
- **Am schimbat ceva în timpul Play-ului și a dispărut.** Tot ce modifici în Play se pierde la oprire.
  Modifică având Play-ul oprit.
- **Un obiect apare roz.** Materialul lui nu e pentru URP: selectează materialul și alege shader-ul
  **Universal Render Pipeline → Lit**.
- **Vreau să mă întorc la o variantă bună.** Scena e în git:
  `git checkout -- Assets/Scenes/XSi0_3D.unity` o readuce la ultimul commit.
