#nullable enable

using DG.Tweening;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    /// <summary>
    /// World-space selector for an enemy or an operator: a slowly turning square ring on the floor at
    /// their feet, split into four glowing lines (optionally with a short open cylinder of light rising from it;
    /// see the TargetHalo shader). The mesh is built once in code at unit size and scaled to whoever
    /// it is shown around in <see cref="ShowAround"/>.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TargetHalo : MonoBehaviour
    {
        private const int Segments = 48;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock? block; // created in EnsureBuilt: Unity forbids it in a field initializer

        [SerializeField] private Material? haloMaterial;
        [Header("Size (derived from the enemy's bounds)")]
        [SerializeField, Min(0.1f)] private float minRadius      = 0.55f;
        [SerializeField, Min(0f)]   private float radiusPadding  = 0.25f; // added to the enemy's half-width
        [SerializeField, Min(0.05f)] private float heightScale   = 0.28f; // of the body's height -- about knee level
        [SerializeField, Min(0.05f)] private float minHeight     = 0.3f;
        [SerializeField]            private float footOffsetY     = 0.02f; // lifts the ring a hair off the floor
        [Header("Look")]
        [Tooltip("Adds the open cylinder of light rising from the ring. Off: just the floor ring.")]
        [SerializeField]            private bool  showColumn      = false;
        [Header("Animation")]
        [SerializeField, Min(0f)]   private float growDuration   = 0.18f;

        private MeshRenderer meshRenderer = null!;
        private Transform? follow;
        private bool built;
        private float sizeMultiplier = 1f;
        private float targetHeight = 1f;
        private float targetRadius = 1f;

        private void Awake()
        {
            this.EnsureBuilt();
            this.meshRenderer.enabled = false;
        }

        private void EnsureBuilt()
        {
            if (this.built) return;

            this.block = new MaterialPropertyBlock();
            this.meshRenderer = this.GetComponent<MeshRenderer>();
            this.GetComponent<MeshFilter>().sharedMesh = BuildMesh(this.showColumn);
            if (this.haloMaterial != null)
                this.meshRenderer.sharedMaterial = this.haloMaterial;

            this.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            this.meshRenderer.receiveShadows    = false;
            this.built = true;
        }

        /// <summary>
        /// Per-instance look on top of the shared material: tint color (via a property block, so
        /// the material asset isn't touched) and an overall size multiplier for radius and height.
        /// </summary>
        public void Configure(Color tint, float sizeMultiplier)
        {
            this.EnsureBuilt();
            this.sizeMultiplier = Mathf.Max(0.05f, sizeMultiplier);

            this.meshRenderer.GetPropertyBlock(this.block!);
            this.block!.SetColor(ColorId, tint);
            this.meshRenderer.SetPropertyBlock(this.block);
        }

        /// <summary>
        /// Shows the halo at an empty slot (e.g. a dead operator's), where there's no body to measure:
        /// default size, glued to the slot.
        /// </summary>
        public void ShowAt(Transform slot)
        {
            this.EnsureBuilt();
            this.Present(slot, hasBounds: false, default, adoptLayer: false);
        }

        /// <summary>Shows the halo at the feet of a character, sized to them.</summary>
        public void ShowAround(GameObject enemy)
        {
            this.EnsureBuilt();

            bool hasBounds = false;
            Bounds bounds = default;
            foreach (var r in enemy.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                    continue;

                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }

            this.Present(enemy.transform, hasBounds, bounds, adoptLayer: true);
        }

        private void Present(Transform target, bool hasBounds, Bounds bounds, bool adoptLayer)
        {
            // Already showing on this very target (e.g. the same operator re-focused): don't replay the grow.
            bool alreadyShown = this.follow == target && this.meshRenderer.enabled;

            this.targetRadius = (hasBounds ? Mathf.Max(this.minRadius, bounds.extents.x + this.radiusPadding) : this.minRadius) * this.sizeMultiplier;
            this.targetHeight = (hasBounds ? Mathf.Max(this.minHeight, bounds.size.y * this.heightScale) : this.minHeight) * this.sizeMultiplier;

            this.follow = target;
            if (adoptLayer)
                this.gameObject.layer = target.gameObject.layer; // rendered by the same camera as the target
            this.UpdatePosition();
            if (alreadyShown) return;

            this.meshRenderer.enabled = true;
            this.transform.DOKill();
            float height = this.showColumn ? this.targetHeight : 1f;
            var finalScale = new Vector3(this.targetRadius, height, this.targetRadius);
            if (this.growDuration > 0f)
            {
                if (this.showColumn)
                {
                    // Grows upward from the floor: the ring is there immediately, the column rises.
                    this.transform.localScale = new Vector3(this.targetRadius, 0.001f, this.targetRadius);
                    this.transform.DOScaleY(height, this.growDuration).SetEase(Ease.OutCubic);
                }
                else
                {
                    // Just the ring: it opens out from a smaller size with a little overshoot.
                    this.transform.localScale = new Vector3(this.targetRadius * 0.6f, height, this.targetRadius * 0.6f);
                    this.transform.DOScale(finalScale, this.growDuration).SetEase(Ease.OutBack);
                }
            }
            else
            {
                this.transform.localScale = finalScale;
            }
        }

        public void Hide()
        {
            this.follow = null;
            this.transform.DOKill();
            if (this.built)
                this.meshRenderer.enabled = false;
        }

        // The enemy can shift (flinch, stagger), so stay glued to its root.
        private void LateUpdate()
        {
            if (this.follow == null) return;

            this.UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (this.follow == null) return;
            var p = this.follow.position;
            this.transform.position = new Vector3(p.x, p.y + this.footOffsetY, p.z);
            this.transform.rotation = Quaternion.identity;
        }

        private void OnDestroy() => this.transform.DOKill();

        // Unit cylinder wall (radius 1, height 1, no caps) + a unit floor disc at y = 0.
        // Vertex color R: 1 = wall, 0 = disc. Wall UV = (around, height); disc UV = planar 0..1.
        private static Mesh BuildMesh(bool includeWall)
        {
            int ringVerts = includeWall ? (Segments + 1) * 2 : 0;
            const int discVerts = 4;
            var vertices  = new Vector3[ringVerts + discVerts];
            var uvs       = new Vector2[vertices.Length];
            var colors    = new Color[vertices.Length];
            var wallTris  = new int[includeWall ? Segments * 6 : 0];
            var discTris  = new int[6];

            for (int i = 0; includeWall && i <= Segments; i++)
            {
                float t     = (float)i / Segments;
                float angle = t * Mathf.PI * 2f;
                float x = Mathf.Cos(angle), z = Mathf.Sin(angle);

                int bottom = i * 2, top = bottom + 1;
                vertices[bottom] = new Vector3(x, 0f, z); uvs[bottom] = new Vector2(t, 0f); colors[bottom] = Color.red;
                vertices[top]    = new Vector3(x, 1f, z); uvs[top]    = new Vector2(t, 1f); colors[top]    = Color.red;

                if (i < Segments)
                {
                    int w = i * 6;
                    wallTris[w]     = bottom; wallTris[w + 1] = top;        wallTris[w + 2] = bottom + 2;
                    wallTris[w + 3] = top;    wallTris[w + 4] = top + 2;    wallTris[w + 5] = bottom + 2;
                }
            }

            // Square floor quad covering [-1, 1]; the shader draws the square ring inside it.
            int q = ringVerts;
            for (int i = 0; i < 4; i++)
            {
                float x = (i == 0 || i == 3) ? -1f : 1f;
                float z = (i < 2) ? -1f : 1f;
                vertices[q + i] = new Vector3(x, 0f, z);
                uvs[q + i]      = new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f);
                colors[q + i]   = Color.black;
            }
            // Wound like the old disc (center, next, current): seen from above, (-1,-1) -> (-1,1) -> (1,1) -> (1,-1).
            discTris[0] = q; discTris[1] = q + 3; discTris[2] = q + 2;
            discTris[3] = q; discTris[4] = q + 2; discTris[5] = q + 1;

            var mesh = new Mesh { name = "TargetHalo" };
            mesh.subMeshCount = 1;
            mesh.vertices = vertices;
            mesh.uv       = uvs;
            mesh.colors   = colors;

            var tris = new int[wallTris.Length + discTris.Length];
            wallTris.CopyTo(tris, 0);
            discTris.CopyTo(tris, wallTris.Length);
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
