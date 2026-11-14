# 08 – Personajul AI: OpponentAnimations, OpponentCharacter

> **Actualizare:** personajul poate fi un obiect din scenă (`Personaj`), pornit cu `Attach(...)`; constantele `WalkSpeed`, `WaveDuration`, `TargetPreview`, `GazeAngle` au devenit câmpuri în Inspector (`walkSpeed`, `waveDuration`, `targetPreview`, `gazeAngle`), alături de `placement`, `height`, `growToReachBoard` și `reach`. Vezi `Docs/Cum_modific_scena.md`.

Fișierele din `Assets/Scripts/Game/Characters/`. Recitește înainte secțiunea 4.4 din `00_Introducere`
(Animator, Humanoid, IK). Personajul implementează `IOpponentPresenter` (vezi `04`).

Ce face personajul, pe scurt:

| Moment | Ce face |
|---|---|
| Pornirea scenei | intră mergând din lateral, se întoarce spre pacient, face cu mâna 2,5 s |
| Meniu | se întoarce la locul lui; tabla redevine opacă |
| Începutul meciului | face cu mâna |
| Rândul pacientului | stă (Idle) și se uită la pacient; dacă pacientul se uită la el, îi face cu mâna; după 8 s se uită în jur |
| Rândul AI-ului | se gândește, vine în spatele tablei (tabla devine transparentă), își duce mâna la căsuță, piesa apare |
| Finalul meciului | se întoarce la loc; aplaudă (victoria pacientului), ridică din umeri (remiză), Victory (victoria lui) |

---

## OpponentAnimations.cs – legătura rol → fișier

```csharp
public static class OpponentAnimations
{
    public const string Idle = "Idle";         // ← numele rolului = numele stării din Animator Controller
    public const string Walk = "Walk";
    public const string Wave = "Wave";
    public const string Think = "Think";
    public const string LookAround = "LookAround";
    public const string PlaceA = "PlaceA";     // ← trei variante de „pune piesa”
    public const string PlaceB = "PlaceB";
    public const string PlaceC = "PlaceC";
    public const string Clap = "Clap";
    public const string Victory = "Victory";
    public const string Shrug = "Shrug";

    public static readonly IReadOnlyDictionary<string, string> Files = new Dictionary<string, string>
    {
        { Idle, "Happy Idle" },                // ← rol → numele fișierului FBX de la Mixamo (fără .fbx)
        { Walk, "Happy Walk" },
        { Wave, "Waving" },
        { Think, "Thinking" },
        { LookAround, "Look Around" },
        { PlaceA, "Button Pushing" },
        { PlaceB, "Pointing" },
        { PlaceC, "Reaching Out" },
        { Clap, "Clapping" },
        { Victory, "Victory Idle" },
        { Shrug, "Shrugging" },
    };

    public static readonly string[] PlaceVariants = { PlaceA, PlaceC };
    // ← variantele folosite efectiv; Pointing arată în gol, așa că nu e folosit (mâna e dusă oricum prin IK)

    public static float PlaceHitTime(string role)   // ← în ce moment al animației (0..1) apare piesa
    {
        switch (role)
        {
            case PlaceB: return 0.40f;
            case PlaceC: return 0.50f;
            default: return 0.45f;
        }
    }
}
```
**Ca să schimbi o animație:** descarci alt FBX de pe Mixamo în `Assets/Art/Character/Animations/`, îi pui
numele aici, apoi rulezi **TRAVEE → Regenerează animațiile personajului**.

---

## OpponentCharacter.cs

### `OpponentPlacement` – unde stă personajul
```csharp
public enum OpponentPlacement
{
    AffectedSide,   // ← implicit: pe partea afectată, ca pacientul să-și întoarcă privirea într-acolo
    HealthySide,
    Left,
    Right,
}
```
Se alege din Inspector, pe obiectul `XSi0`.

