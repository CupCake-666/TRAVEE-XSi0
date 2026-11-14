using System;
using System.Collections;
using System.Collections.Generic;
using Travee.TicTacToe.Audio;
using Travee.TicTacToe.Core;
using Travee.TicTacToe.World;
using UnityEngine;
using A = Travee.TicTacToe.Characters.OpponentAnimations;

namespace Travee.TicTacToe.Characters
{
    /// <summary>Unde sta personajul cand asteapta.</summary>
    public enum OpponentPlacement
    {
        AffectedSide,   // pe partea afectata: pacientul e incurajat sa intoarca privirea spre ea
        HealthySide,
        Left,
        Right,
    }

    /// <summary>
    /// Personajul AI. Asteapta lateral; pe randul lui vine in spatele tablei (care devine transparenta),
    /// casuta aleasa se aprinde rosu, isi duce mana pana o atinge (IK) si pune piesa, apoi se intoarce la loc.
    /// Daca pacientul se uita spre el pe randul pacientului, ii face cu mana.
    /// </summary>
    public sealed class OpponentCharacter : MonoBehaviour, IOpponentPresenter
    {
        const float Fade = 0.25f;               // secunde de tranzitie intre animatii
        const float TurnTime = 0.3f;            // secunde pentru o intoarcere
        const float MaxPlaceDuration = 1.8f;    // animatiile lungi de „pune piesa” sunt accelerate
        const float LookAroundAfter = 8f;       // daca pacientul se gandeste mult, personajul se uita in jur
        const float GazeHold = 0.4f;            // secunde de privit pana sa faca cu mana
        const float WaveCooldown = 4f;
        const float BoardClearance = 0.04f;     // metri: cat loc ramane intre fata/burta personajului si tabla
        const float CurledFingers = 0.6f;       // in animatie degetele sunt indoite: mana e mai „scurta” decat in T-pose
        const float TravelMargin = 0.03f;       // metri: rezerva fata de tabla cat timp mana e pe drum
        const float OpenHand = 1.3f;            // mana cu degetele intinse, plus o rezerva, fata de lungimea masurata
        const float ReachMargin = 0.03f;        // metri: rezerva peste randul de sus cand potrivim inaltimea personajului
        const float StretchTolerance = 0.05f;   // metri: cat poate lipsi pana la randul de sus fara sa ceara ceva pe care sa urce
        const float TouchGap = 0.015f;          // metri: cat de aproape de tabla se opreste degetul
        const float MaxCorrection = 0.20f;      // metri: cat poate fi mutata tinta mainii de corectia fina
        const float StepInterval = 0.45f;       // secunde intre sunetele de pasi
        const float TouchSettle = 0.22f;        // secunde cu mana tinuta pe casuta inainte si dupa ce apare piesa

        // Setarile de mai jos apar in Inspector cand personajul e un obiect din scena („Personaj”).
        [Header("Unde stă și cât de mare e")]
        [SerializeField, Tooltip("Pe ce parte a pacientului așteaptă.")]
        OpponentPlacement placement = OpponentPlacement.AffectedSide;
        [SerializeField, Min(0f), Tooltip("Înălțimea de bază, în metri. 0 = rămâne la scala din Transform.")]
        float height = 1.4f;
        [SerializeField, Tooltip("Dacă nu ajunge la rândul de sus al tablei, e mărit automat exact cât trebuie.")]
        bool growToReachBoard = true;
        [SerializeField, Range(0.5f, 1f), Tooltip("Cât de întins e brațul când atinge căsuța. Mai mic = stă mai aproape, cu cotul îndoit.")]
        float reach = 1f;

        [Header("Mișcare și reacții")]
        [SerializeField, Min(0.1f), Tooltip("Viteza de mers (m/s).")]
        float walkSpeed = 1f;
        [SerializeField, Min(0f), Tooltip("Câte secunde face cu mâna.")]
        float waveDuration = 2.5f;
        [SerializeField, Min(0f), Tooltip("Câte secunde stă aprinsă căsuța roșie înainte să întindă mâna.")]
        float targetPreview = 0.7f;
        [SerializeField, Range(1f, 60f), Tooltip("Cât de „drept” trebuie să se uite pacientul spre el ca să-i facă cu mâna (grade).")]
        float gazeAngle = 15f;

