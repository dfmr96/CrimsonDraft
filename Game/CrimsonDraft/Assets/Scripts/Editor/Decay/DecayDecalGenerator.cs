#nullable enable

using System.Collections.Generic;
using CrimsonDraft.Rendering.Decay;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CrimsonDraft.Editor.Decay
{
    // Calcula dónde van los decals de deterioro de una sala tirando rayos desde adentro de la
    // caja, contra la geometría de ESA sala solamente (sus renderers, sin los excluidos):
    //   - Mugre en la base de las paredes (zócalo).
    //   - Filtraciones que bajan desde la unión pared/techo.
    //   - Óxido chorreado sobre superficies de metal (materiales con DecayMetal).
    //   - Moho en los rincones bajos.
    //   - Manchas de agua en piso y techo.
    // Solo calcula (en mundo, sobre la instancia de la escena); DecayRoomTool los escribe en el
    // prefab. Mismo seed => mismo resultado.
    internal static class DecayDecalGenerator
    {
        public const string ContainerName   = "DecayDecals";
        public const float  ProjectionDepth = 0.3f;

        private const int AttemptsPerDecal = 6;

        private static readonly int MetalId = Shader.PropertyToID("_DecayMetal");

        public readonly struct Placement
        {
            public readonly DecayDecal.Kind Kind;
            public readonly Vector3         Position;  // mundo
            public readonly Quaternion      Rotation;  // mundo
            public readonly Vector2         Size;
            public readonly bool            FlipU;
            public readonly float           Fade;
            public readonly DecayDetail     MinimumDetail;

            public Placement(DecayDecal.Kind kind, Vector3 position, Quaternion rotation, Vector2 size, bool flipU, float fade, DecayDetail minimumDetail)
            {
                this.Kind = kind; this.Position = position; this.Rotation = rotation; this.Size = size;
                this.FlipU = flipU; this.Fade = fade; this.MinimumDetail = minimumDetail;
            }
        }

        private sealed class Context
        {
            public readonly DecayVolume     Volume;
            public readonly System.Random   Rng;
            public readonly Vector3         Size;
            public readonly Quaternion      Rotation;
            public readonly Vector3         Center;
            public readonly List<Vector3>   Occupied = new();
            public readonly List<Placement> Result = new();

            public Context(DecayVolume volume, int seed)
            {
                this.Volume   = volume;
                this.Rng      = new System.Random(seed);
                this.Size     = volume.WorldSize;
                this.Rotation = volume.transform.rotation;
                this.Center   = volume.WorldCenter;
            }

            public float Range(float min, float max) => min + (float)this.Rng.NextDouble() * (max - min);

            // Punto de la caja en coordenadas [0..1] por eje (y = 0 piso, 1 techo).
            public Vector3 Point(float x01, float y01, float z01) =>
                this.Center + this.Rotation * Vector3.Scale(new Vector3(x01 - 0.5f, y01 - 0.5f, z01 - 0.5f), this.Size);

            public Vector3 HorizontalDirection()
            {
                float a = this.Range(0f, Mathf.PI * 2f);
                return this.Rotation * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            }
        }

        /// <param name="occupied">Posiciones (mundo) de decals fijados, para no encimarse.</param>
        public static List<Placement> Compute(DecayVolume volume, int seed, IEnumerable<Vector3> occupied, bool[] kindsAvailable)
        {
            var ctx = new Context(volume, seed);
            ctx.Occupied.AddRange(occupied);

            var temporaryColliders = AddTemporaryColliders(volume);
            try
            {
                Physics.SyncTransforms();

                float d = volume.Decay * volume.DecalDensity;
                float area = Mathf.Sqrt(ctx.Size.x * ctx.Size.z) / 5f; // ~1 para una sala de 5x5
                int Count(DecayDecal.Kind kind, float perRoom, float layer) =>
                    kindsAvailable[(int)kind] ? Mathf.RoundToInt(perRoom * area * d * layer) : 0;

                Place(ctx, DecayDecal.Kind.Dirt,       Count(DecayDecal.Kind.Dirt,       8f, volume.Dirt),                          TryBaseboard);
                Place(ctx, DecayDecal.Kind.Leak,       Count(DecayDecal.Kind.Leak,       5f, volume.Damp),                          TryLeak);
                Place(ctx, DecayDecal.Kind.Rust,       Count(DecayDecal.Kind.Rust,       5f, volume.Rust),                          TryRust);
                Place(ctx, DecayDecal.Kind.Mold,       Count(DecayDecal.Kind.Mold,       4f, volume.Damp * volume.Mold),            TryMold);
                Place(ctx, DecayDecal.Kind.FloorStain, Count(DecayDecal.Kind.FloorStain, 5f, (volume.Dirt + volume.Damp) * 0.5f), TryFloorOrCeiling);
            }
            finally
            {
                foreach (var c in temporaryColliders) Object.DestroyImmediate(c);
                Physics.SyncTransforms();
            }

            return ctx.Result;
        }

        // --- Reglas --------------------------------------------------------------------------

        private delegate bool Rule(Context ctx, out Vector3 point, out Vector3 normal, out Vector2 size, out float anchorUp);

        private static void Place(Context ctx, DecayDecal.Kind kind, int count, Rule rule)
        {
            for (int i = 0; i < count; i++)
            for (int attempt = 0; attempt < AttemptsPerDecal; attempt++)
            {
                if (!rule(ctx, out var point, out var normal, out var size, out float anchorUp)) continue;
                if (TooClose(ctx, point, Mathf.Min(size.x, size.y) * 0.6f)) continue;

                Add(ctx, kind, point, normal, size, anchorUp);
                break;
            }
        }

        private static bool TryBaseboard(Context ctx, out Vector3 point, out Vector3 normal, out Vector2 size, out float anchorUp)
        {
            size = new Vector2(ctx.Range(0.8f, 1.8f), ctx.Range(0.35f, 0.7f));
            anchorUp = -1f; // pegado al piso
            float y = Mathf.Clamp01(ctx.Range(0.1f, 0.3f) / Mathf.Max(ctx.Size.y, 0.01f));
            return CastToWall(ctx, ctx.Point(ctx.Range(0.25f, 0.75f), y, ctx.Range(0.25f, 0.75f)), out point, out normal);
        }

        private static bool TryLeak(Context ctx, out Vector3 point, out Vector3 normal, out Vector2 size, out float anchorUp)
        {
            size = new Vector2(ctx.Range(0.8f, 1.6f), ctx.Range(1f, Mathf.Max(1.2f, ctx.Volume.CeilingStreakLength * 1.5f)));
            anchorUp = 1f; // cuelga del techo
            float y = 1f - Mathf.Clamp01(0.15f / Mathf.Max(ctx.Size.y, 0.01f));
            return CastToWall(ctx, ctx.Point(ctx.Range(0.25f, 0.75f), y, ctx.Range(0.25f, 0.75f)), out point, out normal);
        }

        private static bool TryRust(Context ctx, out Vector3 point, out Vector3 normal, out Vector2 size, out float anchorUp)
        {
            size = new Vector2(ctx.Range(0.25f, 0.6f), ctx.Range(0.5f, 1.2f));
            anchorUp = 0f;
            var origin = ctx.Point(ctx.Range(0.2f, 0.8f), ctx.Range(0.25f, 0.9f), ctx.Range(0.2f, 0.8f));
            if (!Raycast(ctx, origin, ctx.HorizontalDirection(), out var hit)) { point = normal = default; return false; }

            point  = hit.point;
            normal = hit.normal;
            return Mathf.Abs(normal.y) < 0.3f && MetalAt(hit) > 0.3f;
        }

        private static bool TryMold(Context ctx, out Vector3 point, out Vector3 normal, out Vector2 size, out float anchorUp)
        {
            size = Vector2.one * ctx.Range(0.4f, 0.9f);
            anchorUp = -1f;

            // Hacia una esquina de la caja: el rayo termina en el rincón o muy cerca.
            float cx = ctx.Rng.Next(2) == 0 ? 0.03f : 0.97f;
            float cz = ctx.Rng.Next(2) == 0 ? 0.03f : 0.97f;
            float y  = Mathf.Clamp01(ctx.Range(0.1f, 0.4f) / Mathf.Max(ctx.Size.y, 0.01f));
            var origin = ctx.Point(0.5f, y, 0.5f);
            var target = ctx.Point(cx, y, cz);
            if (!Raycast(ctx, origin, (target - origin).normalized, out var hit)) { point = normal = default; return false; }

            point  = hit.point;
            normal = hit.normal;
            return Mathf.Abs(normal.y) < 0.3f;
        }

        private static bool TryFloorOrCeiling(Context ctx, out Vector3 point, out Vector3 normal, out Vector2 size, out float anchorUp)
        {
            size = Vector2.one * ctx.Range(0.6f, 1.6f);
            anchorUp = 0f;
            bool ceiling = ctx.Rng.NextDouble() < 0.35;
            var origin = ctx.Point(ctx.Range(0.1f, 0.9f), 0.5f, ctx.Range(0.1f, 0.9f));
            var dir    = ctx.Rotation * (ceiling ? Vector3.up : Vector3.down);
            if (!Raycast(ctx, origin, dir, out var hit)) { point = normal = default; return false; }

            point  = hit.point;
            normal = hit.normal;
            return ceiling ? normal.y < -0.7f : normal.y > 0.7f;
        }

        private static bool CastToWall(Context ctx, Vector3 origin, out Vector3 point, out Vector3 normal)
        {
            point = normal = default;
            if (!Raycast(ctx, origin, ctx.HorizontalDirection(), out var hit)) return false;
            point  = hit.point;
            normal = hit.normal;
            return Mathf.Abs(normal.y) < 0.3f;
        }

        private static bool Raycast(Context ctx, Vector3 origin, Vector3 dir, out RaycastHit hit)
        {
            float max = ctx.Size.magnitude;
            var hits = Physics.RaycastAll(origin, dir, max, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                // Lo primero que se vea de esta sala; jugador, enemigos y triggers se atraviesan.
                // Si lo primero es otra sala (puerta abierta, pared compartida), no hay decal.
                var renderer = h.collider.GetComponentInParent<Renderer>();
                if (renderer == null || h.collider.attachedRigidbody != null || !IsEnvironment(renderer.gameObject)) continue;
                if (!BelongsToRoom(ctx.Volume, renderer.transform)) break;
                hit = h;
                return true;
            }

            hit = default;
            return false;
        }

        private static float MetalAt(RaycastHit hit)
        {
            var renderer = hit.collider.GetComponentInParent<Renderer>();
            if (renderer == null) return 0f;
            float metal = 0f;
            foreach (var m in renderer.sharedMaterials)
                if (m != null && m.HasProperty(MetalId)) metal = Mathf.Max(metal, m.GetFloat(MetalId));
            return metal;
        }

        private static bool TooClose(Context ctx, Vector3 point, float minDistance)
        {
            foreach (var p in ctx.Occupied)
                if ((p - point).sqrMagnitude < minDistance * minDistance) return true;
            return false;
        }

        private static void Add(Context ctx, DecayDecal.Kind kind, Vector3 point, Vector3 normal, Vector2 size, float anchorUp)
        {
            // Proyecta hacia la superficie; en paredes el "arriba" del decal es el arriba del mundo
            // (las texturas de chorreadura tienen la fuente arriba).
            bool horizontal = Mathf.Abs(normal.y) > 0.7f;
            Vector3 up = horizontal ? ctx.Rotation * Quaternion.Euler(0f, ctx.Range(0f, 360f), 0f) * Vector3.forward : Vector3.up;
            var rotation = Quaternion.LookRotation(-normal, up);

            // Con pivot 0 la caja de proyección queda centrada en la superficie. anchorUp desplaza
            // el decal para que su borde superior (1) o inferior (-1) quede en el punto.
            Vector3 position = point;
            if (!horizontal) position -= Vector3.up * (anchorUp * size.y * 0.5f);

            int index = ctx.Result.Count;
            ctx.Result.Add(new Placement(kind, position, rotation, size,
                flipU: ctx.Rng.Next(2) == 0, fade: ctx.Range(0.6f, 1f),
                minimumDetail: index % 2 == 1 ? DecayDetail.Medium : DecayDetail.Low)); // en Bajo queda la mitad
            ctx.Occupied.Add(point);
        }

        // Paredes, pisos y muebles; no personajes (Animator/CharacterController) ni decals.
        internal static bool IsEnvironment(GameObject go) =>
            go.GetComponentInParent<Animator>() == null &&
            go.GetComponentInParent<CharacterController>() == null &&
            go.GetComponentInParent<DecalProjector>() == null;

        internal static bool BelongsToRoom(DecayVolume volume, Transform t) =>
            t.IsChildOf(volume.Room) && !t.IsChildOf(volume.transform) && !volume.IsExcluded(t);

        // La geometría de las salas no siempre tiene colliders (paredes de fondo, techos que no
        // pisa nadie). Para los rayos se agregan MeshColliders temporales a la sala y se sacan al terminar.
        private static List<Collider> AddTemporaryColliders(DecayVolume volume)
        {
            var added = new List<Collider>();
            foreach (var filter in volume.Room.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || !IsEnvironment(filter.gameObject)) continue;
                if (filter.GetComponent<Collider>() != null || !BelongsToRoom(volume, filter.transform)) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled) continue;

                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.hideFlags = HideFlags.HideAndDontSave;
                collider.sharedMesh = filter.sharedMesh;
                added.Add(collider);
            }
            return added;
        }
    }
}
