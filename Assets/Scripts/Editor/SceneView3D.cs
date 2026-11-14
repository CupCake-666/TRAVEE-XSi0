using UnityEditor;
using UnityEngine;

namespace Travee.TicTacToe.Editor
{
    /// <summary>
    /// Proiectul a pornit din sablonul 2D, asa ca fereastra Scene se deschidea in modul 2D: privire fixa din fata,
    /// fara perspectiva, totul pare plat. Aici o trecem pe 3D – o singura data, automat, prima oara cand editorul
    /// incarca acest script – si punem camera editorului intr-un unghi din care se vede scena.
    /// Dupa aceea alegerea e a ta: butonul „2D” din bara ferestrei Scene comuta oricand intre cele doua moduri.
    /// </summary>
    [InitializeOnLoad]      // clasa e initializata automat de editor la fiecare reincarcare a scripturilor
    public static class SceneView3D
    {
        // EditorPrefs = setari ale editorului, pastrate pe calculator (nu in proiect). Cheia include calea
        // proiectului, ca sa fie „o data per proiect”.
        static string Key => "Travee.XSi0.SceneView3D.v1." + Application.dataPath;

        static SceneView3D()
        {
            // delayCall = „dupa ce editorul termina de incarcat”: abia atunci exista ferestrele Scene.
            EditorApplication.delayCall += ApplyOnce;
        }

        static void ApplyOnce()
        {
            if (EditorPrefs.GetBool(Key, false)) return;
            if (Apply()) EditorPrefs.SetBool(Key, true);
        }

        [MenuItem("TRAVEE/Scena editabilă/Treci fereastra Scene pe 3D", priority = 101)]
        static void ApplyFromMenu()
        {
            if (!Apply())
                EditorUtility.DisplayDialog("Fereastra Scene", "Nu e deschisă nicio fereastră Scene. Deschide una din Window → General → Scene.", "OK");
        }

        /// <summary>Trece toate ferestrele Scene pe 3D. Intoarce false daca nu exista niciuna deschisa.</summary>
        public static bool Apply()
        {
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode3D;     // proiectul: 3D de acum inainte
            if (SceneView.sceneViews.Count == 0) return false;

            foreach (SceneView view in SceneView.sceneViews)
            {
                view.in2DMode = false;          // butonul „2D” dezactivat
                view.orthographic = false;      // perspectiva: obiectele indepartate par mai mici
                view.sceneLighting = true;      // lumina din scena, nu lumina „de lucru” a editorului
                // Privim spre zona de joc (tabla e la ~1 m in fata originii), putin de sus si din lateral.
                view.LookAt(new Vector3(0f, 1.1f, 1.5f), Quaternion.Euler(18f, 30f, 0f), 7f);
                view.Repaint();
            }
            return true;
        }
    }
}
