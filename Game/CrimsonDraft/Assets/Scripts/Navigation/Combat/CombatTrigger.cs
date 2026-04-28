#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure.Scenes;

namespace CrimsonDraft.Navigation.Combat
{
    public sealed class CombatTrigger : MonoBehaviour
    {
        [SerializeField] private string encounterId = string.Empty;

        private ISceneTransitionService sceneTransitionService = null!;

        [Inject]
        public void Construct(ISceneTransitionService sceneTransitionService)
        {
            this.sceneTransitionService = sceneTransitionService;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            if (this.sceneTransitionService.IsInCombat)
                return;

            this.sceneTransitionService.StartCombatAsync(this.encounterId).Forget();
        }
    }
}
