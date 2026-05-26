#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Combat
{
    public sealed class CombatActionQueue
    {
        private readonly Queue<PendingAction> queue = new();

        public int  Count      => this.queue.Count;
        public bool HasPending => this.queue.Count > 0;

        public void          Enqueue(PendingAction action) => this.queue.Enqueue(action);
        public PendingAction Peek()                        => this.queue.Peek();
        public PendingAction Dequeue()                     => this.queue.Dequeue();
        public void          Clear()                       => this.queue.Clear();
        public PendingAction[] ToArray()                   => this.queue.ToArray();
    }
}
