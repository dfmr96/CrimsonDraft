#nullable enable

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Enemy;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation
{
    // Debug-only shortcuts:
    //  F7 -- exercises the dead-operator loot flow without a combat: kills the last alive
    //        operator and runs the same aftermath a combat death gets (weapon released,
    //        corpse recorded and spawned at the player's feet).
    //  F6 -- gets the enemy corpse nearest to the player back up (EnemyNavAgent.Revive), until
    //        a real trigger (milestone, story event) is hooked up.
    //  F5 -- toggles UI/preview camera gating (UiCameraAutoToggle.GatingEnabled) to A/B its
    //        performance against the original always-on cameras.
    public sealed class NavigationDebugKeys : ITickable
    {
        private const Key KillOperatorKey       = Key.F7;
        private const Key ReviveEnemyKey        = Key.F6;
        private const Key ToggleCameraGatingKey = Key.F5;

        private readonly IOperatorRoster         roster;
        private readonly IInventoryService       inventory;
        private readonly OperatorCorpseBootstrap corpses;
        private readonly EnemyNavAgent[]         enemies;
        private readonly PlayerController        player;

        [Preserve]
        public NavigationDebugKeys(
            IOperatorRoster         roster,
            IInventoryService       inventory,
            OperatorCorpseBootstrap corpses,
            EnemyNavAgent[]         enemies,
            PlayerController        player)
        {
            this.roster    = roster;
            this.inventory = inventory;
            this.corpses   = corpses;
            this.enemies   = enemies;
            this.player    = player;
        }

        // Never the last survivor: killing them in navigation would leave no one to play.
        internal static int? PickKillTarget(IOperatorRoster roster)
        {
            var alive = roster.GetAliveSlots();
            return alive.Count > 1 ? alive.Last() : (int?)null;
        }

        internal static int? PickNearest(Vector3 origin, IReadOnlyList<Vector3> candidates)
        {
            int?  best     = null;
            float bestSqr  = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                var sqr = (candidates[i] - origin).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best    = i;
            }
            return best;
        }

        void ITickable.Tick()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[KillOperatorKey].wasPressedThisFrame) KillOperator();
            if (keyboard[ReviveEnemyKey].wasPressedThisFrame)  ReviveNearestCorpse();
            if (keyboard[ToggleCameraGatingKey].wasPressedThisFrame) ToggleCameraGating();
        }

        private static void ToggleCameraGating()
        {
            UiCameraAutoToggle.GatingEnabled = !UiCameraAutoToggle.GatingEnabled;
            Debug.Log(UiCameraAutoToggle.GatingEnabled
                ? "[NavigationDebugKeys] Camera gating ON (fix): UI/preview cameras render only while needed."
                : "[NavigationDebugKeys] Camera gating OFF (original): UI/preview cameras always render.");
        }

        private void KillOperator()
        {
            if (PickKillTarget(this.roster) is not { } slot)
            {
                Debug.LogWarning("[NavigationDebugKeys] Only one operator alive; not killing the last survivor.");
                return;
            }

            var target = this.roster[slot];
            target.ApplyDamage(target.Hp);
            target.ApplyDamage(1);
            this.inventory.ReleaseDeadOperatorWeapons();
            this.corpses.RecordNewDeaths();
            Debug.Log($"[NavigationDebugKeys] Killed operator {slot}.");
        }

        private void ReviveNearestCorpse()
        {
            var corpses = this.enemies
                .Where(e => e != null && e.IsDead && e.gameObject.activeInHierarchy)
                .ToList();

            if (PickNearest(this.player.transform.position, corpses.Select(e => e.transform.position).ToList()) is not { } index)
            {
                Debug.LogWarning("[NavigationDebugKeys] No enemy corpse in the active room to revive.");
                return;
            }

            var target = corpses[index];
            if (target.Revive())
                Debug.Log($"[NavigationDebugKeys] Reviving enemy '{target.name}'.");
            else
                Debug.LogWarning($"[NavigationDebugKeys] '{target.name}' can't be revived right now (combat in progress?).");
        }
    }
}

#endif
