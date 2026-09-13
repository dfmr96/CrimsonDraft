#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;

namespace CrimsonDraft.Tests
{
    // CameraRelativeMovementService.Tick() itself needs a live CinemachineBrain/vcam setup to
    // exercise end-to-end (which camera it resamples), so these tests target the pure decision
    // it delegates to instead: given a held stick direction, when does a fixed-camera-relative
    // "north" get re-anchored to whatever camera is active right now? That's the actual policy
    // this fix changes (deadzone-gated direction change, not release-to-neutral), and it's
    // fully testable without any Cinemachine dependency.
    public sealed class CameraRelativeMovementServiceTests
    {
        private const float Deadzone = 15f;

        [Test]
        public void NoReferenceYet_alwaysResamples()
        {
            bool result = CameraRelativeMovementService.ShouldResampleBasis(
                referenceIsSet: false, referenceDirection: Vector2.zero, heldDirection: Vector2.up, Deadzone);

            Assert.IsTrue(result);
        }

        [Test]
        public void SameDirectionHeld_staysWithinDeadzone_doesNotResample()
        {
            bool result = CameraRelativeMovementService.ShouldResampleBasis(
                referenceIsSet: true, referenceDirection: Vector2.up, heldDirection: Vector2.up, Deadzone);

            Assert.IsFalse(result);
        }

        [Test]
        public void SmallJitterWithinDeadzone_doesNotResample()
        {
            // ~10 degrees off "up" -- within the 15-degree deadzone, should read as noise.
            var jittered = new Vector2(Mathf.Sin(10f * Mathf.Deg2Rad), Mathf.Cos(10f * Mathf.Deg2Rad));

            bool result = CameraRelativeMovementService.ShouldResampleBasis(
                referenceIsSet: true, referenceDirection: Vector2.up, heldDirection: jittered, Deadzone);

            Assert.IsFalse(result);
        }

        [Test]
        public void DirectionChangeBeyondDeadzone_resamplesEvenWithoutReleasing()
        {
            // A full quadrant swing (up -> right) while still holding the stick fully
            // deflected -- this is exactly the case release-to-neutral used to miss.
            bool result = CameraRelativeMovementService.ShouldResampleBasis(
                referenceIsSet: true, referenceDirection: Vector2.up, heldDirection: Vector2.right, Deadzone);

            Assert.IsTrue(result);
        }

        [Test]
        public void DirectionChangeJustBelowDeadzoneBoundary_doesNotResample()
        {
            // Testing the exact boundary angle is at the mercy of Sin/Cos/Angle rounding
            // (constructing a vector at precisely 15 degrees can come back as 15.0000n when
            // re-measured) -- half a degree under it is a robust proxy for "still inside".
            var justBelow = new Vector2(Mathf.Sin((Deadzone - 0.5f) * Mathf.Deg2Rad), Mathf.Cos((Deadzone - 0.5f) * Mathf.Deg2Rad));

            bool result = CameraRelativeMovementService.ShouldResampleBasis(
                referenceIsSet: true, referenceDirection: Vector2.up, heldDirection: justBelow, Deadzone);

            Assert.IsFalse(result);
        }

        [Test]
        public void DirectionChangeJustPastDeadzoneBoundary_resamples()
        {
            var pastBoundary = new Vector2(Mathf.Sin((Deadzone + 1f) * Mathf.Deg2Rad), Mathf.Cos((Deadzone + 1f) * Mathf.Deg2Rad));

            bool result = CameraRelativeMovementService.ShouldResampleBasis(
                referenceIsSet: true, referenceDirection: Vector2.up, heldDirection: pastBoundary, Deadzone);

            Assert.IsTrue(result);
        }

        [Test]
        public void Tick_releasingStick_clearsReferenceSoNextPressAlwaysResamples()
        {
            var brainGo = new GameObject("TestBrain");
            try
            {
                var service = new CameraRelativeMovementService(brainGo.AddComponent<Unity.Cinemachine.CinemachineBrain>());

                service.Tick(Vector2.up);   // establishes a reference direction
                service.Tick(Vector2.zero); // release -- must drop it, not just leave it stale

                Assert.IsFalse(GetReferenceIsSet(service));
            }
            finally
            {
                Object.DestroyImmediate(brainGo);
            }
        }

        private static bool GetReferenceIsSet(CameraRelativeMovementService service)
        {
            var field = typeof(CameraRelativeMovementService).GetField("referenceIsSet",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (bool)field!.GetValue(service)!;
        }
    }
}
