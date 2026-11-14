using UnityEngine;
using UnityEngine.InputSystem;

namespace Travee.TicTacToe.World
{
    /// <summary>
    /// Simuleaza miscarea capului pe calculator: tii apasat click dreapta si misti mouse-ul ca sa privesti in jur,
    /// de jur imprejur (360°), ca un pacient care se intoarce in VR; Space priveste din nou inainte.
    /// In VR componenta nu se mai adauga, camera e miscata de ochelari.
    /// </summary>
    public sealed class DesktopHeadLook : MonoBehaviour
    {
        const float Sensitivity = 0.15f;   // grade pe pixel
        const float MaxPitch = 70f;        // sus / jos, fata de orizont (stanga / dreapta e nelimitat)

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
                yaw = Mathf.Repeat(yaw + delta.x + 180f, 360f) - 180f;     // ramane intre -180° si 180°
                // Limita e fata de orizont, nu fata de privirea de baza (care e deja putin aplecata).
                float basePitch = Mathf.DeltaAngle(0f, baseAngles.x);
                pitch = Mathf.Clamp(pitch - delta.y, -MaxPitch - basePitch, MaxPitch - basePitch);
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                yaw = pitch = 0f;

            transform.rotation = Quaternion.Euler(baseAngles.x + pitch, baseAngles.y + yaw, 0f);
        }
    }
}
