using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class ItemSequencePuzzle : PuzzleBase
    {
        [SerializeField] private List<ItemData> m_Solution = new List<ItemData>();
        [Tooltip("The solution is sorted and the puzzle will only be solved if given in the define order")]
        [SerializeField] private bool m_RespectOrder = true;

        private List<string> m_UsedItems = new List<string>();

        struct ItemUsagePuzzleSaveData
        {
            public bool Solved;
            public List<string> UsedItems;
        }

        public void Add(ItemData item)
        {
            m_UsedItems.Add(item.UniqueId);
            if (CheckSolution())
                Solve();
        }

        private bool CheckSolution()
        {
            if (m_UsedItems.Count < m_Solution.Count)
                return false;

            if (m_RespectOrder)
            {
                int solutionIndex = 0;
                for (int i = m_UsedItems.Count - m_Solution.Count; i < m_UsedItems.Count; ++i)
                {
                    if (m_UsedItems[i] != m_Solution[solutionIndex].UniqueId)
                        return false;

                    ++solutionIndex;
                }
            }
            else
            {
                List<string> solutionIDs = new List<string>();
                for (int i = 0; i < m_Solution.Count; ++i)
                {
                    solutionIDs.Add(m_Solution[i].UniqueId);
                }

                foreach(var itemId in m_UsedItems)
                {
                    if (!solutionIDs.Remove(itemId))
                        return false;
                }
            }

            return true;
        }


        public override string GetSavableData()
        {
            ItemUsagePuzzleSaveData saveData = new ItemUsagePuzzleSaveData()
            {
                Solved = m_Solved,
                UsedItems = m_UsedItems
            };
            
            return JsonUtility.ToJson(saveData);
        }

        public override void SetFromSavedData(string savedData)
        {
            ItemUsagePuzzleSaveData puzzleData = JsonUtility.FromJson<ItemUsagePuzzleSaveData>(savedData);

            m_Solved = puzzleData.Solved;
            m_UsedItems = puzzleData.UsedItems;

            if (m_Solved)
                OnLoadedSolved?.Invoke();
        }

    }
}