### Constante
```csharp
const float Fade = 0.25f;               // ← durata tranziției între animații (secunde)
const float WalkSpeed = 1.0f;           // ← viteza de mers (m/s)
const float TurnTime = 0.3f;            // ← cât durează o întoarcere
const float MaxPlaceDuration = 1.8f;    // ← animațiile lungi de „pune piesa” sunt accelerate până la atât
const float LookAroundAfter = 8f;       // ← după câte secunde de așteptare se uită în jur
const float GazeAngle = 15f;            // ← cât de „drept” trebuie să se uite pacientul spre el (grade)
const float GazeHold = 0.4f;            // ← cât timp trebuie să se uite până să-i facă cu mâna
const float WaveCooldown = 4f;          // ← pauza minimă între două saluturi
const float WaveDuration = 2.5f;        // ← cât durează salutul (animația se repetă)
```
Toate „reglajele” comportamentului sunt aici, sus, ca să le poți schimba ușor.

### Câmpuri
```csharp
readonly Dictionary<string, float> lengths = new Dictionary<string, float>();   // ← numele clipului → durata
Animator animator;
Room3D room;                     // ← pozițiile (unde stă, unde vine la tablă)
Board3DView board;               // ← tabla (pentru transparență și poziția căsuțelor)
Transform patientHead;           // ← camera = capul pacientului
OpponentPlacement placement;
Vector3 home;                    // ← locul unde așteaptă acum
Coroutine routine;               // ← „ce face” personajul acum (o singură rutină odată)
bool entering;                   // ← e încă în animația de intrare în scenă?

float handWeight; Vector3 handTarget;   // ← IK mâna dreaptă: cât de mult (0..1) și unde
float lookWeight; Vector3 lookTarget;   // ← IK privire: cât de mult și spre ce
```

### `Create` și `Setup`
```csharp
public static OpponentCharacter Create(GameObject model, RuntimeAnimatorController controller, Room3D room,
    Board3DView board, OpponentPlacement placement, float targetHeight = 1.4f)
{
    var animator = model.GetComponentInChildren<Animator>();
    if (animator == null || animator.gameObject != model)
        Debug.LogWarning("[TRAVEE] Animator-ul nu e pe rădăcina modelului; IK-ul s-ar putea să nu funcționeze.");
    // ← OnAnimatorIK e apelată doar pe obiectul care are Animator-ul; modelele Mixamo îl au pe rădăcină

    var character = model.AddComponent<OpponentCharacter>();
    character.Setup(animator, controller, room, board, placement, targetHeight);
    return character;
}

void Setup(...)
{
    animator = anim;
    animator.runtimeAnimatorController = controller;   // ← îi dăm mașina de stări generată
    animator.applyRootMotion = false;   // ← animațiile sunt In Place; deplasarea o facem din cod
    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // ← animă și când e în afara ecranului
    foreach (var clip in controller.animationClips) lengths[clip.name] = clip.length;
    // ← clipurile au numele fișierului (le redenumește importul, vezi 09), deci le găsim după nume

    room = stage; board = boardView; patientHead = room.Camera.transform; placement = place;

    NormalizeHeight(targetHeight);      // ← îl aducem la 1,4 m înălțime
    home = room.OpponentHome(AffectedSide.Left);   // ← un loc provizoriu; OnAffectedSideChanged îl corectează
    transform.SetPositionAndRotation(home, FacePatientFrom(home));
}

void NormalizeHeight(float targetHeight)
{
    var renderers = GetComponentsInChildren<Renderer>();
    if (renderers.Length == 0) return;
    var bounds = renderers[0].bounds;   // ← cutia care înconjoară modelul
    foreach (var r in renderers) bounds.Encapsulate(r.bounds);   // ← o lărgim să le cuprindă pe toate
    if (bounds.size.y > 0.01f) transform.localScale *= targetHeight / bounds.size.y;
    // ← scalăm ca înălțimea să fie exact targetHeight (orice model Mixamo ar folosi)
}
```

