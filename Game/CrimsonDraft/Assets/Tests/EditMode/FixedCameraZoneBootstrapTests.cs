#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Gates;
using CrimsonDraft.Navigation.Milestones;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Tests
{
    /// <summary>Drives zones through the real bootstrap with a capsule-sized player, the path the
    /// game uses every frame.</summary>
    public sealed class FixedCameraZoneBootstrapTests
    {
        private readonly List<Object> created = new List<Object>();
        private FixedCameraZoneService service = null!;
        private PlayerController player = null!;
        private FakeMilestoneService milestones = null!;
        private GateRegistry gates = null!;

        [SetUp]
        public void SetUp()
        {
            this.service = new FixedCameraZoneService();
            this.milestones = new FakeMilestoneService();
            this.gates = new GateRegistry();
            var playerGo = Track(new GameObject("Player"));
            this.player = playerGo.AddComponent<PlayerController>();
            var capsule = playerGo.AddComponent<CapsuleCollider>();
            capsule.radius = 0.5f;
            capsule.height = 1.64f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in this.created) if (o != null) Object.DestroyImmediate(o);
            this.created.Clear();
        }

        [Test]
        public void AdjacentZones_walkingBackCutsBackToTheFirstCamera()
        {
            var a = MakeCamera("A");
            var b = MakeCamera("B");
            var bootstrap = MakeBootstrap(MakeZone(-4, 0, a), MakeZone(0, 4, b));

            var seen = Walk(bootstrap, 0f, -2f, -0.4f, 0.3f, -0.3f, -3f);

            CollectionAssert.AreEqual(new[] { a, a, b, a, a }, seen);
        }

        [Test]
        public void InspectionShot_isNotReplacedByZones()
        {
            var z1 = MakeCamera("Z1");
            var inspect = MakeCamera("Inspect");
            var bootstrap = MakeBootstrap(MakeZone(-4, 0, z1));
            Walk(bootstrap, -1f, -2f);

            this.service.ActivateInspectionShot(inspect);
            Walk(bootstrap, -1f, -2f, -1.5f);

            Assert.AreSame(inspect, this.service.CurrentZoneCamera);
        }

        [Test]
        public void ZoneGatedByACompletedMilestone_noLongerCutsToItsCamera()
        {
            var a = MakeCamera("A");
            var b = MakeCamera("B");
            var gated = MakeZone(0, 4, b);
            var graph = ScriptableObject.CreateInstance<MilestoneGraph>();
            this.created.Add(graph);
            var so = new SerializedObject(gated);
            so.FindProperty("completeWhenMilestone.graph").objectReferenceValue = graph;
            so.FindProperty("completeWhenMilestone.nodeId").stringValue = "door-opened";
            so.ApplyModifiedPropertiesWithoutUndo();
            this.milestones.Complete("door-opened");

            var bootstrap = MakeBootstrap(MakeZone(-4, 0, a), gated);
            var seen = Walk(bootstrap, 0f, -2f, 2f);

            CollectionAssert.AreEqual(new[] { a, a }, seen);
        }

        // ---- Priority: a zone with a completion gate still open wins over ordinary zones -------------

        [Test]
        public void PriorityZone_overlappingAnOrdinaryZone_takesTheCameraAndHandsItBack()
        {
            var n = MakeCamera("N");
            var p = MakeCamera("P");
            var bootstrap = MakeBootstrap(MakeZone(-4, 4, n), MakeGatedZone(0, 2, p, "safe"));

            // Into the priority zone (still inside N), out of it (still inside N), and back.
            var seen = Walk(bootstrap, 0f, -2f, 1f, 1.5f, 3f, 1f);

            CollectionAssert.AreEqual(new[] { n, p, p, n, p }, seen);
        }

        [Test]
        public void StartingInsideBoth_thePriorityZoneWins()
        {
            var n = MakeCamera("N");
            var p = MakeCamera("P");
            // The ordinary zone comes first: order must not matter.
            var bootstrap = MakeBootstrap(MakeZone(-4, 4, n), MakeGatedZone(0, 2, p, "safe"));

            CollectionAssert.AreEqual(new[] { p }, Walk(bootstrap, 0f, 1f));
        }

        [Test]
        public void InsideAPriorityZone_enteringAnOrdinaryZoneDoesNotCut()
        {
            var n = MakeCamera("N");
            var m = MakeCamera("M");
            var p = MakeCamera("P");
            var bootstrap = MakeBootstrap(MakeZone(-4, 0, n), MakeZone(0, 4, m), MakeGatedZone(-1, 1, p, "safe"));

            // Crossing from N into M happens inside P; leaving P lands on M.
            var seen = Walk(bootstrap, 0f, -2f, -0.5f, 0.5f, 2f);

            CollectionAssert.AreEqual(new[] { n, p, p, m }, seen);
        }

        [Test]
        public void CompletedPriorityZone_isNoLongerAPriority()
        {
            var n = MakeCamera("N");
            var p = MakeCamera("P");
            this.gates.SetCompleted("safe");
            var bootstrap = MakeBootstrap(MakeZone(-4, 4, n), MakeGatedZone(0, 2, p, "safe"));

            CollectionAssert.AreEqual(new[] { n, n, n }, Walk(bootstrap, 0f, -2f, 1f, 3f));
        }

        [Test]
        public void CompletedWhileInside_keepsItsCameraUntilThePlayerLeaves()
        {
            var n = MakeCamera("N");
            var p = MakeCamera("P");
            var bootstrap = MakeBootstrap(MakeZone(-4, 4, n), MakeGatedZone(0, 2, p, "safe"));
            Walk(bootstrap, 0f, -2f, 1f);

            this.gates.SetCompleted("safe");
            var seen = Walk(bootstrap, 0f, 1.5f, 3f, 1f);

            // Stays while inside, hands back on leaving, and no longer takes over when walked into again.
            CollectionAssert.AreEqual(new[] { p, n, n }, seen);
        }

        private List<CinemachineCamera?> Walk(FixedCameraZoneBootstrap bootstrap, float z, params float[] xs)
        {
            var seen = new List<CinemachineCamera?>();
            foreach (var x in xs)
            {
                this.player.transform.position = new Vector3(x, 1f, z);
                Physics.SyncTransforms();
                ((ITickable)bootstrap).Tick();
                seen.Add(this.service.CurrentZoneCamera);
            }
            return seen;
        }

        private FixedCameraZoneBootstrap MakeBootstrap(params FixedCameraZoneTrigger[] triggers)
        {
            var bootstrap = new FixedCameraZoneBootstrap(
                triggers, this.service, this.player,
                new GateStateService(new ItemSocketStateRegistry(), new PuzzleStateRegistry(), new RadioCallRegistry(), this.gates),
                this.milestones);
            ((IInitializable)bootstrap).Initialize();
            return bootstrap;
        }

        private CinemachineCamera MakeCamera(string name) => Track(new GameObject(name)).AddComponent<CinemachineCamera>();

        private FixedCameraZoneTrigger MakeZone(float xMin, float xMax, CinemachineCamera camera)
        {
            var zone = Track(new GameObject("Zone " + camera.name)).AddComponent<FixedCameraZoneTrigger>();
            var so = new SerializedObject(zone);
            so.FindProperty("zoneCamera").objectReferenceValue = camera;
            var polygon = so.FindProperty("polygon");
            polygon.arraySize = 4;
            polygon.GetArrayElementAtIndex(0).vector2Value = new Vector2(xMin, -2);
            polygon.GetArrayElementAtIndex(1).vector2Value = new Vector2(xMin, 2);
            polygon.GetArrayElementAtIndex(2).vector2Value = new Vector2(xMax, 2);
            polygon.GetArrayElementAtIndex(3).vector2Value = new Vector2(xMax, -2);
            so.FindProperty("minHeight").floatValue = -1f;
            so.FindProperty("maxHeight").floatValue = 3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return zone;
        }

        // A zone that stops cutting once the generic gate gate:<gateId> completes (GateRegistry).
        private FixedCameraZoneTrigger MakeGatedZone(float xMin, float xMax, CinemachineCamera camera, string gateId)
        {
            var zone = MakeZone(xMin, xMax, camera);
            var gate = Track(new GameObject("Gate " + gateId)).AddComponent<FakeGate>();
            gate.Id = GateIds.Generic(gateId);
            var so = new SerializedObject(zone);
            var gatesProperty = so.FindProperty("completeWhenGates");
            gatesProperty.arraySize = 1;
            gatesProperty.GetArrayElementAtIndex(0).objectReferenceValue = gate;
            so.ApplyModifiedPropertiesWithoutUndo();
            return zone;
        }

        private sealed class FakeGate : MonoBehaviour, IGate
        {
            public string Id = "";
            public string GateId => this.Id;
        }

        private GameObject Track(GameObject go)
        {
            this.created.Add(go);
            return go;
        }

        private sealed class FakeMilestoneService : IMilestoneService
        {
            private readonly HashSet<string> done = new HashSet<string>();
            public event System.Action<MilestoneRef>? Completed;
            public bool IsCompleted(MilestoneRef milestone) => this.done.Contains(milestone.nodeId);
            public bool IsCompleted(string milestoneId) => this.done.Contains(milestoneId);
            public void Complete(string nodeId)
            {
                this.done.Add(nodeId);
                this.Completed?.Invoke(new MilestoneRef(null, nodeId));
            }
        }
    }
}
