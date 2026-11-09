using System;
using System.Collections;
using System.Collections.Generic;
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
    /// își duce mâna la căsuță (IK) și pune piesa, apoi se întoarce la loc. Dacă pacientul se uită spre el
    /// pe rândul pacientului, îi face cu mâna.
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

        readonly Dictionary<string, float> lengths = new Dictionary<string, float>();
        Animator animator;
        Room3D room;
        Board3DView board;
        Transform patientHead;
        OpponentPlacement placement;
        Vector3 home;
        Coroutine routine;
        bool entering;

        // IK: unde își duce mâna dreaptă și unde se uită (valori folosite în OnAnimatorIK).
        float handWeight;
        Vector3 handTarget;
        float lookWeight;
        Vector3 lookTarget;

        public static OpponentCharacter Create(GameObject model, RuntimeAnimatorController controller, Room3D room,
            Board3DView board, OpponentPlacement placement, float targetHeight = 1.4f)
        {
            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null || animator.gameObject != model)
                Debug.LogWarning("[TRAVEE] Animator-ul nu e pe rădăcina modelului; IK-ul s-ar putea să nu funcționeze.");

            var character = model.AddComponent<OpponentCharacter>();
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

        // ------------------------------------------------------------------ IOpponentPresenter

        /// <summary>Intrarea în scenă: vine mergând din lateral până la locul lui și face cu mâna.</summary>
        public void Enter() => Run(EnterRoutine());

        public void OnMenu()
        {
            if (entering) return;               // nu întrerupem intrarea în scenă
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
            board.SetSeeThrough(true);
            yield return WalkTo(room.OpponentAtBoard);

            string role = A.PlaceVariants[UnityEngine.Random.Range(0, A.PlaceVariants.Length)];
            float length = Length(role);
            // Personajul e în spatele tablei: mâna atinge căsuța din spate (tabla e transparentă acum).
            handTarget = board.CellWorldPosition(cell) - board.transform.up * 0.03f;
            lookTarget = handTarget;
            lookWeight = 0.9f;

            if (length <= 0f)                    // animația lipsește: punem piesa direct
            {
                place();
                yield break;
            }

            float speed = Mathf.Max(1f, length / MaxPlaceDuration);
            float duration = length / speed;
            float hit = duration * A.PlaceHitTime(role);
            animator.speed = speed;
            Play(role);

            // Mâna se duce la căsuță (IK crește de la 0 la 1), piesa apare, apoi mâna revine (IK scade la 0).
            for (float t = 0; t < hit; t += Time.deltaTime)
            {
                handWeight = Mathf.SmoothStep(0f, 1f, t / hit);
                yield return null;
            }
            place();
            float rest = duration - hit;
            for (float t = 0; t < rest; t += Time.deltaTime)
            {
                handWeight = Mathf.SmoothStep(1f, 0f, t / rest);
                yield return null;
            }
            handWeight = 0f;
            animator.speed = 1f;
            Play(A.Idle);
            // Întoarcerea la loc o face rutina următoare (rândul pacientului sau finalul meciului).
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
            yield return Hold(A.Wave, WaveDuration);
        }

        IEnumerator GoHome(bool thenWave = false)
        {
            yield return WalkTo(home);
            if (thenWave) yield return Hold(A.Wave, WaveDuration);
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
                    yield return Hold(A.Wave, WaveDuration);   // pacientul s-a uitat la el → îi face cu mâna
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
                    Play(A.Clap);               // pacientul a câștigat → aplauze
                    break;
                case MatchOutcome.Draw when celebrateDraw:
                    Play(A.Clap);
                    break;
                case MatchOutcome.Draw:
                    yield return OneShot(A.Shrug);
                    break;
                default:
                    Play(A.Victory);            // discret și prietenos, nu triumfător
                    break;
            }
        }

        /// <summary>Merge până la <paramref name="target"/> (se întoarce spre el, merge, apoi se întoarce spre pacient).</summary>
        IEnumerator WalkTo(Vector3 target)
        {
            if ((transform.position - target).sqrMagnitude > 0.0004f)
            {
                lookWeight = 0f;
                Play(A.Walk);
                yield return TurnTo(Quaternion.LookRotation(target - transform.position));
                // Când pleacă de la tablă, tabla redevine opacă.
                if ((transform.position - room.OpponentAtBoard).sqrMagnitude < 0.01f && target != room.OpponentAtBoard)
                    board.SetSeeThrough(false);

                while ((transform.position - target).sqrMagnitude > 0.0004f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, target, WalkSpeed * Time.deltaTime);
                    yield return null;
                }
                transform.position = target;
                Play(A.Idle);
            }
            yield return TurnTo(FacePatientFrom(target));
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

        /// <summary>Apelat de Unity la fiecare frame (stratul Animator-ului are „IK Pass” activat).</summary>
        void OnAnimatorIK(int layerIndex)
        {
            // Doar capul și ochii (corp 0): rotirea trunchiului peste animație producea poziții ciudate.
            animator.SetLookAtWeight(lookWeight, 0f, 0.7f, 1f, 0.6f);
            animator.SetLookAtPosition(lookTarget);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, handTarget);
        }

        // ------------------------------------------------------------------ utilitare

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