### Metodele din `IOpponentPresenter`
```csharp
public void Enter() => Run(EnterRoutine());    // ← intrarea în scenă (apelată de bootstrap)

public void OnMenu()
{
    if (entering) return;               // ← nu întrerupem intrarea
    board.SetSeeThrough(false);         // ← siguranță: tabla opacă
    Run(GoHome());                      // ← merge la locul lui
}

public void OnAffectedSideChanged(AffectedSide side)
{
    var newHome = room.OpponentHome(SideFor(side));   // ← locul în funcție de setare și de mâna afectată
    if (newHome == home) return;
    home = newHome;
    if (!entering) Run(GoHome());       // ← se mută pe cealaltă parte, mergând
}

public void OnMatchStart() => Run(GoHome(thenWave: true));   // ← la loc + salut
public void OnPatientTurn() => Run(PatientTurn());           // ← la loc + așteaptă + salut la privire
public void OnMatchEnd(MatchOutcome outcome, bool celebrateDraw) => Run(MatchEnd(outcome, celebrateDraw));
```
(În cod, ultimele trei sunt scrise cu acolade; fac exact același lucru.)

`Run(...)` pornește o rutină și o oprește pe cea veche: personajul face un singur lucru odată.

### `Think(seconds)` – AI-ul se gândește
```csharp
public IEnumerator Think(float seconds)
{
    Run(null);                          // ← oprește ce făcea (ex. mergea spre loc)
    // Dacă pacientul a mutat repede și personajul încă mergea, se oprește întâi și se întoarce:
    // altfel animația „Thinking” s-ar amesteca cu mersul.
    Play(A.Idle);
    yield return TurnTo(FacePatientFrom(transform.position));
    lookTarget = board.transform.position;
    lookWeight = 0.7f;                  // ← se uită la tablă
    Play(A.Think);
    yield return new WaitForSeconds(seconds);
}
```
`A` e un alias: `using A = Travee.TicTacToe.Characters.OpponentAnimations;` (sus în fișier), ca să scriem
`A.Idle` în loc de `OpponentAnimations.Idle`.

### Măsurarea brațului – `MeasureArm`, `RequestStepIfNeeded`
Ca mâna să atingă efectiv căsuța, personajul trebuie să stea la distanța potrivită. Distanța nu e scrisă
„din ochi”: se calculează din scheletul modelului, deci rămâne corectă și dacă schimbi personajul.
```csharp
void MeasureArm(float height)
{
    var upper = Bone(HumanBodyBones.RightUpperArm);   // ← umărul (începutul brațului)
    var lower = Bone(HumanBodyBones.RightLowerArm);   // ← cotul
    var hand = Bone(HumanBodyBones.RightHand);        // ← încheietura
    // ← rigul Humanoid dă aceleași nume de oase pentru orice personaj

    // „front” = cât iese corpul în față (nas, burtă), din cutia modelului
    ...
    armLength = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
    fingertip = Bone(HumanBodyBones.RightIndexDistal) ?? Bone(HumanBodyBones.RightMiddleDistal);
    // ← ultima falangă a arătătorului (sau a mijlociului, dacă lipsește)
    handLength = fingertip != null ? Vector3.Distance(hand.position, fingertip.position) : armLength * 0.3f;
    shoulderLocal = Quaternion.Inverse(transform.rotation) * (upper.position - transform.position);
    // ← unde e umărul față de picioare, în axele personajului (x = lateral, y = înălțime)
    minStandDistance = front - shoulderLocal.z + BoardClearance;
    // ← cât de aproape de tablă poate veni umărul fără ca fața să treacă prin ea
}
```
Se apelează în `Setup`, când modelul e încă în poza de bază (T-pose), deci distanțele dintre oase sunt
lungimile reale. Pentru personajul actual: braț 30 cm, mână 15 cm, umăr la 73 cm înălțime. Tot aici se
strâng în lista `handBones` încheietura și toate falangele mâinii drepte (folosite în `LateUpdate`).

