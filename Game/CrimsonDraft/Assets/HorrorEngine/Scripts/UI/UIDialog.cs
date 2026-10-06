using UnityEngine;
using UnityEngine.Serialization;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    [Serializable]
    public struct DialogLine 
    {
        public float Delay;
        public LocalizableText LineText;
        [NonSerialized] public string ProcessedText;

        // TODO - Remove eventually
        [FormerlySerializedAs("Text")]
        [HideInInspector]
        public string Text_DEPRECATED;
        // --------------

        public AudioClip Clip;

#if UNITY_EDITOR
        public void EditorOnly_MigrateUnlocalizedData()
        {
            if (!string.IsNullOrEmpty(Text_DEPRECATED))
            {
                LineText.Unlocalized = Text_DEPRECATED;
                Text_DEPRECATED = "";
            }
        }
#endif
    }

    [Serializable]
    public class DialogData
    {
        [FormerlySerializedAs("Lines")]
        [SerializeField]
        private DialogLine[] m_Lines;

        private Dictionary<string, string> m_Tags = new Dictionary<string, string>();

        public int LineCount => m_Lines.Length;

        // --------------------------------------------------------------------

        public bool IsValid()
        {
            return m_Lines != null && m_Lines.Length > 0;
        }

        // --------------------------------------------------------------------

        public void SetTagReplacement(string tag, string replacement)
        {
            if (m_Tags.ContainsKey(tag))
                m_Tags[tag] = replacement;
            else
                m_Tags.Add(tag, replacement);
        }

        // --------------------------------------------------------------------

        public DialogLine GetProcessedLine(int index)
        {
            DialogLine line = m_Lines[index];

            line.ProcessedText = line.LineText;

            foreach (var tagEntry in m_Tags)
            {
                line.ProcessedText = line.ProcessedText.Replace(tagEntry.Key, tagEntry.Value);
            }

            return line;
        }

        // --------------------------------------------------------------------

        public void SetLines(DialogLine[] lines)
        {
            m_Lines = lines;
        }

#if UNITY_EDITOR
        public void EditorOnly_MigrateUnlocalizedData()
        {
            if (m_Lines != null)
            {
                foreach (var line in m_Lines)
                {
                    line.EditorOnly_MigrateUnlocalizedData();
                }
            }
        }
#endif
    }

    public class UIDialog : MonoBehaviour
    {
        [SerializeField] GameObject m_Content;
        [SerializeField] TextMeshProUGUI m_Text;

        private int m_CurrentLine;
        private DialogData m_Dialog;

        private AppearingText m_AppearingText;

        private IUIInput m_Input;
        private bool m_HideOnEnd;
        private AudioSource m_AudioSource;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_AppearingText = GetComponentInChildren<AppearingText>();
            m_AudioSource = GetComponent<AudioSource>();
            m_Input = GetComponentInParent<IUIInput>();

            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        public void Show(DialogData dialog, bool hideOnEnd = true)
        {
            enabled = true;
            m_Dialog = dialog;
            m_HideOnEnd = hideOnEnd;
            PauseController.Instance.Pause(this);
            m_CurrentLine = -1;

            gameObject.SetActive(true);

            if (m_Dialog.IsValid())
            {
                m_Content.SetActive(true);
                ShowNextLine();
            }
            else
            {
                m_Content.SetActive(false);
            }
        }

        // --------------------------------------------------------------------

        private void ShowNextLine()
        {
            ++m_CurrentLine;
            
            var line = m_Dialog.GetProcessedLine(m_CurrentLine);
            if (line.Delay > 0)
            {
                m_Content.SetActive(false);
                
                if (m_AppearingText)
                    m_AppearingText.Clear();
                else
                    m_Text.text = "";

                StartCoroutine(ShowLineWithDelay(line));
            }
            else
            {
                ShowLine(line);
            }
        }

        // --------------------------------------------------------------------

        private IEnumerator ShowLineWithDelay(DialogLine line)
        {
            yield return Yielders.UnscaledTime(line.Delay);
            ShowLine(line);
        }

        // --------------------------------------------------------------------

        private void ShowLine(DialogLine line)
        {
            if (m_AppearingText)
                m_AppearingText.Show(line.ProcessedText);
            else
                m_Text.text = line.ProcessedText;

            m_Content.SetActive(true);
            if (line.Clip)
                m_AudioSource.PlayOneShot(line.Clip);
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            bool isDismissed = m_Input.IsConfirmDown();
            if (isDismissed && (m_Content.activeSelf || !m_Dialog.IsValid()))
            {
                if (m_Dialog.IsValid() && !m_AppearingText.HasShownAll)
                {
                    m_AppearingText.ShowAllText();
                    return;
                }

                if (m_CurrentLine == m_Dialog.LineCount - 1)
                {
                    if (m_HideOnEnd)
                    {
                        Hide();
                    }
                    else
                    {
                       UIManager.PopAction();
                       enabled = false; // Stop processing update 
                    }
                }
                else
                {
                    ShowNextLine();
                }
            }
        }

        // --------------------------------------------------------------------

        public void Hide()
        {
            enabled = false;
            gameObject.SetActive(false);
            PauseController.Instance.Resume(this);
            
            UIManager.PopAction();
        }

    }
}