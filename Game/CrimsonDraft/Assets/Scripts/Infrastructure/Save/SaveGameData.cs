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
    public sealed class OperatorCorpseEntry
    {
        public int        slotIndex;
        public string     roomId = "";
        public Vector3    position;
        public Quaternion rotation = Quaternion.identity;
    }

    [Serializable]
    public sealed class EnemyCorpseEntry
    {
        public string     enemyId = "";
        public Vector3    position;
        public Quaternion rotation = Quaternion.identity;
    }

    [Serializable]
    public sealed class ItemSocketStateEntry
    {
        public string  socketId = "";
        public bool[]  inserted = Array.Empty<bool>();
    }

    [Serializable]
    public sealed class PushableStateEntry
    {
        public string  pushableId = "";
        public Vector3 position;
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
    public sealed class InventoryItemEntry
    {
        public int    containerKind;
        public int    containerIndex;
        public string itemId = "";
        public int    quantity;
        public int    col = -1;
        public int    row = -1;
        public int    rotation;
        public int    weaponAmmo           = -1;
        public int    keyUsesRemaining     = -1;
        public bool   isExamined;
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
        public int    saveCount;

        public Vector3    playerPosition;
        public Quaternion playerRotation = Quaternion.identity;
        // Fixed-camera shot live at save time, as a transform path under the room. Empty in saves
        // made before it existed; the room then picks its shot from the player position.
        public string     cameraPath     = "";

        public List<DoorStateEntry>     doors              = new List<DoorStateEntry>();
        public List<RoomStateEntry>     rooms              = new List<RoomStateEntry>();
        public List<string>             collectedPickupIds = new List<string>();
        public List<string>             readNoteIds        = new List<string>();
        public List<string>             knownMapIds        = new List<string>();
        public List<string>             defeatedEnemyIds   = new List<string>();
        public List<EnemyCorpseEntry>   enemyCorpses       = new List<EnemyCorpseEntry>();
        public List<string>             revivedEnemyIds    = new List<string>();
        public List<string>             heardRadioCallIds  = new List<string>();
        public List<ItemSocketStateEntry> itemSockets      = new List<ItemSocketStateEntry>();
        public List<OperatorCorpseEntry> operatorCorpses   = new List<OperatorCorpseEntry>();
        public List<PushableStateEntry> pushables          = new List<PushableStateEntry>();
        public List<string>             completedMilestoneIds = new List<string>();
        public List<string>             solvedPuzzleIds       = new List<string>();
        public List<InventorySlotEntry> inventorySlots     = new List<InventorySlotEntry>();
        public List<InventoryItemEntry> inventoryItems     = new List<InventoryItemEntry>();
        public int[]                    operatorHp         = Array.Empty<int>();
    }
}