```csharp
float TouchDistance => armLength * reach + handLength * CurledFingers;
// ← distanța umăr → căsuță la care atinge comod: brațul întins „reach” (0,95 = 95%) + mâna.
//   CurledFingers = 0,6: în animație degetele sunt îndoite, deci mâna e mai scurtă decât în T-pose.

void RequestStepIfNeeded()
{
    float top = board.CellBackPosition(0).y;                       // ← înălțimea rândului de sus
    float bottom = board.CellBackPosition(Board.CellCount - 1).y;  // ← și a celui de jos
    float distance = MaxTouchDistance;                             // ← braț + mână, întinse complet
    float reachUp = Mathf.Sqrt(Mathf.Max(distance * distance - minStandDistance * minStandDistance, 0f));
    // ← cât de sus ajunge de la distanța minimă (Pitagora: ipotenuza = brațul, o catetă = distanța până la tablă)
    if (top - shoulderLocal.y > reachUp + StretchTolerance)
        room.BuildStep((top + bottom) / 2f - shoulderLocal.y);
    // ← nu ajunge: cere ceva pe care să urce (buturugă / stâncă), cât să-i aducă umărul la mijlocul tablei.
    //   De acolo ajunge la fel de ușor sus și jos.
}
```
Verificarea folosește `MaxTouchDistance` (brațul și degetele întinse complet), plus o toleranță de 5 cm.
În practică nu mai cere nimic, pentru că înainte de ea rulează `GrowToReach`:
```csharp
void GrowToReach(float height)
{
    float top = board.CellBackPosition(0).y + ReachMargin;      // ← rândul de sus + 3 cm rezervă
    float factor = top / (shoulderLocal.y + ReachUp());         // ← de câte ori trebuie mărit
    if (factor <= 1.001f) return;                               // ← ajunge deja

    transform.localScale *= factor;
    MeasureArm(height * factor);        // ← oasele s-au mutat: măsurăm din nou brațul, mâna, umărul
}
```
Când mărești un personaj, toate măsurile lui cresc în aceeași proporție: înălțimea umărului, lungimea
brațului, cât iese capul în față. Deci și înălțimea până la care ajunge (umăr + `ReachUp()`) crește
proporțional, iar factorul necesar se află dintr-o singură împărțire. Cu tabla la nivelul ochilor (rândul de
sus la 1,40 m), personajul crește de la 1,40 m la 1,86 m. Dacă tabla e coborâtă, crește mai puțin sau
deloc. Brațele îi rămân proporționale cu corpul, deci arată la fel, doar mai mare. `reach` se reglează din Inspector, pe obiectul `XSi0`
(**Opponent Reach**): mai mic = stă mai aproape, cu cotul îndoit; 1 = braț complet întins.

