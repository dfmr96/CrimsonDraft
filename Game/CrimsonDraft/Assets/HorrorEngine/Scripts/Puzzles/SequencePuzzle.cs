using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public class SequencePuzzle : PuzzleBase
    {
        public UnityEvent<string> OnEntryAdded;
        public UnityEvent<string> OnEntryRemoved;
        public UnityEvent OnIncorrectSolution;
        public UnityEvent OnCleared;

        [SerializeField] private string[] m_Solution;
        [Tooltip("The solution is sorted and the puzzle will only be solved if given in the define order")]
        [SerializeField] private bool m_RespectOrder = true;
        [FormerlySerializedAs("m_AutoSolutionCheck")]
        [SerializeField] private bool m_CheckSolutionAfterEntry = true;
        [Tooltip("Leave this number at 0 to support any length")]
        [FormerlySerializedAs("m_MaxCharacterLimit")]
        [SerializeField] private int m_MaxEntries;

        private List<string> m_Entries = new List<string>();

        private int m_EntriesCount;

        // --------------------------------------------------------------------

        public void Clear()
        {
            m_Entries.Clear();
            OnCleared?.Invoke();
        }

        // --------------------------------------------------------------------

        public void Add(string entry)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (m_Solved)
            {
                Debug.LogWarning("Puzzle is already solved. Adding an entry won't have any effect");
                return;
            }

            if (m_MaxEntries > 0 && m_EntriesCount >= m_MaxEntries)
            {
                return;
            } 

            m_Entries.Add(entry);
            ++m_EntriesCount;

            if (m_Entries.Count > m_Solution.Length)
            {
                m_Entries.RemoveAt(0);
            }

            OnEntryAdded?.Invoke(entry);

            if (m_CheckSolutionAfterEntry)
            {
                if (CheckSolution())
                {
                    Solve();
                }
                else if (m_EntriesCount % m_Solution.Length == 0)
                {
                    OnIncorrectSolution?.Invoke();
                }
            }
        }

        // --------------------------------------------------------------------

        public void Remove()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (m_Solved)
            {
                Debug.LogWarning("Puzzle is already solved. Removing an entry won't have any effect");
                return;
            }

            if (m_Entries.Count > 0)
            {
                string entry = m_Entries[m_Entries.Count - 1];
                m_Entries.RemoveAt(m_Entries.Count - 1);
                --m_EntriesCount;
                OnEntryRemoved?.Invoke(entry);
            }
        }


        // --------------------------------------------------------------------

        public bool CheckSolution()
        {
            if (m_Entries.Count < m_Solution.Length)
                return false;

            if (m_RespectOrder)
            {
                for (int i = 0; i < m_Solution.Length; ++i)
                {
                    if (m_Entries[i] != m_Solution[i])
                    {
                        return false;
                    }
                }
            }
            else
            {
                List<string> remainingSolution = new List<string>(m_Solution);
                for (int i = 0; i < m_Entries.Count; ++i)
                {
                    remainingSolution.Remove(m_Entries[i]);
                }

                return remainingSolution.Count == 0;
            }

            return true;
        }
    }
}