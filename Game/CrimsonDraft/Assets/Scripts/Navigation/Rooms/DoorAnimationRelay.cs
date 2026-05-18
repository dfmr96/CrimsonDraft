#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    // Added at runtime to the Animator's GameObject so Animation Events reach DoorTransitionController.
    public sealed class DoorAnimationRelay : MonoBehaviour
    {
        private DoorTransitionController controller = null!;

        internal void Init(DoorTransitionController controller)
            => this.controller = controller;

        public void OnAnimationComplete() => this.controller.OnAnimationComplete();
    }
}
