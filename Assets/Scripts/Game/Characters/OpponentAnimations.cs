using System.Collections.Generic;

namespace Travee.TicTacToe.Characters
{
    /// <summary>
    /// Legatura dintre „rolul” unei animatii in joc si fisierul Mixamo din care vine.
    /// Numele rolului e si numele starii din Animator Controller (generat de editor).
    /// Ca sa schimbi o animatie: descarci alt FBX in Assets/Art/Character/Animations si ii pui numele aici.
    /// </summary>
    public static class OpponentAnimations
    {
        public const string Idle = "Idle";
        public const string Walk = "Walk";
        public const string Wave = "Wave";
        public const string Think = "Think";
        public const string LookAround = "LookAround";
        public const string PlaceA = "PlaceA";
        public const string PlaceB = "PlaceB";
        public const string PlaceC = "PlaceC";
        public const string Clap = "Clap";
        public const string Victory = "Victory";
        public const string Shrug = "Shrug";

        /// <summary>Rol → numele fisierului FBX (fara extensie).</summary>
        public static readonly IReadOnlyDictionary<string, string> Files = new Dictionary<string, string>
        {
            { Idle, "Happy Idle" },
            { Walk, "Happy Walk" },
            { Wave, "Waving" },
            { Think, "Thinking" },
            { LookAround, "Look Around" },
            { PlaceA, "Button Pushing" },
            { PlaceB, "Pointing" },
            { PlaceC, "Reaching Out" },
            { Clap, "Clapping" },
            { Victory, "Victory Idle" },
            { Shrug, "Shrugging" },
        };

        /// <summary>
        /// Variantele pentru „pune piesa” – se alege una la intamplare, ca sa nu fie repetitiv.
        /// (Pointing arata in gol, asa ca nu il folosim; mana e oricum dusa la casuta prin IK.)
        /// </summary>
        public static readonly string[] PlaceVariants = { PlaceA, PlaceC };

        /// <summary>In ce moment al animatiei (0..1) apare piesa pe tabla, pentru fiecare varianta.</summary>
        public static float PlaceHitTime(string role)
        {
            switch (role)
            {
                case PlaceB: return 0.40f;
                case PlaceC: return 0.50f;
                default: return 0.45f;
            }
        }
    }
}
