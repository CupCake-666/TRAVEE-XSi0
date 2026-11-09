# 08 – Personajul AI: OpponentAnimations, OpponentCharacter

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

### `PlacePiece(cell, place)` – pune piesa
```csharp
public IEnumerator PlacePiece(int cell, Action place)
{
    Run(null);
    board.SetSeeThrough(true);          // ← tabla începe să devină transparentă
    yield return WalkTo(room.OpponentAtBoard);   // ← merge în spatele tablei

    string role = A.PlaceVariants[UnityEngine.Random.Range(0, A.PlaceVariants.Length)];   // ← o variantă la întâmplare
    float length = Length(role);
    handTarget = board.CellWorldPosition(cell) - board.transform.up * 0.03f;
    // ← ținta mâinii: căsuța, cu 3 cm spre spate (personajul e în spatele tablei și o atinge prin ea)
    lookTarget = handTarget;
    lookWeight = 0.9f;                  // ← se uită la căsuță

    if (length <= 0f) { place(); yield break; }   // ← animația lipsește: punem piesa direct

    float speed = Mathf.Max(1f, length / MaxPlaceDuration);   // ← accelerăm doar dacă e mai lungă de 1,8 s
    float duration = length / speed;
    float hit = duration * A.PlaceHitTime(role);   // ← momentul atingerii
    animator.speed = speed;
    Play(role);

    for (float t = 0; t < hit; t += Time.deltaTime)
    {
        handWeight = Mathf.SmoothStep(0f, 1f, t / hit);   // ← mâna trece treptat de la animație la căsuță
        yield return null;
    }
    place();                            // ← piesa apare (Apply din GameController)
    float rest = duration - hit;
    for (float t = 0; t < rest; t += Time.deltaTime)
    {
        handWeight = Mathf.SmoothStep(1f, 0f, t / rest);  // ← mâna revine treptat la animație
        yield return null;
    }
    handWeight = 0f;
    animator.speed = 1f;
    Play(A.Idle);
    // ← întoarcerea la loc o face rutina următoare (rândul pacientului sau finalul meciului)
}
```
`Mathf.SmoothStep(a, b, t)` = interpolare cu început și sfârșit lin (fără smucituri).

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
    yield return Hold(A.Wave, WaveDuration);
}

IEnumerator GoHome(bool thenWave = false)
{
    yield return WalkTo(home);
    if (thenWave) yield return Hold(A.Wave, WaveDuration);
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
            yield return Hold(A.Wave, WaveDuration);   // ← pacientul s-a uitat la el → îi face cu mâna
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
IEnumerator WalkTo(Vector3 target)
{
    if ((transform.position - target).sqrMagnitude > 0.0004f)   // ← mai departe de 2 cm?
    {
        lookWeight = 0f;                // ← nu forțăm privirea în timpul mersului
        Play(A.Walk);
        yield return TurnTo(Quaternion.LookRotation(target - transform.position));   // ← se întoarce spre țintă
        if ((transform.position - room.OpponentAtBoard).sqrMagnitude < 0.01f && target != room.OpponentAtBoard)
            board.SetSeeThrough(false);
        // ← dacă pleacă de la tablă, tabla redevine opacă

        while ((transform.position - target).sqrMagnitude > 0.0004f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, WalkSpeed * Time.deltaTime);
            yield return null;          // ← un pas pe frame; animația Walk e „pe loc”, deci picioarele țin pasul
        }
        transform.position = target;
        Play(A.Idle);
    }
    yield return TurnTo(FacePatientFrom(target));   // ← la final, cu fața spre pacient
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
