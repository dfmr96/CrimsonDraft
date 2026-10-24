#nullable enable

using UnityEngine;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Where the plan (top-down) camera of the Camera Zone Viewer goes for a room.</summary>
    public static class PlanViewFraming
    {
        public const float DefaultHalfSize = 5f;
        /// <summary>Height above the floor plane where the plan cuts the room, so ceilings do not hide it.</summary>
        public const float CutHeight = 2.2f;

        /// <summary>Near clip for a straight-down camera at <paramref name="cameraY"/>: a section cut
        /// <see cref="CutHeight"/> above <paramref name="floorY"/>.</summary>
        public static float NearClip(float cameraY, float floorY) => Mathf.Max(0.1f, cameraY - (floorY + CutHeight));
        private const float Margin = 1.1f;
        private const float HeightAboveRoom = 10f;

        /// <summary>Straight-down orthographic framing of <paramref name="room"/>, scaled by
        /// <paramref name="zoom"/> and offset on the floor by <paramref name="pan"/> (x, z).</summary>
        public static (Vector3 position, Quaternion rotation, float orthographicSize) Fit(Bounds room, float aspect, float zoom, Vector2 pan)
        {
            zoom = Mathf.Max(zoom, 0.0001f);
            aspect = aspect > 0f ? aspect : 1f;
            float half = Mathf.Max(room.extents.z, room.extents.x / aspect) * Margin;
            if (half <= 0f || float.IsNaN(half)) half = DefaultHalfSize;

            var position = new Vector3(room.center.x + pan.x, room.max.y + HeightAboveRoom, room.center.z + pan.y);
            return (position, Quaternion.Euler(90f, 0f, 0f), half / zoom);
        }

        /// <summary>The room's floor area: its "Bounds" child (colliders, then renderers), else every renderer
        /// of the room, else an empty box on the room pivot.</summary>
        public static Bounds RoomBounds(RoomController room)
        {
            var boundsRoot = room.transform.Find("Bounds");
            if (boundsRoot != null)
            {
                if (TryCombine(boundsRoot.GetComponentsInChildren<Collider>(true), c => c.bounds, out var colliders)) return colliders;
                if (TryCombine(boundsRoot.GetComponentsInChildren<Renderer>(true), r => r.bounds, out var marked)) return marked;
            }
            return TryCombine(room.GetComponentsInChildren<Renderer>(true), r => r.bounds, out var all)
                ? all : new Bounds(room.transform.position, Vector3.zero);
        }

        private static bool TryCombine<T>(T[] items, System.Func<T, Bounds> bounds, out Bounds result)
        {
            result = default;
            bool any = false;
            foreach (var item in items)
            {
                var b = bounds(item);
                if (b.size == Vector3.zero) continue; // disabled colliders report empty bounds at the origin
                if (!any) { result = b; any = true; }
                else result.Encapsulate(b);
            }
            return any;
        }
    }
}
