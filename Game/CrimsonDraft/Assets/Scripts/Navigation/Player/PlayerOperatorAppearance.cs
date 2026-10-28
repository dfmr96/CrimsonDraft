#nullable enable

using System;
using MessagePipe;
using UnityEngine;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Operators;
using CrimsonDraft.Operators.Appearance;

namespace CrimsonDraft.Navigation.Player
{
    /// <summary>
    /// The Navigation player wears the look of the first alive operator of the roster (party order,
    /// same one Combat uses per slot), so it matches the leader's model on the battlefield.
    /// Re-applied after every combat, since the leader may have died there.
    /// </summary>
    public sealed class PlayerOperatorAppearance : IInitializable, IDisposable
    {
        private readonly IOperatorRoster               roster;
        private readonly PlayerController              player;
        private readonly ISubscriber<CombatEndedEvent> combatEndedSubscriber;

        private SoldierAppearanceProfile? applied;
        private IDisposable?              combatEndedSubscription;

        [Preserve]
        public PlayerOperatorAppearance(
            IOperatorRoster               roster,
            PlayerController              player,
            ISubscriber<CombatEndedEvent> combatEndedSubscriber)
        {
            this.roster                = roster;
            this.player                = player;
            this.combatEndedSubscriber = combatEndedSubscriber;
        }

        void IInitializable.Initialize()
        {
            this.combatEndedSubscription = this.combatEndedSubscriber.Subscribe(_ => this.Refresh());
            this.Refresh();
        }

        public void Refresh()
        {
            SoldierAppearanceProfile? profile = null;
            for (int i = 0; i < this.roster.Count; i++)
            {
                if (!this.roster[i].IsAlive) continue;
                profile = this.roster[i].Data?.AppearanceProfile;
                break;
            }
            if (profile == null || profile == this.applied) return;

            var appearance = this.player.GetComponentInChildren<SoldierAppearance>(true);
            if (appearance == null)
            {
                Debug.LogWarning("[PlayerOperatorAppearance] The player has no SoldierAppearance (Update Appearance Prefab Variants installs it on Player.prefab).", this.player);
                return;
            }

            appearance.Apply(profile);
            this.applied = profile;
        }

        void IDisposable.Dispose() => this.combatEndedSubscription?.Dispose();
    }
}
