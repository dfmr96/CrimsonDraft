#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Navigation.Map;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.UI
{
    /// <summary>Fullscreen map screen with pan and deck cycling.</summary>
    public sealed class MapScreenController : IInitializable, ITickable, IDisposable
    {
        private const float PanSpeed = 12f;

        private readonly IInputService inputService;
        private readonly MapScreenView view;
        private readonly MapRenderer renderer;
        private readonly IRoomOrchestrator roomOrchestrator;
        private readonly MapSceneConfig sceneConfig;
        private readonly MapDataSet mapSet;
        private readonly RoomStateRegistry rooms;
        private readonly KnownMapsRegistry knownMaps;

        private MapData? shownMap;

        [Preserve]
        public MapScreenController(
            IInputService inputService,
            MapScreenView view,
            MapRenderer renderer,
            IRoomOrchestrator roomOrchestrator,
            MapSceneConfig sceneConfig,
            MapDataSet mapSet,
            RoomStateRegistry rooms,
            KnownMapsRegistry knownMaps)
        {
            this.inputService = inputService;
            this.view = view;
            this.renderer = renderer;
            this.roomOrchestrator = roomOrchestrator;
            this.sceneConfig = sceneConfig;
            this.mapSet = mapSet;
            this.rooms = rooms;
            this.knownMaps = knownMaps;
        }

        void IInitializable.Initialize()
        {
            this.inputService.OpenMap.performed += OnOpenMap;
            this.inputService.UIBack.performed += OnBack;
            this.inputService.UIConfirm.performed += OnCycleDeck;
        }

        public void Dispose()
        {
            this.inputService.OpenMap.performed -= OnOpenMap;
            this.inputService.UIBack.performed -= OnBack;
            this.inputService.UIConfirm.performed -= OnCycleDeck;
        }

        void ITickable.Tick()
        {
            if (!this.view.IsVisible)
                return;

            var nav = this.inputService.UINavigate.ReadValue<Vector2>();
            if (nav.sqrMagnitude < 0.01f)
                return;

            this.renderer.Pan(nav * (PanSpeed * Time.unscaledDeltaTime));
        }

        private void OnOpenMap(InputAction.CallbackContext _)
        {
            if (this.view.IsVisible)
                return;

            var currentDeck = this.sceneConfig.Map;
            if (currentDeck == null)
            {
                Debug.LogWarning("[MapScreen] No MapData bound to MapSceneConfig.");
                return;
            }

            Time.timeScale = 0f;
            this.inputService.SwitchToUI();
            ShowDeck(currentDeck);
        }

        private void OnBack(InputAction.CallbackContext _)
        {
            if (!this.view.IsVisible)
                return;

            this.view.Hide();
            this.renderer.SetVisible(false);
            this.shownMap = null;
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
        }

        private void OnCycleDeck(InputAction.CallbackContext _)
        {
            if (!this.view.IsVisible || this.shownMap == null)
                return;

            var known = KnownDecks();
            if (known.Count <= 1)
                return;

            int idx = known.IndexOf(this.shownMap);
            ShowDeck(known[(idx + 1) % known.Count]);
        }

        private void ShowDeck(MapData map)
        {
            this.shownMap = map;

            string? currentRoomId = ReferenceEquals(map, this.sceneConfig.Map)
                ? this.roomOrchestrator.CurrentRoom?.RoomId
                : null;

            this.renderer.SetVisible(true);
            this.renderer.Generate(map, currentRoomId);
            var texture = this.renderer.Texture;
            if (texture != null)
                this.view.Show(texture, map.DisplayName);
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
