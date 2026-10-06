using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [Serializable]
    public class SequencePuzzleEntry
    {
        public SequencePuzzle Puzzle;
        public UnityEvent OnActivated;
        public UnityEvent OnDeactivated;
    }

    public class MultiSequencePuzzle : PuzzleBase
    {
        [SerializeField] UnityEvent m_OnIncorrectPuzzles;

        [SerializeField] SequencePuzzleEntry[] m_SequencePuzzles;

        private int m_CurrentPuzzle;

        // --------------------------------------------------------------------

        public void Awake()
        {
            m_CurrentPuzzle = 0; 
        }

        // --------------------------------------------------------------------

        public void ClearAndReset()
        {
            if (CheckAllPuzzlesSolved())
            {
                Solve();
                return;
            }

            foreach (var entry in m_SequencePuzzles)
            {
                entry.OnDeactivated?.Invoke();
                entry.Puzzle.Clear();
            }

            m_CurrentPuzzle = 0;
            m_SequencePuzzles[m_CurrentPuzzle].OnActivated?.Invoke();
        }

        // --------------------------------------------------------------------

        public void Add(string entry)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            m_SequencePuzzles[m_CurrentPuzzle].Puzzle.Add(entry);
        }

        // --------------------------------------------------------------------

        public void Remove()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            m_SequencePuzzles[m_CurrentPuzzle].Puzzle.Remove();
            
        }

        // --------------------------------------------------------------------

        public void EnterSolution()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (m_CurrentPuzzle >= m_SequencePuzzles.Length - 1)
            {
                if (CheckAllPuzzlesSolved())
                {
                    Solve();
                }
                else
                {
                    m_SequencePuzzles[m_CurrentPuzzle].OnDeactivated?.Invoke();
                    m_CurrentPuzzle = 0;
                    m_OnIncorrectPuzzles?.Invoke();
                    m_SequencePuzzles[m_CurrentPuzzle].OnActivated?.Invoke();
                }
            }
            else
            {
                m_SequencePuzzles[m_CurrentPuzzle].OnDeactivated?.Invoke();
                m_CurrentPuzzle++;
                m_SequencePuzzles[m_CurrentPuzzle].OnActivated?.Invoke();
            }
        }

        // --------------------------------------------------------------------

        private bool CheckAllPuzzlesSolved()
        {
            foreach (var entry in m_SequencePuzzles)
            {
                if (!entry.Puzzle.IsSolved && !entry.Puzzle.CheckSolution())
                {
                    return false; 
                }
            }

            return true;
        }

        // --------------------------------------------------------------------

        public override void Solve()
        {
            base.Solve();

            foreach (var entry in m_SequencePuzzles)
            {
                entry.Puzzle.Solve();
            }
        }
    }

}

