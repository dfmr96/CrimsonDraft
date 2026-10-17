#nullable enable

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// One highlight bar per navigation panel instead of an underline image on every button: it
    /// glides to whichever item is current (matching its world rect, so a hover scale-up on the
    /// item carries over) and turns white to mark a confirmed selection, green otherwise.
    ///
    /// Two ways to drive it. With <see cref="buttons"/> filled (the title's four buttons) it
    /// follows the EventSystem's selected button on its own, is hidden while none of them is
    /// selected, follows that button's CanvasGroup alpha (so the staggered reveal fades it in too),
    /// and flashes white whenever one of them is clicked. Left empty, a controller drives it
    /// through <see cref="Follow"/> / <see cref="SetConfirmed"/> / <see cref="Flash"/>.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class MenuSelectionBar : MonoBehaviour
    {
        [Header("Follow")]
        [Tooltip("Botones que la barra sigue por seleccion del EventSystem. Vacio = la maneja un controlador por codigo.")]
        [SerializeField] private Button[] buttons = System.Array.Empty<Button>();
        [SerializeField] private float followSpeed = 18f;

        [Header("Colors")]
        [Tooltip("Color de la barra confirmada. El color normal (verde de la UI) es el que ya tiene la Image.")]
        [SerializeField] private Color confirmedColor = Color.white;
        [SerializeField] private float flashDuration  = 0.35f;
        [SerializeField] private float colorSpeed     = 20f;

        private Image         image = null!;
        private RectTransform rect  = null!;
        private Color         normalColor;

        private RectTransform? target;
        private CanvasGroup?   targetGroup;
        private bool           snapNext = true;
        private bool           confirmed;
        private float          flash;      // 1 right after Flash(), decays to 0
        private float          whiteAmount;
        private float          alpha;

        private void Awake()
        {
            this.image = GetComponent<Image>();
            this.rect  = (RectTransform)transform;
            this.image.raycastTarget = false;

            // Position and size are driven from the target's world rect, so the bar's own
            // anchoring is collapsed to its centre to make sizeDelta mean "size".
            this.rect.anchorMin = this.rect.anchorMax = this.rect.pivot = new Vector2(0.5f, 0.5f);

            this.normalColor = this.image.color;
            this.alpha       = buttons.Length > 0 ? 0f : this.normalColor.a;
        }

        private void OnEnable()
        {
            this.snapNext = true;
            foreach (var button in this.buttons)
                if (button != null) button.onClick.AddListener(Flash);
        }

        private void OnDisable()
        {
            foreach (var button in this.buttons)
                if (button != null) button.onClick.RemoveListener(Flash);
            this.flash = 0f;
        }

        /// <summary>Points the bar at an item. snap = jump there instead of gliding.</summary>
        public void Follow(RectTransform item, bool snap = false)
        {
            if (snap || this.target == null) this.snapNext = true;
            this.target      = item;
            this.targetGroup = null;
        }

        /// <summary>Next update jumps straight to the target (e.g. when a panel opens).</summary>
        public void Snap() => this.snapNext = true;

        /// <summary>Holds the bar white (a confirmed selection) or releases it back to green.</summary>
        public void SetConfirmed(bool value) => this.confirmed = value;

        /// <summary>Brief white pulse that fades back to green.</summary>
        public void Flash() => this.flash = 1f;

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            float targetAlpha = this.normalColor.a;

            if (this.buttons.Length > 0)
            {
                var found = FindSelected();
                if (found != null && found != this.target) Follow(found, snap: this.alpha < 0.05f);
                if (found != null) this.targetGroup = found.GetComponentInParent<CanvasGroup>();
                targetAlpha = found == null ? 0f : this.normalColor.a * (this.targetGroup != null ? this.targetGroup.alpha : 1f);
            }

            this.flash = Mathf.Max(0f, this.flash - dt / Mathf.Max(0.0001f, this.flashDuration));

            if (this.target != null)
                MoveToTarget(dt);

            float k = 1f - Mathf.Exp(-this.colorSpeed * dt);
            float whiteTarget = this.confirmed ? 1f : this.flash;
            this.whiteAmount = this.snapNext ? whiteTarget : Mathf.Lerp(this.whiteAmount, whiteTarget, k);
            this.alpha       = Mathf.Lerp(this.alpha, targetAlpha, k);

            var color = Color.Lerp(this.normalColor, this.confirmedColor, this.whiteAmount);
            color.a = this.alpha;
            this.image.color = color;

            this.snapNext = false;
        }

        private RectTransform? FindSelected()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return null;
            foreach (var button in this.buttons)
                if (button != null && button.gameObject == selected) return (RectTransform)button.transform;
            return null;
        }

        private readonly Vector3[] corners = new Vector3[4];

        private void MoveToTarget(float dt)
        {
            this.target!.GetWorldCorners(this.corners);
            Vector3 center = (this.corners[0] + this.corners[2]) * 0.5f;
            Vector3 scale  = this.rect.lossyScale;
            var size = new Vector2(
                (this.corners[3] - this.corners[0]).magnitude / Mathf.Max(0.0001f, scale.x),
                (this.corners[1] - this.corners[0]).magnitude / Mathf.Max(0.0001f, scale.y));

            if (this.snapNext)
            {
                this.rect.position  = center;
                this.rect.sizeDelta = size;
                return;
            }

            float k = 1f - Mathf.Exp(-this.followSpeed * dt);
            this.rect.position  = Vector3.Lerp(this.rect.position, center, k);
            this.rect.sizeDelta = Vector2.Lerp(this.rect.sizeDelta, size, k);
        }
    }
}
