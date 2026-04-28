#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    [ExecuteAlways]
    public sealed class InteractableLayerDebug : MonoBehaviour
    {
#if UNITY_EDITOR
        private const int InteractableLayer = 8;

        private static readonly Color HitColor  = new(0f, 1f, 0.2f, 0.15f);
        private static readonly Color HitWire   = new(0f, 1f, 0.2f, 0.9f);
        private static readonly Color MissColor = new(1f, 0.8f, 0f, 0.08f);
        private static readonly Color MissWire  = new(1f, 0.8f, 0f, 0.6f);

        private void OnDrawGizmos()
        {
            if (Application.isPlaying && Time.timeScale == 0f) return;

            foreach (var col in FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (col.gameObject.layer != InteractableLayer) continue;

                bool isInteractable = col.TryGetComponent<IInteractable>(out _);

                Gizmos.matrix = col.transform.localToWorldMatrix;

                switch (col)
                {
                    case BoxCollider box:
                        Gizmos.color = isInteractable ? HitColor : MissColor;
                        Gizmos.DrawCube(box.center, box.size);
                        Gizmos.color = isInteractable ? HitWire : MissWire;
                        Gizmos.DrawWireCube(box.center, box.size);
                        break;

                    case SphereCollider sphere:
                        Gizmos.matrix = Matrix4x4.identity;
                        var center = col.transform.TransformPoint(sphere.center);
                        float radius = sphere.radius * col.transform.lossyScale.x;
                        Gizmos.color = isInteractable ? HitColor : MissColor;
                        Gizmos.DrawSphere(center, radius);
                        Gizmos.color = isInteractable ? HitWire : MissWire;
                        Gizmos.DrawWireSphere(center, radius);
                        break;

                    default:
                        Gizmos.matrix = Matrix4x4.identity;
                        Gizmos.color = isInteractable ? HitWire : MissWire;
                        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
                        break;
                }

                Gizmos.matrix = Matrix4x4.identity;

                UnityEditor.Handles.color = isInteractable ? HitWire : MissWire;
                UnityEditor.Handles.Label(
                    col.bounds.center + Vector3.up * (col.bounds.extents.y + 0.2f),
                    col.gameObject.name);
            }
        }
#endif
    }
}
