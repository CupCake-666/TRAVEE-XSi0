# Jurnalul schimbărilor

Ce s-a schimbat în proiect, de ce și cum a fost verificat. Cele mai noi sunt sus.
Explicațiile de cod, linie cu linie, sunt în `Docs/Explicatii/`.

---

## 13 noiembrie 2026 (a noua rundă) – fereastra Scene în 3D, interfața în scenă

**Fereastra Scene tot în 2D.** Runda trecută o trecusem pe 3D dintr-o comandă rulată fără interfața
editorului, deci setarea nu a ajuns la fereastra deschisă pe ecran. Acum `Editor/SceneView3D.cs` o trece pe 3D
automat, o singură dată, când editorul încarcă scripturile, și există și comanda
**TRAVEE → Scena editabilă → Treci fereastra Scene pe 3D**.

**Interfața e în scenă** (obiectul `Interfata`: Canvas cu `GameScreen`, `CoinFlipScreen`, `ResultScreen`,
`MenuScreen`). Poziții, mărimi, culori, fonturi și texte fixe se schimbă din editor; logica a rămas în cod.

- Cele patru ecrane erau într-un singur fișier (`Screens.cs`), deci nu puteau fi salvate în scenă; fiecare
  are acum fișierul lui. `Screens.cs` mai conține doar numele nivelurilor.
- Fiecare ecran ține **legături serializate** spre obiectele lui (`[SerializeField]`: butoanele, textele) și
  are metoda `Wire()`, care leagă butoanele de logică la pornire. Legăturile făcute din cod (`AddListener`)
  nu se salvează în scenă, de aceea se refac de fiecare dată.
- La pornire, dacă cele patru ecrane există în scenă, jocul le folosește; altfel construiește interfața din
  cod, ca înainte.
- Moneda folosește o imagine salvată ca asset (`Assets/Art/UI/Circle.png`, generată din cod de comanda din
  editor).
- **Cardul de rezultat s-a mutat în dreapta tablei.** De când tabla e la nivelul ochilor, cardul lat de jos
  acoperea rândul de jos și linia câștigătoare.

**Verificare (în Unity, cu editorul închis):** comanda a pus interfața în scenă fără erori și fără scripturi
lipsă; jocul folosește un singur Canvas, cel din scenă; un test a apăsat din cod butoanele din scenă –
„Peisaj” a schimbat peisajul, „Nivel 1” a pornit meciul, meciul s-a jucat până la ecranul de rezultat, iar
„Joacă din nou” a repornit; capturi cu meniul, moneda și rezultatul. **Nu am putut verifica** trecerea
automată a ferestrei Scene pe 3D (are nevoie de editorul deschis pe ecran) și nici click-urile reale cu
mouse-ul pe butoane.

---

## 13 noiembrie 2026 (a opta rundă) – tot ce ține de Unity e în scenă

Observații după prima încercare: peisajele apăreau „într-o lume 2D” și obiectele nu puteau fi selectate unul
câte unul. Cerință: control complet asupra scenei – inclusiv personajul și Animator-ul lui – cu codul rămas
doar pentru ce e logică.

**Cauzele celor două probleme**
- *2D:* proiectul a pornit din șablonul 2D, deci fereastra Scene se deschidea în modul 2D (privire fixă din
  față). Comanda de pregătire o trece acum pe 3D și setează proiectul pe 3D.
- *Selectarea:* fiecare obiect de decor are acum componenta `PropHolder` (`[SelectionBase]`): un click pe
  model selectează obiectul lui, gata de mutat. În plus, componentele mici (`Flicker`, `Tide`, `FlameDance`,
  `Firefly`) erau definite în același fișier cu alt cod și **nu se salvau** în scenă; fiecare are acum
  fișierul ei.

**Ce e acum în scenă** (creat de **TRAVEE → Scena editabilă → 1. Pune totul în scenă**, doar dacă lipsește)

