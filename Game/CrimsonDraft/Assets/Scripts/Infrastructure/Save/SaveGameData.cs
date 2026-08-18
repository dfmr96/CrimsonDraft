#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Save
{
    [Serializable]
    public sealed class DoorStateEntry
    {
        public string       doorId = "";
        public DoorMapState state;
    }

    [Serializable]
    public sealed class RoomStateEntry
    {
        public string       roomId = "";
        public RoomMapState state;
    }

    [Serializable]
    public sealed class InventorySlotEntry
    {
        public int    slotIndex;
        public string itemId = "";
        public int    slotQuantity;
        public int    ammoBoxQuantity      = -1; // AmmoBoxItem.Quantity; -1 = not an ammo box
        public int    weaponAmmo           = -1; // WeaponItem.CurrentAmmo; -1 = not a weapon
        public int    keyUsesRemaining     = -1; // KeyItem.UsesRemaining; -1 = not a key item
        public bool   isExamined;
        public int    gridCol              = -1;
        public int    gridRow              = -1;
        public int    gridRotation;
        public int    equippedOperatorSlot = -1;
        public int    equippedWeaponSlot   = -1;
    }

    [Serializable]
    public sealed class SaveGameData
    {
        public string sceneName    = "";
        public string roomId       = "";
        public string timestampIso = "";
        public float  playtimeSeconds;

        public Vector3    playerPosition;
        public Quaternion playerRotation = Quaternion.identity;

        public List<DoorStateEntry>     doors              = new List<DoorStateEntry>();
        public List<RoomStateEntry>     rooms              = new List<RoomStateEntry>();
        public List<string>             collectedPickupIds = new List<string>();
        public List<string>             readNoteIds        = new List<string>();
        public List<string>             knownMapIds        = new List<string>();
        public List<string>             defeatedEnemyIds   = new List<string>();
        public List<InventorySlotEntry> inventorySlots     = new List<InventorySlotEntry>();
        public int[]                    operatorHp         = Array.Empty<int>();
    }
}
