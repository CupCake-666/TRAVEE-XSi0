using System.Collections;
using System.Collections.Generic;
using Travee.TicTacToe.Core;
using Travee.TicTacToe.Core.AI;
using Travee.TicTacToe.UI;
using UnityEngine;

namespace Travee.TicTacToe
{
    /// <summary>
    /// Fluxul jocului: Meniu → Coin flip → Meci (pacient vs AI) → Rezultat.
    /// Nu știe nimic despre mouse sau VR: primește selecțiile prin <see cref="ICellInputSource"/>.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        readonly List<ICellInputSource> inputSources = new List<ICellInputSource>();

        DifficultySettings settings;
        AffectedSide affectedSide;
        float aiMoveDelay;
        System.Random rng;
        SessionLogger logger;

        MenuScreen menu;
        CoinFlipScreen coinFlip;
        GameScreen game;
        ResultScreen result;

        Match match;
        DifficultyLevel currentLevel;
        bool awaitingPatient;
        int selectedCell = -1;

        public void Init(DifficultySettings settings, AffectedSide affectedSide, float aiMoveDelay, bool logData,
            int randomSeed, MenuScreen menu, CoinFlipScreen coinFlip, GameScreen game, ResultScreen result)
        {
            this.settings = settings;
            this.affectedSide = affectedSide;
            this.aiMoveDelay = aiMoveDelay;
            rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
            logger = logData ? new SessionLogger() : null;

            this.menu = menu;
            this.coinFlip = coinFlip;
            this.game = game;
            this.result = result;

            menu.LevelChosen += StartGame;
            menu.SideToggleRequested += ToggleAffectedSide;
            game.MenuRequested += ShowMenu;
            result.PlayAgainRequested += () => StartGame(currentLevel);
            result.MenuRequested += ShowMenu;

            AddInputSource(game.Board);
            ShowMenu();
        }

        public void AddInputSource(ICellInputSource source)
        {
            if (inputSources.Contains(source)) return;
            inputSources.Add(source);
            source.CellSelected += OnCellSelected;
            source.InputEnabled = awaitingPatient;
        }

        public void RemoveInputSource(ICellInputSource source)
        {
            if (!inputSources.Remove(source)) return;
            source.CellSelected -= OnCellSelected;
            source.InputEnabled = false;
        }

        void ShowMenu()
        {
            AbortCurrentMatch();
            menu.SetAffectedSide(affectedSide);
            menu.gameObject.SetActive(true);
            coinFlip.gameObject.SetActive(false);
            game.gameObject.SetActive(false);
            result.gameObject.SetActive(false);
        }

        void ToggleAffectedSide()
        {
            affectedSide = affectedSide == AffectedSide.Left ? AffectedSide.Right : AffectedSide.Left;
            menu.SetAffectedSide(affectedSide);
        }

        void StartGame(DifficultyLevel level)
        {
            AbortCurrentMatch();
            currentLevel = level;
            StartCoroutine(MatchRoutine(level));
        }

        IEnumerator MatchRoutine(DifficultyLevel level)
        {
            menu.gameObject.SetActive(false);
            result.gameObject.SetActive(false);
            game.gameObject.SetActive(true);
            game.Setup(level);

            bool patientStarts = CoinFlip.PatientStarts(level, settings, rng);
            coinFlip.gameObject.SetActive(true);
            yield return coinFlip.Play(patientStarts);
            coinFlip.gameObject.SetActive(false);

            match = new Match(level, patientStarts);
            var ai = AIFactory.Create(level, settings, affectedSide, rng);
            ai.OnNewGame();
            logger?.BeginGame(match, affectedSide);
            game.SetPlayers(match.PatientMark);

            while (!match.IsOver)
            {
                if (match.IsPatientTurn)
                {
                    game.SetStatus("Rândul tău – alege o căsuță");
                    selectedCell = -1;
                    float turnStart = Time.realtimeSinceStartup;
                    SetAwaitingPatient(true);
                    while (selectedCell < 0) yield return null;
                    SetAwaitingPatient(false);

                    double reactionMs = (Time.realtimeSinceStartup - turnStart) * 1000.0;
                    Apply(selectedCell, reactionMs);
                }
                else
                {
                    game.SetStatus("AI-ul se gândește…");
                    yield return new WaitForSeconds(aiMoveDelay);
                    Apply(ai.ChooseMove(match.Board.Clone(), match.AIMark), null);
                }
            }

            logger?.EndGame();
            match.Board.Winner(out int[] line);
            game.Board.HighlightLine(line);
            game.SetStatus("");
            yield return new WaitForSeconds(1.2f);

            ShowResult(match.Outcome, level);
            match = null;
        }

        void Apply(int cell, double? reactionMs)
        {
            match.Play(cell);
            var move = match.Moves[match.Moves.Count - 1];
            game.Board.SetMark(cell, move.Mark);
            logger?.LogMove(move, reactionMs);
        }

        void OnCellSelected(int cell)
        {
            if (!awaitingPatient || match == null || !match.IsPatientTurn) return;
            if (!match.Board.IsEmpty(cell)) return; // căsuță ocupată: ignorăm, pacientul mai încearcă
            selectedCell = cell;
        }

        void SetAwaitingPatient(bool value)
        {
            awaitingPatient = value;
            foreach (var source in inputSources) source.InputEnabled = value;
        }

        void AbortCurrentMatch()
        {
            StopAllCoroutines();
            SetAwaitingPatient(false);
            if (match != null)
            {
                logger?.EndGame(); // meci neterminat → "Abandoned"
                match = null;
            }
        }

        void ShowResult(MatchOutcome outcome, DifficultyLevel level)
        {
            bool gentleLevel = level == DifficultyLevel.Level1 || level == DifficultyLevel.Level2;
            switch (outcome)
            {
                case MatchOutcome.PatientWin:
                    result.Show("Felicitări, ai câștigat!", "Ai jucat excelent.", Theme.X);
                    break;
                case MatchOutcome.Draw when gentleLevel:
                    // La nivelurile 1-2 remiza e prezentată tot ca un succes.
                    result.Show("Bravo, ai jucat foarte bine!", "Ai fost foarte aproape de victorie – mai încearcă o dată!", Theme.X);
                    break;
                case MatchOutcome.Draw:
                    result.Show("Remiză", level == DifficultyLevel.Level4
                        ? "Ai ținut piept AI-ului! Pentru victorie e nevoie de o strategie aproape perfectă."
                        : "Un meci echilibrat. Mai încerci?", Theme.Text);
                    break;
                default:
                    result.Show("AI-ul a câștigat de data aceasta", "Nu-i nimic – fiecare meci este un exercițiu bun!", Theme.O);
                    break;
            }
        }
    }
}