| Obiect | Înainte | Acum |
|---|---|---|
| `Main Camera` | mutată din cod la pornire | așezată în scenă; jocul n-o mai mută |
| `Tabla` | creată din cod | obiect în scenă; Transform-ul îi dă poziția, rotația, mărimea |
| `Personaj` | instanțiat din cod | instanță a modelului, cu Animator și setări în Inspector |
| `Loc_Stanga`, `Loc_Dreapta` | coordonate în cod | obiecte goale pe care le muți |
| `Decor_... → Peisaj` | construit din cod la pornire | obiecte salvate, selectabile individual |
| `Decor_... → Soare` | lumină creată din cod | lumină în scenă |
| `Theme Settings` pe `Decor_...` | valori în cod | cer, ceață, lumină ambientă, sunet de fundal în Inspector |

Regula din cod: **ce există în scenă e folosit ca atare; ce lipsește e creat din cod**, cu valorile implicite.
Deci jocul pornește și dintr-o scenă goală, iar un obiect șters din greșeală nu strică nimic.

**Alte schimbări**
- Animator Controller-ul nu mai e șters și regenerat la fiecare rulare a meniului TRAVEE (s-ar fi pierdut
  orice modificare făcută în fereastra Animator). Se regenerează doar la cerere, păstrând același fișier.
- Setările personajului (parte, înălțime, mărire automată, întinderea brațului, viteza de mers, durata
  salutului, durata căsuței roșii, unghiul privirii) au devenit câmpuri în Inspector.
- Tabla se vede în editor ca un contur (gizmo), ca s-o poți așeza.

**A rămas în cod:** interfața (meniul, moneda, textele, ecranul de rezultat) – e construită la pornire și nu
apare în Hierarchy înainte de Play. Mutarea ei în scenă e un pas separat. La fel plăcuțele tablei și piesele,
care apar și dispar în timpul jocului.

**Verificare (în Unity, cu editorul închis):** comanda a rulat fără erori; în scenă nu există scripturi
lipsă; jocul pornit din această scenă folosește personajul și tabla din scenă, nu construiește nimic din cod
(0 obiecte generate), are o singură lumină direcțională activă și 776 de obiecte de decor în pădure;
atingerea a fost măsurată din nou pe toate cele 9 căsuțe (minimum 1,3 cm față de tablă, deget la 1,5–1,7 cm
la atingere); capturi din cele trei peisaje, față și spate. **Nu am putut verifica** cum arată și cum se
lucrează efectiv în fereastra Scene (selectarea cu mouse-ul, gizmo-ul tablei), pentru că verificarea
automată nu are interfața editorului.

---

## 13 noiembrie 2026 (a șaptea rundă) – peisajele vizibile și editabile în fereastra Scene

Cerință: peisajele să apară efectiv în Scene, cu toate asset-urile, ca să poată fi aranjate de mână și
completate, pentru toate cele trei.

- **`Editor/WorldBaker.cs` (nou)**, meniul **TRAVEE → Peisaje editabile**: construiește cele trei peisaje cu
  același cod ca jocul, dar în editor, și le salvează în scenă sub `Decor_... → Peisaj`. Comenzi: pune
  peisajele în scenă; arată în editor unul dintre ele (cu cerul, ceața și lumina lui); reconstruiește din cod
  peisajul afișat; scoate peisajele din scenă.
- **Jocul folosește peisajul din scenă dacă există** (`Room3D.HasEditableLandscape`); altfel îl construiește
  din cod, ca până acum. Deci nimic nu se schimbă până nu rulezi comanda.
- **Aspectul separat de obiecte**: `EnvironmentBuilder.Look(theme)` întoarce cerul, lumina, ceața și sunetul
  fără să construiască nimic, iar `Room3D.ApplyLook` le aplică (și din editor, pentru previzualizare).
- `WorldFactory.Remove`: ștergere care funcționează și în editor (unde `Destroy` nu e permis).

**Verificare:** tot codul compilează (verificat separat, cu compilatorul .NET). **Nu a fost rulat în
Unity**: editorul era deschis, deci verificarea automată nu a putut porni. Comanda de construire rulează în
editor cod scris pentru joc, deci prima rulare trebuie urmărită în Console.

---

## 13 noiembrie 2026 (a șasea rundă) – decor pus de mână

Cerință: cum pot fi modificate de mână lucruri în scenă (poziții, obiecte noi).

Peisajele sunt construite din cod la rulare, deci în editor nu era nimic de mutat. Am adăugat o cale pentru
lucrul de mână, fără să renunț la construcția din cod:

