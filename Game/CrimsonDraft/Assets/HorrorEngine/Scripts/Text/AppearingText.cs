using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class AppearingText : MonoBehaviour
    {
        [SerializeField] private bool m_AppearOnEnable = false;
        [SerializeField] private AudioClip[] m_CharacterClips;
        [SerializeField] private float m_TimePerCharacter = 0.1f;
        [SerializeField] private float m_TimePerSound = 0.1f;
        [SerializeField] private bool m_UseUnscaledTime;
        [SerializeField] private AudioSource m_AudioSource;

        public UnityEvent OnStartShowing;
        public UnityEvent OnShowAll;

        private TMPro.TextMeshProUGUI m_Text;

        private float m_CurrentTime;
        private float m_AudioTime;
        private string m_ParsedText;

        public bool HasShownAll { get { return !enabled; } }

        // --------------------------------------------------------------------

        private void Awake()
        {
            if (!m_AppearOnEnable)
                enabled = false;

            m_Text = GetComponent<TMPro.TextMeshProUGUI>();
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            if (m_AppearOnEnable)
            {
                Show(m_Text.text);
            }
        }

        // --------------------------------------------------------------------

        public void Show()
        {
            Show(m_Text.text);
        }

        // --------------------------------------------------------------------

        public void Show(string text)
        {
            m_CurrentTime = 0;
            
            m_Text.text = text;
            m_Text.maxVisibleCharacters = 0;

            OnStartShowing?.Invoke();

            enabled = true;
        }

        // --------------------------------------------------------------------

        public void Clear()
        {
            m_Text.text = "";
            enabled = false;
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            if (m_Text.maxVisibleCharacters == 0)
                m_ParsedText = m_Text.GetParsedText();

            if (m_ParsedText.Length > 0)
            {
                m_CurrentTime += m_UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                if (m_CurrentTime > m_TimePerCharacter)
                {
                    m_CurrentTime = 0f;

                    m_Text.maxVisibleCharacters = m_Text.maxVisibleCharacters + 1;
                    if (m_Text.maxVisibleCharacters >= m_ParsedText.Length)
                    {
                        enabled = false;
                        OnShowAll?.Invoke();
                    }
                }

                if (m_CharacterClips.Length > 0)
                {
                    m_AudioTime += m_UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    if (m_AudioTime > m_TimePerSound && m_Text.maxVisibleCharacters > 0 && m_ParsedText[m_Text.maxVisibleCharacters - 1] != ' ')
                    {
                        m_AudioTime = 0;
                        m_AudioSource.PlayOneShot(m_CharacterClips[UnityEngine.Random.Range(0, m_CharacterClips.Length)]);
                    }
                }
            }
        }

        // --------------------------------------------------------------------

        public void ShowAllText()
        {
            m_Text.maxVisibleCharacters = int.MaxValue;
            enabled = false;
            OnShowAll?.Invoke();
        }
    }
}