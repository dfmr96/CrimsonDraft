#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    /// <summary>
    /// Attached to an enemy's battlefield prefab. Receives the Animation Event fired
    /// from the Attack clip at the moment the hit actually connects, and forwards it
    /// to whatever BattlefieldView bound for the currently in-flight attack.
    /// </summary>
    public sealed class EnemyAttackEventRelay : MonoBehaviour
    {
        private Action? onAttackImpact;

        public void Bind(Action onAttackImpact) => this.onAttackImpact = onAttackImpact;

        // Called by Animation Event on the enemy's Attack clip.
        public void OnAttackImpact()
        {
            // One-shot: cleared before invoking so a replayed clip, or BattlefieldView's
            // no-event fallback, can never deliver the same hit twice.
            Action? callback = this.onAttackImpact;
            this.onAttackImpact = null;
            callback?.Invoke();
        }
    }
}
