using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{

    public class StageCompleteTrigger : MonoBehaviour
    {
        [SerializeField] private RankingDataSet m_RankingDataSet;
        [SerializeField] private SceneTransition m_ExitTransition;

        [Tooltip("Character that will start in the next scene. Leave empty to use the current character")]
        [SerializeField] private CharacterData m_StartWithCharacter;

        public void CompleteStage()
        {

            UIManager.Get<UIRanking>().Show(m_RankingDataSet, m_ExitTransition, m_StartWithCharacter);
        }
    }

}
