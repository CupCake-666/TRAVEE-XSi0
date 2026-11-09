using UnityEngine;
using UnityEngine.InputSystem;

namespace Travee.TicTacToe.World
{
    /// <summary>
    /// Simulează mișcarea capului pe calculator: ții apăsat click dreapta și miști mouse-ul ca să privești în jur;
    /// Space privește din nou înainte. În VR componenta nu se mai adaugă, camera e mișcată de ochelari.
    /// </summary>
    public sealed class DesktopHeadLook : MonoBehaviour
    {
        const float Sensitivity = 0.15f;   // grade pe pixel
        const float MaxYaw = 75f;          // stânga / dreapta
        const float MaxPitch = 40f;        // sus / jos

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
                yaw = Mathf.Clamp(yaw + delta.x, -MaxYaw, MaxYaw);
                pitch = Mathf.Clamp(pitch - delta.y, -MaxPitch, MaxPitch);
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                yaw = pitch = 0f;

            transform.rotation = Quaternion.Euler(baseAngles.x + pitch, baseAngles.y + yaw, 0f);
        }
    }
}
