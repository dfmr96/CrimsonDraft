using UnityEngine;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "Animator State", menuName = "Horror Engine/Animator State Handle")]
    public class AnimatorStateHandle : ScriptableObject
    {
        [FormerlySerializedAs("m_StateName")]
        public string StateName;
        public int Hash = 0;
        public int Layer = 0;
        public void OnValidate()
        {
            Hash = Animator.StringToHash(StateName);
        }

        public void ResetName(string name)
        {
            StateName = name;
            Hash = Animator.StringToHash(StateName);
        }
    }
}