        readonly Dictionary<string, float> lengths = new Dictionary<string, float>();
        Animator animator;
        Room3D room;
        Board3DView board;
        Transform patientHead;
        Vector3 home;
        Coroutine routine;
        bool entering;
        bool atBoard;                           // e in spatele tablei (tabla e transparenta)

        // Masurile bratului drept (in metri, dupa scalare), folosite ca sa ajunga exact la casuta.
        float armLength;                        // umar → incheietura mainii
        float handLength;                       // incheietura → ultima falanga a degetului (in T-pose)
        Vector3 shoulderLocal;                  // pozitia umarului drept fata de picioarele personajului
        Transform fingertip;                    // osul ultimei falange a degetului aratator
        float minStandDistance;                 // cat de aproape de tabla poate veni umarul fara ca fata sa intre in ea

        readonly List<Transform> handBones = new List<Transform>();   // incheietura si toate falangele mainii drepte

        // Atingerea casutei (vezi PlacePiece si LateUpdate).
        bool touching;
        float handReach;                        // 0 = mana langa corp, 1 = mana la casuta
        Vector3 handRest;                       // punctul de langa corp din care porneste si in care revine mana
        Vector3 touchPoint, plannedHandTarget;  // unde se opreste degetul; unde am calculat ca vine incheietura
        Vector3 handCorrection;                 // cu cat e mutata tinta incheieturii (= handFine + retragerea)
        Vector3 handFine;                       // corectia fina care aduce degetul pe casuta
        float handPushBack;                     // retragerea de la tabla (metri), cand un os ajunge prea aproape
        Vector3 boardNormal;                    // dinspre tabla spre personaj

        // IK: unde isi duce mana dreapta si unde se uita (valori folosite in OnAnimatorIK).
        float handWeight;
        Vector3 handTarget;
        float lookWeight;
        Vector3 lookTarget;

        public static OpponentCharacter Create(GameObject model, RuntimeAnimatorController controller, Room3D room,
            Board3DView board, OpponentPlacement placement, float reach = 1f, float targetHeight = 1.4f)
        {
            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null || animator.gameObject != model)
                Debug.LogWarning("[TRAVEE] Animator-ul nu e pe rădăcina modelului; IK-ul s-ar putea să nu funcționeze.");

            var character = model.AddComponent<OpponentCharacter>();
            character.reach = Mathf.Clamp(reach, 0.5f, 1f);
            character.placement = placement;
            character.height = targetHeight;
            character.Setup(animator, controller, room, board);
            return character;
        }

        /// <summary>
        /// Porneste un personaj care exista deja in scena (obiectul „Personaj”, cu Animator si cu aceasta
        /// componenta). Setarile sunt cele din Inspector; Animator Controller-ul e cel pus pe Animator sau,
        /// daca lipseste, <paramref name="fallbackController"/>. Intoarce false daca personajul nu poate fi folosit.
        /// </summary>
        public bool Attach(RuntimeAnimatorController fallbackController, Room3D room, Board3DView board)
        {
            var anim = GetComponent<Animator>();
            var controller = anim != null && anim.runtimeAnimatorController != null
                ? anim.runtimeAnimatorController
                : fallbackController;
            if (anim == null || anim.avatar == null || controller == null)
            {
                Debug.LogWarning("[TRAVEE] Obiectul „Personaj” din scenă nu are Animator cu avatar și controller " +
                                 "(lipsește modelul Mixamo?). Jocul merge fără personaj.");
                gameObject.SetActive(false);
                return false;
            }
            Setup(anim, controller, room, board);
            return true;
        }

        void Setup(Animator anim, RuntimeAnimatorController controller, Room3D stage, Board3DView boardView)
        {
            float targetHeight = height;
            animator = anim;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;   // animatiile sunt In Place; deplasarea o facem din cod
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var clip in controller.animationClips) lengths[clip.name] = clip.length;

            room = stage;
            board = boardView;
            patientHead = room.Camera.transform;

