using System.Collections.Generic;

namespace Travee.TicTacToe.Characters
{
    /// <summary>
    /// Legătura dintre „rolul” unei animații în joc și fișierul Mixamo din care vine.
    /// Numele rolului e și numele stării din Animator Controller (generat de editor).
    /// Ca să schimbi o animație: descarci alt FBX în Assets/Art/Character/Animations și îi pui numele aici.
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

        /// <summary>Rol → numele fișierului FBX (fără extensie).</summary>
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
        /// Variantele pentru „pune piesa” – se alege una la întâmplare, ca să nu fie repetitiv.
        /// (Pointing arată în gol, așa că nu îl folosim; mâna e oricum dusă la căsuță prin IK.)
        /// </summary>
        public static readonly string[] PlaceVariants = { PlaceA, PlaceC };

        /// <summary>În ce moment al animației (0..1) apare piesa pe tablă, pentru fiecare variantă.</summary>
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
