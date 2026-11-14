using System;
using System.Collections;
using Travee.TicTacToe.Audio;
using Travee.TicTacToe.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Travee.TicTacToe.UI
{
    // Ecranele (MenuScreen, CoinFlipScreen, GameScreen, ResultScreen) au fiecare fisierul lui: o componenta se
    // poate salva in scena doar daca fisierul poarta numele clasei.

    public static class Texts
    {
        public static string LevelName(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Level1: return "Nivel 1 – Foarte ușor";
                case DifficultyLevel.Level2: return "Nivel 2 – Ușor";
                case DifficultyLevel.Level3: return "Nivel 3 – Mediu";
                default: return "Nivel 4 – Provocare";
            }
        }

        public static string SideName(AffectedSide side) => side == AffectedSide.Left ? "Stânga" : "Dreapta";
    }
}
