#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    /// <summary>
    /// Attached to an operator's battlefield prefab. Exposes the point
    /// BattlefieldView spawns hit VFX from on a landed enemy attack, and the
    /// blood-pool object revealed once the operator dies.
    /// </summary>
    public sealed class OperatorHitFxMarker : MonoBehaviour
    {
        [SerializeField] private Transform? hitFxPoint;
        [SerializeField] private GameObject? bloodPool;

        public Transform? HitFxPoint => this.hitFxPoint;
        public GameObject? BloodPool => this.bloodPool;
    }
}
