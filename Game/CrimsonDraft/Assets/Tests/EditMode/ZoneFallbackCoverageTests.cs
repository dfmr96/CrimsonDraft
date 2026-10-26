#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Editor;
using CrimsonDraft.Navigation.Gates;

namespace CrimsonDraft.Tests
{
    /// <summary>The Camera Zone Viewer's "Fallback" view: where a gated (priority) zone stops firing once
    /// its gate completes, the cells no ordinary zone covers are holes -- nothing would switch back to a
    /// normal camera there.</summary>
    public sealed class ZoneFallbackCoverageTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in this.created) if (o != null) Object.DestroyImmediate(o);
            this.created.Clear();
        }

        [Test]
        public void GatedZoneFullyInsideAnOrdinaryOne_hasNoHoles()
        {
            var zones = new[] { Zone(-4, 4, gated: false), Zone(0, 2, gated: true) };
            Assert.AreEqual(0, ZoneFallbackCoverage.Holes(zones, 0.25f).Count);
        }

        [Test]
        public void PartOfTheGatedZoneOutsideEveryOrdinaryZone_isAHole()
        {
            // Gated x 0..2 (z -2..2); ordinary only up to x = 1: the x 1..2 half is uncovered.
            var zones = new[] { Zone(-4, 1, gated: false), Zone(0, 2, gated: true) };

            var holes = ZoneFallbackCoverage.Holes(zones, 0.25f);

            Assert.AreEqual(4 * 16, holes.Count);
            foreach (var hole in holes) Assert.Greater(hole.Center.x, 1f);
        }

        [Test]
        public void OtherGatedZones_doNotCountAsCover()
        {
            // They can switch off too, so they can't be relied on to take over.
            // Every cell of both is a hole: 8 m x 4 m and 2 m x 4 m at 0.25 m.
            var zones = new[] { Zone(-4, 4, gated: true), Zone(0, 2, gated: true) };
            Assert.AreEqual(32 * 16 + 8 * 16, ZoneFallbackCoverage.Holes(zones, 0.25f).Count);
        }

        [Test]
        public void ZonesWithoutAGate_neverReportHoles()
        {
            var zones = new[] { Zone(-4, 0, gated: false), Zone(2, 4, gated: false) };
            Assert.AreEqual(0, ZoneFallbackCoverage.Holes(zones, 0.25f).Count);
        }

        [Test]
        public void HoleCorners_areOnTheZoneFloor()
        {
            var gated = Zone(0, 2, gated: true);
            var hole = ZoneFallbackCoverage.Holes(new[] { gated }, 0.25f)[0];

            Assert.AreEqual(4, hole.Corners.Length);
            foreach (var corner in hole.Corners) Assert.AreEqual(gated.MinHeight, corner.y, 1e-4f);
        }

        private FixedCameraZoneTrigger Zone(float xMin, float xMax, bool gated)
        {
            var go = new GameObject($"Zone {xMin}..{xMax}");
            this.created.Add(go);
            var zone = go.AddComponent<FixedCameraZoneTrigger>();
            var so = new SerializedObject(zone);
            var polygon = so.FindProperty("polygon");
            polygon.arraySize = 4;
            polygon.GetArrayElementAtIndex(0).vector2Value = new Vector2(xMin, -2);
            polygon.GetArrayElementAtIndex(1).vector2Value = new Vector2(xMin, 2);
            polygon.GetArrayElementAtIndex(2).vector2Value = new Vector2(xMax, 2);
            polygon.GetArrayElementAtIndex(3).vector2Value = new Vector2(xMax, -2);
            so.FindProperty("minHeight").floatValue = 0f;
            so.FindProperty("maxHeight").floatValue = 3f;
            if (gated)
            {
                var gate = go.AddComponent<FakeGate>();
                var gates = so.FindProperty("completeWhenGates");
                gates.arraySize = 1;
                gates.GetArrayElementAtIndex(0).objectReferenceValue = gate;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return zone;
        }

        private sealed class FakeGate : MonoBehaviour, IGate
        {
            public string GateId => "gate:test";
        }
    }
}
