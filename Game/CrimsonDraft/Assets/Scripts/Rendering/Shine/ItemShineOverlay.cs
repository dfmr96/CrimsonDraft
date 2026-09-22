#nullable enable

using UnityEngine;
using UnityEngine.Rendering;

namespace CrimsonDraft.Rendering.Shine
{
    // Adds the RE1-Remake-style "glint passing over the item" without touching the item's own
    // material: for every MeshFilter/MeshRenderer under this GameObject, spawns a sibling
    // renderer that redraws the exact same mesh with ItemShineSweep.shader, an additive-only
    // pass drawn just above the original surface (see the shader's own Offset comment) -- so
    // it layers on top rather than replacing or modifying the existing material asset, same
    // "duplicate mesh, separate renderer" technique KnobOutline.shader uses for its outline
    // shell, just coincident with the surface instead of extruded outward.
    [DisallowMultipleComponent]
    public sealed class ItemShineOverlay : MonoBehaviour
    {
        private static readonly int SweepMinId   = Shader.PropertyToID("_SweepMin");
        private static readonly int SweepMaxId   = Shader.PropertyToID("_SweepMax");
        private static readonly int SweepWidthId = Shader.PropertyToID("_SweepWidth");
        private static readonly int SweepDirId   = Shader.PropertyToID("_SweepDir");
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");

        [SerializeField] private Material shineMaterial      = null!;
        [SerializeField] private Vector3  sweepDirection     = new(0.35f, 1f, 0f);
        [SerializeField] private float    maxTimeOffset       = 2.5f;

        // Fraction of the mesh's own projected span, not an absolute object-space size --
        // pickup FBXs in this project import at wildly different native scales (this 9mm box's
        // mesh bounds are ~0.02 units, compensated by a 60x Transform.localScale), so a fixed
        // band width would read as either invisible or a full-object wash depending on the mesh.
        [SerializeField, Range(0.05f, 1f)] private float sweepWidthFraction = 0.35f;

        void Awake()
        {
            if (this.shineMaterial == null)
            {
                Debug.LogWarning($"{nameof(ItemShineOverlay)} on '{name}' has no shine material assigned -- skipping.", this);
                return;
            }

            Vector3 dir = this.sweepDirection.sqrMagnitude > 0.0001f
                ? this.sweepDirection.normalized
                : Vector3.up;

            foreach (var sourceFilter in GetComponentsInChildren<MeshFilter>())
            {
                var sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();
                var mesh           = sourceFilter.sharedMesh;
                // Skip disabled renderers -- some pickups keep an inactive placeholder mesh
                // (e.g. a leftover primitive) alongside the real visible model; overlaying
                // that would shine in the placeholder's shape instead of the item's.
                if (sourceRenderer == null || !sourceRenderer.enabled || mesh == null) continue;

                SpawnOverlay(sourceFilter, mesh, dir);
            }
        }

        private void SpawnOverlay(MeshFilter sourceFilter, Mesh mesh, Vector3 dir)
        {
            var overlayObject = new GameObject("ShineOverlay");
            overlayObject.transform.SetParent(sourceFilter.transform, worldPositionStays: false);
            // Marks this subtree so PickupPreviewView.Show() leaves its layer alone instead of
            // pulling it onto "ItemPreview" with the rest of the instantiated model -- the world
            // pickup and the inspect/inventory preview share the same prefab, but only world
            // cameras (which include the default layer) should ever render this glint.
            overlayObject.AddComponent<ShineOverlayRoot>();

            var overlayFilter = overlayObject.AddComponent<MeshFilter>();
            overlayFilter.sharedMesh = mesh;

            var overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = this.shineMaterial;
            overlayRenderer.sharedMaterials    = materials;
            overlayRenderer.shadowCastingMode  = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows     = false;
            overlayRenderer.lightProbeUsage    = LightProbeUsage.Off;
            overlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // Exact min/max of the mesh's own AABB projected onto the sweep axis -- the
            // standard box-onto-axis projection (center dot axis +/- sum of |axis component| *
            // extent) -- so the band's travel exactly spans this mesh regardless of its size,
            // with zero per-item tuning.
            Bounds bounds = mesh.bounds;
            float  radius = Mathf.Abs(dir.x) * bounds.extents.x
                           + Mathf.Abs(dir.y) * bounds.extents.y
                           + Mathf.Abs(dir.z) * bounds.extents.z;
            float centerProjection = Vector3.Dot(dir, bounds.center);
            float width = Mathf.Max(radius * 2f * this.sweepWidthFraction, 0.0001f);

            var block = new MaterialPropertyBlock();
            block.SetFloat(SweepMinId, centerProjection - radius);
            block.SetFloat(SweepMaxId, centerProjection + radius);
            block.SetFloat(SweepWidthId, width);
            block.SetVector(SweepDirId, dir);
            block.SetFloat(TimeOffsetId, Random.Range(0f, this.maxTimeOffset));
            overlayRenderer.SetPropertyBlock(block);
        }
    }
}
