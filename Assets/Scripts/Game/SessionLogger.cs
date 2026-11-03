using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Travee.TicTacToe.Core;
using UnityEngine;

namespace Travee.TicTacToe
{
    /// <summary>
    /// Salvează datele pentru analiză în două fișiere CSV (separator ',', cultură invariantă):
    ///  - games.csv: un rând pe meci
    ///  - moves.csv: un rând pe mutare (inclusiv timpul de reacție al pacientului)
    /// Locația: Application.persistentDataPath/TRAVEE/XSi0/
    /// </summary>
    public sealed class SessionLogger
    {
        const string GamesHeader =
            "session_id,game_id,start_time,difficulty,affected_side,patient_started,patient_mark,outcome," +
            "total_moves,patient_moves,duration_s,avg_patient_reaction_ms,patient_moves_on_affected_side";

        const string MovesHeader =
            "session_id,game_id,move_no,actor,mark,cell,row,col,column_side,on_affected_side,reaction_ms,time_since_start_ms";

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        readonly string gamesPath;
        readonly string movesPath;
        readonly string sessionId;
        readonly List<string> pendingMoves = new List<string>();

        int gameCounter;
        string gameId;
        DateTime gameStartTime;
        float gameStartRealtime;
        Match match;
        AffectedSide side;
        int patientMoves;
        int patientMovesOnAffectedSide;
        double totalReactionMs;

        public string Directory { get; }

        public SessionLogger()
        {
            sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv);
            Directory = Path.Combine(Application.persistentDataPath, "TRAVEE", "XSi0");
            System.IO.Directory.CreateDirectory(Directory);
            gamesPath = Path.Combine(Directory, "games.csv");
            movesPath = Path.Combine(Directory, "moves.csv");
            EnsureHeader(gamesPath, GamesHeader);
            EnsureHeader(movesPath, MovesHeader);
            Debug.Log($"[TRAVEE] Datele sesiunii {sessionId} se salvează în: {Directory}");
        }

        public void BeginGame(Match newMatch, AffectedSide affectedSide)
        {
            match = newMatch;
            side = affectedSide;
            gameCounter++;
            gameId = $"{sessionId}_{gameCounter:D3}";
            gameStartTime = DateTime.Now;
            gameStartRealtime = Time.realtimeSinceStartup;
            pendingMoves.Clear();
            patientMoves = 0;
            patientMovesOnAffectedSide = 0;
            totalReactionMs = 0;
        }

        /// <param name="reactionMs">Timpul de la începutul rândului pacientului până la selecție; null pentru AI.</param>
        public void LogMove(MoveRecord move, double? reactionMs)
        {
            if (match == null) return;

            int row = Board.Row(move.Cell);
            int col = Board.Column(move.Cell);
            string columnSide = col == 0 ? "Left" : col == 2 ? "Right" : "Center";
            bool onAffected = (side == AffectedSide.Left && col == 0) || (side == AffectedSide.Right && col == 2);

            if (move.ByPatient)
            {
                patientMoves++;
                if (onAffected) patientMovesOnAffectedSide++;
                totalReactionMs += reactionMs ?? 0;
            }

            double sinceStart = (Time.realtimeSinceStartup - gameStartRealtime) * 1000.0;
            pendingMoves.Add(string.Join(",",
                sessionId, gameId, move.Number.ToString(Inv),
                move.ByPatient ? "Patient" : "AI", move.Mark.ToString(),
                move.Cell.ToString(Inv), row.ToString(Inv), col.ToString(Inv),
                columnSide, onAffected ? "1" : "0",
                reactionMs.HasValue ? reactionMs.Value.ToString("F0", Inv) : "",
                sinceStart.ToString("F0", Inv)));
        }

        /// <summary>Închide meciul curent. Dacă meciul nu s-a terminat (pacientul a ieșit), se loghează ca "Abandoned".</summary>
        public void EndGame()
        {
            if (match == null) return;

            string outcome = match.Outcome == MatchOutcome.InProgress ? "Abandoned" : match.Outcome.ToString();
            double duration = Time.realtimeSinceStartup - gameStartRealtime;
            string avgReaction = patientMoves > 0 ? (totalReactionMs / patientMoves).ToString("F0", Inv) : "";

            string gameRow = string.Join(",",
                sessionId, gameId, gameStartTime.ToString("yyyy-MM-ddTHH:mm:ss", Inv),
                ((int)match.Level).ToString(Inv), side.ToString(),
                match.PatientStarts ? "1" : "0", match.PatientMark.ToString(), outcome,
                match.Moves.Count.ToString(Inv), patientMoves.ToString(Inv),
                duration.ToString("F2", Inv), avgReaction, patientMovesOnAffectedSide.ToString(Inv));

            try
            {
                File.AppendAllText(gamesPath, gameRow + Environment.NewLine, Encoding.UTF8);
                if (pendingMoves.Count > 0)
                    File.AppendAllText(movesPath, string.Join(Environment.NewLine, pendingMoves) + Environment.NewLine, Encoding.UTF8);
            }
            catch (IOException e)
            {
                Debug.LogError($"[TRAVEE] Nu s-au putut salva datele (fișierul e deschis în Excel?): {e.Message}");
            }

            match = null;
            pendingMoves.Clear();
        }

        static void EnsureHeader(string path, string header)
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                File.WriteAllText(path, header + Environment.NewLine, Encoding.UTF8);
        }
    }
}