            // Masuratorile se fac cu personajul drept, in origine (in scena poate fi rotit oricum).
            transform.SetPositionAndRotation(new Vector3(transform.position.x, 0f, transform.position.z), Quaternion.identity);
            if (targetHeight > 0f) NormalizeHeight(targetHeight);
            else targetHeight = MeasuredHeight();
            MeasureArm(targetHeight);
            if (growToReachBoard) GrowToReach(targetHeight);
            RequestStepIfNeeded();
            home = room.OpponentHome(AffectedSide.Left);
            transform.SetPositionAndRotation(home, FacePatientFrom(home));
        }

        /// <summary>Modelele Mixamo au inaltimi diferite: il scalam la o inaltime potrivita.</summary>
        void NormalizeHeight(float targetHeight)
        {
            float current = MeasuredHeight();
            if (current > 0.01f) transform.localScale *= targetHeight / current;
        }

        /// <summary>Inaltimea actuala a modelului, din „cutia” care il incadreaza.</summary>
        float MeasuredHeight()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 0f;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds.size.y;
        }

        /// <summary>
        /// Masoara bratul drept pe scheletul modelului (acum e in poza de baza, T-pose): distantele dintre
        /// oasele umar → cot → incheietura → varful degetului mijlociu. Cu ele calculam unde trebuie sa stea
        /// personajul ca sa ajunga cu degetele exact pe casuta, indiferent de modelul folosit.
        /// </summary>
        void MeasureArm(float height)
        {
            handBones.Clear();          // (se poate apela a doua oara, dupa ce personajul a fost marit)
            var upper = Bone(HumanBodyBones.RightUpperArm);
            var lower = Bone(HumanBodyBones.RightLowerArm);
            var hand = Bone(HumanBodyBones.RightHand);
            // Cat iese corpul (nasul, burta) in fata umerilor: din „cutia” modelului. Personajul nu are voie
            // sa se apropie de tabla mai mult de atat, altfel fata i-ar trece prin ea.
            float front = 0.15f * height;
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                front = Vector3.Dot(bounds.max - transform.position, transform.forward);
            }

            if (upper == null || lower == null || hand == null)
            {
                // Model fara rig Humanoid: proportii medii ale corpului.
                armLength = 0.33f * height;
                handLength = 0.10f * height;
                shoulderLocal = new Vector3(0.11f * height, 0.82f * height, 0f);
                minStandDistance = front + BoardClearance;
                return;
            }

            armLength = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
            fingertip = Bone(HumanBodyBones.RightIndexDistal) ?? Bone(HumanBodyBones.RightMiddleDistal);
            // Toate oasele mainii drepte: in enum, de la RightThumbProximal la RightLittleDistal sunt la rand.
            handBones.Add(hand);
            for (var bone = HumanBodyBones.RightThumbProximal; bone <= HumanBodyBones.RightLittleDistal; bone++)
            {
                var transformOfBone = Bone(bone);
                if (transformOfBone != null) handBones.Add(transformOfBone);
            }
            handLength = fingertip != null ? Vector3.Distance(hand.position, fingertip.position) : armLength * 0.3f;
            shoulderLocal = Quaternion.Inverse(transform.rotation) * (upper.position - transform.position);
            minStandDistance = front - shoulderLocal.z + BoardClearance;
        }

        /// <summary>Cat de sus ajunge cu mana, masurat de la umar, cand sta cat se poate de aproape de tabla.</summary>
        float ReachUp()
        {
            float distance = MaxTouchDistance;
            return Mathf.Sqrt(Mathf.Max(distance * distance - minStandDistance * minStandDistance, 0f));
        }

        /// <summary>
        /// Daca personajul e prea scund ca sa ajunga la randul de sus al tablei, il marim exact cat e nevoie.
        /// Toate masurile lui (inaltimea umarului, bratul, mana) cresc proportional cu scala, deci factorul
        /// necesar se afla dintr-o singura impartire: inaltimea de atins / inaltimea pana la care ajunge acum.
        /// </summary>
        void GrowToReach(float height)
        {
            float top = board.CellBackPosition(0).y + ReachMargin;
            float factor = top / (shoulderLocal.y + ReachUp());
            if (factor <= 1.001f) return;

            transform.localScale *= factor;
            MeasureArm(height * factor);        // oasele s-au mutat: masuram din nou
            Debug.Log($"[TRAVEE] Personajul a fost mărit la {height * factor:F2} m ca să ajungă la rândul de sus al tablei.");
        }

        /// <summary>Distanta umar → casuta la care personajul atinge comod: bratul intins „reach” + mana (degete indoite).</summary>
        float TouchDistance => armLength * reach + handLength * CurledFingers;

        /// <summary>Cat ajunge cel mult: bratul intins de tot, cu degetele intinse (asa atinge casutele de sus).</summary>
        float MaxTouchDistance => armLength + handLength;

        /// <summary>
        /// Daca de pe sol nu ajunge la randul de sus (personaj scund sau cu brate scurte), cere scenei o treapta
        /// care ii aduce umarul la inaltimea mijlocului tablei. De acolo ajunge si sus, si jos.
        /// </summary>
        void RequestStepIfNeeded()
        {
            float top = board.CellBackPosition(0).y;
            float bottom = board.CellBackPosition(Board.CellCount - 1).y;
            float distance = MaxTouchDistance;
            float reachUp = Mathf.Sqrt(Mathf.Max(distance * distance - minStandDistance * minStandDistance, 0f));
            if (top - shoulderLocal.y > reachUp + StretchTolerance)
                room.BuildStep((top + bottom) / 2f - shoulderLocal.y);
        }

        Transform Bone(HumanBodyBones bone) => animator.isHuman ? animator.GetBoneTransform(bone) : null;

        // ------------------------------------------------------------------ IOpponentPresenter

        /// <summary>Intrarea in scena: vine mergand din lateral pana la locul lui si face cu mana.</summary>
        public void Enter() => Run(EnterRoutine());

        public void OnMenu()
        {
            if (entering) return;               // nu intrerupem intrarea in scena
            atBoard = false;
            board.SetSeeThrough(false);
            Run(GoHome());
        }

        public void OnAffectedSideChanged(AffectedSide side)
        {
            var newHome = room.OpponentHome(SideFor(side));
            if (newHome == home) return;
            home = newHome;
            if (!entering) Run(GoHome());
        }

        public void OnMatchStart()
        {
            Run(GoHome(thenWave: true));
        }

        public void OnPatientTurn()
        {
            Run(PatientTurn());
        }

        public IEnumerator Think(float seconds)
        {
            Run(null);
            // Daca pacientul a mutat repede si personajul inca mergea spre locul lui, se opreste intai
            // si se intoarce spre tabla: altfel animatia „Thinking” s-ar amesteca cu mersul.
            Play(A.Idle);
            yield return TurnTo(FacePatientFrom(transform.position));
            lookTarget = board.transform.position;
            lookWeight = 0.7f;                  // se uita la tabla cat se gandeste
            Play(A.Think);
            yield return new WaitForSeconds(seconds);
        }

        public IEnumerator PlacePiece(int cell, Action place)
        {
            Run(null);
            atBoard = false;                    // (daca e deja la tabla, mersul de mai jos nu o face opaca)
            board.SetSeeThrough(true);

            // 1. Vine in spatele tablei, in dreptul casutei, la distanta de la care ajunge cu mana.
            var touch = board.CellBackPosition(cell);
            var stand = StandPointFor(touch, out var facing);
            yield return WalkTo(stand, facing);
            atBoard = true;

            // 2. Casuta aleasa se aprinde rosu (ca hover-ul pacientului) si personajul se uita la ea.
            //    Tot acum IK-ul preia mana dreapta, cat e inca langa corp (handWeight 0 → 1): de aici si pana la
            //    final, drumul mainii il decidem noi, nu animatia. Animatia singura ar duce mana prin tabla,
            //    pentru ca personajul sta mai aproape de tabla decat lungimea bratului.
            var shoulder = Bone(HumanBodyBones.RightUpperArm);
            var from = shoulder != null ? shoulder.position : transform.position + transform.rotation * shoulderLocal;
            // Tinta IK-ului e incheietura, nu degetele. O oprim intentionat PREA DEVREME: cu o mana intreaga
            // (degete intinse, plus o rezerva) inainte de tabla, pe linia umar → casuta. Ultimii centimetri ii
            // face corectia fina din LateUpdate, care apropie mana incet si se opreste la primul deget ajuns.
            plannedHandTarget = touch + (from - touch).normalized * (handLength * OpenHand);
            boardNormal = -board.transform.up;
            touchPoint = touch + boardNormal * TouchGap;            // osul degetului se opreste la 1,5 cm de tabla
            // Mana porneste dintr-un punct de langa corp (sub umar, putin in fata) si se intoarce tot acolo.
            handRest = from + transform.forward * 0.10f - Vector3.up * 0.22f;
            handCorrection = handFine = Vector3.zero;
            handPushBack = 0f;
            handReach = 0f;
            touching = true;

            board.SetAITarget(cell);
            Sfx.Play(Sound.Target, 0.7f);
            lookTarget = touch;
            lookWeight = 0.5f;                  // doar pe jumatate: cu capul aplecat de tot ar intra in tabla
            for (float t = 0; t < targetPreview; t += Time.deltaTime)
            {
                handWeight = Mathf.SmoothStep(0f, 1f, t / (targetPreview * 0.6f));
                yield return null;
            }
            handWeight = 1f;

            // 3. Animatia de „pune piesa” da miscarea corpului; mana aluneca de langa corp pana la casuta
            //    (handReach 0 → 1), piesa apare, apoi mana se retrage pe acelasi drum (handReach 1 → 0).
            string role = A.PlaceVariants[UnityEngine.Random.Range(0, A.PlaceVariants.Length)];
            float length = Length(role);
            float speed = length > 0f ? Mathf.Max(1f, length / MaxPlaceDuration) : 1f;
            float duration = length > 0f ? length / speed : 1.2f;       // fara animatie: doar mana, 1,2 s
            float hit = duration * A.PlaceHitTime(role);
            animator.speed = speed;
            Play(role);

            for (float t = 0; t < hit; t += Time.deltaTime)
            {
                handReach = Mathf.SmoothStep(0f, 1f, t / hit);
                yield return null;
            }
            handReach = 1f;
            // Cateva cadre cu mana ajunsa, ca sa lucreze corectia fina (LateUpdate): degetul se asaza pe casuta.
            yield return new WaitForSeconds(TouchSettle);
            place();                            // piesa apare; tabla stinge singura glow-ul rosu
            yield return new WaitForSeconds(TouchSettle);
            float back = Mathf.Max(duration - hit - 2f * TouchSettle, 0.3f);
            for (float t = 0; t < back; t += Time.deltaTime)
            {
                handReach = Mathf.SmoothStep(1f, 0f, t / back);
                yield return null;
            }
            handReach = 0f;

            // 4. Abia dupa ce animatia s-a terminat si personajul revine la Idle (bratul jos) eliberam mana.
            animator.speed = 1f;
            Play(A.Idle);
            float release = Fade + 0.15f;
            for (float t = 0; t < release; t += Time.deltaTime)
            {
                handWeight = Mathf.SmoothStep(1f, 0f, t / release);
                yield return null;
            }
            touching = false;
            handWeight = 0f;
            // Intoarcerea la loc o face rutina urmatoare (randul pacientului sau finalul meciului).
        }

        /// <summary>
        /// Unde sta personajul ca sa atinga punctul <paramref name="touch"/> de pe spatele tablei.
        /// Umarul drept vine in dreptul casutei, la o distanta la care bratul e intins <see cref="reach"/>
        /// din lungimea lui (Pitagora: stim distanta totala umar–casuta si diferenta de inaltime).
        /// </summary>
        Vector3 StandPointFor(Vector3 touch, out Quaternion facing)
        {
            var normal = -board.transform.up;   // dinspre tabla spre personaj
            normal.y = 0f;
            normal.Normalize();
            facing = Quaternion.LookRotation(-normal);

            float floor = room.StepHeight;                          // la tabla sta pe treapta (daca exista)
            float distance = TouchDistance;                         // umar → casuta
            float dy = touch.y - (floor + shoulderLocal.y);
            float horizontal = Mathf.Sqrt(Mathf.Max(distance * distance - dy * dy, 0f));
            horizontal = Mathf.Max(horizontal, minStandDistance);

            var shoulder = touch + normal * horizontal;             // unde trebuie sa ajunga umarul
            var stand = shoulder - facing * new Vector3(shoulderLocal.x, 0f, shoulderLocal.z);
            stand.y = floor;
            return stand;
        }

        public void OnMatchEnd(MatchOutcome outcome, bool celebrateDraw)
        {
            Run(MatchEnd(outcome, celebrateDraw));
        }

        // ------------------------------------------------------------------ rutine

        IEnumerator EnterRoutine()
        {
            entering = true;
            float outward = Mathf.Sign(home.x == 0 ? -1f : home.x);
            var start = home + new Vector3(outward * 1.8f, 0f, 0.4f);
            transform.SetPositionAndRotation(start, Quaternion.LookRotation(home - start));
            yield return WalkTo(home);
            entering = false;
            yield return Greet();
        }

        IEnumerator GoHome(bool thenWave = false)
        {
            yield return WalkTo(home);
            if (thenWave) yield return Greet();
        }

        IEnumerator PatientTurn()
        {
            yield return WalkTo(home);
            Play(A.Idle);

            float gaze = 0f, cooldown = 0f, waiting = 0f;
            bool lookedAround = false;
            while (true)
            {
                LookAtPatient(0.5f);
                cooldown -= Time.deltaTime;
                waiting += Time.deltaTime;
                gaze = IsPatientLookingAtMe() ? gaze + Time.deltaTime : 0f;

                if (gaze >= GazeHold && cooldown <= 0f)
                {
                    yield return Greet();           // pacientul s-a uitat la el → ii face cu mana
                    cooldown = WaveCooldown;
                    gaze = 0f;
                }
                else if (!lookedAround && waiting >= LookAroundAfter)
                {
                    lookedAround = true;
                    yield return OneShot(A.LookAround);
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
                case MatchOutcome.PatientWin:
                case MatchOutcome.Draw when celebrateDraw:
                    Play(A.Clap);               // pacientul a castigat (sau remiza la nivelurile blande) → aplauze
                    Sfx.PlayLong(Sound.Applause, 0.9f);
                    break;
                case MatchOutcome.Draw:
                    yield return OneShot(A.Shrug);
                    break;
                default:
                    Play(A.Victory);            // discret si prietenos, nu triumfator
                    break;
            }
        }

        /// <summary>Face cu mana (fara sunet).</summary>
        IEnumerator Greet()
        {
            yield return Hold(A.Wave, waveDuration);
        }

        /// <summary>
        /// Merge pana la <paramref name="target"/>: se intoarce spre el, merge (cu sunet de pasi), apoi se
        /// intoarce spre pacient sau in directia <paramref name="facing"/>, daca e data.
        /// </summary>
        IEnumerator WalkTo(Vector3 target, Quaternion? facing = null)
        {
            target.y = room.FloorHeight(target);
            if ((transform.position - target).sqrMagnitude > 0.0004f)
            {
                lookWeight = 0f;
                Play(A.Walk);
                var direction = target - transform.position;
                direction.y = 0f;               // se intoarce doar pe orizontala, chiar daca urca pe treapta
                yield return TurnTo(Quaternion.LookRotation(direction));
                if (atBoard)                    // pleaca de la tabla: tabla redevine opaca
                {
                    atBoard = false;
                    board.SetSeeThrough(false);
                }

                float step = StepInterval * 0.5f;
                while (Flat(transform.position - target).sqrMagnitude > 0.0004f)
                {
                    // Merge pe orizontala; inaltimea o da „podeaua” (urca lin pe treapta din spatele tablei).
                    var next = Vector3.MoveTowards(Flat(transform.position), Flat(target), walkSpeed * Time.deltaTime);
                    next.y = room.FloorHeight(next);
                    transform.position = next;
                    step += Time.deltaTime;
                    if (step >= StepInterval)
                    {
                        step = 0f;
                        Sfx.PlayAt(Sound.Footstep, transform.position, 0.6f);
                    }
                    yield return null;
                }
                transform.position = target;
                Play(A.Idle);
            }
            yield return TurnTo(facing ?? FacePatientFrom(target));
        }

        IEnumerator TurnTo(Quaternion rotation)
        {
            var from = transform.rotation;
            if (Quaternion.Angle(from, rotation) < 1f) yield break;
            for (float t = 0; t < TurnTime; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Slerp(from, rotation, t / TurnTime);
                yield return null;
            }
            transform.rotation = rotation;
        }

        /// <summary>Animatie care ruleaza o singura data, apoi revine la Idle.</summary>
        IEnumerator OneShot(string role)
        {
            float length = Length(role);
            if (length <= 0f) yield break;
            Play(role);
            yield return new WaitForSeconds(Mathf.Max(0f, length - Fade));
            Play(A.Idle);
        }

        /// <summary>Tine o animatie care se repeta (ex. salutul) un anumit timp, apoi revine la Idle.</summary>
        IEnumerator Hold(string role, float seconds)
        {
            if (Length(role) <= 0f) yield break;
            Play(role);
            yield return new WaitForSeconds(seconds);
            Play(A.Idle);
        }

        /// <summary>Opreste rutina curenta a personajului si (optional) porneste alta.</summary>
        void Run(IEnumerator next)
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (entering)
            {
                // Intrarea a fost intrerupta (a inceput meciul): il punem direct la locul lui.
                entering = false;
                transform.SetPositionAndRotation(home, FacePatientFrom(home));
            }
            animator.speed = 1f;
            handWeight = 0f;
            touching = false;
            Sfx.StopLong();                     // aplauzele se opresc cand personajul trece la altceva
            board.SetAITarget(-1);              // daca a fost intrerupt inainte sa puna piesa
            if (next != null) routine = StartCoroutine(next);
        }

        // ------------------------------------------------------------------ privire si IK

        bool IsPatientLookingAtMe()
        {
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var target = head != null ? head.position : transform.position + Vector3.up;
            return Vector3.Angle(patientHead.forward, target - patientHead.position) < gazeAngle;
        }

        void LookAtPatient(float weight)
        {
            lookTarget = patientHead.position;
            lookWeight = weight;
        }

        /// <summary>
        /// Corectia fina a atingerii. IK-ul duce INCHEIETURA la tinta, dar pe noi ne intereseaza DEGETUL, iar
        /// pozitia lui fata de incheietura depinde de animatie. LateUpdate ruleaza dupa animatie si IK, deci aici
        /// vedem unde a ajuns de fapt degetul: mutam tinta incheieturii cu o parte din eroarea ramasa. In cateva
        /// cadre, degetul ajunge pe casuta (bucla de reglaj, ca un regulator proportional).
        /// </summary>
        void LateUpdate()
        {
            if (!touching) return;

            // 1. Niciun os al mainii nu are voie mai aproape de tabla decat TouchGap (pe drum: plus o rezerva).
            //    „nearest” = distanta celui mai apropiat os fata de limita permisa; negativ = a depasit-o.
            bool arrived = handReach >= 0.999f;
            float clearance = arrived ? 0f : TravelMargin;
            float nearest = float.MaxValue;
            foreach (var bone in handBones)
                nearest = Mathf.Min(nearest, Vector3.Dot(bone.position - touchPoint, boardNormal) - clearance);
            if (nearest < 0f)
            {
                handPushBack -= nearest;                        // de la cadrul urmator: tinta retrasa cu cat a depasit
                handBones[0].position -= boardNormal * nearest; // in cadrul acesta: mutam direct mana (cu degetele ei) inapoi
            }
            else
            {
                // Cand e loc, retragerea se relaxeaza incet: cel mult 30 cm/s si cel mult jumatate din spatiul liber.
                handPushBack = Mathf.Max(0f, handPushBack - Mathf.Min(nearest * 0.5f, 0.3f * Time.deltaTime));
            }

            // 2. Cu mana ajunsa la casuta, aducem varful aratatorului pe punctul de atingere.
            if (fingertip != null && arrived)
            {
                var error = touchPoint - fingertip.position;
                float towardBoard = Vector3.Dot(error, boardNormal);       // negativ = mai are de mers spre tabla
                // Inaintam spre tabla doar daca niciun alt deget nu e deja la limita; altfel corectam doar
                // lateral si pe inaltime.
                if ((handPushBack > 0f || nearest < 0.002f) && towardBoard < 0f) error -= boardNormal * towardBoard;
                // O fractiune din eroare pe cadru, independenta de numarul de cadre pe secunda.
                handFine = Vector3.ClampMagnitude(handFine + error * (1f - Mathf.Exp(-22f * Time.deltaTime)), MaxCorrection);
            }
            handCorrection = handFine + boardNormal * handPushBack;
        }

        /// <summary>Apelat de Unity la fiecare frame (stratul Animator-ului are „IK Pass” activat).</summary>
        void OnAnimatorIK(int layerIndex)
        {
            // Doar capul si ochii (corp 0): rotirea trunchiului peste animatie producea pozitii ciudate.
            animator.SetLookAtWeight(lookWeight, 0f, 0.7f, 1f, 0.6f);
            animator.SetLookAtPosition(lookTarget);
            if (touching) handTarget = Vector3.Lerp(handRest, plannedHandTarget + handCorrection, handReach);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, handTarget);
        }

        // ------------------------------------------------------------------ utilitare

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        Quaternion FacePatientFrom(Vector3 position)
        {
            var toPatient = room.Camera.transform.position - position;
            toPatient.y = 0f;
            return Quaternion.LookRotation(toPatient);
        }

        AffectedSide SideFor(AffectedSide affected)
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
            int hash = Animator.StringToHash(role);
            if (animator.HasState(0, hash)) animator.CrossFadeInFixedTime(hash, fade, 0);
        }

        float Length(string role) =>
            A.Files.TryGetValue(role, out var file) && lengths.TryGetValue(file, out var length) ? length : 0f;
    }
}
