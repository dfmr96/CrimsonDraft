#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Infrastructure.Save.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Operators;
using CrimsonDraft.UI.MainMenu;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class SaveController : ISaveController, IInitializable, IDisposable
    {
        private readonly IInputService       inputService;
        private readonly ISaveGameService    saveGameService;
        private readonly IInventoryService   inventoryService;
        private readonly IOperatorRoster     roster;
        private readonly IRoomOrchestrator   roomOrchestrator;
        private readonly PlayerController    player;
        private readonly WorldStateRegistries world;
        private readonly PlaytimeTracker     playtimeTracker;
        private readonly IFixedCameraZoneService zoneService;
        private readonly SaveSlotNavigator   navigator;
        // The save telegraph reuses the main menu's list sounds (same slot-list UI).
        private readonly MainMenuSfxData?    menuSfx;
        private Action?                      pendingOnSaved;

        // Telegraph (save room) session -- null while the legacy overlay or nothing is open.
        private enum TelegraphPhase { Browsing, Revealing, Done }
        private LoadGameSaveListView?        telegraphView;
        private SaveSlotNavigator?           telegraphNavigator;
        private TelegraphPhase               telegraphPhase;
        private Action?                      telegraphOnClosed;
        private ITelegraphMorse?             telegraphMorse;
        private CancellationTokenSource?     morseCancellation;
        private int                          pendingReveals;

        [Preserve]
        public SaveController(
            IInputService        inputService,
            SaveSlotListView     view,
            ISaveGameService     saveGameService,
            IInventoryService    inventoryService,
            IOperatorRoster      roster,
            IRoomOrchestrator    roomOrchestrator,
            PlayerController     player,
            WorldStateRegistries world,
            PlaytimeTracker      playtimeTracker,
            IFixedCameraZoneService zoneService,
            MainMenuSfxData?     menuSfx = null)
        {
            this.inputService     = inputService;
            this.saveGameService  = saveGameService;
            this.inventoryService = inventoryService;
            this.roster           = roster;
            this.roomOrchestrator = roomOrchestrator;
            this.player           = player;
            this.world            = world;
            this.playtimeTracker  = playtimeTracker;
            this.zoneService      = zoneService;
            this.menuSfx          = menuSfx;
            this.navigator = new SaveSlotNavigator(view, "Save to", Save, canConfirm: null, onClosed: OnNavigatorClosed);
        }

        void IInitializable.Initialize()
        {
            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIConfirm.performed  += OnConfirm;
            this.inputService.UICancel.performed   += OnBack;
        }

        public void Open(Action? onSaved = null)
        {
            if (this.navigator.IsOpen) return;
            this.pendingOnSaved = onSaved;
            Time.timeScale = 0f;
            this.inputService.SwitchToUI();
            this.navigator.Open(this.saveGameService.ListSlotSummaries());
        }

        // SaveSlotNavigator closes before it calls Save, so pendingOnSaved is NOT cleared here --
        // it is replaced on every Open and cleared once Save has used it.
        private void OnNavigatorClosed()
        {
            Time.timeScale = 1f;
            DeferredInputAction.Run(this.inputService.SwitchToGameplay);
        }

        public void OpenTelegraph(LoadGameSaveListView view, Action? onSaved, Action? onClosed, ITelegraphMorse? morse = null)
        {
            if (this.telegraphNavigator != null || this.navigator.IsOpen) return;
            this.pendingOnSaved    = onSaved;
            this.telegraphOnClosed = onClosed;
            this.telegraphMorse    = morse;
            this.telegraphView     = view;
            this.telegraphPhase    = TelegraphPhase.Browsing;

            // Stays open after a confirmed save (closeOnConfirm: false) so the list can come back
            // with the written file typing itself out; the cursor stops at the last row.
            this.telegraphNavigator = new SaveSlotNavigator(
                view, "Save", SaveTelegraph, onClosed: OnTelegraphClosed, wrapCursor: false, closeOnConfirm: false);

            this.inputService.SwitchToUI();
            this.telegraphNavigator.Open(VisibleSlots(view));
        }

        // The panel only has rows for view.SlotCapacity slots; the rest of the save files exist
        // on disk but can't be reached from here.
        private IReadOnlyList<SaveSlotSummary> VisibleSlots(LoadGameSaveListView view)
        {
            var all = this.saveGameService.ListSlotSummaries();
            if (all.Count <= view.SlotCapacity) return all;

            var visible = new List<SaveSlotSummary>(view.SlotCapacity);
            for (int i = 0; i < view.SlotCapacity; i++) visible.Add(all[i]);
            return visible;
        }

        private void SaveTelegraph(int slot)
        {
            Save(slot);

            // Done once the file's text has typed out AND, if the station has a key, its Morse
            // message ("SAVE 01") has been sent -- both start together.
            this.telegraphPhase = TelegraphPhase.Revealing;
            this.pendingReveals = this.telegraphMorse != null ? 2 : 1;

            // The text types out over exactly as long as the Morse message lasts, so the typing keeps
            // the rhythm of the pulses. No input is accepted until both are done (see OnConfirm/OnBack).
            string message       = $"SAVE {slot + 1:00}";
            float  morseSeconds  = this.telegraphMorse?.MeasureSeconds(message) ?? 0f;
            this.telegraphView!.ShowSaved(VisibleSlots(this.telegraphView), slot, OnRevealPartDone, morseSeconds);

            if (this.telegraphMorse != null)
                TransmitSaveMorseAsync(this.telegraphMorse, message).Forget();
        }

        private void OnRevealPartDone()
        {
            if (--this.pendingReveals <= 0) this.telegraphPhase = TelegraphPhase.Done;
        }

        private async UniTaskVoid TransmitSaveMorseAsync(ITelegraphMorse morse, string message)
        {
            this.morseCancellation?.Cancel();
            this.morseCancellation?.Dispose();
            this.morseCancellation = new CancellationTokenSource();
            var token = this.morseCancellation.Token;

            try
            {
                await morse.TransmitAsync(message, token);
                OnRevealPartDone();
            }
            catch (OperationCanceledException)
            {
                // Panel closed mid-message: the station already released the key.
            }
        }

        private void OnTelegraphClosed()
        {
            this.telegraphNavigator = null;
            this.telegraphView      = null;

            this.morseCancellation?.Cancel();
            this.morseCancellation?.Dispose();
            this.morseCancellation = null;
            this.telegraphMorse?.StopTransmission();
            this.telegraphMorse = null;

            // Saved or cancelled -- either way the owner leaves the telegraph camera.
            var onClosed = this.telegraphOnClosed;
            this.telegraphOnClosed = null;
            this.pendingOnSaved    = null;
            onClosed?.Invoke();
        }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            var direction = ctx.ReadValue<Vector2>();
            if (this.telegraphNavigator == null)
            {
                this.navigator.HandleNavigate(direction);
                return;
            }

            if (this.telegraphPhase != TelegraphPhase.Browsing) return;

            // While the Yes/No prompt is up, horizontal input picks the option instead.
            if (this.telegraphNavigator.IsConfirming)
            {
                if (direction.x > 0.5f)       { this.telegraphView!.SetConfirmSelection(yesSelected: true);  this.menuSfx?.PlayCursor(this.player.gameObject); }
                else if (direction.x < -0.5f) { this.telegraphView!.SetConfirmSelection(yesSelected: false); this.menuSfx?.PlayCursor(this.player.gameObject); }
                return;
            }

            if (direction.y > 0.5f || direction.y < -0.5f)
                this.menuSfx?.PlayFilesChange(this.player.gameObject);
            this.telegraphNavigator.HandleNavigate(direction);
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (this.telegraphNavigator == null)
            {
                this.navigator.HandleConfirm();
                return;
            }

            switch (this.telegraphPhase)
            {
                case TelegraphPhase.Browsing:
                    if (this.telegraphNavigator.IsConfirming && !this.telegraphView!.IsYesSelected)
                    {
                        this.menuSfx?.PlayCancel(this.player.gameObject);
                        this.telegraphNavigator.HandleBack();
                    }
                    else
                    {
                        this.menuSfx?.PlayDecide(this.player.gameObject);
                        this.telegraphNavigator.HandleConfirm();
                    }
                    break;
                case TelegraphPhase.Done:
                    this.menuSfx?.PlayDecide(this.player.gameObject);
                    this.telegraphNavigator.Close();
                    break;
            }
        }

        private void OnBack(InputAction.CallbackContext _)
        {
            if (this.telegraphNavigator == null)
            {
                this.navigator.HandleBack();
                return;
            }

            if (this.telegraphPhase == TelegraphPhase.Browsing)
            {
                this.menuSfx?.PlayCancel(this.player.gameObject);
                this.telegraphNavigator.HandleBack();
            }
        }

        public void Save(int slot)
        {
            int previousSaveCount = this.saveGameService.ReadFromDisk(slot)?.saveCount ?? 0;
            this.saveGameService.WriteToDisk(slot, BuildSaveData(previousSaveCount + 1));

            var onSaved = this.pendingOnSaved;
            this.pendingOnSaved = null;
            onSaved?.Invoke();
        }

        private SaveGameData BuildSaveData(int saveCount)
        {
            var data = new SaveGameData
            {
                sceneName       = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                roomId          = this.roomOrchestrator.CurrentRoom != null ? this.roomOrchestrator.CurrentRoom.RoomId : "",
                timestampIso    = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                saveCount       = saveCount,
                playtimeSeconds = this.playtimeTracker.CurrentSeconds,
                playerPosition  = this.player.transform.position,
                playerRotation  = this.player.transform.rotation,
                cameraPath      = GameplayCameraPath(),
                operatorHp      = this.roster.GetHpSnapshot(),
            };

            foreach (var pair in this.world.Doors.GetState())
                data.doors.Add(new DoorStateEntry { doorId = pair.Key, state = pair.Value });

            foreach (var pair in this.world.Rooms.GetState())
                data.rooms.Add(new RoomStateEntry { roomId = pair.Key, state = pair.Value });

            data.collectedPickupIds.AddRange(this.world.Pickups.CollectedIds);
            data.readNoteIds.AddRange(this.world.Notes.CollectedIds);
            data.knownMapIds.AddRange(this.world.KnownMaps.GetState());
            data.defeatedEnemyIds.AddRange(this.world.Enemies.GetDefeated());
            foreach (var pose in this.world.Enemies.GetCorpsePoses())
                data.enemyCorpses.Add(new EnemyCorpseEntry { enemyId = pose.EnemyKey, position = pose.Position, rotation = pose.Rotation });
            data.revivedEnemyIds.AddRange(this.world.Enemies.GetRevived());
            data.heardRadioCallIds.AddRange(this.world.RadioCalls.HeardIds);

            foreach (var pair in this.world.ItemSockets.GetState())
                data.itemSockets.Add(new ItemSocketStateEntry { socketId = pair.Key, inserted = pair.Value });

            foreach (var entry in this.world.OperatorCorpses.GetAll())
            {
                data.operatorCorpses.Add(new OperatorCorpseEntry
                {
                    slotIndex = entry.SlotIndex,
                    roomId    = entry.RoomId,
                    position  = entry.Position,
                    rotation  = entry.Rotation,
                });
            }

            foreach (var pair in this.world.Pushables.GetState())
                data.pushables.Add(new PushableStateEntry { pushableId = pair.Key, position = pair.Value });

            data.completedMilestoneIds.AddRange(this.world.Milestones.CompletedIds);
            data.solvedPuzzleIds.AddRange(this.world.Puzzles.SolvedIds);

            data.inventoryItems.AddRange(InventorySerializer.Capture(this.inventoryService));

            return data;
        }

        // The room shot the player returns to -- at the telegraph the live camera is its close-up,
        // which GameplayZoneCamera skips. Stored as a path under the room so it resolves again
        // after the scene reloads.
        private string GameplayCameraPath()
        {
            var room   = this.roomOrchestrator.CurrentRoom;
            var camera = this.zoneService.GameplayZoneCamera;
            if (room == null || camera == null || !camera.transform.IsChildOf(room.transform)) return "";

            var names = new List<string>();
            for (var t = camera.transform; t != room.transform; t = t.parent)
                names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
        }

        void IDisposable.Dispose()
        {
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIConfirm.performed  -= OnConfirm;
            this.inputService.UICancel.performed   -= OnBack;
        }
    }
}
