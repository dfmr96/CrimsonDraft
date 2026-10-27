#nullable enable

using System;
using UnityEngine.Scripting;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation.Gates
{
    /// <summary>
    /// Answers gate completion from the saved registries each gate kind already writes to, so it
    /// holds for gates whose room or scene isn't loaded. Owns no state of its own.
    /// </summary>
    public sealed class GateStateService : IGateStates, IDisposable
    {
        private readonly ItemSocketStateRegistry sockets;
        private readonly PuzzleStateRegistry     puzzles;
        private readonly RadioCallRegistry       radios;
        private readonly GateRegistry            generic;

        public event Action? Changed;

        [Preserve]
        public GateStateService(
            ItemSocketStateRegistry sockets, PuzzleStateRegistry puzzles, RadioCallRegistry radios, GateRegistry generic)
        {
            this.sockets = sockets;
            this.puzzles = puzzles;
            this.radios  = radios;
            this.generic = generic;

            this.sockets.Changed += OnChanged;
            this.puzzles.Changed += OnChanged;
            this.radios.Changed  += OnChanged;
            this.generic.Changed += OnChanged;
        }

        public bool IsCompleted(string gateId)
        {
            if (!GateIds.TryParse(gateId, out var kind, out var id)) return false;
            return kind switch
            {
                // The socket records completion itself (filled, and turned when it has a valve).
                GateKind.Socket     => IsSet(this.sockets.GetInserted(ItemSocketInteractable.CompletedKey(id))),
                GateKind.Puzzle     => this.puzzles.IsSolved(id),
                GateKind.Radio      => this.radios.IsHeard(id),
                GateKind.Generic    => this.generic.IsCompleted(id),
                GateKind.SocketSlot => IsSlotFilled(id),
                _                   => false,
            };
        }

        private static bool IsSet(bool[] flag) => flag.Length > 0 && flag[0];

        // id is "<socketId>#<slot index>": the slot's own flag in the socket's saved inserted[] array.
        private bool IsSlotFilled(string id)
        {
            int split = id.LastIndexOf('#');
            if (split <= 0 || !int.TryParse(id.Substring(split + 1), out int slot) || slot < 0) return false;
            var inserted = this.sockets.GetInserted(id.Substring(0, split));
            return slot < inserted.Length && inserted[slot];
        }

        private void OnChanged() => Changed?.Invoke();

        public void Dispose()
        {
            this.sockets.Changed -= OnChanged;
            this.puzzles.Changed -= OnChanged;
            this.radios.Changed  -= OnChanged;
            this.generic.Changed -= OnChanged;
        }
    }
}
