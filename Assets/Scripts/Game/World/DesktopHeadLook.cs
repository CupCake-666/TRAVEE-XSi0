using UnityEngine;
using UnityEngine.InputSystem;

namespace Travee.TicTacToe.World
{
    /// <summary>
    /// Simulează mișcarea capului pe calculator: ții apăsat click dreapta și miști mouse-ul ca să privești în jur,
    /// de jur împrejur (360°), ca un pacient care se întoarce în VR; Space privește din nou înainte.
    /// În VR componenta nu se mai adaugă, camera e mișcată de ochelari.
    /// </summary>
    public sealed class DesktopHeadLook : MonoBehaviour
    {
        const float Sensitivity = 0.15f;   // grade pe pixel
        const float MaxPitch = 70f;        // sus / jos, față de orizont (stânga / dreapta e nelimitat)

        Vector3 baseAngles;
        float yaw, pitch;

        void Start()
        {
            baseAngles = transform.eulerAngles;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue() * Sensitivity;
                yaw = Mathf.Repeat(yaw + delta.x + 180f, 360f) - 180f;     // rămâne între -180° și 180°
                // Limita e față de orizont, nu față de privirea de bază (care e deja puțin aplecată).
                float basePitch = Mathf.DeltaAngle(0f, baseAngles.x);
                pitch = Mathf.Clamp(pitch - delta.y, -MaxPitch - basePitch, MaxPitch - basePitch);
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                yaw = pitch = 0f;

            transform.rotation = Quaternion.Euler(baseAngles.x + pitch, baseAngles.y + yaw, 0f);
        }
    }
}
