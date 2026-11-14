using System;

namespace Travee.TicTacToe.Core
{
    /// <summary>
    /// Parametrii reglabili ai nivelurilor de dificultate. E [Serializable] ca sa poata fi editat din Inspector.
    /// </summary>
    [Serializable]
    public class DifficultySettings
    {
        // Sansa ca pacientul sa castige coin flip-ul (si deci sa inceapa cu X).
        public float level1PatientStartChance = 1.00f;
        public float level2PatientStartChance = 0.75f;
        public float level3PatientStartChance = 0.60f;
        public float level4PatientStartChance = 0.50f;

        // Nivel 2: cat de mult (in probabilitate de victorie a pacientului) are voie AI-ul sa "sacrifice"
        // ca sa faca mutari care par defensive (ex. blocheaza o linie). 0 = la fel de ajutator ca nivelul 1.
        public float level2DefenseSlack = 0.25f;

        // Nivel 3: probabilitatea ca o mutare a AI-ului sa fie o greseala (altfel joaca optim).
        public float level3MistakeChance = 0.45f;

        // Nivel 4: probabilitatea, la fiecare mutare, ca AI-ul sa faca o greseala subtila
        // (maximum una pe meci, si doar una care poate fi pedepsita printr-un singur raspuns precis).
        public float level4MistakeChance = 0.20f;
        public int level4MaxPunishingReplies = 1;

        // Nivelurile 1-2: dintre mutarile la fel de bune, AI-ul prefera sa ocupe partea sanatoasa,
        // lasand casutele de pe partea afectata pentru pacient.
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
        /// <summary>True daca pacientul castiga aruncarea monedei (si incepe cu X).</summary>
        public static bool PatientStarts(DifficultyLevel level, DifficultySettings settings, Random rng)
        {
            return rng.NextDouble() < settings.PatientStartChance(level);
        }
    }
}