- **Obiecte `Decor_...` în scenă** (`Decor_All`, `Decor_Forest`, `Decor_Mountain`, `Decor_Beach`): ce pui sub
  ele în editor apare la rulare, în toate peisajele sau doar în cel potrivit (`Room3D.ShowHandPlacedDecor`).
- **Meniul TRAVEE nu mai recreează scena de la zero** dacă ea există: o deschide și îi actualizează doar
  legăturile, deci decorul pus de mână și valorile schimbate în Inspector se păstrează.
- Ghid pas cu pas: `Docs/Cum_modific_scena.md` (cele trei metode: obiecte puse de mână, schimbarea liniilor
  din `Environments.cs`, găsirea pozițiilor în timpul Play-ului).

**Verificare:** codul compilează (verificat separat, cu compilatorul .NET). **Nu a fost rulat în Unity**:
editorul era deschis, deci verificarea automată nu a putut porni. De încercat: rulezi meniul TRAVEE, apar
cele patru obiecte `Decor_...`, pui un model sub unul dintre ele și apeși Play.

---

## 13 noiembrie 2026 (a cincea rundă) – tabla la nivelul ochilor

Cerință: centrul tablei la înălțimea liniei ochilor, iar personajul să ajungă la toate căsuțele, cu IK, și
să arate bine.

- **Tabla: centrul la 1,2 m** (nivelul ochilor unui pacient așezat; de la 0,88 m). Rândurile sunt la
  ~1,40 / 1,20 / 1,00 m.
- **Personajul e mărit automat** (`GrowToReach`): la 1,40 m nu ar ajunge nici la rândul din mijloc. Toate
  măsurile lui cresc proporțional cu scala, deci factorul se calculează exact: ajunge la **1,86 m**. Am ales
  mărirea pentru că celelalte căi au fost deja respinse (podium, obiect sub picioare) sau ar arăta nefiresc
  (brațe lungite). Dacă tabla e coborâtă din Inspector, personajul crește mai puțin sau deloc.
- **Locul în care așteaptă** s-a mutat puțin mai departe (1,3 m lateral, 80 cm în spatele tablei), ca
  personajul mai mare să nu umple marginea imaginii.
- **Atingerea, măsurată din nou** (9 căsuțe × 2 treceri): niciun os al mâinii mai aproape de 1,3 cm de tablă
  pe toată mișcarea; la atingere, cel mai apropiat vârf de deget e la 1,5–1,7 cm de suprafață și la cel mult
  7,1 cm de centrul căsuței.

**De discutat cu terapeutul:** la această înălțime, rândul de sus (1,40 m) e mult peste umărul unui pacient
așezat. Cu mouse-ul nu contează; la atingerea cu mâna în VR, cere ridicarea brațului peste umăr. Înălțimea
rămâne reglabilă (**Board Height**).

**Verificare:** scena recreată în Unity, capturi din cele trei peisaje, măsurătorile de mai sus, build de
Windows de probă.

---

## 13 noiembrie 2026 (a patra rundă) – tabla la înălțime naturală, fără „Cameră”, fără beep

Cerințe, după ce jocul a fost încercat în editor: tabla puțin mai sus, la o înălțime naturală (să nu se stea
cu capul aplecat), cu personajul ridicând mâna ca să ajungă la căsuțele de sus, dar tot atingând, nu trecând
prin tablă; scoaterea peisajului „Cameră”; scoaterea sunetului de la salutul personajului.

- **Tabla: centrul la 0,88 m** (de la 0,75 m). Centrul e acum cu ~18° sub linia privirii unui pacient așezat
  (înainte: 27°), rândul de sus cu ~7°. Rândurile sunt la ~1,08 / 0,88 / 0,68 m. Este cea mai mare înălțime la
  care personajul (care ajunge până la ~1,08–1,10 m cu brațul întins în sus) atinge și rândul de sus fără nimic sub
  picioare. Se reglează în continuare din Inspector (**Board Height**).
- **Personajul întinde mâna în sus** pentru rândul de sus: verificarea „ajunge?” folosește acum brațul și
  degetele întinse complet (`MaxTouchDistance`), cu 5 cm toleranță; **Opponent Reach** implicit e 1.
