#nullable enable

using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>Reacts, at room load, to an ItemSocketInteractable elsewhere -- a different
    /// scene/deck, so no direct object reference to it is possible -- already being fully
    /// filled. E.g. the Lavatory's steam clears once the Blue/Valve Handle (KI_11) has been
    /// placed into its socket back in the Deck C hallway. ItemSocketStateRegistry is a
    /// cross-scene, save-backed singleton (see WorldStateRegistries), so this only reads state
    /// that's already tracked -- nothing here needs to persist anything itself.</summary>
    public sealed class RemoteSocketGate : MonoBehaviour
    {
        [SerializeField] private string      socketId = "";
        [SerializeField] private GameObject? hideOnFilled;
        [SerializeField] private Collider?   blockingCollider;

        [Inject] private ItemSocketStateRegistry registry = null!;

        void Start()
        {
            var inserted = this.registry.GetInserted(this.socketId);
            if (inserted.Length == 0) return;

            foreach (bool slot in inserted)
                if (!slot) return;

            if (this.hideOnFilled != null)
                this.hideOnFilled.SetActive(false);

            if (this.blockingCollider != null)
                this.blockingCollider.enabled = false;
        }
    }
}
