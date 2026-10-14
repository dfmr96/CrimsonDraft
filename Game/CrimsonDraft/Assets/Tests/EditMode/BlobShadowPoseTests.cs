#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Rendering.Shadows;

namespace CrimsonDraft.Tests
{
    public sealed class BlobShadowPoseTests
    {
        private const float Tolerance = 1e-4f;

        private static void AssertVector(Vector3 expected, Vector3 actual)
            => Assert.Less(Vector3.Distance(expected, actual), Tolerance, $"expected {expected} but was {actual}");

        [Test]
        public void FlatGround_positionIsHitPointLiftedByBias()
        {
            var pose = BlobShadowPose.FromGroundHit(new Vector3(2f, 0f, 3f), Vector3.up, Vector3.right, 0.01f);

            AssertVector(new Vector3(2f, 0.01f, 3f), pose.position);
        }

        [Test]
        public void FlatGround_upAxisMatchesGroundNormal()
        {
            var pose = BlobShadowPose.FromGroundHit(Vector3.zero, Vector3.up, Vector3.right, 0f);

            AssertVector(Vector3.up, pose.rotation * Vector3.up);
        }

        [Test]
        public void Slope_upAxisMatchesSlopeNormal()
        {
            var slopeNormal = new Vector3(0f, 1f, 1f).normalized;

            var pose = BlobShadowPose.FromGroundHit(Vector3.zero, slopeNormal, Vector3.right, 0f);

            AssertVector(slopeNormal, pose.rotation * Vector3.up);
        }

        [Test]
        public void Slope_biasIsAppliedAlongWorldUp()
        {
            var slopeNormal = new Vector3(0f, 1f, 1f).normalized;

            var pose = BlobShadowPose.FromGroundHit(Vector3.zero, slopeNormal, Vector3.right, 0.01f);

            AssertVector(new Vector3(0f, 0.01f, 0f), pose.position);
        }

        [Test]
        public void ForwardAxisFollowsOwnerFacing()
        {
            var pose = BlobShadowPose.FromGroundHit(Vector3.zero, Vector3.up, Vector3.forward, 0f);

            AssertVector(Vector3.Cross(Vector3.forward, Vector3.up), pose.rotation * Vector3.forward);
        }

        [Test]
        public void FootprintScale_widthMapsToShoulderAxisAndDepthToFacingAxis()
        {
            var scale = BlobShadowPose.FootprintScale(new Vector2(0.9f, 0.5f), 1f);

            AssertVector(new Vector3(0.9f, 1f, 0.5f), scale);
        }

        [Test]
        public void FootprintScale_pulseScalesFootprintUniformly()
        {
            var scale = BlobShadowPose.FootprintScale(new Vector2(0.9f, 0.5f), 1.2f);

            AssertVector(new Vector3(1.08f, 1.2f, 0.6f), scale);
        }

        [Test]
        public void OwnerRightParallelToNormal_stillProducesOrientationAlignedToNormal()
        {
            var pose = BlobShadowPose.FromGroundHit(Vector3.zero, Vector3.right, Vector3.right, 0f);

            AssertVector(Vector3.right, pose.rotation * Vector3.up);
        }
    }
}
