#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation.Milestones
{
    public enum EdgeStyle { Solid, Dashed }

    public enum MilestoneTriggerKind { None, PickupCollected, ItemHeld, SocketCompleted, DoorUnlocked, NoteRead, PuzzleSolved }

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
        public string        targetId = "";   // pickupId / socketId / doorId / puzzleId
        public ItemData?     item;            // ItemHeld
        public DocumentData? note;            // NoteRead
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
