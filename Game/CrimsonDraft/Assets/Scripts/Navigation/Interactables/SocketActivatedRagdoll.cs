#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    // Turns a rigged corpse into a physics ragdoll once the linked ItemSocketInteractable is
    // activated (e.g. the hanging body dropping when its chain breaks). Rigidbodies, capsule
    // colliders and CharacterJoints are generated on the DEF-* bones at that moment, so the prefab
    // stays clean. Once the body stops moving the physics components are frozen.
    //
    // A live simulation never lands the same way twice (frame timing, release moment), and the
    // corpse carries a pickup that has to end up in a reachable, predictable spot. So the fall is
    // simulated once in the editor (Assets/CrimsonDraft/Bake Ragdoll Fall on the prefab), every bone
    // pose is stored in this component, and at runtime that recording is just played back. It also
    // gives revisiting the room for free: if the socket is already activated the first time we look,
    // the last frame is applied and the body lies where it fell. With no recording baked it falls
    // back to the live simulation. Polls IsActivated instead of subscribing for the same reason as
    // SocketActivatedMover: an already-activated socket is restored without replaying onActivated.
    public sealed class SocketActivatedRagdoll : MonoBehaviour
    {
        private const string RagdollLayerName = "Ragdoll";

        private readonly struct PartDef
        {
            public readonly string Bone;
            public readonly string End;      // bone the capsule stretches towards
            public readonly string? Parent;  // bone of the part this one is jointed to
            public readonly float Radius;    // meters
            public readonly float Mass;      // kg
            public readonly float Swing;     // degrees
            public readonly float Twist;     // degrees

            public PartDef(string bone, string end, string? parent, float radius, float mass, float swing, float twist)
            {
                Bone = bone; End = end; Parent = parent; Radius = radius; Mass = mass; Swing = swing; Twist = twist;
            }
        }

        // Parents must come before their children.
        private static readonly PartDef[] Defs =
        {
            new("DEF-spine",       "DEF-spine.001",  null,             0.14f, 10f,  0f,  0f),
            new("DEF-spine.001",   "DEF-spine.004",  "DEF-spine",      0.16f, 16f, 25f, 20f),
            new("DEF-neck",        "DEF-head_end",   "DEF-spine.001",  0.10f,  4f, 40f, 30f),
            new("DEF-upper_arm.L", "DEF-forearm.L",  "DEF-spine.001",  0.055f, 2.5f, 70f, 40f),
            new("DEF-forearm.L",   "DEF-hand.L_end", "DEF-upper_arm.L", 0.045f, 1.5f, 60f, 20f),
            new("DEF-upper_arm.R", "DEF-forearm.R",  "DEF-spine.001",  0.055f, 2.5f, 70f, 40f),
            new("DEF-forearm.R",   "DEF-hand.R_end", "DEF-upper_arm.R", 0.045f, 1.5f, 60f, 20f),
            new("DEF-thigh.L",     "DEF-shin.L",     "DEF-spine",      0.085f, 7f, 40f, 20f),
            new("DEF-shin.L",      "DEF-foot.L",     "DEF-thigh.L",    0.065f, 4f, 40f, 10f),
            new("DEF-foot.L",      "DEF-toe.L_end",  "DEF-shin.L",     0.045f, 1.2f, 30f, 10f),
            new("DEF-thigh.R",     "DEF-shin.R",     "DEF-spine",      0.085f, 7f, 40f, 20f),
            new("DEF-shin.R",      "DEF-foot.R",     "DEF-thigh.R",    0.065f, 4f, 40f, 10f),
            new("DEF-foot.R",      "DEF-toe.R_end",  "DEF-shin.R",     0.045f, 1.2f, 30f, 10f),
        };

        private const int TorsoIndex = 1;

        private enum State { Hanging, Playing, Falling, Down }

        [SerializeField] private ItemSocketInteractable socket = null!;
        [Tooltip("Root of the rigged model (the object holding the Animator and the DEF-* bones).")]
        [SerializeField] private Transform skeletonRoot = null!;
        [SerializeField] private Animator? animator;
        [Tooltip("Objects that ride on the body (e.g. the pickup on the corpse). Reparented to the torso bone on Awake.")]
        [SerializeField] private Transform[] followBody = Array.Empty<Transform>();
        [SerializeField, Min(0f)] private float releaseDelay;
        [Tooltip("Seconds the body must stay still before it's frozen.")]
        [SerializeField, Min(0.1f)] private float stillTime = 0.75f;
        [Tooltip("Frozen after this long even if it keeps jittering.")]
        [SerializeField, Min(1f)] private float maxFallTime = 8f;
        [SerializeField] private float linearDamping = 0.1f;
        [SerializeField] private float angularDamping = 0.8f;

        [Header("Baked fall (written by the baker, don't edit by hand)")]
        [SerializeField] private float frameInterval = 0.04f;
        // Flattened [frame * part count + part], local to each bone's parent.
        [SerializeField] private Vector3[] framePositions = Array.Empty<Vector3>();
        [SerializeField] private Quaternion[] frameRotations = Array.Empty<Quaternion>();

        private sealed class Part
        {
            public Transform Bone = null!;
            public Rigidbody Body = null!;
            public CharacterJoint? Joint;
        }

        private readonly List<Part> parts = new();
        private Transform[]? bones;
        private State state = State.Hanging;
        private bool firstCheck = true;
        private float timer;
        private float still;

        public float FrameInterval => this.frameInterval;

        public bool HasBakedFall =>
            this.framePositions.Length >= 2 * Defs.Length
            && this.framePositions.Length % Defs.Length == 0
            && this.frameRotations.Length == this.framePositions.Length;

        private int FrameCount => this.framePositions.Length / Defs.Length;

        public bool IsDown => this.state == State.Down;

        private void Awake()
        {
            if (this.followBody.Length == 0) return;

            var torso = ResolveBones()[TorsoIndex];
            foreach (var follower in this.followBody)
                if (follower != null) follower.SetParent(torso, true);
        }

        private void Update()
        {
            if (this.state == State.Hanging) PollSocket();
            else if (this.state == State.Playing) TickPlayback();
            else if (this.state == State.Falling) TickFalling();
        }

        private void PollSocket()
        {
            if (this.socket == null || !this.socket.IsActivated)
            {
                this.firstCheck = false;
                return;
            }

            var baked = HasBakedFall;
            if (this.firstCheck && baked)
            {
                PrepareForRelease();
                ApplyFrame(FrameCount - 1);
                this.state = State.Down;
                enabled = false;
                return;
            }

            this.firstCheck = false;
            this.timer += Time.deltaTime;
            if (this.timer < this.releaseDelay) return;

            if (baked) BeginPlayback();
            else BeginRagdoll();
        }

        private void BeginPlayback()
        {
            PrepareForRelease();
            this.timer = 0f;
            this.state = State.Playing;
        }

        private void TickPlayback()
        {
            this.timer += Time.deltaTime;
            var position = this.timer / Mathf.Max(0.001f, this.frameInterval);
            var from = Mathf.FloorToInt(position);

            if (from >= FrameCount - 1)
            {
                ApplyFrame(FrameCount - 1);
                this.state = State.Down;
                enabled = false;
                return;
            }

            ApplyFrame(from, from + 1, position - from);
        }

        private void ApplyFrame(int frame) => ApplyFrame(frame, frame, 0f);

        private void ApplyFrame(int from, int to, float t)
        {
            var allBones = ResolveBones();
            for (var i = 0; i < allBones.Length; i++)
            {
                var a = from * Defs.Length + i;
                var b = to * Defs.Length + i;
                allBones[i].localPosition = Vector3.Lerp(this.framePositions[a], this.framePositions[b], t);
                allBones[i].localRotation = Quaternion.Slerp(this.frameRotations[a], this.frameRotations[b], t);
            }
        }

        private void PrepareForRelease()
        {
            if (this.animator != null) this.animator.enabled = false;
            DisableBlockers();
        }

        private void TickFalling()
        {
            this.timer += Time.deltaTime;

            var moving = false;
            foreach (var part in this.parts)
            {
                if (part.Body.linearVelocity.sqrMagnitude > 0.01f || part.Body.angularVelocity.sqrMagnitude > 0.1f)
                {
                    moving = true;
                    break;
                }
            }

            this.still = moving ? 0f : this.still + Time.deltaTime;

            if (this.still >= this.stillTime || this.timer >= this.maxFallTime)
                Freeze();
        }

        // Builds the physics ragdoll and lets go. Also driven by the editor baker.
        public void BeginRagdoll()
        {
            if (this.state != State.Hanging) return;

            var allBones = ResolveBones();
            var layer = LayerMask.NameToLayer(RagdollLayerName);
            if (layer >= 0) ApplyLayerRules(layer);
            else layer = 0;

            if (this.animator != null) this.animator.enabled = false;
            foreach (var smr in this.skeletonRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;

            DisableBlockers();

            this.parts.Clear();
            for (var i = 0; i < Defs.Length; i++)
                this.parts.Add(CreateBody(Defs[i], allBones[i], layer));

            for (var i = 0; i < Defs.Length; i++)
            {
                if (Defs[i].Parent == null) continue;
                var parent = this.parts[IndexOf(Defs[i].Parent!)];
                this.parts[i].Joint = CreateJoint(Defs[i], this.parts[i], parent);
            }

            this.timer = 0f;
            this.still = 0f;
            this.state = State.Falling;
        }

        public void Freeze()
        {
            foreach (var part in this.parts)
                if (part.Joint != null) Destroy(part.Joint);

            // Nothing physical is left on the corpse once it's down, so it can't get in the player's way.
            foreach (var part in this.parts)
            {
                part.Body.linearVelocity = Vector3.zero;
                part.Body.angularVelocity = Vector3.zero;
                part.Body.isKinematic = true;
                if (part.Bone.TryGetComponent<Collider>(out var collider)) collider.enabled = false;
            }

            this.state = State.Down;
            enabled = false;
        }

        // --- Editor baking: the baker runs BeginRagdoll in a preview physics scene, calls AppendFrame
        // every few steps until IsStill, then stores the result with SetBakedFall.

        public bool IsStill()
        {
            foreach (var part in this.parts)
                if (part.Body.linearVelocity.sqrMagnitude > 0.01f || part.Body.angularVelocity.sqrMagnitude > 0.1f)
                    return false;
            return true;
        }

        public void AppendFrame(List<Vector3> positions, List<Quaternion> rotations)
        {
            foreach (var bone in ResolveBones())
            {
                positions.Add(bone.localPosition);
                rotations.Add(bone.localRotation);
            }
        }

        public void SetBakedFall(float interval, Vector3[] positions, Quaternion[] rotations)
        {
            this.frameInterval = interval;
            this.framePositions = positions;
            this.frameRotations = rotations;
        }

        // The model's solid box collider blocks the player while the body hangs. It stays in the
        // hanging pose, so it would prop the falling body up and, left on, trap the player. It
        // stays off for good once the body is released.
        private void DisableBlockers()
        {
            foreach (var box in this.skeletonRoot.GetComponentsInChildren<BoxCollider>(true))
                if (!box.isTrigger) box.enabled = false;
        }

        private Transform[] ResolveBones()
        {
            if (this.bones != null) return this.bones;

            var byName = new Dictionary<string, Transform>();
            foreach (var t in this.skeletonRoot.GetComponentsInChildren<Transform>(true))
                byName[t.name] = t;

            var resolved = new Transform[Defs.Length];
            for (var i = 0; i < Defs.Length; i++)
            {
                if (!byName.TryGetValue(Defs[i].Bone, out resolved[i]))
                    throw new InvalidOperationException($"[SocketActivatedRagdoll] Bone '{Defs[i].Bone}' not found under '{this.skeletonRoot.name}'.");
            }

            this.bones = resolved;
            return resolved;
        }

        private static int IndexOf(string bone)
        {
            for (var i = 0; i < Defs.Length; i++)
                if (Defs[i].Bone == bone) return i;
            throw new InvalidOperationException($"[SocketActivatedRagdoll] Unknown part '{bone}'.");
        }

        private Part CreateBody(PartDef def, Transform bone, int layer)
        {
            var end = FindBone(def.End);
            var localEnd = bone.InverseTransformPoint(end.position);
            var scale = Mathf.Max(0.0001f, bone.lossyScale.x);
            var worldLength = Vector3.Distance(bone.position, end.position);

            // Capsule axis = dominant axis of the bone direction in its own space.
            var abs = new Vector3(Mathf.Abs(localEnd.x), Mathf.Abs(localEnd.y), Mathf.Abs(localEnd.z));
            var direction = abs.x >= abs.y && abs.x >= abs.z ? 0 : abs.y >= abs.z ? 1 : 2;

            var radius = def.Radius / scale;
            var collider = bone.gameObject.AddComponent<CapsuleCollider>();
            collider.direction = direction;
            collider.radius = radius;
            collider.height = Mathf.Max(worldLength / scale, radius * 2f);
            collider.center = localEnd * 0.5f;

            bone.gameObject.layer = layer;

            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = def.Mass;
            body.linearDamping = this.linearDamping;
            body.angularDamping = this.angularDamping;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.solverIterations = 12;
            body.maxAngularVelocity = 20f;

            return new Part { Bone = bone, Body = body };
        }

        private static CharacterJoint CreateJoint(PartDef def, Part part, Part parent)
        {
            var joint = part.Bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent.Body;
            joint.enableProjection = true;

            // Twist axis along the bone, swing axis any perpendicular.
            var twistAxis = part.Bone.GetComponent<CapsuleCollider>().center.normalized;
            joint.axis = twistAxis;
            joint.swingAxis = Vector3.Cross(twistAxis, Mathf.Abs(twistAxis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;

            joint.lowTwistLimit = new SoftJointLimit { limit = -def.Twist };
            joint.highTwistLimit = new SoftJointLimit { limit = def.Twist };
            joint.swing1Limit = new SoftJointLimit { limit = def.Swing };
            joint.swing2Limit = new SoftJointLimit { limit = def.Swing };
            return joint;
        }

        private Transform FindBone(string boneName)
        {
            foreach (var t in this.skeletonRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == boneName) return t;
            throw new InvalidOperationException($"[SocketActivatedRagdoll] Bone '{boneName}' not found under '{this.skeletonRoot.name}'.");
        }

        // The ragdoll only collides with Default (room geometry) and Floor, never with the player,
        // enemies or interaction raycast layers.
        private static void ApplyLayerRules(int ragdollLayer)
        {
            var floor = LayerMask.NameToLayer("Floor");
            for (var i = 0; i < 32; i++)
            {
                var collides = i == 0 || i == ragdollLayer || i == floor;
                Physics.IgnoreLayerCollision(ragdollLayer, i, !collides);
            }
        }
    }
}
