#nullable enable

using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Scenes;

namespace CrimsonDraft.Navigation.Combat
{
    public sealed class CombatTrigger : MonoBehaviour
    {
        [SerializeField] private string encounterId = string.Empty;

        private ISceneTransitionService?              sceneTransitionService;
        private ISubscriber<CombatEndedEvent>?         combatEndedSubscriber;
        private IEncounterContext?                     encounterContext;
        private IDisposable?                           combatEndedSubscription;

        public void Construct(
            ISceneTransitionService       sceneTransitionService,
            ISubscriber<CombatEndedEvent> combatEndedSubscriber,
            IEncounterContext             encounterContext)
        {
            this.sceneTransitionService = sceneTransitionService;
            this.combatEndedSubscriber  = combatEndedSubscriber;
            this.encounterContext       = encounterContext;
        }

        private void Start()
        {
            this.combatEndedSubscription = this.combatEndedSubscriber?.Subscribe(OnCombatEnded);
        }

        private void OnDestroy()
        {
            this.combatEndedSubscription?.Dispose();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (this.sceneTransitionService == null) return;
            if (this.sceneTransitionService.IsInCombat) return;

            this.sceneTransitionService.StartCombatAsync(this.encounterId).Forget();
        }

        private void OnCombatEnded(CombatEndedEvent ev)
        {
            if (!ev.Victory) return;
            if (this.encounterContext?.CurrentEncounterId != this.encounterId) return;
            this.transform.parent.gameObject.SetActive(false);
        }
    }
}
