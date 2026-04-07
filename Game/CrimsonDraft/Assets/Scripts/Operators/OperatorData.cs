#nullable enable

using UnityEngine;

namespace CrimsonDraft.Operators
{
    [CreateAssetMenu(fileName = "OperatorData", menuName = "CrimsonDraft/Operators/Operator Data")]
    public sealed class OperatorData : ScriptableObject
    {
        [SerializeField] private string operatorId  = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private Sprite portrait     = null!;

        public string OperatorId   => this.operatorId;
        public string DisplayName  => this.displayName;
        public Sprite Portrait     => this.portrait;
    }
}