### `PlacePiece(cell, place)` – pune piesa
Regula de bază: **mâna atinge tabla, nu trece prin ea**. Personajul stă mai aproape de tablă decât lungimea
brațului lui, deci animația singură („Button Pushing”, „Reaching Out”) i-ar duce mâna prin tablă – la
măsurătoare trecea cu până la 25 cm. De aceea, pe toată durata animației, mâna dreaptă e condusă de IK, iar
animația dă doar mișcarea corpului.
```csharp
public IEnumerator PlacePiece(int cell, Action place)
{
    Run(null);
    atBoard = false;
    board.SetSeeThrough(true);          // ← tabla începe să devină transparentă

    // 1. Vine în spatele tablei, în dreptul căsuței
    var touch = board.CellBackPosition(cell);            // ← punctul de atins, pe spatele tablei
    var stand = StandPointFor(touch, out var facing);    // ← unde trebuie să stea și încotro să privească
    yield return WalkTo(stand, facing);
    atBoard = true;

    // 2. Căsuța se aprinde roșu; IK-ul preia mâna cât e încă lângă corp
    plannedHandTarget = touch + (from - touch).normalized * (handLength * OpenHand);
    // ← ținta IK-ului este ÎNCHEIETURA. O oprim intenționat prea devreme: cu o mână întreagă (degete
    //   întinse, plus 30% rezervă) înainte de tablă. Ultimii centimetri îi face corecția fină.
    boardNormal = -board.transform.up;                   // ← direcția dinspre tablă spre personaj
    touchPoint = touch + boardNormal * TouchGap;         // ← osul degetului se oprește la 1,5 cm de tablă
    handRest = from + transform.forward * 0.10f - Vector3.up * 0.22f;   // ← punct de lângă corp, sub umăr
    handReach = 0f;                                      // ← 0 = mâna lângă corp, 1 = mâna la căsuță
    touching = true;

    board.SetAITarget(cell);
    for (float t = 0; t < TargetPreview; t += Time.deltaTime)   // ← 0,7 s: pacientul vede ce alege AI-ul
    {
        handWeight = Mathf.SmoothStep(0f, 1f, t / (TargetPreview * 0.6f));   // ← IK 0 → 1, cu brațul încă jos
        yield return null;
    }

    // 3. Animația pornește; mâna alunecă de lângă corp până la căsuță și înapoi
    Play(role);
    for (...) handReach = Mathf.SmoothStep(0f, 1f, t / hit);    // ← spre căsuță
    yield return new WaitForSeconds(TouchSettle);        // ← 0,22 s: corecția fină așază degetul
    place();                                             // ← piesa apare; tabla stinge singură glow-ul
    yield return new WaitForSeconds(TouchSettle);
    for (...) handReach = Mathf.SmoothStep(1f, 0f, t / back);   // ← înapoi lângă corp

    // 4. Abia după ce revine la Idle (brațul jos) eliberăm mâna
    Play(A.Idle);
    for (...) handWeight = Mathf.SmoothStep(1f, 0f, t / release);
    touching = false;
}
```
Sunt două „butoane” separate: `handWeight` (cât de mult ascultă mâna de IK) și `handReach` (unde e ținta, între
corp și căsuță). `handWeight` e 1 pe toată durata animației; se schimbă doar la început și la sfârșit, când
brațul e oricum jos. Ținta se calculează în `OnAnimatorIK`:
```csharp
if (touching) handTarget = Vector3.Lerp(handRest, plannedHandTarget + handCorrection, handReach);
```

### `StandPointFor` – unde stă ca să ajungă
```csharp
Vector3 StandPointFor(Vector3 touch, out Quaternion facing)
{
    var normal = -board.transform.up;   // ← direcția dinspre tablă spre personaj
    normal.y = 0f;
    normal.Normalize();
    facing = Quaternion.LookRotation(-normal);           // ← cu fața spre tablă

    float floor = room.StepHeight;                       // ← la tablă stă pe treaptă (0 dacă nu există)
    float distance = TouchDistance;                      // ← umăr → căsuță
    float dy = touch.y - (floor + shoulderLocal.y);      // ← cu cât e căsuța mai sus/jos decât umărul
    float horizontal = Mathf.Sqrt(Mathf.Max(distance * distance - dy * dy, 0f));
    // ← Pitagora: distanța² = orizontal² + vertical². Pentru o căsuță mai sus sau mai jos decât umărul,
    //   trebuie să stea mai aproape de tablă.
    horizontal = Mathf.Max(horizontal, minStandDistance);   // ← dar nu atât de aproape încât să intre în ea

    var shoulder = touch + normal * horizontal;          // ← unde trebuie să ajungă umărul
    var stand = shoulder - facing * new Vector3(shoulderLocal.x, 0f, shoulderLocal.z);
    // ← picioarele = umărul minus decalajul umărului față de picioare (rotit în direcția în care privește)
    stand.y = floor;
    return stand;
}
```
Rezultatul: umărul drept ajunge exact în dreptul coloanei căsuței, deci brațul se întinde drept înainte.

