#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Editor;

namespace CrimsonDraft.Tests
{
    public sealed class PlanViewFramingTests
    {
        [Test]
        public void Fit_wideRoom_sizesOnWidthOverAspect()
        {
            var (_, _, size) = PlanViewFraming.Fit(new Bounds(Vector3.zero, new Vector3(20, 3, 6)), 2f, 1f, Vector2.zero);
            Assert.That(size, Is.EqualTo(5.5f).Within(1e-4));
        }

        [Test]
        public void Fit_tallRoom_sizesOnDepth()
        {
            var (_, _, size) = PlanViewFraming.Fit(new Bounds(Vector3.zero, new Vector3(4, 3, 10)), 1f, 1f, Vector2.zero);
            Assert.That(size, Is.EqualTo(5.5f).Within(1e-4));
        }

        [Test]
        public void Fit_zoomAndPan_scaleAndOffset()
        {
            var room = new Bounds(new Vector3(10, 1, 20), new Vector3(4, 2, 10));
            var (position, _, size) = PlanViewFraming.Fit(room, 1f, 2f, new Vector2(1, 2));
            Assert.That(size, Is.EqualTo(2.75f).Within(1e-4));
            Assert.That(position.x, Is.EqualTo(11f).Within(1e-4));
            Assert.That(position.z, Is.EqualTo(22f).Within(1e-4));
            Assert.That(position.y, Is.EqualTo(room.max.y + 10f).Within(1e-4));
        }

        [Test]
        public void Fit_emptyBounds_usesDefaultSize()
        {
            var (_, _, size) = PlanViewFraming.Fit(new Bounds(Vector3.zero, Vector3.zero), 1.5f, 1f, Vector2.zero);
            Assert.That(size, Is.EqualTo(PlanViewFraming.DefaultHalfSize).Within(1e-4));
            Assert.IsFalse(float.IsNaN(size));
        }

        [Test]
        public void NearClip_cutsAboveHeadHeight()
        {
            // Camera 12 m up, floor at 1: everything above 1 + CutHeight is clipped away (ceilings).
            Assert.That(PlanViewFraming.NearClip(12f, 1f), Is.EqualTo(12f - 1f - PlanViewFraming.CutHeight).Within(1e-4));
            Assert.That(PlanViewFraming.NearClip(1f, 1f), Is.EqualTo(0.1f).Within(1e-4));
        }

        [Test]
        public void Fit_looksStraightDown()
        {
            var (_, rotation, _) = PlanViewFraming.Fit(new Bounds(Vector3.zero, Vector3.one), 1f, 1f, Vector2.zero);
            Assert.That(Vector3.Dot(rotation * Vector3.forward, Vector3.down), Is.EqualTo(1f).Within(1e-4));
        }
    }
}