- **Atingerea, măsurată din nou la noua înălțime** (9 căsuțe × 2 treceri): niciun os al mâinii mai aproape de
  1,5 cm de tablă pe toată mișcarea; la atingere, cel mai apropiat vârf de deget e la 1,5–1,7 cm de suprafață
  și la cel mult 5,4 cm de centrul căsuței, pe toate rândurile.
- **Peisajul „Cameră” a fost scos** din meniu: rămân Pădure, Munte noaptea și Plajă. Decorul simplu mai există
  doar ca rezervă internă, dacă lipsește biblioteca de asset-uri.
- **Sunetul de la salut a fost scos** (fișierul `pluck_002` a fost șters din proiect). Nu am pus altul în loc:
  nu pot asculta sunetele, deci n-aș fi putut alege unul „mai potrivit” în cunoștință de cauză.
- **Textele din joc** (nivel, „Tu: X / AI: 0”, rândul cui e) au acum un card semi-opac în spate: în captura
  din editor nu se citeau peste peisajul de noapte. Indiciul de control s-a mutat în stânga-jos, ca să nu se
  suprapună cu tabla.

**Verificare:** scena recreată în Unity; capturi din cele trei peisaje; măsurătorile de atingere de mai sus;
build de Windows de probă. Cardurile de text nu apar în capturile automate (acestea nu includ interfața),
deci aspectul lor trebuie văzut în editor.

---

## 13 noiembrie 2026 (a treia rundă) – tabla mai jos, atingere corectă, 360°, detalii

Cerințe: jocul va fi în VR, deci pacientul se poate întoarce 360° și trebuie să poată vedea toată harta;
hărțile mai înfrumusețate; tabla la o înălțime accesibilă, eventual mai jos, ca personajul să nu mai aibă
nevoie de nimic sub picioare; mâna personajului să atingă căsuța, nu să treacă prin ea; alte aplauze (cele
dinainte aveau zgomot de fond).

### Tabla, mai jos
Centrul tablei a coborât de la 1,12 m la **0,75 m**. Rândurile sunt acum la ~0,95 / 0,75 / 0,55 m. Pentru un
pacient așezat (umărul la ~1 m), niciun rând nu mai cere ridicarea brațului peste umăr; înainte, rândul de
sus era la 1,32 m. La această înălțime personajul ajunge de pe sol la toate căsuțele, deci **buturuga și
stânca de sub el au dispărut** (codul lor a rămas ca rezervă, pentru cazul în care tabla e ridicată).
Înălțimea și distanța sunt acum în Inspector: **Board Height** și **Board Distance**.

De reținut: distanța a rămas 0,9 m, bună pentru mouse. Pentru atingerea cu mâna în VR trebuie adusă la
lungimea brațului pacientului (orientativ 0,5–0,65 m). Valorile sunt puncte de pornire rezonabile, nu
recomandări clinice: le stabilește terapeutul.

Privirea de bază a camerei e acum la jumătatea dintre orizont și tablă, ca pe ecran să încapă și tabla, și
peisajul cu personajul.

### Mâna atinge, nu trece prin tablă
Am măsurat, pe toată mișcarea, distanța fiecărui os al mâinii față de spatele tablei. Observația a fost
corectă: la una dintre cele două animații, mâna trecea cu până la **25 cm** prin tablă, la începutul și la
sfârșitul mișcării (când IK-ul avea pondere mică și conducea animația).

Ce s-a schimbat în `OpponentCharacter.cs`:
- mâna e condusă de IK pe **toată** durata animației; ținta ei alunecă de lângă corp până la căsuță și înapoi;
- o **limită de plan**: niciun os al mâinii (încheietură + 15 falange) nu are voie mai aproape de 1,5 cm de
  tablă; dacă ajunge, mâna e trasă înapoi în același cadru;
- mâna se oprește intenționat mai devreme, iar ultimii centimetri îi face corecția fină, care se oprește la
  primul deget ajuns.

Rezultat, pe toate cele 9 căsuțe × 2 treceri: distanța minimă a oricărui os este +1,5 cm (niciodată prin
tablă), iar la atingere un deget e la 1,6–2,8 cm de suprafață (măsurat la os; grosimea degetului acoperă
diferența). La animația cu palma deschisă, pe rândul de sus atinge uneori cu inelarul, nu cu arătătorul.

