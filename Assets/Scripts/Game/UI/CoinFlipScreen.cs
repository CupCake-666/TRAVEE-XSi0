using System.Collections;
using Travee.TicTacToe.Audio;
using Travee.TicTacToe.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Travee.TicTacToe.UI
{
    /// <summary>Aruncarea monedei: cine incepe. Obiectele pot fi in scena; animatia si textele raman in cod.</summary>
    public sealed class CoinFlipScreen : MonoBehaviour
    {
        [Header("Legături spre obiectele din scenă")]
        [SerializeField] Text title;
        [SerializeField] Image coin;
        [SerializeField] Text coinLabel;
        [SerializeField] Text resultLabel;

        [Header("Culorile monedei")]
        [SerializeField] Color patientFace = new Color(0.25f, 0.55f, 0.90f);
        [SerializeField] Color aiFace = new Color(0.95f, 0.62f, 0.25f);

        public static CoinFlipScreen Create(Transform parent)
        {
            var root = UIFactory.Panel("CoinFlipScreen", parent, Theme.Overlay);
            root.rectTransform.Stretch();
            var screen = root.gameObject.AddComponent<CoinFlipScreen>();
            screen.Build();
            return screen;
        }

        void Build()
        {
            // Textele de aici sunt doar exemple, ca sa ai ce vedea in editor; jocul le inlocuieste (vezi Play).
            title = UIFactory.Label("Title", transform, "Aruncăm moneda…", 60, Theme.Text, FontStyle.Bold);
            title.rectTransform.Centered(new Vector2(0, 330), new Vector2(1400, 90));

            coin = UIFactory.Panel("Coin", transform, patientFace);
            coin.rectTransform.Centered(new Vector2(0, 20), new Vector2(340, 340));

            coinLabel = UIFactory.Label("Face", coin.transform, "TU", 110, Color.white, FontStyle.Bold);
            coinLabel.rectTransform.Stretch();

            resultLabel = UIFactory.Label("Result", transform, "Începi tu – tu ești X", 56, Theme.Text, FontStyle.Bold);
            resultLabel.rectTransform.Centered(new Vector2(0, -300), new Vector2(1400, 90));
        }

        /// <summary>Imaginea rotunda a monedei (un asset). Daca lipseste, se genereaza una din cod la pornire.</summary>
        public void SetCoinSprite(Sprite sprite)
        {
            if (coin != null) coin.sprite = sprite;
        }

        /// <summary>Animatia aruncarii monedei; rezultatul e deja decis de <see cref="CoinFlip"/>.</summary>
        public IEnumerator Play(bool patientStarts, float resultHoldSeconds = 1.6f)
        {
            if (coin.sprite == null) coin.sprite = UIFactory.Circle;
            title.text = "Aruncăm moneda…";
            resultLabel.text = "";

            int halfTurns = Random.Range(7, 10);
            // Fata initiala e aleasa astfel incat dupa halfTurns intoarceri sa ajunga la rezultatul corect.
            bool showingPatient = (halfTurns % 2 == 0) == patientStarts;
            SetFace(showingPatient);

            for (int i = 0; i < halfTurns; i++)
            {
                float duration = Mathf.Lerp(0.06f, 0.22f, i / (float)(halfTurns - 1)); // incetineste spre final
                yield return Scale(1f, 0f, duration);
                showingPatient = !showingPatient;
                SetFace(showingPatient);
                Sfx.Play(Sound.CoinTick, 0.7f);
                yield return Scale(0f, 1f, duration);
            }

            title.text = patientStarts ? "Ai câștigat aruncarea!" : "AI-ul a câștigat aruncarea";
            resultLabel.text = patientStarts ? "Începi tu – tu ești X" : "Începe AI-ul – tu ești 0";
            Sfx.Play(Sound.CoinResult);
            yield return new WaitForSeconds(resultHoldSeconds);
        }

        void SetFace(bool patient)
        {
            coin.color = patient ? patientFace : aiFace;
            coinLabel.text = patient ? "TU" : "AI";
        }

        IEnumerator Scale(float from, float to, float duration)
        {
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                coin.transform.localScale = new Vector3(Mathf.Lerp(from, to, t / duration), 1f, 1f);
                yield return null;
            }
            coin.transform.localScale = new Vector3(to, 1f, 1f);
        }
    }
}
