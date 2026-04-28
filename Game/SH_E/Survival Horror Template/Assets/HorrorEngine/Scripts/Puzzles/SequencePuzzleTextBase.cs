using UnityEngine;

namespace HorrorEngine
{
    public abstract class SequencePuzzleTextBase : MonoBehaviour
    {
        [SerializeField] SequencePuzzle m_Puzzle;
        [SerializeField] TextReplacerBase m_TextReplacer;

        protected abstract string Text { get; set; }

        // --------------------------------------------------------------------

        private void Start()
        {
            Text = "";

            m_Puzzle.OnEntryAdded.AddListener(OnEntryAdded);
            m_Puzzle.OnEntryRemoved.AddListener(OnEntryRemoved);
            m_Puzzle.OnCleared.AddListener(OnCleared);
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            m_Puzzle.OnEntryAdded.RemoveListener(OnEntryAdded);
            m_Puzzle.OnEntryRemoved.RemoveListener(OnEntryRemoved);
            m_Puzzle.OnCleared.RemoveListener(OnCleared);
        }

        // --------------------------------------------------------------------

        private void OnCleared()
        {
            Text = "";
        }

        // --------------------------------------------------------------------

        private void OnEntryAdded(string entry)
        {
            if (m_TextReplacer == null)
            {
                Text += entry;
            }
            else
            {
                Text += m_TextReplacer.Replace(entry);
            }
        }

        // --------------------------------------------------------------------

        private void OnEntryRemoved(string entry)
        {
            if (Text.Length > 0)
            {
                Text = Text.Remove(Text.Length - 1);
            }
        }
    }
}