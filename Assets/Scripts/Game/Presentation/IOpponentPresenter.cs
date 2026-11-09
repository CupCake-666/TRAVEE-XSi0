using System;
using System.Collections;
using Travee.TicTacToe.Core;
using UnityEngine;

namespace Travee.TicTacToe
{
    /// <summary>
    /// Cum "arată" AI-ul în timpul jocului. În 2D nu are corp (<see cref="NoOpponent"/>);
    /// în 3D e personajul animat care vine la tablă.
    /// </summary>
    public interface IOpponentPresenter
    {
        /// <summary>Jucătorul e în meniu.</summary>
        void OnMenu();

        /// <summary>S-a schimbat partea afectată (personajul își poate schimba locul).</summary>
        void OnAffectedSideChanged(AffectedSide side);

        /// <summary>Începe un meci (după coin flip).</summary>
        void OnMatchStart();

        /// <summary>E rândul pacientului: AI-ul așteaptă.</summary>
        void OnPatientTurn();

        /// <summary>AI-ul "se gândește" cel puțin <paramref name="seconds"/> secunde.</summary>
        IEnumerator Think(float seconds);

        /// <summary>
        /// AI-ul pune piesa în <paramref name="cell"/>; <paramref name="place"/> se apelează în momentul
        /// în care piesa apare pe tablă.
        /// </summary>
        IEnumerator PlacePiece(int cell, Action place);

        /// <summary>Reacția la finalul meciului.</summary>
        void OnMatchEnd(MatchOutcome outcome, bool celebrateDraw);
    }

    /// <summary>AI fără corp (varianta 2D): doar pauza de gândire.</summary>
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
