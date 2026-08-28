#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Operator Corpse Settings")]
    public sealed class OperatorCorpseSettings : ScriptableObject
    {
        [SerializeField] private GameObject corpsePrefab = null!;

        public GameObject CorpsePrefab => this.corpsePrefab;
    }
}
