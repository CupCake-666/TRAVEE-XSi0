using System;

namespace Travee.TicTacToe.Core
{
    /// <summary>
    /// Parametrii reglabili ai nivelurilor de dificultate. E [Serializable] ca să poată fi editat din Inspector.
    /// </summary>
    [Serializable]
    public class DifficultySettings
    {
        // Șansa ca pacientul să câștige coin flip-ul (și deci să înceapă cu X).
        public float level1PatientStartChance = 1.00f;
        public float level2PatientStartChance = 0.75f;
        public float level3PatientStartChance = 0.60f;
        public float level4PatientStartChance = 0.50f;

        // Nivel 2: cât de mult (în probabilitate de victorie a pacientului) are voie AI-ul să "sacrifice"
        // ca să facă mutări care par defensive (ex. blochează o linie). 0 = la fel de ajutător ca nivelul 1.
        public float level2DefenseSlack = 0.25f;

        // Nivel 3: probabilitatea ca o mutare a AI-ului să fie o greșeală (altfel joacă optim).
        public float level3MistakeChance = 0.45f;

        // Nivel 4: probabilitatea, la fiecare mutare, ca AI-ul să facă o greșeală subtilă
        // (maximum una pe meci, și doar una care poate fi pedepsită printr-un singur răspuns precis).
        public float level4MistakeChance = 0.20f;
        public int level4MaxPunishingReplies = 1;

        // Nivelurile 1-2: dintre mutările la fel de bune, AI-ul preferă să ocupe partea sănătoasă,
        // lăsând căsuțele de pe partea afectată pentru pacient.
        public bool steerTowardAffectedSide = true;

        public float PatientStartChance(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Level1: return level1PatientStartChance;
                case DifficultyLevel.Level2: return level2PatientStartChance;
                case DifficultyLevel.Level3: return level3PatientStartChance;
                default: return level4PatientStartChance;
            }
        }
    }

    public static class CoinFlip
    {
        /// <summary>True dacă pacientul câștigă aruncarea monedei (și începe cu X).</summary>
        public static bool PatientStarts(DifficultyLevel level, DifficultySettings settings, Random rng)
        {
            return rng.NextDouble() < settings.PatientStartChance(level);
        }
    }
}
