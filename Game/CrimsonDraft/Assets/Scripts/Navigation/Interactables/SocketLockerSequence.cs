#nullable enable

using System.Collections;
using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    // Locker that opens once its ItemSocketInteractable is completed: the door swings open, then
    // the object stored inside (inactive until now) is revealed and topples out onto the floor,
    // as if the door had been holding it up.
    //
    // Put this on the same GameObject as the socket. The animation is started by the socket's
    // OnActivatedAfterDialogue, so it plays after the player has read the "hinge gave way" line.
    // Re-entering the room with the socket already activated skips the animation and snaps
    // everything to the final state (ItemSocketInteractable restores its state without replaying
    // events, so a short polling window right after enable covers that case).
    public sealed class SocketLockerSequence : MonoBehaviour
    {
        [SerializeField] private ItemSocketInteractable socket = null!;

        [Header("Door")]
        [SerializeField] private Transform door = null!;
        [Tooltip("Local Z rotation (Euler, degrees) the door ends at when opened. That is the only thing the door does -- X and Y keep their authored values.")]
        [SerializeField] private float doorOpenLocalZ = -142f;
        [SerializeField, Min(0f)] private float startDelay    = 0.4f;
        [SerializeField, Min(0.01f)] private float doorDuration = 1f;

        [Header("Falling object")]
        [Tooltip("Starts inactive; revealed once the door is open.")]
        [SerializeField] private GameObject fallingObject = null!;
        [Tooltip("Direction the object topples toward, in THIS object's local space (the open side of the locker). Horizontal component only is used.")]
        [SerializeField] private Vector3 fallDirectionLocal = Vector3.forward;
        [SerializeField, Min(0.01f)] private float tipDuration  = 0.6f;
        [SerializeField, Min(0.01f)] private float dropDuration = 0.25f;

        // How long after enable an already-activated socket is still treated as "restored".
        private const float RestoreWindow = 1f;

        private Vector3    doorStartLocalEuler;
        private float      enabledAt;
        private bool      played;

        // Falling object start pose + toppling geometry, captured by PrepareFall.
        private Vector3    fallStartPos;
        private Quaternion fallStartRot;
        private Vector3    tipPivot;
        private Vector3    tipAxis;
        private float      tipSign = 1f;
        private float      dropDistance;

        void Awake()
        {
            this.doorStartLocalEuler = this.door.localEulerAngles;
        }

        void OnEnable()
        {
            this.enabledAt = Time.time;
            this.socket.OnActivatedAfterDialogue.AddListener(Play);
        }

        void OnDisable()
        {
            this.socket.OnActivatedAfterDialogue.RemoveListener(Play);
        }

        void Update()
        {
            if (this.played || Time.time - this.enabledAt > RestoreWindow) return;
            if (!this.socket.IsActivated) return;

            this.played = true;
            SnapToFinal();
        }

        private void Play()
        {
            if (this.played) return;
            this.played = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (this.startDelay > 0f)
                yield return new WaitForSeconds(this.startDelay);

            PrepareDoor();
            for (float t = 0f; t < 1f; t += Time.deltaTime / this.doorDuration)
            {
                // Ease-out: the door starts fast and settles as it reaches the open position.
                float k = 1f - (1f - t) * (1f - t);
                SetDoor(k);
                yield return null;
            }
            SetDoor(1f);

            PrepareFall();
            this.fallingObject.SetActive(true);

            for (float t = 0f; t < 1f; t += Time.deltaTime / this.tipDuration)
            {
                SetTip(t * t * 90f);   // ease-in: gravity takes over slowly, then speeds up
                yield return null;
            }
            SetTip(90f);

            for (float t = 0f; t < 1f; t += Time.deltaTime / this.dropDuration)
            {
                SetDrop(t * t);
                yield return null;
            }
            SetDrop(1f);
        }

        private void SnapToFinal()
        {
            PrepareDoor();
            SetDoor(1f);

            PrepareFall();
            this.fallingObject.SetActive(true);
            SetTip(90f);
            SetDrop(1f);
        }

        // ── Door ────────────────────────────────────────────────────────────

        private void PrepareDoor()
        {
            this.door.localEulerAngles = this.doorStartLocalEuler;
        }

        // k: 0 = closed (authored rotation), 1 = open (local Z = doorOpenLocalZ).
        private void SetDoor(float k)
        {
            float z = Mathf.LerpAngle(this.doorStartLocalEuler.z, this.doorOpenLocalZ, k);
            this.door.localEulerAngles = new Vector3(this.doorStartLocalEuler.x, this.doorStartLocalEuler.y, z);
        }

        // ── Falling object ──────────────────────────────────────────────────

        private Vector3 FallDirection()
        {
            Vector3 d = transform.TransformDirection(this.fallDirectionLocal);
            d.y = 0f;
            return d.sqrMagnitude < 1e-6f ? Vector3.forward : d.normalized;
        }

        private void PrepareFall()
        {
            var t = this.fallingObject.transform;
            this.fallStartPos = t.position;
            this.fallStartRot = t.rotation;

            Bounds b = WorldBounds(this.fallingObject);
            Vector3 dir = FallDirection();

            // Bottom edge of the face that looks out of the locker: the object tips over it.
            float halfAlongDir = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
            this.tipPivot = b.center + dir * halfAlongDir - Vector3.up * b.extents.y;
            this.tipAxis  = Vector3.Cross(Vector3.up, dir).normalized;

            // Whichever sign tilts "up" toward the open side.
            this.tipSign = Vector3.Dot(Quaternion.AngleAxis(90f, this.tipAxis) * Vector3.up, dir) >= 0f ? 1f : -1f;

            // After toppling, the face that was the bottom edge's neighbour lies flat at the
            // pivot's height, so it only has to drop from there to whatever is below.
            this.dropDistance = Mathf.Max(0f, this.tipPivot.y - FloorHeightBelow(this.tipPivot));
        }

        private void SetTip(float angle)
        {
            var q = Quaternion.AngleAxis(this.tipSign * angle, this.tipAxis);
            this.fallingObject.transform.SetPositionAndRotation(
                this.tipPivot + q * (this.fallStartPos - this.tipPivot),
                q * this.fallStartRot);
        }

        private void SetDrop(float k)
        {
            var q = Quaternion.AngleAxis(this.tipSign * 90f, this.tipAxis);
            Vector3 lying = this.tipPivot + q * (this.fallStartPos - this.tipPivot);
            this.fallingObject.transform.position = lying + Vector3.down * (this.dropDistance * k);
        }

        // Highest solid surface below the point, ignoring this locker and the object inside it.
        private float FloorHeightBelow(Vector3 from)
        {
            float best = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.point.y > best) best = hit.point.y;
            }
            return float.IsNegativeInfinity(best) ? from.y : best;
        }

        // Works while the object is inactive (Renderer.bounds does not), via the mesh itself.
        private static Bounds WorldBounds(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                return new Bounds(go.transform.position, Vector3.one);

            var local = mf.sharedMesh.bounds;
            var m     = go.transform.localToWorldMatrix;
            var world = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                world.Encapsulate(m.MultiplyPoint3x4(corner));
            }
            return world;
        }
    }
}
