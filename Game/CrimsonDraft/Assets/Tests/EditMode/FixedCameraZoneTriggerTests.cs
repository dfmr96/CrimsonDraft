#nullable enable

using System.Linq;
using System.Reflection;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Gates;
using CrimsonDraft.Navigation.Milestones;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

namespace CrimsonDraft.Tests
{
    public sealed class FixedCameraZoneTriggerTests
    {
        private GameObject root = null!;
        private FixedCameraZoneTrigger trigger = null!;
        private CinemachineCamera a = null!;
        private CinemachineCamera b = null!;

        [SetUp]
        public void SetUp()
        {
            this.root = new GameObject("Camera zone test");
            this.trigger = this.root.AddComponent<FixedCameraZoneTrigger>();
            this.a = CreateCamera("A");
            this.b = CreateCamera("B");
            SetField("zoneCamera", this.a);
        }

        private readonly System.Collections.Generic.List<Object> createdAssets = new();

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(this.root);
            foreach (var asset in this.createdAssets) Object.DestroyImmediate(asset);
            this.createdAssets.Clear();
        }

        [Test]
        public void ReferencedCameras_isTheZoneCameraOnly()
            => CollectionAssert.AreEqual(new[] { this.a }, this.trigger.ReferencedCameras.ToArray());

        [Test]
        public void Contains_followsAConcavePolygon()
        {
            SetField("polygon", new[]
            {
                new Vector2(0, 0), new Vector2(4, 0), new Vector2(4, 1),
                new Vector2(1, 1), new Vector2(1, 4), new Vector2(0, 4),
            });
            Assert.IsTrue(this.trigger.Contains(new Vector3(0.5f, 1, 3)));
            Assert.IsTrue(this.trigger.Contains(new Vector3(3, 1, 0.5f)));
            Assert.IsFalse(this.trigger.Contains(new Vector3(3, 1, 3)));
        }

        [Test]
        public void Contains_rejectsPositionsOutsideTheHeightRange()
        {
            SetField("minHeight", 0f);
            SetField("maxHeight", 2f);
            Assert.IsTrue(this.trigger.Contains(new Vector3(0, 1, 0)));
            Assert.IsFalse(this.trigger.Contains(new Vector3(0, 3, 0)));
            Assert.IsFalse(this.trigger.Contains(new Vector3(0, -1, 0)));
        }

