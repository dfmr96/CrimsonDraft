#nullable enable

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Infrastructure.Save.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class SaveController : IInitializable, IDisposable
    {
        private readonly IInputService       inputService;
        private readonly ISaveGameService    saveGameService;
        private readonly IInventoryService   inventoryService;
        private readonly IOperatorRoster     roster;
        private readonly IRoomOrchestrator   roomOrchestrator;
        private readonly PlayerController    player;
        private readonly WorldStateRegistries world;
        private readonly SaveSlotNavigator   navigator;

        [Preserve]
        public SaveController(
            IInputService        inputService,
            SaveSlotListView     view,
            ISaveGameService     saveGameService,
            IInventoryService    inventoryService,
            IOperatorRoster      roster,
            IRoomOrchestrator    roomOrchestrator,
            PlayerController     player,
            WorldStateRegistries world)
        {
            this.inputService     = inputService;
            this.saveGameService  = saveGameService;
            this.inventoryService = inventoryService;
            this.roster           = roster;
            this.roomOrchestrator = roomOrchestrator;
            this.player           = player;
            this.world            = world;
            this.navigator = new SaveSlotNavigator(view, "Save to", Save, canConfirm: null, onClosed: OnNavigatorClosed);
        }

        void IInitializable.Initialize()
        {
            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIConfirm.performed  += OnConfirm;
            this.inputService.UICancel.performed   += OnBack;
        }

        public void Open()
        {
            if (this.navigator.IsOpen) return;
            Time.timeScale = 0f;
            this.inputService.SwitchToUI();
            this.navigator.Open(this.saveGameService.ListSlotSummaries());
        }

        private void OnNavigatorClosed()
        {
            Time.timeScale = 1f;
            DeferredInputAction.Run(this.inputService.SwitchToGameplay);
        }

        private void OnNavigate(InputAction.CallbackContext ctx) => this.navigator.HandleNavigate(ctx.ReadValue<Vector2>());
        private void OnConfirm(InputAction.CallbackContext _)    => this.navigator.HandleConfirm();
        private void OnBack(InputAction.CallbackContext _)       => this.navigator.HandleBack();

        public void Save(int slot)
        {
            this.saveGameService.WriteToDisk(slot, BuildSaveData());
        }

        private SaveGameData BuildSaveData()
        {
            var data = new SaveGameData
            {
                sceneName       = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                roomId          = this.roomOrchestrator.CurrentRoom != null ? this.roomOrchestrator.CurrentRoom.RoomId : "",
                timestampIso    = DateTime.UtcNow.ToString("o"),
                playtimeSeconds = Time.realtimeSinceStartup,
                playerPosition  = this.player.transform.position,
                playerRotation  = this.player.transform.rotation,
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

            var slots = this.inventoryService.GetRawSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty) continue;
                var item = slots[i].Item!;
                data.inventorySlots.Add(new InventorySlotEntry
                {
                    slotIndex            = i,
                    itemId               = item.Data.ItemId,
                    slotQuantity         = slots[i].Quantity,
                    ammoBoxQuantity      = item is AmmoBoxItem box ? box.Quantity : -1,
                    weaponAmmo           = item is WeaponItem weapon ? weapon.CurrentAmmo : -1,
                    keyUsesRemaining     = item is KeyItem key ? key.UsesRemaining : -1,
                    isExamined           = item.IsExamined,
                    gridCol              = slots[i].GridCol,
                    gridRow              = slots[i].GridRow,
                    gridRotation         = slots[i].GridRotation,
                    equippedOperatorSlot = item.EquippedBySlot,
                    equippedWeaponSlot   = item.EquippedWeaponSlot,
                });
            }

            return data;
        }

        void IDisposable.Dispose()
        {
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIConfirm.performed  -= OnConfirm;
            this.inputService.UICancel.performed   -= OnBack;
        }
    }
}
