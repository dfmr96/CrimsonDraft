#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    /// <summary>
    /// Attached to an operator's battlefield prefab. Exposes the point
    /// BattlefieldView spawns hit VFX from on a landed enemy attack.
    /// </summary>
    public sealed class OperatorHitFxMarker : MonoBehaviour
    {
        [SerializeField] private Transform? hitFxPoint;

        public Transform? HitFxPoint => this.hitFxPoint;
    }
}
