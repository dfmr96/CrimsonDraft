using NUnit.Framework;
using CrimsonDraft.Combat;

namespace CrimsonDraft.Tests
{
    public sealed class CombatActionQueueTests
    {
        [Test]
        public void HasPending_falseWhenEmpty()
        {
            var queue = new CombatActionQueue();
            Assert.IsFalse(queue.HasPending);
        }

        [Test]
        public void Enqueue_increasesCount()
        {
            var queue = new CombatActionQueue();
            queue.Enqueue(PendingAction.Defend(0));
            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void Peek_doesNotRemove()
        {
            var queue = new CombatActionQueue();
            queue.Enqueue(PendingAction.Defend(0));
            _ = queue.Peek();
            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void Dequeue_removesFromFront()
        {
            var queue = new CombatActionQueue();
            queue.Enqueue(PendingAction.Defend(0));
            queue.Enqueue(PendingAction.EnemyAttack(0, 1, 10));
            PendingAction first = queue.Dequeue();
            Assert.AreEqual(PendingActionType.Defend, first.Type);
            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void Dequeue_preservesFifoOrder()
        {
            var queue = new CombatActionQueue();
            queue.Enqueue(PendingAction.Shoot(0));
            queue.Enqueue(PendingAction.Reload(1, 2));
            queue.Enqueue(PendingAction.EnemyAttack(0, 0, 15));
            Assert.AreEqual(PendingActionType.Shoot,       queue.Dequeue().Type);
            Assert.AreEqual(PendingActionType.Reload,      queue.Dequeue().Type);
            Assert.AreEqual(PendingActionType.EnemyAttack, queue.Dequeue().Type);
        }

        [Test]
        public void Clear_emptiesQueue()
        {
            var queue = new CombatActionQueue();
            queue.Enqueue(PendingAction.Defend(0));
            queue.Enqueue(PendingAction.Shoot(1));
            queue.Clear();
            Assert.AreEqual(0, queue.Count);
            Assert.IsFalse(queue.HasPending);
        }

        [Test]
        public void PendingAction_Reload_storesPayload()
        {
            var action = PendingAction.Reload(operatorSlot: 2, ammoBoxIndex: 5);
            Assert.AreEqual(PendingActionType.Reload, action.Type);
            Assert.AreEqual(2, action.SlotIndex);
            Assert.AreEqual(5, action.AmmoBoxIndex);
        }

        [Test]
        public void PendingAction_EnemyAttack_storesPayload()
        {
            var action = PendingAction.EnemyAttack(enemySlot: 1, targetOperatorSlot: 0, damage: 25);
            Assert.AreEqual(PendingActionType.EnemyAttack, action.Type);
            Assert.AreEqual(1,  action.SlotIndex);
            Assert.AreEqual(0,  action.TargetOperatorSlot);
            Assert.AreEqual(25, action.Damage);
        }
    }
}
