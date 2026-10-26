#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI
{
    /// <summary>
    /// The Files tab's 3D carousel: one model per note category, drawn by its own camera into a
    /// RenderTexture shown by a RawImage (same trick as the Inspect model preview). The selected model
    /// sits in front at the centre and spins; the other two wait behind it at the sides. Cycling slides
    /// all three to their next place.
    /// </summary>
    public sealed class NoteCarouselView : MonoBehaviour
    {
        [Header("Output")]
        [SerializeField] private RawImage   output      = null!;
        [SerializeField] private Vector2Int textureSize = new Vector2Int(384, 288);

        [Header("Rig")]
        [Tooltip("Camara del preview de Inspect: se clona (con su configuracion de render) para dibujar el carrusel.")]
        [SerializeField] private Camera referenceCamera = null!;
        [Tooltip("Luz del preview de Inspect: sirve de referencia de color/intensidad para la luz propia del carrusel. Opcional.")]
        [SerializeField] private Light? referenceLight;
        [SerializeField] private string previewLayerName = "ItemPreview";
        [Tooltip("Donde se arma el set 3D. Lejos del mundo y del preview de Inspect (su modelo esta en (0, 600, 0) y su camara solo ve 20 m): si coinciden, los modelos de las notas se cuelan en Inspect.")]
        [SerializeField] private Vector3 rigPosition = new Vector3(0f, 900f, 0f);
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 5.8f, -5f);

        [Header("Models")]
        [Tooltip("Un modelo por categoria, en el mismo orden que el carrusel.")]
        [SerializeField] private GameObject[] modelPrefabs = System.Array.Empty<GameObject>();
        [Tooltip("Rotacion base de cada modelo (opcional, mismo orden). Sirve para apoyarlos como se quiera.")]
        [SerializeField] private Vector3[] modelEulers = System.Array.Empty<Vector3>();
        [Tooltip("Escala de cada modelo (mismo orden), sobre la escala propia de su prefab. Los tres miden distinto.")]
        [SerializeField] private float[] modelScales = { 3.7f, 4.8f, 2.8f };

        [Header("Layout")]
        [SerializeField] private Vector3 centerPosition = new Vector3(0f, 0.3f, 0.3f);
        [SerializeField] private Vector3 leftPosition   = new Vector3(-2.1f, 0f, -0.6f);
        [SerializeField] private Vector3 rightPosition  = new Vector3(2.1f, 0f, -0.6f);
        [SerializeField] private float   sideScale      = 0.8f;

        [Header("Motion")]
        [SerializeField] private float slideDuration = 0.35f;
        [Tooltip("Grados por segundo del giro del seleccionado.")]
        [SerializeField] private float spinSpeed     = 40f;
        [SerializeField] private float dimmedBrightness = 0.35f;

        private Camera?      rigCamera;
        private Light?       rigLight;
        private RenderTexture? texture;
        private GameObject?  rigRoot;

        private Transform[] pivots = System.Array.Empty<Transform>();
        private float[]     yaws   = System.Array.Empty<float>();
        private int         selected;

        public int Count    => this.pivots.Length;
        public int Selected => this.selected;

        void Awake()
        {
            BuildRig();
            ApplyPoses(animated: false);
        }

        void OnEnable()
        {
            SetRigActive(true);
            SetDimmed(false, instant: true);
        }

        void OnDisable()
        {
            SetRigActive(false);
        }

        void OnDestroy()
        {
            DOTween.Kill(this);
            if (this.rigRoot != null) Destroy(this.rigRoot);
            if (this.texture != null)
            {
                this.texture.Release();
                Destroy(this.texture);
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < this.pivots.Length; i++)
            {
                // The selected model spins; the others turn back to their resting angle.
                this.yaws[i] = i == this.selected
                    ? Mathf.Repeat(this.yaws[i] + this.spinSpeed * dt, 360f)
                    : Mathf.MoveTowardsAngle(this.yaws[i], 0f, 360f * dt);
                this.pivots[i].localRotation = Quaternion.Euler(0f, this.yaws[i], 0f);
            }
        }

        // ── API ──────────────────────────────────────────────────────────────

        public void Select(int index, bool animated)
        {
            if (this.pivots.Length == 0) return;
            this.selected = ((index % this.pivots.Length) + this.pivots.Length) % this.pivots.Length;
            ApplyPoses(animated);
        }

        public void Cycle(int direction) => Select(this.selected + direction, animated: true);

        /// <summary>Darkens the render while a window is open over it.</summary>
        public void SetDimmed(bool dimmed, bool instant = false)
        {
            float b     = dimmed ? this.dimmedBrightness : 1f;
            var   color = new Color(b, b, b, 1f);
            this.output.DOKill();
            if (instant) this.output.color = color;
            else         this.output.DOColor(color, 0.25f).SetUpdate(true).SetTarget(this);
        }

        /// <summary>World position (on the output RawImage) of the selected model, for a window to pop out of.</summary>
        public bool TryGetSelectedAnchor(out Vector3 world)
        {
            world = default;
            if (this.rigCamera == null || this.pivots.Length == 0) return false;

            Vector3 vp   = this.rigCamera.WorldToViewportPoint(this.pivots[this.selected].position);
            Rect    rect = this.output.rectTransform.rect;
            var     local = new Vector3(
                rect.xMin + Mathf.Clamp01(vp.x) * rect.width,
                rect.yMin + Mathf.Clamp01(vp.y) * rect.height,
                0f);
            world = this.output.rectTransform.TransformPoint(local);
            return true;
        }

        // ── Rig ──────────────────────────────────────────────────────────────

        void BuildRig()
        {
            int layer = LayerMask.NameToLayer(this.previewLayerName);

            this.rigRoot = new GameObject("NoteCarouselRig");
            this.rigRoot.transform.position = this.rigPosition;

            // Same camera setup as the Inspect preview (render pipeline data included), minus its children.
            var camGo = Instantiate(this.referenceCamera.gameObject, this.rigRoot.transform);
            camGo.name = "NoteCarouselCamera";
            for (int i = camGo.transform.childCount - 1; i >= 0; i--)
                Destroy(camGo.transform.GetChild(i).gameObject);

            camGo.transform.localScale = Vector3.one; // the source sits under a zero-scale canvas chain
            camGo.transform.position   = this.rigPosition + this.cameraOffset;
            camGo.transform.LookAt(this.rigPosition + this.centerPosition);

            this.texture = new RenderTexture(this.textureSize.x, this.textureSize.y, 24, RenderTextureFormat.ARGB32)
            {
                name       = "NoteCarouselRT",
                filterMode = FilterMode.Point,
            };
            this.rigCamera = camGo.GetComponent<Camera>();
            this.rigCamera.targetTexture = this.texture;
            this.rigCamera.enabled       = false;
            this.output.texture          = this.texture;

            // The preview layer is lit only by this light (plus whatever the scene's own directionals reach).
            var lightGo = new GameObject("NoteCarouselLight");
            lightGo.transform.SetParent(this.rigRoot.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            this.rigLight             = lightGo.AddComponent<Light>();
            this.rigLight.type        = LightType.Directional;
            this.rigLight.shadows     = LightShadows.None;
            this.rigLight.intensity   = this.referenceLight != null ? this.referenceLight.intensity : 1.2f;
            this.rigLight.color       = this.referenceLight != null ? this.referenceLight.color : Color.white;
            this.rigLight.cullingMask = layer >= 0 ? 1 << layer : ~0;
            this.rigLight.enabled     = false;

            int count = this.modelPrefabs.Length;
            this.pivots = new Transform[count];
            this.yaws   = new float[count];
            for (int i = 0; i < count; i++)
                this.pivots[i] = BuildModel(i, layer);
        }

        Transform BuildModel(int index, int layer)
        {
            var pivot = new GameObject($"Pivot_{index}").transform;
            pivot.SetParent(this.rigRoot!.transform, false);

            var model = CloneVisual(this.modelPrefabs[index], pivot).gameObject;
            model.transform.localPosition = Vector3.zero;
            // The prefab's own rotation (how it was laid out in the prefab editor) comes first; the
            // per-model euler of this component is an extra adjustment on top of it.
            model.transform.localRotation = Quaternion.Euler(index < this.modelEulers.Length ? this.modelEulers[index] : Vector3.zero)
                                            * this.modelPrefabs[index].transform.localRotation;
            SetLayerRecursively(model.transform, layer); // keeps the prefab's own authored scale

            // The three models are authored at very different sizes (and some systems rewrite mesh
            // bounds at runtime, so measuring them is not reliable): each one carries its own scale.
            float scale = index < this.modelScales.Length && this.modelScales[index] > 0f ? this.modelScales[index] : 1f;
            pivot.localScale = Vector3.one * scale;
            return pivot;
        }

        // Rebuilds only the meshes of a prefab as plain, non-static objects. Instantiating the prefab itself
        // is not an option: these models are flagged Batching Static, and Unity then merges the instance into
        // a combined mesh with its position baked in -- it can no longer be moved or rotated.
        static Transform CloneVisual(GameObject prefab, Transform parent)
        {
            var root = new GameObject(prefab.name).transform;
            root.SetParent(parent, false);
            root.localScale = prefab.transform.localScale;

            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null) continue;

                var part = new GameObject(filter.name).transform;
                part.SetParent(root, false);
                if (filter.transform != prefab.transform)
                {
                    part.localPosition = prefab.transform.InverseTransformPoint(filter.transform.position);
                    part.localRotation = Quaternion.Inverse(prefab.transform.rotation) * filter.transform.rotation;
                    part.localScale    = filter.transform.localScale;
                }

                part.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                part.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            return root;
        }

        static void SetLayerRecursively(Transform t, int layer)
        {
            if (layer < 0) return;
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursively(t.GetChild(i), layer);
        }

        void SetRigActive(bool active)
        {
            if (this.rigCamera != null) this.rigCamera.enabled = active;
            if (this.rigLight  != null) this.rigLight.enabled  = active;
        }

        // ── Layout ───────────────────────────────────────────────────────────

        void ApplyPoses(bool animated)
        {
            int n = this.pivots.Length;
            for (int i = 0; i < n; i++)
            {
                // 0 = selected (centre), 1 = next (right), n-1 = previous (left).
                int relative = ((i - this.selected) % n + n) % n;
                Vector3 position;
                float   scale;
                if (relative == 0)      { position = this.centerPosition; scale = 1f; }
                else if (relative == 1) { position = this.rightPosition;  scale = this.sideScale; }
                else                    { position = this.leftPosition;   scale = this.sideScale; }

                var   pivot       = this.pivots[i];
                float baseScale   = this.BaseScale(pivot);
                Vector3 target    = this.rigPosition + position;
                Vector3 targetScl = Vector3.one * (baseScale * scale);

                pivot.DOKill();
                if (animated)
                {
                    pivot.DOMove(target, this.slideDuration).SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(this);
                    pivot.DOScale(targetScl, this.slideDuration).SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(this);
                }
                else
                {
                    pivot.position   = target;
                    pivot.localScale = targetScl;
                }
            }
        }

        // The fit-to-size scale computed once per model; later poses scale relative to it.
        readonly System.Collections.Generic.Dictionary<Transform, float> baseScales = new();

        float BaseScale(Transform pivot)
        {
            if (!this.baseScales.TryGetValue(pivot, out float s))
            {
                s = pivot.localScale.x;
                this.baseScales[pivot] = s;
            }
            return s;
        }
    }
}
