#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    /// <summary>
    /// Attached to an enemy's battlefield prefab. Exposes the blood-pool object that
    /// BattlefieldView reveals once the enemy is finished off, marking it as
    /// definitively dead (RE-style feedback). The pool itself starts inactive and is
    /// expected to handle its own grow-in effect once activated. Also exposes the point
    /// BattlefieldView spawns hit VFX from on a landed shot, the head renderer hidden on a
    /// headshot kill, and the actual "head" skeleton bone used to place that explosion FX.
    /// The bone -- not the renderer -- is what tracks the animated head: SkinnedMeshRenderer.
    /// bounds is a fixed box from the bind pose that just rigidly follows the root bone, so
    /// it's only roughly right while standing and badly wrong once staggered/lying down.
    /// </summary>
    public sealed class EnemyDeathMarker : MonoBehaviour
    {
        [SerializeField] private GameObject bloodPool = null!;
        [SerializeField] private Transform? hitFxPoint;
        [SerializeField] private SkinnedMeshRenderer? headRenderer;
        [SerializeField] private Transform? headBone;

        public GameObject BloodPool => this.bloodPool;
        public Transform? HitFxPoint => this.hitFxPoint;
        public SkinnedMeshRenderer? HeadRenderer => this.headRenderer;
        public Transform? HeadBone => this.headBone;
    }
}
