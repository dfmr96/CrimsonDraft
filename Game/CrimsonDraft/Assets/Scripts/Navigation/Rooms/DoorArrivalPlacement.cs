#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    /// <summary>Where a door's arrival point goes by default: centred on the doorway, at a fixed
    /// distance inside the room, facing into it (back to the door). The door plane comes from the
    /// thinner horizontal axis of the door's renderers, because door pivots are often off-centre and
    /// their forward axis does not always face out of the doorway.</summary>
    public static class DoorArrivalPlacement
    {
        public const float DefaultDistance = 1f;
        /// <summary>Height above the bottom of the door, matching the player pivot every spawn used.</summary>
        public const float ArrivalHeight = 1f;

        public static Bounds DoorBounds(Component door)
        {
            Bounds bounds = default;
            bool any = false;
            foreach (var renderer in door.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { bounds = renderer.bounds; any = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return any ? bounds : new Bounds(door.transform.position, new Vector3(0.2f, 2f, 1.2f));
        }

        /// <summary>A point inside the room, used to tell which side of a door is the interior.</summary>
        public static Vector3 InteriorHint(RoomController room)
        {
            var boundsRoot = room.transform.Find("Bounds");
            if (boundsRoot != null && TryCombine(boundsRoot, out var roomBounds)) return roomBounds.center;
            if (TryCombineRenderers(room.transform, out var rendered)) return rendered.center;
            return room.transform.position;
        }

        public static Pose DefaultPose(Bounds doorBounds, Vector3 interiorHint, float distance)
        {
            var size = doorBounds.size;
            var normal = size.x < size.z ? Vector3.right : Vector3.forward;
            if (Vector3.Dot(interiorHint - doorBounds.center, normal) < -1e-4f) normal = -normal;

            var position = doorBounds.center + normal * distance;
            position.y = doorBounds.min.y + ArrivalHeight;
            return new Pose(position, Quaternion.LookRotation(normal, Vector3.up));
        }

        private static bool TryCombine(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (!any) { bounds = collider.bounds; any = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            return any || TryCombineRenderers(root, out bounds);
        }

        private static bool TryCombineRenderers(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { bounds = renderer.bounds; any = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return any;
        }
    }
}