        [Test]
        public void Contains_measuresInTheParentSpace_notTheZoneObject()
        {
            var room = new GameObject("Room");
            try
            {
                room.transform.SetPositionAndRotation(new Vector3(10, 0, 0), Quaternion.Euler(0, 90, 0));
                room.transform.localScale = new Vector3(2, 1, 1);
                this.root.transform.SetParent(room.transform, false);
                // Moving or turning the zone object itself (the camera) never moves where it activates.
                this.root.transform.localPosition = new Vector3(30, 5, 30);
                this.root.transform.localRotation = Quaternion.Euler(40, 70, 0);

                // The 2x2 square is in room space: room X is stretched to 4 units and turned along -Z.
                Assert.IsTrue(this.trigger.Contains(new Vector3(10.5f, 1, -1.5f)));
                Assert.IsFalse(this.trigger.Contains(new Vector3(12f, 1, 0)));
            }
            finally
            {
                this.root.transform.SetParent(null);
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void Evaluate_activatesOnEntryAndNotAgainWhileInside()
        {
            var service = new FakeZoneService();
            this.trigger.Construct(service);

            this.trigger.Evaluate(new Vector3(0, 1, 0));
            Assert.AreEqual(1, service.ActivateCount);
            Assert.AreSame(this.a, service.CurrentZoneCamera);

            this.a.enabled = true;
            this.trigger.Evaluate(new Vector3(0.1f, 1, 0));
            Assert.AreEqual(1, service.ActivateCount);

            this.trigger.Evaluate(new Vector3(50, 1, 0));
            service.Force(this.b);
            this.trigger.Evaluate(new Vector3(0, 1, 0));
            Assert.AreEqual(2, service.ActivateCount);
        }

        [Test]
        public void Evaluate_ignoresAPlayerOutsideThePolygon()
        {
            var service = new FakeZoneService();
            this.trigger.Construct(service);
            this.trigger.Evaluate(new Vector3(20, 1, 0));
            Assert.AreEqual(0, service.ActivateCount);
        }

        [Test]
        public void CompletedGate_stopsTheZoneFromCuttingToItsCamera()
        {
            var gate = AddGate("puzzle:valves");
            SetField("completeWhenGates", new MonoBehaviour[] { gate });
            var gates = new FakeGateStates();
            var service = new FakeZoneService();
            this.trigger.Construct(service, gates);

            Assert.IsFalse(this.trigger.IsCompleted);
            this.trigger.Evaluate(new Vector3(0, 1, 0));
            Assert.AreSame(this.a, service.CurrentZoneCamera);

            gates.Complete("puzzle:valves");
            Assert.IsTrue(this.trigger.IsCompleted);

            // Completed while the player is inside: the camera stays, nothing cuts back or away.
            service.Force(this.b);
            this.trigger.Evaluate(new Vector3(0.1f, 1, 0));
            Assert.AreSame(this.b, service.CurrentZoneCamera);

            // Leaving and coming back no longer fires it either.
            this.trigger.Evaluate(new Vector3(50, 1, 0));
            this.trigger.Evaluate(new Vector3(0, 1, 0));
            Assert.AreSame(this.b, service.CurrentZoneCamera);
        }

        [Test]
        public void EveryGate_mustBeComplete()
        {
            // Any mix of kinds: a socket, a puzzle and a radio call all count as gates.
            SetField("completeWhenGates", new MonoBehaviour[]
            {
                AddGate("socket:fuse"), AddGate("puzzle:valves"), AddGate("radio:radio_01"),
            });
            var gates = new FakeGateStates();
            this.trigger.Construct(new FakeZoneService(), gates);

            gates.Complete("socket:fuse");
            gates.Complete("radio:radio_01");
            Assert.IsFalse(this.trigger.IsCompleted, "puzzle still open");

            gates.Complete("puzzle:valves");
            Assert.IsTrue(this.trigger.IsCompleted);
        }

        [Test]
        public void GateWithoutGateStates_isNotCompleted()
        {
            SetField("completeWhenGates", new MonoBehaviour[] { AddGate("puzzle:valves") });
            this.trigger.Construct(new FakeZoneService());
            Assert.IsFalse(this.trigger.IsCompleted);
        }

        [Test]
        public void EmptyGateSlots_areIgnored()
        {
            SetField("completeWhenGates", new MonoBehaviour[] { null! });
            this.trigger.Construct(new FakeZoneService(), new FakeGateStates());
            Assert.IsFalse(this.trigger.IsCompleted, "an empty slot is not a gate, so the zone is ungated");
        }

        [Test]
        public void ZoneWithoutAGate_isNeverCompleted()
        {
            this.trigger.Construct(new FakeZoneService(), new FakeGateStates(), new FakeMilestoneService());
            Assert.IsFalse(this.trigger.IsCompleted);
        }

        [Test]
        public void CompletedMilestone_stopsTheZoneFromCuttingToItsCamera()
        {
            var milestone = MakeMilestone("power-restored");
            SetField("completeWhenMilestone", milestone);
            var milestones = new FakeMilestoneService();
            var service = new FakeZoneService();
            this.trigger.Construct(service, new FakeGateStates(), milestones);

            Assert.IsFalse(this.trigger.IsCompleted);
            this.trigger.Evaluate(new Vector3(0, 1, 0));
            Assert.AreSame(this.a, service.CurrentZoneCamera);

            milestones.Complete("power-restored");
            Assert.IsTrue(this.trigger.IsCompleted);

            // Same as the gates: leaving and coming back no longer fires it.
            service.Force(this.b);
            this.trigger.Evaluate(new Vector3(50, 1, 0));
            this.trigger.Evaluate(new Vector3(0, 1, 0));
            Assert.AreSame(this.b, service.CurrentZoneCamera);
        }

        [Test]
        public void MilestoneAndGate_bothMustBeDone()
        {
            SetField("completeWhenGates", new MonoBehaviour[] { AddGate("puzzle:valves") });
            SetField("completeWhenMilestone", MakeMilestone("power-restored"));
            var gates = new FakeGateStates();
            var milestones = new FakeMilestoneService();
            this.trigger.Construct(new FakeZoneService(), gates, milestones);

            milestones.Complete("power-restored");
            Assert.IsFalse(this.trigger.IsCompleted, "gate still open");

            gates.Complete("puzzle:valves");
            Assert.IsTrue(this.trigger.IsCompleted);
        }

        [Test]
        public void MilestoneGate_withoutAMilestoneService_isNotCompleted()
        {
            // No service to ask: keep firing rather than silently switching the zone off.
            SetField("completeWhenMilestone", MakeMilestone("power-restored"));
            this.trigger.Construct(new FakeZoneService(), new FakeGateStates());
            Assert.IsFalse(this.trigger.IsCompleted);
        }

        private FakeGate AddGate(string gateId)
        {
            var gate = new GameObject(gateId).AddComponent<FakeGate>();
            gate.transform.SetParent(this.root.transform);
            gate.Id = gateId;
            return gate;
        }

        private MilestoneRef MakeMilestone(string nodeId)
        {
            var graph = ScriptableObject.CreateInstance<MilestoneGraph>();
            this.createdAssets.Add(graph);
            return new MilestoneRef(graph, nodeId);
        }

        private sealed class FakeGate : MonoBehaviour, IGate
        {
            public string Id = "";
            public string GateId => this.Id;
        }

        private sealed class FakeGateStates : IGateStates
        {
            private readonly System.Collections.Generic.HashSet<string> done = new();
            public event System.Action? Changed;
            public bool IsCompleted(string gateId) => this.done.Contains(gateId);
            public void Complete(string gateId)
            {
                this.done.Add(gateId);
                this.Changed?.Invoke();
            }
        }

        private sealed class FakeMilestoneService : IMilestoneService
        {
            private readonly System.Collections.Generic.HashSet<string> done = new();
            public event System.Action<MilestoneRef>? Completed;
            public bool IsCompleted(MilestoneRef milestone) => this.done.Contains(milestone.nodeId);
            public bool IsCompleted(string milestoneId) => this.done.Contains(milestoneId);
            public void Complete(string nodeId)
            {
                this.done.Add(nodeId);
                this.Completed?.Invoke(new MilestoneRef(null, nodeId));
            }
        }

        private sealed class FakeZoneService : IFixedCameraZoneService
        {
            public CinemachineCamera? CurrentZoneCamera { get; private set; }
            public CinemachineCamera? GameplayZoneCamera => this.CurrentZoneCamera;
            public int ActivateCount { get; private set; }

            public void ActivateZone(CinemachineCamera zoneCamera)
            {
                this.CurrentZoneCamera = zoneCamera;
                this.ActivateCount++;
            }

            public void ActivateInspectionShot(CinemachineCamera inspectCamera) => this.CurrentZoneCamera = inspectCamera;

            public void Force(CinemachineCamera camera) => this.CurrentZoneCamera = camera;
        }

        private CinemachineCamera CreateCamera(string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(this.root.transform);
            return obj.AddComponent<CinemachineCamera>();
        }

        private void SetField(string name, object? value) => typeof(FixedCameraZoneTrigger)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this.trigger, value);
    }
}
