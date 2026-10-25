#nullable enable

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.DebugMenu;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation
{
    // Debug menu entry "Teleport" (group "Navigation"): Left/Right cycles every room of the game
    // (RoomCatalog), Enter jumps there. Same deck: instant, through RoomOrchestrator.TeleportToRoom.
    // Other deck: the deck scene is swapped (no door animation) and starts in that room.
    public sealed class DebugRoomTeleport : IInitializable, IDisposable
    {
        private readonly IDebugMenu              menu;
        private readonly RoomCatalog             catalog;
        private readonly IRoomOrchestrator       rooms;
        private readonly IFloorTransitionService floors;
        private readonly ISceneTransitionService scenes;

        private IDisposable? registration;

        [Preserve]
        public DebugRoomTeleport(
            IDebugMenu menu, RoomCatalog catalog, IRoomOrchestrator rooms,
            IFloorTransitionService floors, ISceneTransitionService scenes)
        {
            this.menu    = menu;
            this.catalog = catalog;
            this.rooms   = rooms;
            this.floors  = floors;
            this.scenes  = scenes;
        }

        internal static bool IsSameScene(string activeScene, string targetScene)
            => string.Equals(activeScene, targetScene, StringComparison.Ordinal);

        void IInitializable.Initialize()
        {
            var ids = this.catalog.Rooms.Select(r => r.RoomId).ToList();
            this.registration = this.menu.Register(DebugMenuEntry.Choice("Navigation", "Teleport", () => ids, Teleport));
        }

        void IDisposable.Dispose() => this.registration?.Dispose();

        internal void Teleport(string roomId)
        {
            if (this.scenes.IsInCombat || this.rooms.IsTransitioning)
            {
                // A deck swap mid door transition would leave the DoorTransition scene loaded and audio paused.
                Debug.LogWarning($"[DebugRoomTeleport] Teleport to '{roomId}' ignored during combat or a door transition.");
                return;
            }
            if (!this.catalog.TryGetScene(roomId, out var targetScene))
            {
                Debug.LogError($"[DebugRoomTeleport] '{roomId}' is not in the RoomCatalog.");
                return;
            }

            var activeScene = SceneManager.GetActiveScene().name;
            if (!IsSameScene(activeScene, targetScene))
            {
                this.floors.TeleportToFloorAsync(activeScene, targetScene, roomId).Forget();
                return;
            }

            var room = RoomLookup.FindLoaded(roomId);
            if (room == null)
            {
                Debug.LogError($"[DebugRoomTeleport] '{roomId}' is not in the loaded scene; rebuild the RoomCatalog.");
                return;
            }
            this.rooms.TeleportToRoom(room);
        }
    }
}

#endif
