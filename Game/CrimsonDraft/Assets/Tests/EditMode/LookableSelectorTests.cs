#nullable enable

using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Tests
{
    public sealed class LookableSelectorTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static (GameObject go, Collider col, Lookable lookable) MakeLookable(
            Vector3 position, int priority = 0)
        {
            var go       = new GameObject();
            go.transform.position = position;
            var col      = go.AddComponent<BoxCollider>();
            var lookable = go.AddComponent<Lookable>();
            var so       = new SerializedObject(lookable);
            so.FindProperty("priority").intValue = priority;
            so.ApplyModifiedPropertiesWithoutUndo();
            return (go, col, lookable);
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void SelectBest_singleCandidateInCone_returnsIt()
        {
            var (go, col, lookable) = MakeLookable(Vector3.forward * 2f);
            var colliders = new Collider[] { col };

            var result = PlayerHeadLookController.SelectBest(
                colliders, count: 1,
                origin: Vector3.zero, forward: Vector3.forward, maxAngle: 60f);

            Assert.AreEqual(lookable, result);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SelectBest_candidateOutsideCone_returnsNull()
        {
            var (go, col, _) = MakeLookable(Vector3.right * 2f);
            var colliders = new Collider[] { col };

            var result = PlayerHeadLookController.SelectBest(
                colliders, count: 1,
                origin: Vector3.zero, forward: Vector3.forward, maxAngle: 60f);

            Assert.IsNull(result);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SelectBest_noCandidates_returnsNull()
        {
            var result = PlayerHeadLookController.SelectBest(
                new Collider[4], count: 0,
                origin: Vector3.zero, forward: Vector3.forward, maxAngle: 60f);

            Assert.IsNull(result);
        }

        [Test]
        public void SelectBest_higherPriorityWins()
        {
            var (goA, colA, _)         = MakeLookable(Vector3.forward * 2f, priority: 1);
            var (goB, colB, lookableB)  = MakeLookable(Vector3.forward * 1f, priority: 5);
            var colliders = new Collider[] { colA, colB };

            var result = PlayerHeadLookController.SelectBest(
                colliders, count: 2,
                origin: Vector3.zero, forward: Vector3.forward, maxAngle: 60f);

            Assert.AreEqual(lookableB, result);
            Object.DestroyImmediate(goA);
            Object.DestroyImmediate(goB);
        }

        [Test]
        public void SelectBest_samePriorityNearestWins()
        {
            var (goA, colA, lookableA) = MakeLookable(Vector3.forward * 1f, priority: 0);
            var (goB, colB, _)         = MakeLookable(Vector3.forward * 3f, priority: 0);
            var colliders = new Collider[] { colA, colB };

            var result = PlayerHeadLookController.SelectBest(
                colliders, count: 2,
                origin: Vector3.zero, forward: Vector3.forward, maxAngle: 60f);

            Assert.AreEqual(lookableA, result);
            Object.DestroyImmediate(goA);
            Object.DestroyImmediate(goB);
        }

        [Test]
        public void SelectBest_colliderWithNoLookable_isIgnored()
        {
            var go  = new GameObject();
            var col = go.AddComponent<BoxCollider>();
            go.transform.position = Vector3.forward * 2f;
            var colliders = new Collider[] { col };

            var result = PlayerHeadLookController.SelectBest(
                colliders, count: 1,
                origin: Vector3.zero, forward: Vector3.forward, maxAngle: 60f);

            Assert.IsNull(result);
            Object.DestroyImmediate(go);
        }
    }
}
