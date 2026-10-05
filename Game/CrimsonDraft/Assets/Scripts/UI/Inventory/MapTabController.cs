#nullable enable

using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Navigation.Map;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Navigation.UI;

namespace CrimsonDraft.UI
{
    /// <summary>Drives the MAP tab: opens on the player's floor and steps between the
    /// available floors with the vertical axis (one step per press). Cancel is handled
    /// centrally by TabManager; nothing here reacts while the tab bar is active.</summary>
    public sealed class MapTabController : MonoBehaviour
    {
        [SerializeField] private MapScreenView mapScreenView = null!;

        [Inject] private IInputService     inputService     = null!;
        [Inject] private TabManager        tabManager       = null!;
        [Inject] private MapSceneConfig    sceneConfig      = null!;
        [Inject] private MapDataSet        mapSet           = null!;
        [Inject] private IRoomOrchestrator roomOrchestrator = null!;
        [Inject] private RoomStateRegistry rooms            = null!;
        [Inject] private PickupRegistry    pickups          = null!;
        [Inject] private KnownMapsRegistry knownMaps        = null!;

        private readonly AxisStepper stepper = new();
        private MapFloors? floors;

        void OnEnable()
        {
            if (this.inputService == null) return;

            var playerFloor = this.sceneConfig != null ? this.sceneConfig.Map : null;
            this.floors = new MapFloors(
                MapFloors.Available(this.mapSet.Maps, this.rooms, this.knownMaps, playerFloor),
                playerFloor);
            this.stepper.Reset();
            ShowCurrentFloor();
        }

        void OnDisable()
        {
            this.mapScreenView?.Hide();
            this.floors = null;
        }

        void Update()
        {
            if (this.tabManager == null || this.floors == null) return;

            if (this.tabManager.IsTabBarActive)
            {
                this.stepper.Reset();
                return;
            }

            int step = this.stepper.Update(this.inputService.InventoryNavigate.ReadValue<Vector2>().y);
            if (step != 0 && this.floors.Step(step))
                ShowCurrentFloor();
        }

        private void ShowCurrentFloor()
        {
            var map = this.floors?.Current;
            if (map == null)
            {
                this.mapScreenView.Hide();
                return;
            }

            string? currentRoomId = ReferenceEquals(map, this.sceneConfig.Map)
                ? this.roomOrchestrator.CurrentRoom?.RoomId
                : null;

            var visuals = MapRoomVisuals.Resolve(map, this.rooms, this.pickups, this.knownMaps, currentRoomId);
            this.mapScreenView.Show(visuals, map.DisplayName, this.floors!.HasUp, this.floors.HasDown);
        }
    }
}