### Privire 360°
Cu click dreapta + mouse te poți întoarce acum complet (înainte: ±75°), iar sus-jos până la 70°.

### Detalii în peisaje (de jur împrejur)
- **Pădure:** iaz cu nuferi, pietre cu mușchi și sălcii (în spate-stânga); pâlc de mesteceni (în
  spate-dreapta); cărare de pietre spre tabără; tufe cu fructe; petale; flacără la foc.
- **Munte:** brazi și stânci înzăpezite la distanță; cerc de pietre și flacără la foc; licurici.
- **Plajă:** loc de foc cu bușteni; a doua canoe, pe apă; în spate, dealurile verzi ale insulei, stânci cu
  mușchi, iarbă de dună.

Modelele noi sunt tot din Quaternius Ultimate Nature Pack (deja descărcat, CC0). Flăcările și licuricii sunt
generați din cod, cu un material „Unlit” nou (`Glow.mat`).

### Aplauze
Înlocuite cu o înregistrare cu **o singură persoană** care aplaudă (BigSoundBank nr. 1767, CC0, 16 s),
descrisă de autor ca fiind ușor de pus în buclă. Se potrivește și cu faptul că aplaudă un singur personaj.
Se opresc când pleci de pe ecranul de rezultat. Nu le-am putut asculta.

### Verificare
Scena recreată în Unity fără erori (93 de modele, 20 de sunete); capturi din fiecare peisaj în patru
direcții (față, dreapta, spate, stânga); măsurătorile de atingere de mai sus; build de Windows de probă.

---

## 13 noiembrie 2026 (a doua rundă) – asset-uri mai bune, fără podium, sunete revizuite

După ce a văzut prima variantă, cerințele au fost: asset-uri de calitate mai bună (cu aceeași condiție de
licență) și hărțile refăcute cu ele; fără podiumul de lemn; un sunet de valuri mai bun; un sunet de aplauze.

### Peisajele, refăcute

| Ce | Înainte | Acum |
|---|---|---|
| Copaci, tufe, plante, pietre | Kenney Nature Kit (forme simple, culori uniforme) | **Quaternius Stylized Nature MegaKit** (58 de modele, scoarță și frunze texturate) |
| Palmieri, buturugi, bușteni | Kenney | **Quaternius Ultimate Nature Pack** (8 modele) |
| Cort, foc de tabără, canoe | Kenney | Kenney (au rămas; pachetele noi nu au așa ceva) |
| Cerul | desenat din cod (gradient, stele, lună) | **fotografii 360° de pe Poly Haven**: nori de zi (pădure, plajă), Calea Lactee (munte). Cerul din cod a rămas ca rezervă. |
| Munții și insulele | conuri simple | mesh generat din 5 inele, cu creste neregulate și linie a zăpezii neregulată |
| Solul | o culoare uniformă | textură de pete în două nuanțe, generată din cod |
| Marea | o placă albastră cu spumă | în plus, o fâșie de apă mică, mai deschisă, lângă mal |

Toate sunt CC0. Am ales MegaKit-ul pentru că are stilul cel mai apropiat de pachetul „Fantasy Forest
Environment” folosit în alte jocuri TRAVEE (stilizat, cu texturi pictate). Am folosit varianta gratuită
(„Standard”, 68 de modele din 116); variantele plătite nu sunt necesare.

Modelele Quaternius vin fără materiale pentru URP, așa că `Editor/NatureImport.cs` (nou) le creează: frunzele
sunt decupate după transparența texturii și se văd din ambele părți. Copacii „Twisted” din pachet au frunze
roșii; în pădure am lăsat doar câțiva, ca accent.

### Fără podium

Personajul are brațe de 30 cm și umărul la 73 cm, deci de pe sol ajunge doar la rândul de jos al tablei.
Dintre variantele propuse (scânteie din deget, personaj mai înalt, doar întinde mâna, obiect natural) a fost
aleasă cea cu **obiect natural**: urcă pe o buturugă cu mușchi în pădure și pe o lespede de stâncă la munte și
pe plajă. Logica e aceeași ca la podium (`Room3D.FloorHeight`); s-a schimbat doar obiectul
(`EnvironmentBuilder.BuildStand`), care se reface la fiecare schimbare de peisaj. Podiumul de lemn mai apare
doar în peisajul „Cameră”.

