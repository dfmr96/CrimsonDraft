#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Navigation.Map;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Navigation.UI;

namespace CrimsonDraft.UI
{
    /// <summary>Drives the map inside its inventory tab: generates the current deck on
    /// enable, pans while focused, and cycles known decks. Cancel is handled centrally by
    /// TabManager (Map doesn't own it — see TabManager.OnCancelTab), so this class only
    /// reacts to Confirm and navigation while the tab bar isn't active.</summary>
    public sealed class MapTabController : MonoBehaviour
    {
        [SerializeField] private MapScreenView mapScreenView = null!;
        [SerializeField] private float         panSpeed      = 12f;

        [Inject] private IInputService     inputService     = null!;
        [Inject] private TabManager        tabManager       = null!;
        [Inject] private MapRenderer       mapRenderer      = null!;
        [Inject] private MapSceneConfig    sceneConfig      = null!;
        [Inject] private MapDataSet        mapSet           = null!;
        [Inject] private IRoomOrchestrator roomOrchestrator = null!;
        [Inject] private RoomStateRegistry rooms            = null!;
        [Inject] private KnownMapsRegistry knownMaps        = null!;

        private MapData? shownMap;

        void OnEnable()
        {
            if (this.inputService == null) return;
            this.inputService.InventoryConfirm.performed += OnConfirm;

            ShowCurrentDeck();
        }

        void OnDisable()
        {
            if (this.inputService == null) return;
            this.inputService.InventoryConfirm.performed -= OnConfirm;

            this.mapRenderer?.SetVisible(false);
            this.mapScreenView?.Hide();
            this.shownMap = null;
        }

        void Update()
        {
            if (this.tabManager == null || this.tabManager.IsTabBarActive) return;

            var nav = this.inputService.InventoryNavigate.ReadValue<Vector2>();
            if (nav.sqrMagnitude < 0.01f) return;

            this.mapRenderer.Pan(nav * (this.panSpeed * Time.unscaledDeltaTime));
        }

        void OnConfirm(InputAction.CallbackContext _)
        {
            if (this.tabManager.IsTabBarActive || this.shownMap == null) return;

            var known = KnownDecks();
            if (known.Count <= 1) return;

            int idx = known.IndexOf(this.shownMap);
            ShowDeck(known[(idx + 1) % known.Count]);
        }

        private void ShowCurrentDeck()
        {
            var current = this.sceneConfig != null ? this.sceneConfig.Map : null;
            if (current == null)
            {
                Debug.LogWarning("[MapTab] No MapData bound to MapSceneConfig.", this);
                return;
            }
            ShowDeck(current);
        }

        private void ShowDeck(MapData map)
        {
            this.shownMap = map;

            // Highlight the player's room only when showing the deck the player is on.
            string? currentRoomId = ReferenceEquals(map, this.sceneConfig.Map)
                ? this.roomOrchestrator.CurrentRoom?.RoomId
                : null;

            this.mapRenderer.SetVisible(true);
            this.mapRenderer.Generate(map, currentRoomId);

            var texture = this.mapRenderer.Texture;
            if (texture != null)
                this.mapScreenView.Show(texture, map.DisplayName);
        }

        private List<MapData> KnownDecks()
        {
            var result = new List<MapData>();
            foreach (var map in this.mapSet.Maps)
            {
                if (MapStateResolver.IsDeckKnown(map, this.rooms, this.knownMaps))
                    result.Add(map);
            }
            return result;
        }
    }
}
