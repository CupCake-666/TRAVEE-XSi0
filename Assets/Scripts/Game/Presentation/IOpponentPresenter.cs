using System;
using System.Collections;
using Travee.TicTacToe.Core;
using UnityEngine;

namespace Travee.TicTacToe
{
    /// <summary>
    /// Cum "arata" AI-ul in timpul jocului. In 2D nu are corp (<see cref="NoOpponent"/>);
    /// in 3D e personajul animat care vine la tabla.
    /// </summary>
    public interface IOpponentPresenter
    {
        /// <summary>Jucatorul e in meniu.</summary>
        void OnMenu();

        /// <summary>S-a schimbat partea afectata (personajul isi poate schimba locul).</summary>
        void OnAffectedSideChanged(AffectedSide side);

        /// <summary>Incepe un meci (dupa coin flip).</summary>
        void OnMatchStart();

        /// <summary>E randul pacientului: AI-ul asteapta.</summary>
        void OnPatientTurn();

        /// <summary>AI-ul "se gandeste" cel putin <paramref name="seconds"/> secunde.</summary>
        IEnumerator Think(float seconds);

        /// <summary>
        /// AI-ul pune piesa in <paramref name="cell"/>; <paramref name="place"/> se apeleaza in momentul
        /// in care piesa apare pe tabla.
        /// </summary>
        IEnumerator PlacePiece(int cell, Action place);

        /// <summary>Reactia la finalul meciului.</summary>
        void OnMatchEnd(MatchOutcome outcome, bool celebrateDraw);
    }

    /// <summary>AI fara corp (varianta 2D): doar pauza de gandire.</summary>
    public sealed class NoOpponent : IOpponentPresenter
    {
        public void OnMenu() { }
        public void OnAffectedSideChanged(AffectedSide side) { }
        public void OnMatchStart() { }
        public void OnPatientTurn() { }

        public IEnumerator Think(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        public IEnumerator PlacePiece(int cell, Action place)
        {
            place();
            yield break;
        }

        public void OnMatchEnd(MatchOutcome outcome, bool celebrateDraw) { }
    }
}
