#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation
{
    public sealed class MorseTestDriver : MonoBehaviour
    {
        [SerializeField] private float holdThreshold = 0.3f;

        private MorseDecoder _decoder = new();
        private float _pressStart;
        private bool  _pressing;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.spaceKey.wasPressedThisFrame)
            {
                _pressStart = Time.unscaledTime;
                _pressing   = true;
            }

            if (kb.spaceKey.wasReleasedThisFrame && _pressing)
            {
                bool isLong = (Time.unscaledTime - _pressStart) >= holdThreshold;
                if (isLong) _decoder.InputDash();
                else        _decoder.InputDot();
                _pressing = false;
            }
        }

        private void OnGUI()
        {
            var e = Event.current;

            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    { _decoder.Confirm(); e.Use(); }
                else if (e.keyCode == KeyCode.Backspace)
                    { _decoder.Backspace(); e.Use(); }
                else if (e.keyCode == KeyCode.Escape)
                    { _decoder.Reset(); e.Use(); }
            }

            if (e.type != EventType.Repaint) return;

            var big   = new GUIStyle(GUI.skin.label) { fontSize = 28, wordWrap = false };
            var mid   = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = false };
            var small = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = false };

            float elapsed    = _pressing ? Time.unscaledTime - _pressStart : 0f;
            string indicator = _pressing
                ? (elapsed >= holdThreshold ? "[ — ] suelta para guión" : "[ · ] suelta para punto")
                : "";

            GUI.Label(new Rect(30, 30,  1200, 40), $"Secuencia: {_decoder.CurrentSequence}", big);
            GUI.Label(new Rect(30, 80,  1200, 35), indicator, mid);
            GUI.Label(new Rect(30, 130, 1200, 40), $"Palabra:   {_decoder.GetWord()}", big);
            GUI.Label(new Rect(30, 195, 1200, 30), "SPACE tap=·  SPACE hold=—  ENTER=Confirmar  BACKSPACE=Borrar  ESC=Reset", small);
        }
    }
}
