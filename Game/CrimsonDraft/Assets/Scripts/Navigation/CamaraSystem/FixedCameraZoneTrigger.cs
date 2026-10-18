#nullable enable

using Unity.Cinemachine;
using UnityEngine;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.CamaraSystem
{
    public sealed class FixedCameraZoneTrigger : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera zoneCamera = null!;
        [Tooltip("Use Zone Camera on the negative Forward side and Positive Side Camera on the positive side.")]
        [SerializeField] private bool useTwoSidedSwitch;
        [SerializeField] private CinemachineCamera? positiveSideCamera;
        [Tooltip("Distance in world units from the collider center to each switching threshold. Total neutral width is twice this value.")]
        [Min(0f)] [SerializeField] private float sideMargin = 0.25f;

        public CinemachineCamera ZoneCamera => this.zoneCamera;

        public CinemachineCamera? ResolveCameraAt(Vector3 position, CinemachineCamera? current)
        {
            if (!this.useTwoSidedSwitch) return this.zoneCamera;
            // A partially configured switch must not turn off the current camera.
            if (this.zoneCamera == null || this.positiveSideCamera == null) return null;

            float distance = Vector3.Dot(position - this.SwitchCenter, this.transform.forward);
            float margin = Mathf.Max(0f, this.sideMargin);
            if (distance > margin) return this.positiveSideCamera;
            if (distance < -margin) return this.zoneCamera;
            if (current == this.zoneCamera || current == this.positiveSideCamera) return current;

            // Spawning in the neutral band still needs a deterministic initial shot.
            return distance >= 0f ? this.positiveSideCamera : this.zoneCamera;
        }

        private IFixedCameraZoneService? zoneService;

        private Vector3 SwitchCenter
        {
            get
            {
                var collider = this.GetComponent<Collider>();
                if (collider is BoxCollider box) return this.transform.TransformPoint(box.center);
                if (collider is SphereCollider sphere) return this.transform.TransformPoint(sphere.center);
                if (collider is CapsuleCollider capsule) return this.transform.TransformPoint(capsule.center);
                if (collider is MeshCollider mesh && mesh.sharedMesh != null)
                    return this.transform.TransformPoint(mesh.sharedMesh.bounds.center);
                return this.transform.position;
            }
        }

        public void Construct(IFixedCameraZoneService zoneService)
        {
            this.zoneService = zoneService;
        }

        private void OnTriggerEnter(Collider other)
        {
            this.TryActivate(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (this.useTwoSidedSwitch) this.TryActivate(other);
        }

        private void TryActivate(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (this.zoneService == null) return;

            var player = other.GetComponentInParent<PlayerController>();
            var reference = player != null ? player.transform
                : other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
            var camera = this.ResolveCameraAt(reference.position, this.zoneService.CurrentZoneCamera);
            if (camera == null) return;
            if (camera == this.zoneService.CurrentZoneCamera && camera.enabled) return;

            this.zoneService.ActivateZone(camera);
        }

        private void OnDrawGizmosSelected()
        {
            if (!this.useTwoSidedSwitch) return;
            float margin = Mathf.Max(0f, this.sideMargin);
            DrawThreshold(-margin, Color.cyan);
            DrawThreshold(margin, Color.yellow);
        }

        private void DrawThreshold(float offset, Color color)
        {
            Gizmos.color = color;
            var center = this.SwitchCenter + this.transform.forward * offset;
            var right = this.transform.right;
            var up = this.transform.up;
            var box = this.GetComponent<BoxCollider>();
            if (box != null)
            {
                right *= this.transform.TransformVector(Vector3.right * box.size.x).magnitude * 0.5f;
                up *= this.transform.TransformVector(Vector3.up * box.size.y).magnitude * 0.5f;
            }
            Gizmos.DrawLine(center - right - up, center + right - up);
            Gizmos.DrawLine(center + right - up, center + right + up);
            Gizmos.DrawLine(center + right + up, center - right + up);
            Gizmos.DrawLine(center - right + up, center - right - up);
            Gizmos.DrawRay(center, this.transform.forward * 0.5f);
        }
    }
}
