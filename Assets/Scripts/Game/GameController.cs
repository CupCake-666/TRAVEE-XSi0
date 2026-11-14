using System.Collections;
using System.Collections.Generic;
using Travee.TicTacToe.Audio;
using Travee.TicTacToe.Core;
using Travee.TicTacToe.Core.AI;
using Travee.TicTacToe.UI;
using UnityEngine;

namespace Travee.TicTacToe
{
    /// <summary>
    /// Fluxul jocului: Meniu → Coin flip → Meci (pacient vs AI) → Rezultat.
    /// Nu stie nimic despre mouse, VR, 2D sau 3D: primeste selectiile prin <see cref="ICellInputSource"/>,
    /// afiseaza tabla prin <see cref="IBoardView"/> si „joaca” personajul AI prin <see cref="IOpponentPresenter"/>.
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
        IBoardView board;
        IOpponentPresenter opponent;

        Match match;
        DifficultyLevel currentLevel;
        bool awaitingPatient;
        int selectedCell = -1;

        public void Init(DifficultySettings settings, AffectedSide affectedSide, float aiMoveDelay, bool logData,
            int randomSeed, MenuScreen menu, CoinFlipScreen coinFlip, GameScreen game, ResultScreen result,
            IBoardView board, IOpponentPresenter opponent = null)
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
            this.board = board;
            this.opponent = opponent ?? new NoOpponent();
            this.opponent.OnAffectedSideChanged(affectedSide);

            menu.LevelChosen += StartGame;
            menu.SideToggleRequested += ToggleAffectedSide;
            game.MenuRequested += ShowMenu;
            result.PlayAgainRequested += () => StartGame(currentLevel);
            result.MenuRequested += ShowMenu;

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
            opponent.OnMenu();
        }

        void ToggleAffectedSide()
        {
            affectedSide = affectedSide == AffectedSide.Left ? AffectedSide.Right : AffectedSide.Left;
            menu.SetAffectedSide(affectedSide);
            opponent.OnAffectedSideChanged(affectedSide);
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
            board.ResetBoard();
            opponent.OnMenu();

            bool patientStarts = CoinFlip.PatientStarts(level, settings, rng);
            coinFlip.gameObject.SetActive(true);
            yield return coinFlip.Play(patientStarts);
            coinFlip.gameObject.SetActive(false);

            match = new Match(level, patientStarts);
            var ai = AIFactory.Create(level, settings, affectedSide, rng);
            ai.OnNewGame();
            logger?.BeginGame(match, affectedSide);
            game.SetPlayers(match.PatientMark);
            opponent.OnMatchStart();

            while (!match.IsOver)
            {
                if (match.IsPatientTurn)
                {
                    game.SetStatus("Rândul tău – alege o căsuță");
                    selectedCell = -1;
                    opponent.OnPatientTurn();
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
                    int cell = ai.ChooseMove(match.Board.Clone(), match.AIMark);
                    yield return opponent.Think(aiMoveDelay);
                    yield return opponent.PlacePiece(cell, () => Apply(cell, null));
                }
            }

            logger?.EndGame();
            match.Board.Winner(out int[] line);
            board.HighlightLine(line);
            game.SetStatus("");
            opponent.OnMatchEnd(match.Outcome, IsGentle(level));
            yield return new WaitForSeconds(1.2f);

            ShowResult(match.Outcome, level);
            match = null;
        }

        void Apply(int cell, double? reactionMs)
        {
            match.Play(cell);
            var move = match.Moves[match.Moves.Count - 1];
            board.SetMark(cell, move.Mark);
            // reactionMs exista doar pentru mutarile pacientului: asa stim al cui sunet sa redam.
            Sfx.Play(reactionMs.HasValue ? Sound.PlacePatient : Sound.PlaceAI);
            logger?.LogMove(move, reactionMs);
        }

        void OnCellSelected(int cell)
        {
            if (!awaitingPatient || match == null || !match.IsPatientTurn) return;
            if (!match.Board.IsEmpty(cell)) return; // casuta ocupata: ignoram, pacientul mai incearca
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
            bool gentleLevel = IsGentle(level);
            switch (outcome)
            {
                case MatchOutcome.PatientWin:
                    Sfx.Play(Sound.Win);
                    result.Show("Felicitări, ai câștigat!", "Ai jucat excelent.", Theme.X);
                    break;
                case MatchOutcome.Draw when gentleLevel:
                    // La nivelurile 1-2 remiza e prezentata tot ca un succes.
                    Sfx.Play(Sound.Win);
                    result.Show("Bravo, ai jucat foarte bine!", "Ai fost foarte aproape de victorie – mai încearcă o dată!", Theme.X);
                    break;
                case MatchOutcome.Draw:
                    Sfx.Play(Sound.Draw);
                    result.Show("Remiză", level == DifficultyLevel.Level4
                        ? "Ai ținut piept AI-ului! Pentru victorie e nevoie de o strategie aproape perfectă."
                        : "Un meci echilibrat. Mai încerci?", Theme.Text);
                    break;
                default:
                    Sfx.Play(Sound.Lose);
                    result.Show("AI-ul a câștigat de data aceasta", "Nu-i nimic – fiecare meci este un exercițiu bun!", Theme.O);
                    break;
            }
        }

        /// <summary>Nivelurile 1-2: remiza e prezentata ca un succes.</summary>
        static bool IsGentle(DifficultyLevel level) =>
            level == DifficultyLevel.Level1 || level == DifficultyLevel.Level2;
    }
}
