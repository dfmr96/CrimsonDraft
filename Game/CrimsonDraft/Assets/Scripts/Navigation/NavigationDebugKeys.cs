#nullable enable

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.DebugMenu;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Infrastructure.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Enemy;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation
{
    // Debug menu entries (group "Navigation"; no keys of their own any more):
    //  Kill last alive operator -- exercises the dead-operator loot flow without a combat: kills the last alive
    //        operator and runs the same aftermath a combat death gets (weapon released,
    //        corpse recorded and spawned at the player's feet).
    //  Revive nearest enemy corpse -- gets the enemy corpse nearest to the player back up (EnemyNavAgent.Revive), until
    //        a real trigger (milestone, story event) is hooked up.
    //  UI camera gating -- toggles UI/preview camera gating (UiCameraAutoToggle.GatingEnabled) to A/B its
    //        performance against the original always-on cameras.
    //  Reveal all maps -- marks every deck of the MapDataSet as known, the same effect picking up its map has,
    //        so the MAP tab draws every floor including unvisited rooms (no map item added, no pickup consumed).
    public sealed class NavigationDebugKeys : IInitializable, IDisposable
    {
        private const string Group = "Navigation";

        private readonly IDebugMenu              menu;
        private readonly IOperatorRoster         roster;
        private readonly IInventoryService       inventory;
        private readonly OperatorCorpseBootstrap corpses;
        private readonly EnemyNavAgent[]         enemies;
        private readonly PlayerController        player;
        private readonly MapDataSet              maps;
        private readonly KnownMapsRegistry       knownMaps;

        [Preserve]
        public NavigationDebugKeys(
            IDebugMenu              menu,
            IOperatorRoster         roster,
            IInventoryService       inventory,
            OperatorCorpseBootstrap corpses,
            EnemyNavAgent[]         enemies,
            PlayerController        player,
            MapDataSet              maps,
            KnownMapsRegistry       knownMaps)
        {
            this.menu      = menu;
            this.roster    = roster;
            this.inventory = inventory;
            this.corpses   = corpses;
            this.enemies   = enemies;
            this.player    = player;
            this.maps      = maps;
            this.knownMaps = knownMaps;
        }

        // Returns how many decks were not known before.
        internal static int RevealMaps(IEnumerable<MapData?> maps, KnownMapsRegistry knownMaps)
        {
            int revealed = 0;
            foreach (var map in maps)
            {
                if (map == null || knownMaps.IsKnown(map.SceneName)) continue;
                knownMaps.MarkKnown(map.SceneName);
                revealed++;
            }
            return revealed;
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

        private IDisposable[] registrations = Array.Empty<IDisposable>();

        void IInitializable.Initialize()
        {
            this.registrations = new[]
            {
                this.menu.Register(DebugMenuEntry.Action(Group, "Kill last alive operator", KillOperator)),
                this.menu.Register(DebugMenuEntry.Action(Group, "Revive nearest enemy corpse", ReviveNearestCorpse)),
                this.menu.Register(DebugMenuEntry.Toggle(Group, "UI camera gating",
                    () => UiCameraAutoToggle.GatingEnabled, SetCameraGating)),
                this.menu.Register(DebugMenuEntry.Action(Group, "Reveal all maps", RevealAllMaps)),
            };
        }

        void IDisposable.Dispose()
        {
            foreach (var registration in this.registrations) registration.Dispose();
        }

        private static void SetCameraGating(bool enabled)
        {
            UiCameraAutoToggle.GatingEnabled = enabled;
            Debug.Log(enabled
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

        private void RevealAllMaps()
        {
            int revealed = RevealMaps(this.maps.Maps, this.knownMaps);
            Debug.Log($"[NavigationDebugKeys] Revealed {revealed} map(s); every deck is now known.");
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