### `LateUpdate` – mâna nu trece prin tablă, degetul ajunge pe căsuță
`LateUpdate` rulează după animație și IK, deci aici vedem unde au ajuns de fapt oasele mâinii.
```csharp
void LateUpdate()
{
    if (!touching) return;

    // 1. Niciun os al mâinii mai aproape de tablă decât TouchGap (pe drum: plus 3 cm rezervă)
    bool arrived = handReach >= 0.999f;
    float clearance = arrived ? 0f : TravelMargin;
    float nearest = float.MaxValue;
    foreach (var bone in handBones)                       // ← încheietura + toate falangele (16 oase)
        nearest = Mathf.Min(nearest, Vector3.Dot(bone.position - touchPoint, boardNormal) - clearance);
    // ← produsul scalar cu normala = distanța „cu semn” față de planul de atingere:
    //   pozitivă = de partea personajului, negativă = a trecut de plan

    if (nearest < 0f)
    {
        handPushBack -= nearest;                          // ← de la cadrul următor: ținta retrasă cu cât a depășit
        handBones[0].position -= boardNormal * nearest;   // ← în cadrul acesta: mutăm direct mâna înapoi
    }
    else
    {
        handPushBack = Mathf.Max(0f, handPushBack - Mathf.Min(nearest * 0.5f, 0.3f * Time.deltaTime));
        // ← când e loc, retragerea se relaxează: cel mult jumătate din spațiul liber pe cadru
    }

    // 2. Cu mâna ajunsă, aducem vârful arătătorului pe punctul de atingere
    if (fingertip != null && arrived)
    {
        var error = touchPoint - fingertip.position;
        float towardBoard = Vector3.Dot(error, boardNormal);
        if ((handPushBack > 0f || nearest < 0.002f) && towardBoard < 0f) error -= boardNormal * towardBoard;
        // ← dacă alt deget e deja la limită, nu mai înaintăm: corectăm doar lateral și pe înălțime
        handFine = Vector3.ClampMagnitude(handFine + error * (1f - Mathf.Exp(-22f * Time.deltaTime)), MaxCorrection);
        // ← o fracțiune din eroare pe cadru; formula cu Exp o face independentă de numărul de cadre pe secundă
    }
    handCorrection = handFine + boardNormal * handPushBack;
}
```
Sunt două reguli. Prima e o **limită**: mâna e tratată ca un obiect care nu poate intra în tablă; dacă un os
ajunge prea aproape, mâna e trasă înapoi imediat, iar ținta IK-ului e retrasă pentru cadrele următoare. A
doua e o **buclă de reglaj** (regulator proporțional): măsurăm cât mai are degetul până la căsuță și mutăm
ținta cu o parte din eroare, până ajunge. Nu ghicim poziția degetului din animație; o măsurăm.

**Verificare:** pentru toate cele 9 căsuțe, de două ori (cu ambele animații), am înregistrat cea mai mică
distanță a oricărui os al mâinii față de spatele tablei pe **toată** mișcarea. Înainte: până la −25 cm (prin
tablă). Acum, cu tabla la nivelul ochilor: minimum +1,3 cm în toate cele 18 cazuri, iar în momentul în care
apare piesa cel mai apropiat vârf de deget e la 1,5–1,7 cm de suprafață (măsurat la os; grosimea degetului
acoperă diferența), la cel mult 7,1 cm de centrul căsuței (căsuța are ~19 cm lățime).

### Rutinele
```csharp
IEnumerator EnterRoutine()
{
    entering = true;
    float outward = Mathf.Sign(home.x == 0 ? -1f : home.x);   // ← -1 = vine din stânga, +1 = din dreapta
    var start = home + new Vector3(outward * 1.8f, 0f, 0.4f); // ← pornește din afara cadrului
    transform.SetPositionAndRotation(start, Quaternion.LookRotation(home - start));
    yield return WalkTo(home);
    entering = false;
    yield return Greet();               // ← sunet + animația de salut
}

IEnumerator GoHome(bool thenWave = false)
{
    yield return WalkTo(home);
    if (thenWave) yield return Greet();
}
```