### Sunetele

- **Aplauze** (nou): când personajul aplaudă victoria pacientului. Înregistrare de 9 secunde, BigSoundBank, CC0.
- **Valuri**: înlocuite cu o înregistrare reală de mare, de 58 de secunde (BigSoundBank, CC0), în locul celei
  de 9 secunde.
- **Reluarea ambianței fără „cusătură”**: înregistrările nu sunt bucle perfecte, așa că sfârșitul fiecăreia e
  încrucișat cu începutul ei, pe două surse audio. Se aplică la toate cele trei ambianțe.
- Restul efectelor au rămas (au fost apreciate ca potrivite).

**Ce nu am putut verifica:** tot nu pot asculta sunetele; aplauzele și valurile au fost alese după descriere
și durată.

### Verificare

Import și recreare a scenei în Unity fără erori (72 de modele, 20 de sunete, 2 panorame de cer); capturi
automate din fiecare peisaj, din față, din lateral și cu personajul urcat pe buturugă/stâncă atingând rândul
de sus; build de Windows de probă pornit fără erori în jurnal.

---

## 13 noiembrie 2026 – glow-ul AI-ului, peisaje, sunete

Cerințele au venit după discuția cu profesorul: (1) căsuța aleasă de AI să se aprindă roșu și mâna
personajului să o atingă efectiv; (2) o lume mai frumoasă, cu mai multe peisaje, din asset-uri gratuite;
(3) sunete. Asset-urile trebuie să aibă o licență care să nu împiedice folosirea în TRAVEE.

### 1. Căsuța aleasă de AI și atingerea ei

**Ce vede pacientul acum:** personajul vine în spatele tablei → căsuța pe care o va alege pulsează roșu
0,7 secunde → întinde mâna și o atinge → apare piesa și glow-ul se stinge.

| Schimbare | Fișier |
|---|---|
| `SetAITarget(cell)`: glow roșu care pulsează (culoare și transparență); se stinge singur când apare piesa | `Board3DView.cs` |
| `CellBackPosition(cell)`: punctul de pe spatele tablei pe care îl atinge personajul | `Board3DView.cs` |
| `MeasureArm`: măsoară brațul, mâna și poziția umărului pe scheletul modelului | `OpponentCharacter.cs` |
| `StandPointFor`: calculează, pentru fiecare căsuță, unde trebuie să stea ca să ajungă la ea | `OpponentCharacter.cs` |
| `LateUpdate`: corecție fină care aduce vârful degetului exact pe căsuță | `OpponentCharacter.cs` |
| `BuildStep`, `FloorHeight`: podiumul din spatele tablei | `Room3D.cs` |
| câmp nou în Inspector: **Opponent Reach** (0,5–1; implicit 0,95) | `TicTacToeBootstrap.cs` |

**De ce a fost nevoie de un podium.** Am măsurat scheletul personajului: brațul are 30 cm, iar umărul e la
73 cm de sol. Rândul de sus al tablei e la 1,32 m, deci de pe sol nu poate fi atins, oricât de aproape ar
sta. Soluția: un podium de lemn de 39 cm în spatele tablei, cu câte o treaptă pe laterale, pe care
personajul urcă lin. Înălțimea e calculată din măsurători; un personaj mai înalt nu primește podium.
Alternativele ar fi fost un personaj mai mare (schimbă aspectul deja aprobat) sau o tablă mai jos (schimbă
poziția gândită pentru pacient).

**Verificare:** rulare automată în Unity, cu o cameră laterală și cu pozițiile oaselor scrise în log, pentru
câte o căsuță din fiecare rând. Vârful degetului arătător a ajuns exact în dreptul căsuței (eroare sub
1 mm pe lățime și înălțime), la 1,5 cm de suprafața tablei. Capul personajului nu mai intră în tablă
(la prima încercare intra, la rândul de jos; de aceea există distanța minimă calculată din mărimea modelului).

### 2. Peisajele

Patru variante, alese cu butonul **Peisaj** din meniu (alegerea se ține minte): **Pădure**,
**Munte, noaptea**, **Plajă** și **Cameră** (scena simplă de la început).

