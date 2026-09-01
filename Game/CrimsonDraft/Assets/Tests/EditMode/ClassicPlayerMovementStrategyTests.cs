#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Player.Movement;

namespace CrimsonDraft.Tests
{
    public sealed class ClassicPlayerMovementStrategyTests
    {
        private ClassicPlayerMovementStrategy strategy        = null!;
        private Transform                     playerTransform = null!;

        [SetUp]
        public void SetUp()
        {
            this.strategy        = new ClassicPlayerMovementStrategy();
            this.playerTransform = new GameObject("Player").transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (this.playerTransform != null) Object.DestroyImmediate(this.playerTransform.gameObject);
        }

        [Test]
        public void Tick_positiveX_rotatesAroundWorldUp_atTurnSpeedTimesDeltaTime()
        {
            this.strategy.Tick(this.playerTransform, new Vector2(1f, 0f), null, isAiming: false, deltaTime: 0.5f);

            // turnSpeedDegPerSec (180) * x (1) * deltaTime (0.5) = 90 degrees around world up.
            var expected = Quaternion.AngleAxis(90f, Vector3.up) * Vector3.forward;
            Assert.Less(Vector3.Distance(expected, this.playerTransform.forward), 0.01f);
        }

        [Test]
        public void Tick_negativeX_rotatesTheOppositeWay()
        {
            this.strategy.Tick(this.playerTransform, new Vector2(-1f, 0f), null, isAiming: false, deltaTime: 0.5f);

            var expected = Quaternion.AngleAxis(-90f, Vector3.up) * Vector3.forward;
            Assert.Less(Vector3.Distance(expected, this.playerTransform.forward), 0.01f);
        }

        [Test]
        public void Tick_zeroX_doesNotRotate()
        {
            this.strategy.Tick(this.playerTransform, new Vector2(0f, 1f), null, isAiming: false, deltaTime: 0.5f);

            Assert.AreEqual(Vector3.forward, this.playerTransform.forward);
        }

        [Test]
        public void Tick_positiveY_returnsCurrentForward_allowsSprint()
        {
            this.playerTransform.forward = Vector3.right;

            var result = this.strategy.Tick(this.playerTransform, new Vector2(0f, 1f), null, isAiming: false, deltaTime: 0.02f);

            // transform.forward round-trips through a quaternion internally, so compare with a
            // tolerance rather than exact float equality.
            Assert.Less(Vector3.Distance(Vector3.right, result.Direction), 0.0001f);
            Assert.IsTrue(result.AllowSprint);
        }

        [Test]
        public void Tick_negativeY_returnsOppositeOfForward_disallowsSprint()
        {
            this.playerTransform.forward = Vector3.right;

            var result = this.strategy.Tick(this.playerTransform, new Vector2(0f, -1f), null, isAiming: false, deltaTime: 0.02f);

            Assert.Less(Vector3.Distance(-Vector3.right, result.Direction), 0.0001f);
            Assert.IsFalse(result.AllowSprint);
        }

        [Test]
        public void Tick_zeroY_returnsIdleDirection_turningInPlaceIsValid()
        {
            var result = this.strategy.Tick(this.playerTransform, new Vector2(1f, 0f), null, isAiming: false, deltaTime: 0.5f);

            Assert.AreEqual(Vector3.zero, result.Direction);
            // Rotation still happened even though there's no translation this frame.
            Assert.AreNotEqual(Vector3.forward, this.playerTransform.forward);
        }

        [Test]
        public void Tick_whileAiming_returnsIdle_andDoesNotRotate()
        {
            var result = this.strategy.Tick(this.playerTransform, new Vector2(1f, 1f), null, isAiming: true, deltaTime: 0.5f);

            Assert.AreEqual(Vector3.zero, result.Direction);
            Assert.AreEqual(Vector3.forward, this.playerTransform.forward);
        }
    }
}