```csharp
IEnumerator PatientTurn()
{
    yield return WalkTo(home);          // ← dacă era la tablă, se întoarce (tabla redevine opacă)
    Play(A.Idle);

    float gaze = 0f, cooldown = 0f, waiting = 0f;
    bool lookedAround = false;
    while (true)                        // ← rulează până când e oprită de Run(...) (rândul AI-ului)
    {
        LookAtPatient(0.5f);            // ← se uită la pacient
        cooldown -= Time.deltaTime;
        waiting += Time.deltaTime;
        gaze = IsPatientLookingAtMe() ? gaze + Time.deltaTime : 0f;
        // ← cât timp, fără întrerupere, s-a uitat pacientul la el

        if (gaze >= GazeHold && cooldown <= 0f)
        {
            yield return Greet();           // ← pacientul s-a uitat la el → îi face cu mâna
            cooldown = WaveCooldown;
            gaze = 0f;
        }
        else if (!lookedAround && waiting >= LookAroundAfter)
        {
            lookedAround = true;
            yield return OneShot(A.LookAround);        // ← pacientul se gândește mult → se uită în jur (o dată)
        }
        yield return null;
    }
}

IEnumerator MatchEnd(MatchOutcome outcome, bool celebrateDraw)
{
    yield return WalkTo(home);
    LookAtPatient(0.6f);
    switch (outcome)
    {
        case MatchOutcome.PatientWin: Play(A.Clap); break;            // ← aplauze
        case MatchOutcome.Draw when celebrateDraw: Play(A.Clap); break; // ← și la remiză, pe nivelurile 1-2
        case MatchOutcome.Draw: yield return OneShot(A.Shrug); break;  // ← ridică din umeri
        default: Play(A.Victory); break;                               // ← discret, nu triumfător
    }
}
```

### Mersul și întoarcerea
```csharp
IEnumerator WalkTo(Vector3 target, Quaternion? facing = null)
// ← `Quaternion?` = poate fi null („nullable”). Fără el, la final se întoarce spre pacient.
{
    target.y = room.FloorHeight(target);        // ← ținta e pe „podea” (pe treaptă, dacă e acolo)
    if ((transform.position - target).sqrMagnitude > 0.0004f)   // ← mai departe de 2 cm?
    {
        lookWeight = 0f;                // ← nu forțăm privirea în timpul mersului
        Play(A.Walk);
        var direction = target - transform.position;
        direction.y = 0f;               // ← se întoarce doar pe orizontală, chiar dacă urcă
        yield return TurnTo(Quaternion.LookRotation(direction));
        if (atBoard)                    // ← pleacă de la tablă: tabla redevine opacă
        {
            atBoard = false;
            board.SetSeeThrough(false);
        }

        float step = StepInterval * 0.5f;       // ← primul pas se aude repede
        while (Flat(transform.position - target).sqrMagnitude > 0.0004f)
        {
            var next = Vector3.MoveTowards(Flat(transform.position), Flat(target), WalkSpeed * Time.deltaTime);
            next.y = room.FloorHeight(next);    // ← merge pe orizontală; înălțimea o dă podeaua
            transform.position = next;
            step += Time.deltaTime;
            if (step >= StepInterval)           // ← la fiecare 0,45 s: un sunet de pas,
            {                                   //   auzit din direcția personajului
                step = 0f;
                Sfx.PlayAt(Sound.Footstep, transform.position, 0.6f);
            }
            yield return null;          // ← un pas pe frame; animația Walk e „pe loc”, deci picioarele țin pasul
        }
        transform.position = target;
        Play(A.Idle);
    }
    yield return TurnTo(facing ?? FacePatientFrom(target));
    // ← `a ?? b` = „a, iar dacă e null, b”
}

IEnumerator TurnTo(Quaternion rotation)
{
    var from = transform.rotation;
    if (Quaternion.Angle(from, rotation) < 1f) yield break;   // ← deja orientat
    for (float t = 0; t < TurnTime; t += Time.deltaTime)
    {
        transform.rotation = Quaternion.Slerp(from, rotation, t / TurnTime);
        yield return null;
    }
    transform.rotation = rotation;
}
```
`Flat(v)` întoarce vectorul cu `y = 0` (doar componenta orizontală). `Greet()` este salutul: ține animația
de salut `WaveDuration` secunde (fără sunet).

