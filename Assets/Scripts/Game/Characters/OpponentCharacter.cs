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
    /// <summary>Unde stă personajul când așteaptă.</summary>
    public enum OpponentPlacement
    {
        AffectedSide,   // pe partea afectată: pacientul e încurajat să întoarcă privirea spre ea
        HealthySide,
        Left,
        Right,
    }

    /// <summary>
    /// Personajul AI. Așteaptă lateral; pe rândul lui vine în spatele tablei (care devine transparentă),
    /// căsuța aleasă se aprinde roșu, își duce mâna până o atinge (IK) și pune piesa, apoi se întoarce la loc.
    /// Dacă pacientul se uită spre el pe rândul pacientului, îi face cu mâna.
    /// </summary>
    public sealed class OpponentCharacter : MonoBehaviour, IOpponentPresenter
    {
        const float Fade = 0.25f;               // secunde de tranziție între animații
        const float WalkSpeed = 1.0f;           // m/s
        const float TurnTime = 0.3f;            // secunde pentru o întoarcere
        const float MaxPlaceDuration = 1.8f;    // animațiile lungi de „pune piesa” sunt accelerate
        const float LookAroundAfter = 8f;       // dacă pacientul se gândește mult, personajul se uită în jur
        const float GazeAngle = 15f;            // grade: cât de „drept” trebuie să se uite pacientul spre el
        const float GazeHold = 0.4f;            // secunde de privit până să facă cu mâna
        const float WaveCooldown = 4f;
        const float WaveDuration = 2.5f;        // cât timp face cu mâna (animația se repetă)
        const float TargetPreview = 0.7f;       // cât stă aprinsă căsuța (glow roșu) înainte să întindă mâna
        const float BoardClearance = 0.04f;     // metri: cât loc rămâne între fața/burta personajului și tablă
        const float CurledFingers = 0.6f;       // în animație degetele sunt îndoite: mâna e mai „scurtă” decât în T-pose
        const float TravelMargin = 0.03f;       // metri: rezervă față de tablă cât timp mâna e pe drum
        const float OpenHand = 1.3f;            // mâna cu degetele întinse, plus o rezervă, față de lungimea măsurată
        const float ReachMargin = 0.03f;        // metri: rezervă peste rândul de sus când potrivim înălțimea personajului
        const float StretchTolerance = 0.05f;   // metri: cât poate lipsi până la rândul de sus fără să ceară ceva pe care să urce
        const float TouchGap = 0.015f;          // metri: cât de aproape de tablă se oprește degetul
        const float MaxCorrection = 0.20f;      // metri: cât poate fi mutată ținta mâinii de corecția fină
        const float StepInterval = 0.45f;       // secunde între sunetele de pași
        const float TouchSettle = 0.22f;        // secunde cu mâna ținută pe căsuță înainte și după ce apare piesa

        readonly Dictionary<string, float> lengths = new Dictionary<string, float>();
        Animator animator;
        Room3D room;
        Board3DView board;
        Transform patientHead;
        OpponentPlacement placement;
        Vector3 home;
        Coroutine routine;
        bool entering;
        bool atBoard;                           // e în spatele tablei (tabla e transparentă)

        // Măsurile brațului drept (în metri, după scalare), folosite ca să ajungă exact la căsuță.
        float reach;                            // cât din lungimea brațului întinde (0,5 – 1)
        float armLength;                        // umăr → încheietura mâinii
        float handLength;                       // încheietură → ultima falangă a degetului (în T-pose)
        Vector3 shoulderLocal;                  // poziția umărului drept față de picioarele personajului
        Transform fingertip;                    // osul ultimei falange a degetului arătător
        float minStandDistance;                 // cât de aproape de tablă poate veni umărul fără ca fața să intre în ea

        readonly List<Transform> handBones = new List<Transform>();   // încheietura și toate falangele mâinii drepte

        // Atingerea căsuței (vezi PlacePiece și LateUpdate).
        bool touching;
        float handReach;                        // 0 = mâna lângă corp, 1 = mâna la căsuță
        Vector3 handRest;                       // punctul de lângă corp din care pornește și în care revine mâna
        Vector3 touchPoint, plannedHandTarget;  // unde se oprește degetul; unde am calculat că vine încheietura
        Vector3 handCorrection;                 // cu cât e mutată ținta încheieturii (= handFine + retragerea)
        Vector3 handFine;                       // corecția fină care aduce degetul pe căsuță
        float handPushBack;                     // retragerea de la tablă (metri), când un os ajunge prea aproape
        Vector3 boardNormal;                    // dinspre tablă spre personaj

        // IK: unde își duce mâna dreaptă și unde se uită (valori folosite în OnAnimatorIK).
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
            character.Setup(animator, controller, room, board, placement, targetHeight);
            return character;
        }

        void Setup(Animator anim, RuntimeAnimatorController controller, Room3D stage, Board3DView boardView,
            OpponentPlacement place, float targetHeight)
        {
            animator = anim;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;   // animațiile sunt In Place; deplasarea o facem din cod
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var clip in controller.animationClips) lengths[clip.name] = clip.length;

            room = stage;
            board = boardView;
            patientHead = room.Camera.transform;
            placement = place;

            NormalizeHeight(targetHeight);
            MeasureArm(targetHeight);
            GrowToReach(targetHeight);
            RequestStepIfNeeded();
            home = room.OpponentHome(AffectedSide.Left);
            transform.SetPositionAndRotation(home, FacePatientFrom(home));
        }

        /// <summary>Modelele Mixamo au înălțimi diferite: îl scalăm la o înălțime potrivită.</summary>
        void NormalizeHeight(float targetHeight)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            if (bounds.size.y > 0.01f) transform.localScale *= targetHeight / bounds.size.y;
        }

        /// <summary>
        /// Măsoară brațul drept pe scheletul modelului (acum e în poza de bază, T-pose): distanțele dintre
        /// oasele umăr → cot → încheietură → vârful degetului mijlociu. Cu ele calculăm unde trebuie să stea
        /// personajul ca să ajungă cu degetele exact pe căsuță, indiferent de modelul folosit.
        /// </summary>
        void MeasureArm(float height)
        {
            handBones.Clear();          // (se poate apela a doua oară, după ce personajul a fost mărit)
            var upper = Bone(HumanBodyBones.RightUpperArm);
            var lower = Bone(HumanBodyBones.RightLowerArm);
            var hand = Bone(HumanBodyBones.RightHand);
            // Cât iese corpul (nasul, burta) în fața umerilor: din „cutia” modelului. Personajul nu are voie
            // să se apropie de tablă mai mult de atât, altfel fața i-ar trece prin ea.
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
                // Model fără rig Humanoid: proporții medii ale corpului.
                armLength = 0.33f * height;
                handLength = 0.10f * height;
                shoulderLocal = new Vector3(0.11f * height, 0.82f * height, 0f);
                minStandDistance = front + BoardClearance;
                return;
            }

            armLength = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
            fingertip = Bone(HumanBodyBones.RightIndexDistal) ?? Bone(HumanBodyBones.RightMiddleDistal);
            // Toate oasele mâinii drepte: în enum, de la RightThumbProximal la RightLittleDistal sunt la rând.
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

        /// <summary>Cât de sus ajunge cu mâna, măsurat de la umăr, când stă cât se poate de aproape de tablă.</summary>
        float ReachUp()
        {
            float distance = MaxTouchDistance;
            return Mathf.Sqrt(Mathf.Max(distance * distance - minStandDistance * minStandDistance, 0f));
        }

        /// <summary>
        /// Dacă personajul e prea scund ca să ajungă la rândul de sus al tablei, îl mărim exact cât e nevoie.
        /// Toate măsurile lui (înălțimea umărului, brațul, mâna) cresc proporțional cu scala, deci factorul
        /// necesar se află dintr-o singură împărțire: înălțimea de atins / înălțimea până la care ajunge acum.
        /// </summary>
        void GrowToReach(float height)
        {
            float top = board.CellBackPosition(0).y + ReachMargin;
            float factor = top / (shoulderLocal.y + ReachUp());
            if (factor <= 1.001f) return;

            transform.localScale *= factor;
            MeasureArm(height * factor);        // oasele s-au mutat: măsurăm din nou
            Debug.Log($"[TRAVEE] Personajul a fost mărit la {height * factor:F2} m ca să ajungă la rândul de sus al tablei.");
        }

        /// <summary>Distanța umăr → căsuță la care personajul atinge comod: brațul întins „reach” + mâna (degete îndoite).</summary>
        float TouchDistance => armLength * reach + handLength * CurledFingers;

        /// <summary>Cât ajunge cel mult: brațul întins de tot, cu degetele întinse (așa atinge căsuțele de sus).</summary>
        float MaxTouchDistance => armLength + handLength;

        /// <summary>
        /// Dacă de pe sol nu ajunge la rândul de sus (personaj scund sau cu brațe scurte), cere scenei o treaptă
        /// care îi aduce umărul la înălțimea mijlocului tablei. De acolo ajunge și sus, și jos.
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

        /// <summary>Intrarea în scenă: vine mergând din lateral până la locul lui și face cu mâna.</summary>
        public void Enter() => Run(EnterRoutine());

        public void OnMenu()
        {
            if (entering) return;               // nu întrerupem intrarea în scenă
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
            // Dacă pacientul a mutat repede și personajul încă mergea spre locul lui, se oprește întâi
            // și se întoarce spre tablă: altfel animația „Thinking” s-ar amesteca cu mersul.
            Play(A.Idle);
            yield return TurnTo(FacePatientFrom(transform.position));
            lookTarget = board.transform.position;
            lookWeight = 0.7f;                  // se uită la tablă cât se gândește
            Play(A.Think);
            yield return new WaitForSeconds(seconds);
        }

        public IEnumerator PlacePiece(int cell, Action place)
        {
            Run(null);
            atBoard = false;                    // (dacă e deja la tablă, mersul de mai jos nu o face opacă)
            board.SetSeeThrough(true);

            // 1. Vine în spatele tablei, în dreptul căsuței, la distanța de la care ajunge cu mâna.
            var touch = board.CellBackPosition(cell);
            var stand = StandPointFor(touch, out var facing);
            yield return WalkTo(stand, facing);
            atBoard = true;

            // 2. Căsuța aleasă se aprinde roșu (ca hover-ul pacientului) și personajul se uită la ea.
            //    Tot acum IK-ul preia mâna dreaptă, cât e încă lângă corp (handWeight 0 → 1): de aici și până la
            //    final, drumul mâinii îl decidem noi, nu animația. Animația singură ar duce mâna prin tablă,
            //    pentru că personajul stă mai aproape de tablă decât lungimea brațului.
            var shoulder = Bone(HumanBodyBones.RightUpperArm);
            var from = shoulder != null ? shoulder.position : transform.position + transform.rotation * shoulderLocal;
            // Ținta IK-ului e încheietura, nu degetele. O oprim intenționat PREA DEVREME: cu o mână întreagă
            // (degete întinse, plus o rezervă) înainte de tablă, pe linia umăr → căsuță. Ultimii centimetri îi
            // face corecția fină din LateUpdate, care apropie mâna încet și se oprește la primul deget ajuns.
            plannedHandTarget = touch + (from - touch).normalized * (handLength * OpenHand);
            boardNormal = -board.transform.up;
            touchPoint = touch + boardNormal * TouchGap;            // osul degetului se oprește la 1,5 cm de tablă
            // Mâna pornește dintr-un punct de lângă corp (sub umăr, puțin în față) și se întoarce tot acolo.
            handRest = from + transform.forward * 0.10f - Vector3.up * 0.22f;
            handCorrection = handFine = Vector3.zero;
            handPushBack = 0f;
            handReach = 0f;
            touching = true;

            board.SetAITarget(cell);
            Sfx.Play(Sound.Target, 0.7f);
            lookTarget = touch;
            lookWeight = 0.5f;                  // doar pe jumătate: cu capul aplecat de tot ar intra în tablă
            for (float t = 0; t < TargetPreview; t += Time.deltaTime)
            {
                handWeight = Mathf.SmoothStep(0f, 1f, t / (TargetPreview * 0.6f));
                yield return null;
            }
            handWeight = 1f;

            // 3. Animația de „pune piesa” dă mișcarea corpului; mâna alunecă de lângă corp până la căsuță
            //    (handReach 0 → 1), piesa apare, apoi mâna se retrage pe același drum (handReach 1 → 0).
            string role = A.PlaceVariants[UnityEngine.Random.Range(0, A.PlaceVariants.Length)];
            float length = Length(role);
            float speed = length > 0f ? Mathf.Max(1f, length / MaxPlaceDuration) : 1f;
            float duration = length > 0f ? length / speed : 1.2f;       // fără animație: doar mâna, 1,2 s
            float hit = duration * A.PlaceHitTime(role);
            animator.speed = speed;
            Play(role);

            for (float t = 0; t < hit; t += Time.deltaTime)
            {
                handReach = Mathf.SmoothStep(0f, 1f, t / hit);
                yield return null;
            }
            handReach = 1f;
            // Câteva cadre cu mâna ajunsă, ca să lucreze corecția fină (LateUpdate): degetul se așază pe căsuță.
            yield return new WaitForSeconds(TouchSettle);
            place();                            // piesa apare; tabla stinge singură glow-ul roșu
            yield return new WaitForSeconds(TouchSettle);
            float back = Mathf.Max(duration - hit - 2f * TouchSettle, 0.3f);
            for (float t = 0; t < back; t += Time.deltaTime)
            {
                handReach = Mathf.SmoothStep(1f, 0f, t / back);
                yield return null;
            }
            handReach = 0f;

            // 4. Abia după ce animația s-a terminat și personajul revine la Idle (brațul jos) eliberăm mâna.
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
            // Întoarcerea la loc o face rutina următoare (rândul pacientului sau finalul meciului).
        }

        /// <summary>
        /// Unde stă personajul ca să atingă punctul <paramref name="touch"/> de pe spatele tablei.
        /// Umărul drept vine în dreptul căsuței, la o distanță la care brațul e întins <see cref="reach"/>
        /// din lungimea lui (Pitagora: știm distanța totală umăr–căsuță și diferența de înălțime).
        /// </summary>
        Vector3 StandPointFor(Vector3 touch, out Quaternion facing)
        {
            var normal = -board.transform.up;   // dinspre tablă spre personaj
            normal.y = 0f;
            normal.Normalize();
            facing = Quaternion.LookRotation(-normal);

            float floor = room.StepHeight;                          // la tablă stă pe treaptă (dacă există)
            float distance = TouchDistance;                         // umăr → căsuță
            float dy = touch.y - (floor + shoulderLocal.y);
            float horizontal = Mathf.Sqrt(Mathf.Max(distance * distance - dy * dy, 0f));
            horizontal = Mathf.Max(horizontal, minStandDistance);

            var shoulder = touch + normal * horizontal;             // unde trebuie să ajungă umărul
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
                    yield return Greet();           // pacientul s-a uitat la el → îi face cu mâna
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
                    Play(A.Clap);               // pacientul a câștigat (sau remiză la nivelurile blânde) → aplauze
                    Sfx.PlayLong(Sound.Applause, 0.9f);
                    break;
                case MatchOutcome.Draw:
                    yield return OneShot(A.Shrug);
                    break;
                default:
                    Play(A.Victory);            // discret și prietenos, nu triumfător
                    break;
            }
        }

        /// <summary>Face cu mâna (fără sunet).</summary>
        IEnumerator Greet()
        {
            yield return Hold(A.Wave, WaveDuration);
        }

        /// <summary>
        /// Merge până la <paramref name="target"/>: se întoarce spre el, merge (cu sunet de pași), apoi se
        /// întoarce spre pacient sau în direcția <paramref name="facing"/>, dacă e dată.
        /// </summary>
        IEnumerator WalkTo(Vector3 target, Quaternion? facing = null)
        {
            target.y = room.FloorHeight(target);
            if ((transform.position - target).sqrMagnitude > 0.0004f)
            {
                lookWeight = 0f;
                Play(A.Walk);
                var direction = target - transform.position;
                direction.y = 0f;               // se întoarce doar pe orizontală, chiar dacă urcă pe treaptă
                yield return TurnTo(Quaternion.LookRotation(direction));
                if (atBoard)                    // pleacă de la tablă: tabla redevine opacă
                {
                    atBoard = false;
                    board.SetSeeThrough(false);
                }

                float step = StepInterval * 0.5f;
                while (Flat(transform.position - target).sqrMagnitude > 0.0004f)
                {
                    // Merge pe orizontală; înălțimea o dă „podeaua” (urcă lin pe treapta din spatele tablei).
                    var next = Vector3.MoveTowards(Flat(transform.position), Flat(target), WalkSpeed * Time.deltaTime);
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

        /// <summary>Animație care rulează o singură dată, apoi revine la Idle.</summary>
        IEnumerator OneShot(string role)
        {
            float length = Length(role);
            if (length <= 0f) yield break;
            Play(role);
            yield return new WaitForSeconds(Mathf.Max(0f, length - Fade));
            Play(A.Idle);
        }

        /// <summary>Ține o animație care se repetă (ex. salutul) un anumit timp, apoi revine la Idle.</summary>
        IEnumerator Hold(string role, float seconds)
        {
            if (Length(role) <= 0f) yield break;
            Play(role);
            yield return new WaitForSeconds(seconds);
            Play(A.Idle);
        }

        /// <summary>Oprește rutina curentă a personajului și (opțional) pornește alta.</summary>
        void Run(IEnumerator next)
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (entering)
            {
                // Intrarea a fost întreruptă (a început meciul): îl punem direct la locul lui.
                entering = false;
                transform.SetPositionAndRotation(home, FacePatientFrom(home));
            }
            animator.speed = 1f;
            handWeight = 0f;
            touching = false;
            Sfx.StopLong();                     // aplauzele se opresc când personajul trece la altceva
            board.SetAITarget(-1);              // dacă a fost întrerupt înainte să pună piesa
            if (next != null) routine = StartCoroutine(next);
        }

        // ------------------------------------------------------------------ privire și IK

        bool IsPatientLookingAtMe()
        {
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var target = head != null ? head.position : transform.position + Vector3.up;
            return Vector3.Angle(patientHead.forward, target - patientHead.position) < GazeAngle;
        }

        void LookAtPatient(float weight)
        {
            lookTarget = patientHead.position;
            lookWeight = weight;
        }

        /// <summary>
        /// Corecția fină a atingerii. IK-ul duce ÎNCHEIETURA la țintă, dar pe noi ne interesează DEGETUL, iar
        /// poziția lui față de încheietură depinde de animație. LateUpdate rulează după animație și IK, deci aici
        /// vedem unde a ajuns de fapt degetul: mutăm ținta încheieturii cu o parte din eroarea rămasă. În câteva
        /// cadre, degetul ajunge pe căsuță (buclă de reglaj, ca un regulator proporțional).
        /// </summary>
        void LateUpdate()
        {
            if (!touching) return;

            // 1. Niciun os al mâinii nu are voie mai aproape de tablă decât TouchGap (pe drum: plus o rezervă).
            //    „nearest” = distanța celui mai apropiat os față de limita permisă; negativ = a depășit-o.
            bool arrived = handReach >= 0.999f;
            float clearance = arrived ? 0f : TravelMargin;
            float nearest = float.MaxValue;
            foreach (var bone in handBones)
                nearest = Mathf.Min(nearest, Vector3.Dot(bone.position - touchPoint, boardNormal) - clearance);
            if (nearest < 0f)
            {
                handPushBack -= nearest;                        // de la cadrul următor: ținta retrasă cu cât a depășit
                handBones[0].position -= boardNormal * nearest; // în cadrul acesta: mutăm direct mâna (cu degetele ei) înapoi
            }
            else
            {
                // Când e loc, retragerea se relaxează încet: cel mult 30 cm/s și cel mult jumătate din spațiul liber.
                handPushBack = Mathf.Max(0f, handPushBack - Mathf.Min(nearest * 0.5f, 0.3f * Time.deltaTime));
            }

            // 2. Cu mâna ajunsă la căsuță, aducem vârful arătătorului pe punctul de atingere.
            if (fingertip != null && arrived)
            {
                var error = touchPoint - fingertip.position;
                float towardBoard = Vector3.Dot(error, boardNormal);       // negativ = mai are de mers spre tablă
                // Înaintăm spre tablă doar dacă niciun alt deget nu e deja la limită; altfel corectăm doar
                // lateral și pe înălțime.
                if ((handPushBack > 0f || nearest < 0.002f) && towardBoard < 0f) error -= boardNormal * towardBoard;
                // O fracțiune din eroare pe cadru, independentă de numărul de cadre pe secundă.
                handFine = Vector3.ClampMagnitude(handFine + error * (1f - Mathf.Exp(-22f * Time.deltaTime)), MaxCorrection);
            }
            handCorrection = handFine + boardNormal * handPushBack;
        }

        /// <summary>Apelat de Unity la fiecare frame (stratul Animator-ului are „IK Pass” activat).</summary>
        void OnAnimatorIK(int layerIndex)
        {
            // Doar capul și ochii (corp 0): rotirea trunchiului peste animație producea poziții ciudate.
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
