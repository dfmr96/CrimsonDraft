#nullable enable

using MessagePipe;
using UnityEngine.Scripting;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>
    /// The "take it?" preview of a world pickup. It shows nothing itself: it asks the inventory UI
    /// (over MessagePipe, same as the storage box) to open with the item ghosted where it would
    /// land and the Inspect panel up -- 3D model, name and the prompt. The prompt runs on the
    /// Inspect dialogue runner (<see cref="Dialogue"/>), whose Yes/No lives inside that panel.
    /// </summary>
    public sealed class PickupPreviewController
    {
        private readonly IPublisher<PickupPreviewStartedEvent> started;
        private readonly IPublisher<PickupPreviewEndedEvent>   ended;

        [Preserve]
        public PickupPreviewController(
            IPublisher<PickupPreviewStartedEvent> started,
            IPublisher<PickupPreviewEndedEvent>   ended,
            IInspectDialogueService               dialogue)
        {
            this.started  = started;
            this.ended    = ended;
            this.Dialogue = dialogue;
        }

        /// <summary>Where the prompt of a previewed pickup has to run so it shows inside the Inspect panel.</summary>
        public IDialogueService Dialogue { get; }

        public void Show(ItemData item) => this.started.Publish(new PickupPreviewStartedEvent(item));

        public void Hide() => this.ended.Publish(new PickupPreviewEndedEvent());
    }
}
