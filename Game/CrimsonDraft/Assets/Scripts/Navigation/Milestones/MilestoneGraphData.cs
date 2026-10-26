#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Gates;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation.Milestones
{
    public enum EdgeStyle { Solid, Dashed }

    // Serialized as numbers: never renumber. 3 (SocketCompleted) and 6 (PuzzleSolved) were folded into
    // GateCompleted; MilestoneTrigger.UpgradeLegacy converts graphs still holding them.
    public enum MilestoneTriggerKind
    {
        None            = 0,
        PickupCollected = 1,
        ItemHeld        = 2,
        DoorUnlocked    = 4,
        NoteRead        = 5,
        GateCompleted   = 7,
    }

    [Serializable]
    public sealed class MilestoneLane
    {
        public string id    = "";
        public string name  = "Lane";
        public Color  color = Color.white;
    }

    [Serializable]
    public sealed class MilestoneTrigger
    {
        public MilestoneTriggerKind kind;
        public string        targetId = "";   // pickupId / doorId / gate id (GateIds: "socket:x", "puzzle:x", "radio:x")
        public ItemData?     item;            // ItemHeld
        public DocumentData? note;            // NoteRead

        private const int LegacySocketCompleted = 3;
        private const int LegacyPuzzleSolved    = 6;

        /// <summary>Turns a pre-gates SocketCompleted / PuzzleSolved trigger into GateCompleted with the
        /// matching gate id. Returns whether anything changed.</summary>
        public bool UpgradeLegacy()
        {
            switch ((int)this.kind)
            {
                case LegacySocketCompleted: this.targetId = Prefixed(GateIds.Socket); break;
                case LegacyPuzzleSolved:    this.targetId = Prefixed(GateIds.Puzzle); break;
                default:                    return false;
            }
            this.kind = MilestoneTriggerKind.GateCompleted;
            return true;
        }

        private string Prefixed(Func<string, string> gateId) =>
            string.IsNullOrEmpty(this.targetId) ? "" : gateId(this.targetId);
    }

    [Serializable]
    public sealed class MilestoneNode
    {
        public string           id       = "";
        public string           title    = "";
        public string           laneId   = "";
        public Vector2          position;
        public MilestoneTrigger trigger  = new();
    }

    // Documentation only: arrows never gate a milestone.
    [Serializable]
    public sealed class MilestoneEdge
    {
        public string    fromNodeId = "";
        public string    toNodeId   = "";
        public EdgeStyle style;
        public string    laneId = "";
        public bool      useCustomColor;
        public Color     customColor = Color.white;
    }

    [Serializable]
    public sealed class MilestoneNote
    {
        public string id   = "";
        public string text = "";
        public Rect   rect = new(0f, 0f, 200f, 120f);
    }

    [Serializable]
    public sealed class MilestoneFrame
    {
        public string       id      = "";
        public string       title   = "";
        public string       laneId  = "";
        public List<string> nodeIds = new();
    }
}
