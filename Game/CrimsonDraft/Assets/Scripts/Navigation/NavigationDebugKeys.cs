#nullable enable

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation
{
    // Debug-only shortcut for exercising the dead-operator loot flow without a combat:
    // kills the last alive operator and runs the same aftermath a combat death gets
    // (weapon released, corpse recorded and spawned at the player's feet).
    public sealed class NavigationDebugKeys : ITickable
    {
        private const Key KillOperatorKey = Key.F7;

        private readonly IOperatorRoster         roster;
        private readonly IInventoryService       inventory;
        private readonly OperatorCorpseBootstrap corpses;

        [Preserve]
        public NavigationDebugKeys(IOperatorRoster roster, IInventoryService inventory, OperatorCorpseBootstrap corpses)
        {
            this.roster    = roster;
            this.inventory = inventory;
            this.corpses   = corpses;
        }

        // Never the last survivor: killing them in navigation would leave no one to play.
        internal static int? PickKillTarget(IOperatorRoster roster)
        {
            var alive = roster.GetAliveSlots();
            return alive.Count > 1 ? alive.Last() : (int?)null;
        }

        void ITickable.Tick()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[KillOperatorKey].wasPressedThisFrame) return;

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
    }
}

#endif
