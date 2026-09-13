#nullable enable

using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Player.Movement;

namespace CrimsonDraft.Tests
{
    public sealed class ModernPlayerMovementStrategyTests
    {
        private FakeCameraRelativeMovementService cameraService = null!;
        private ModernPlayerMovementStrategy      strategy      = null!;
        private Transform                         playerTransform = null!;
        private Gamepad?                          gamepad;

        [SetUp]
        public void SetUp()
        {
            this.cameraService   = new FakeCameraRelativeMovementService();
            this.strategy        = new ModernPlayerMovementStrategy(this.cameraService);
            this.playerTransform = new GameObject("Player").transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (this.playerTransform != null) Object.DestroyImmediate(this.playerTransform.gameObject);
            if (this.gamepad != null) InputSystem.RemoveDevice(this.gamepad);
        }

        [Test]
        public void Tick_alwaysTicksCameraService_evenWhileAiming()
        {
            this.strategy.Tick(this.playerTransform, new Vector2(1f, 0f), null, isAiming: true, deltaTime: 0.02f);

            Assert.AreEqual(1, this.cameraService.TickCallCount);
        }

        [Test]
        public void Tick_whileAiming_returnsIdle()
        {
            var result = this.strategy.Tick(this.playerTransform, new Vector2(1f, 0f), null, isAiming: true, deltaTime: 0.02f);

            Assert.AreEqual(Vector3.zero, result.Direction);
        }

        [Test]
        public void Tick_whileAiming_doesNotRotateTransform()
        {
            this.playerTransform.forward = Vector3.back;

            this.strategy.Tick(this.playerTransform, new Vector2(1f, 0f), null, isAiming: true, deltaTime: 0.02f);

            Assert.AreEqual(Vector3.back, this.playerTransform.forward);
        }

        [Test]
        public void Tick_keyboardDevice_quantizesDiagonalInput_andCombinesRightForward()
        {
            this.cameraService.Right   = Vector3.right;
            this.cameraService.Forward = Vector3.forward;

            var result = this.strategy.Tick(this.playerTransform, new Vector2(0.6f, 0.6f), null, isAiming: false, deltaTime: 0.02f);

            var expected = new Vector3(1f, 0f, 1f).normalized;
            Assert.Less(Vector3.Distance(expected, result.Direction), 0.001f);
            Assert.AreEqual(new Vector2(1f, 1f).normalized, this.cameraService.LastTickDirection);
        }

        [Test]
        public void Tick_gamepadDevice_normalizesInput_andCombinesRightForward()
        {
            this.gamepad = InputSystem.AddDevice<Gamepad>();
            this.cameraService.Right   = Vector3.right;
            this.cameraService.Forward = Vector3.forward;

            var result = this.strategy.Tick(this.playerTransform, new Vector2(0.3f, 0.4f), this.gamepad, isAiming: false, deltaTime: 0.02f);

            var expected = new Vector3(0.6f, 0f, 0.8f);
            Assert.Less(Vector3.Distance(expected, result.Direction), 0.001f);
            Assert.Less(Vector2.Distance(new Vector2(0.6f, 0.8f), this.cameraService.LastTickDirection), 0.001f);
        }

        [Test]
        public void Tick_nonZeroDirection_setsTransformForward()
        {
            this.cameraService.Right   = Vector3.right;
            this.cameraService.Forward = Vector3.forward;

            var result = this.strategy.Tick(this.playerTransform, new Vector2(0f, 1f), null, isAiming: false, deltaTime: 0.02f);

            // transform.forward round-trips through a quaternion internally, so compare with a
            // tolerance rather than exact float equality.
            Assert.Less(Vector3.Distance(result.Direction, this.playerTransform.forward), 0.0001f);
        }

        [Test]
        public void Tick_zeroInput_returnsIdleDirection_andDoesNotRotateTransform()
        {
            this.playerTransform.forward = Vector3.back;
            this.cameraService.Right     = Vector3.right;
            this.cameraService.Forward   = Vector3.forward;

            var result = this.strategy.Tick(this.playerTransform, Vector2.zero, null, isAiming: false, deltaTime: 0.02f);

            Assert.AreEqual(Vector3.zero, result.Direction);
            Assert.AreEqual(Vector3.back, this.playerTransform.forward);
        }

        [Test]
        public void Tick_alwaysAllowsSprint()
        {
            this.cameraService.Right   = Vector3.right;
            this.cameraService.Forward = Vector3.forward;

            var result = this.strategy.Tick(this.playerTransform, new Vector2(0f, 1f), null, isAiming: false, deltaTime: 0.02f);

            Assert.IsTrue(result.AllowSprint);
        }

        private sealed class FakeCameraRelativeMovementService : ICameraRelativeMovementService
        {
            public int     TickCallCount     { get; private set; }
            public Vector2 LastTickDirection { get; private set; }
            public Vector3 Forward           { get; set; } = Vector3.forward;
            public Vector3 Right             { get; set; } = Vector3.right;

            public void Tick(Vector2 heldDirection)
            {
                this.TickCallCount++;
                this.LastTickDirection = heldDirection;
            }
        }
    }
}
