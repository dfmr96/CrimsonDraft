#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    /// <summary>
    /// Attached to an enemy's battlefield prefab. Exposes the blood-pool object that
    /// BattlefieldView reveals once the enemy is finished off, marking it as
    /// definitively dead (RE-style feedback). The pool itself starts inactive and is
    /// expected to handle its own grow-in effect once activated.
    /// </summary>
    public sealed class EnemyDeathMarker : MonoBehaviour
    {
        [SerializeField] private GameObject bloodPool = null!;

        public GameObject BloodPool => this.bloodPool;
    }
}
