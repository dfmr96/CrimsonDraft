#nullable enable

using UnityEngine;

namespace CrimsonDraft.Operators
{
    [CreateAssetMenu(fileName = "OperatorData", menuName = "CrimsonDraft/Operators/Operator Data")]
    public sealed class OperatorData : ScriptableObject
    {
        [SerializeField] private string operatorId = string.Empty;
        [SerializeField] private Sprite sprite     = null!;

        public string OperatorId => this.operatorId;
        public Sprite Sprite     => this.sprite;
    }
}
