#nullable enable

using System;
using UnityEngine;
using Yarn.Unity;

namespace CrimsonDraft.Inventory
{
    // Placed on an ItemData.PreviewModel prefab to make specific colliders on the model
    // resolve to different examine dialogue when hit by PickupPreviewView's inspect raycast.
    // Items without this component keep using ItemData.ExamineDialogue unchanged.
    public sealed class ItemExamineHotspots : MonoBehaviour
    {
        [Serializable]
        public struct Hotspot
        {
            public Collider collider;
            public DialogueReference dialogue;
        }

        [SerializeField] private Hotspot[] hotspots = Array.Empty<Hotspot>();
        [SerializeField] private DialogueReference defaultDialogue = new();

        public DialogueReference GetDialogue(Collider? hitCollider)
        {
            if (hitCollider != null)
            {
                foreach (var h in this.hotspots)
                    if (h.collider == hitCollider) return h.dialogue;
            }
            return this.defaultDialogue;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            foreach (var h in this.hotspots)
            {
                if (h.collider == null) continue;
                Gizmos.matrix = h.collider.transform.localToWorldMatrix;
                switch (h.collider)
                {
                    case BoxCollider box:
                        Gizmos.DrawWireCube(box.center, box.size);
                        break;
                    case SphereCollider sphere:
                        Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                        break;
                    default:
                        Gizmos.DrawWireCube(Vector3.zero, h.collider.bounds.size);
                        break;
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
