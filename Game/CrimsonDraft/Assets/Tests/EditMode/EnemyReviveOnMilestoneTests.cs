#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Enemy;
using Step = CrimsonDraft.Navigation.Enemy.EnemyReviveOnMilestone.Step;

namespace CrimsonDraft.Tests
{
    public sealed class EnemyReviveOnMilestoneTests
    {
        private const float Radius = 3f;

        [Test]
        public void Decide_deadCorpse_waitsUntilThePlayerIsWithinRadius()
        {
            Assert.AreEqual(Step.Wait,   EnemyReviveOnMilestone.Decide(isDead: true, isRising: false, hasBeenRevived: false, playerDistance: 3.5f, Radius));
            Assert.AreEqual(Step.Revive, EnemyReviveOnMilestone.Decide(isDead: true, isRising: false, hasBeenRevived: false, playerDistance: 3f,   Radius));
        }

        [Test]
        public void Decide_aliveWhenTriggered_spendsTheRevive()
            => Assert.AreEqual(Step.Spend, EnemyReviveOnMilestone.Decide(isDead: false, isRising: false, hasBeenRevived: false, playerDistance: 10f, Radius));

        [Test]
        public void Decide_alreadyRevivedOnce_neverRevivesAgain()
            => Assert.AreEqual(Step.Wait, EnemyReviveOnMilestone.Decide(isDead: true, isRising: false, hasBeenRevived: true, playerDistance: 0f, Radius));

        [Test]
        public void Decide_whileRising_waits()
            => Assert.AreEqual(Step.Wait, EnemyReviveOnMilestone.Decide(isDead: false, isRising: true, hasBeenRevived: false, playerDistance: 0f, Radius));

        [Test]
        public void HorizontalDistance_ignoresHeight()
            => Assert.AreEqual(2f, EnemyReviveOnMilestone.HorizontalDistance(new Vector3(0f, 0f, 0f), new Vector3(2f, 5f, 0f)), 1e-5f);
    }
}
