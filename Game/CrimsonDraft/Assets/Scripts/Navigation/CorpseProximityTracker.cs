#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation
{
    public sealed class CorpseProximityTracker : ICorpseAccess
    {
        private readonly HashSet<int> slotsInRange = new HashSet<int>();

        [Preserve]
        public CorpseProximityTracker() { }

        public bool CanAccess(int operatorSlot) => this.slotsInRange.Contains(operatorSlot);

        public void Enter(int operatorSlot) => this.slotsInRange.Add(operatorSlot);

        public void Exit(int operatorSlot) => this.slotsInRange.Remove(operatorSlot);
    }
}