### `OneShot`, `Hold`, `Run`
```csharp
IEnumerator OneShot(string role)        // ← o animație o singură dată, apoi Idle
{
    float length = Length(role);
    if (length <= 0f) yield break;
    Play(role);
    yield return new WaitForSeconds(Mathf.Max(0f, length - Fade));   // ← pornim tranziția puțin înainte de final
    Play(A.Idle);
}

IEnumerator Hold(string role, float seconds)   // ← o animație care se repetă, ținută un anumit timp
{
    if (Length(role) <= 0f) yield break;
    Play(role);
    yield return new WaitForSeconds(seconds);
    Play(A.Idle);
}

void Run(IEnumerator next)              // ← „schimbă activitatea”
{
    if (routine != null) StopCoroutine(routine);   // ← oprește rutina veche
    routine = null;
    if (entering)
    {
        entering = false;               // ← intrarea a fost întreruptă: îl punem direct la locul lui
        transform.SetPositionAndRotation(home, FacePatientFrom(home));
    }
    animator.speed = 1f;                // ← resetăm ce ar fi putut lăsa rutina veche
    handWeight = 0f;
    if (next != null) routine = StartCoroutine(next);
}
```
`Hold` e folosit pentru salut: clipul `Waving` are doar ~0,5 s, dar e importat ca animație în buclă, deci
se repetă cele 2,5 secunde.

### Privirea pacientului și IK
```csharp
bool IsPatientLookingAtMe()
{
    var head = animator.GetBoneTransform(HumanBodyBones.Head);   // ← osul capului (știut datorită rigului Humanoid)
    var target = head != null ? head.position : transform.position + Vector3.up;
    return Vector3.Angle(patientHead.forward, target - patientHead.position) < GazeAngle;
    // ← unghiul dintre direcția privirii pacientului și direcția spre capul personajului sub 15° = se uită la el
}

void LookAtPatient(float weight) { lookTarget = patientHead.position; lookWeight = weight; }

void OnAnimatorIK(int layerIndex)       // ← apelată de Unity în fiecare frame (stratul are „IK Pass”)
{
    animator.SetLookAtWeight(lookWeight, 0f, 0.7f, 1f, 0.6f);
    // ← (total, corp, cap, ochi, limită): doar capul și ochii se întorc, trunchiul nu
    //   (rotirea trunchiului peste animație producea poziții ciudate)
    animator.SetLookAtPosition(lookTarget);
    animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handWeight);   // ← cât de mult se aplică IK-ul mâinii
    animator.SetIKPosition(AvatarIKGoal.RightHand, handTarget);         // ← unde trebuie să ajungă mâna
}
```
În VR, `patientHead` va fi capul real al pacientului (ochelarii), deci detecția privirii funcționează la fel.

### Utilitare
```csharp
Quaternion FacePatientFrom(Vector3 position)   // ← rotația „cu fața spre pacient”, doar pe orizontală
{
    var toPatient = room.Camera.transform.position - position;
    toPatient.y = 0f;                   // ← fără înclinare în sus/jos
    return Quaternion.LookRotation(toPatient);
}

AffectedSide SideFor(AffectedSide affected)    // ← transformă setarea (partea afectată/sănătoasă...) în stânga/dreapta
{
    var healthy = affected == AffectedSide.Left ? AffectedSide.Right : AffectedSide.Left;
    switch (placement)
    {
        case OpponentPlacement.AffectedSide: return affected;
        case OpponentPlacement.HealthySide: return healthy;
        case OpponentPlacement.Left: return AffectedSide.Left;
        default: return AffectedSide.Right;
    }
}

void Play(string role, float fade = Fade)
{
    int hash = Animator.StringToHash(role);    // ← Unity identifică stările după un număr calculat din nume
    if (animator.HasState(0, hash)) animator.CrossFadeInFixedTime(hash, fade, 0);
    // ← tranziție lină de 0,25 s spre starea respectivă (doar dacă există; o animație lipsă nu dă eroare)
}

float Length(string role) =>
    A.Files.TryGetValue(role, out var file) && lengths.TryGetValue(file, out var length) ? length : 0f;
// ← rol → fișier → durata clipului; 0 dacă lipsește
```