| Schimbare | Fișier |
|---|---|
| biblioteca de modele și sunete (ScriptableObject) | `GameAssets.cs` (nou) |
| construirea celor patru peisaje; munți din cod; focul care pâlpâie; mareea | `World/Environments.cs` (nou) |
| cerul desenat din cod: gradient, 1500 de stele, lună | `World/Sky.cs` (nou) |
| `SetTheme`: schimbă decorul, lumina, ceața, cerul și ambianța | `World/Room3D.cs` |
| `Cone`: mesh de con neregulat (munți, insule) | `World/WorldFactory.cs` |
| butonul „Peisaj” și evenimentul lui | `UI/Screens.cs`, `TicTacToeBootstrap.cs` |
| adună modelele și sunetele; creează materialul de cer; salvează scena cu ceață | `Editor/SceneSetup.cs` |

Modelele sunt din **Kenney Nature Kit 2.1** (53 de fișiere din cele 330 ale pachetului, 1,9 MB), licență
CC0. Am ales acest pachet pentru că e gratuit, e în domeniul public (fără nicio restricție pentru TRAVEE)
și are atât copaci de pădure, cât și palmieri, stânci, corturi și foc de tabără, deci acoperă toate cele
trei peisaje cu același stil. Pachetul „Fantasy Forest Environment” de pe Asset Store, folosit în alte
jocuri TRAVEE, nu a fost folosit (nu e gratuit).

Cerul, munții, insulele și marea nu sunt asset-uri: sunt generate din cod.

**Verificare:** capturi automate din fiecare peisaj (în față, spre stânga și în sus), făcute în Unity.

### 3. Sunetele

| Schimbare | Fișier |
|---|---|
| sistemul de sunet: efecte 2D, efecte 3D (din direcția personajului), ambianță cu trecere lină | `Audio/Sfx.cs` (nou) |
| click la toate butoanele | `UI/UIFactory.cs` |
| moneda (la fiecare întoarcere și la final) | `UI/Screens.cs` |
| piesa pacientului / piesa AI-ului; victorie / remiză / înfrângere | `GameController.cs` |
| hover pe căsuță | `World/Board3DView.cs` |
| pașii, salutul, aprinderea căsuței AI-ului | `Characters/OpponentCharacter.cs` |
| ambianța fiecărui peisaj (păsări, greieri, valuri) | `World/Room3D.cs` |
| volume în Inspector: **Effects Volume**, **Ambient Volume** | `TicTacToeBootstrap.cs` |

Toate cele 19 fișiere audio sunt CC0 (Kenney și OpenGameArt); sursele exacte sunt în `CREDITS.md`.

**Ce nu am putut verifica:** nu pot asculta sunetele. Le-am ales după nume și durată, iar codul a rulat
fără erori cu ele, dar potrivirea lor (mai ales cele trei jingle-uri de final) trebuie ascultată. Se
schimbă ușor: tabelul `Files` din `Sfx.cs`.

### 4. Corecturi pentru build

- **Build-ul arăta meniul, moneda și rezultatul suprapuse, fără scenă 3D.** Cauza: materialul implicit al
  pipeline-ului URP există doar în editor. Acum obiectele pornesc de la `Assets/Art/Materials/WorldLit.mat`.
- **Scena salvată nu primea biblioteca de asset-uri** (rămânea „None”, deci fără peisaje). Cauza: crearea
  unei scene noi descarcă din memorie obiectul abia creat. Acum e reîncărcat de pe disc înainte de a fi legat.
- **Ceața** e activată și în scena salvată, ca shaderele ei să nu fie scoase din build.

**Verificare:** am făcut un build de Windows de probă (în afara proiectului) și l-am pornit: pornește fără
nicio eroare în jurnal și ajunge la meniu. Nu l-am privit pe ecran, deci aspectul din build (ceața, cerul)
nu e confirmat vizual.

### Ce trebuie făcut în Unity

Nimic obligatoriu: scena `XSi0_3D` a fost deja recreată. Dacă adaugi modele în `Assets/Art/Nature` sau
sunete în `Assets/Audio`, rulează din nou **TRAVEE → Creează scena X și 0 (3D)**.
