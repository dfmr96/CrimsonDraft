#nullable enable

using Unity.Cinemachine;
using UnityEngine;
using VContainer;
using Yarn.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Infrastructure.Save.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>
    /// Save-room wiring that sits next to a <see cref="SavePointInteractable"/>: the camera it
    /// cuts to, the world-space SaveGame_canva panel, and the one-time telegraph-room intro.
    /// Without this component the save point falls back to the screen-overlay save menu.
    /// </summary>
    public sealed class TelegraphSaveStation : MonoBehaviour
    {
        // The intro has no world object of its own to track, so its "seen" flag borrows the
        // pickup registry -- already saved, loaded and reset together with a save file.
        private const string IntroSeenId = "intro/telegraph_room";

        [SerializeField] private CinemachineCamera     telegraphCamera = null!;
        [SerializeField] private LoadGameSaveListView  saveView        = null!;
        [SerializeField] private DialogueReference     introDialogue   = new();

        private WorldStateRegistries world = null!;

        public CinemachineCamera    Camera        => this.telegraphCamera;
        public LoadGameSaveListView View          => this.saveView;
        public string?              IntroNodeName => this.introDialogue.nodeName;

        public bool IntroSeen => this.world.Pickups.IsCollected(IntroSeenId);

        [Inject]
        public void Construct(WorldStateRegistries world) => this.world = world;

        private void Awake()
        {
            // Kept off until InspectionController cuts to it (see MusicBoxInteractable).
            this.telegraphCamera.enabled = false;
        }

        public void MarkIntroSeen() => this.world.Pickups.SetCollected(IntroSeenId);
    }
}
