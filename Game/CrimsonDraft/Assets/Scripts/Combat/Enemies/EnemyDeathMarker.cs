#nullable enable

using UnityEngine;
using CrimsonDraft.Rendering.Blood;

namespace CrimsonDraft.Combat
{
    /// <summary>
    /// Attached to an enemy's battlefield prefab. Exposes the blood blob that
    /// BattlefieldView grows once the enemy is finished off, marking it as
    /// definitively dead (RE-style feedback). The blob starts inactive and grows itself
    /// in once shown (the same CorpseBloodBlob navigation corpses use). Also exposes the point
    /// BattlefieldView spawns hit VFX from on a landed shot, the head renderer hidden on a
    /// headshot kill, and the actual "head" skeleton bone used to place that explosion FX.
    /// The bone -- not the renderer -- is what tracks the animated head: SkinnedMeshRenderer.
    /// bounds is a fixed box from the bind pose that just rigidly follows the root bone, so
    /// it's only roughly right while standing and badly wrong once staggered/lying down.
    /// </summary>
    public sealed class EnemyDeathMarker : MonoBehaviour
    {
        [SerializeField] private CorpseBloodBlob? bloodBlob;
        [SerializeField] private Transform? hitFxPoint;
        [SerializeField] private SkinnedMeshRenderer? headRenderer;
        [SerializeField] private Transform? headBone;

        public CorpseBloodBlob? BloodBlob => this.bloodBlob;
        public Transform? HitFxPoint => this.hitFxPoint;
        public SkinnedMeshRenderer? HeadRenderer => this.headRenderer;
        public Transform? HeadBone => this.headBone;
    }
}
