#nullable enable

using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrimsonDraft.Combat
{
    public sealed class CommandPanelView : MonoBehaviour, ICommandPanelView
    {
        #region Events

        public event Action<CombatCommand>?  OnCommandSelected;
        public event Action<RectTransform>?  OnEntryFocused;

        #endregion

        #region Fields

        [Serializable]
        private sealed class CommandEntry
        {
            public ActionMenuItem item    = null!;
            public CombatCommand  command;
        }

        private sealed class EntryVisualState
        {
            public Graphic[] Graphics = Array.Empty<Graphic>();
            public Color[]   Colors   = Array.Empty<Color>();
        }

        [SerializeField] private CommandEntry[] entries        = Array.Empty<CommandEntry>();
        [SerializeField] private Vector2        offset         = Vector2.zero;
        [SerializeField] private Image          dimmingOverlay = null!;
        [SerializeField] private CanvasGroup?   contentGroup;
        [SerializeField] private float          revealDuration = 0.12f;

        [Header("Open Animation")]
        [SerializeField, Range(0f, 1f)] private float openStartScale = 0.5f;
        [SerializeField] private float openDuration = 0.15f;

        [Header("Tactical Swap")]
        [SerializeField] private float closeDuration = 0.15f;

        // The card's focus bounce is still moving when the panel opens, so the panel keeps tracking the card's top edge.
        private RectTransform? followTarget;

        // The operator card's "Tactical" strip (ATB bar) is what the panel grows out of: it is hidden while the
        // panel is open and comes back once the panel has collapsed to its size again.
        private RectTransform? tactical;
        private CanvasGroup?   tacticalGroup;
        private const string   TacticalName = "Tactical";

        private Action[] submitHandlers   = Array.Empty<Action>();
        private Action[] selectedHandlers = Array.Empty<Action>();
        private EntryVisualState[] entryVisualStates = Array.Empty<EntryVisualState>();
        private static readonly Color DisabledCommandColor = new Color(0.45f, 0.45f, 0.45f, 1f);

        #endregion

        #region ICommandPanelView

        public RectTransform PanelRect => (RectTransform)this.transform;

        // Panel pivot is bottom-center — with a Tactical strip on the card this aligns that pivot to the strip's
        // bottom-center (the panel takes its place); without one, to the top-center of the operator's card, so
        // the panel sits directly above it with a shared edge (unrelated to how tall the panel currently is
        // mid-unfurl).
        public void RepositionTo(RectTransform operatorRect)
        {
            this.followTarget = operatorRect;

            var panel   = (RectTransform)this.transform;
            var hudRoot = (RectTransform)this.transform.parent;

            var strip = FindTactical(operatorRect);
            var corners = new Vector3[4];
            (strip != null ? strip : operatorRect).GetWorldCorners(corners);
            // 0=BL, 1=TL, 2=TR, 3=BR
            var anchorPoint = strip != null
                ? (corners[0] + corners[3]) * 0.5f
                : (corners[1] + corners[2]) * 0.5f;
            var localPos = hudRoot.InverseTransformPoint(anchorPoint);

            panel.localPosition = new Vector3(localPos.x + this.offset.x, localPos.y + this.offset.y, 0f);
        }

        // Positions/activates with its content hidden; call RevealContent() to fade it in.
        public void Show(RectTransform operatorRect)
        {
            this.RestoreTactical();
            RepositionTo(operatorRect);

            this.contentGroup?.DOKill();
            if (this.contentGroup != null)
            {
                this.contentGroup.alpha          = 0f;
                this.contentGroup.interactable   = true;
                this.contentGroup.blocksRaycasts = true;
            }

            this.gameObject.SetActive(true);

            // Pivot is bottom-center, so it opens like a window growing out of the Tactical strip (or the
            // card's top edge when there is none).
            this.tactical = FindTactical(operatorRect);
            this.transform.DOKill();
            this.transform.localScale = this.ComputeCollapsedScale();
            this.SetTacticalHidden(true);
            this.transform.DOScale(Vector3.one, this.openDuration).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        public void RevealContent()
        {
            if (this.contentGroup != null)
                this.contentGroup.DOFade(1f, this.revealDuration);
            SelectFirstNextFrame().Forget();
        }

        public void Focus() => SelectFirstNextFrame().Forget();

        public void SetCommandEnabled(CombatCommand command, bool enabled)
        {
            for (int i = 0; i < this.entries.Length; i++)
            {
                if (this.entries[i].command != command || this.entries[i].item == null)
                    continue;

                this.entries[i].item.interactable = enabled;
                this.ApplyCommandVisualState(i, enabled);
                RerouteNavigationAround(this.entries[i].item, enabled);
            }
        }

        // Entries use Explicit navigation with hardcoded neighbor links, so a disabled entry's own
        // interactable/mode flags don't stop a NEIGHBOR from still navigating onto it via its
        // hardcoded selectOnUp/selectOnDown. Splice the disabled entry out of the chain instead —
        // its neighbors point at each other while it's disabled, and get reconnected through it
        // again once it's re-enabled. The entry's own selectOnUp/selectOnDown never change.
        private static void RerouteNavigationAround(ActionMenuItem item, bool enabled)
        {
            var nav  = item.navigation;
            var up   = nav.selectOnUp   as ActionMenuItem;
            var down = nav.selectOnDown as ActionMenuItem;

            if (up != null)
            {
                var upNav = up.navigation;
                upNav.selectOnDown = enabled ? item : down;
                up.navigation = upNav;
            }

            if (down != null)
            {
                var downNav = down.navigation;
                downNav.selectOnUp = enabled ? item : up;
                down.navigation = downNav;
            }
        }

        public void SetDimmed(bool dimmed)
        {
            if (this.dimmingOverlay != null)
                this.dimmingOverlay.DOFade(dimmed ? 0.6f : 0f, 0.1f);
        }

        private void LateUpdate()
        {
            if (this.followTarget != null)
                this.RepositionTo(this.followTarget);
        }

        public void Hide()
        {
            if (this.dimmingOverlay != null)
            {
                this.dimmingOverlay.DOKill();
                var c = this.dimmingOverlay.color;
                c.a = 0f;
                this.dimmingOverlay.color = c;
            }

            // Kill any in-flight reveal so the next Show() always starts clean (DOTween
            // tweens keep running on an inactive target otherwise).
            this.contentGroup?.DOKill();
            if (this.contentGroup != null)
            {
                this.contentGroup.alpha          = 0f;
                this.contentGroup.interactable   = false;
                this.contentGroup.blocksRaycasts = false;
            }

            this.transform.DOKill();

            // Nothing on screen to animate (already hidden, or no Tactical strip to collapse into): close at once.
            if (!this.gameObject.activeInHierarchy || this.tactical == null)
            {
                this.FinishHide();
                return;
            }

            // Collapses back into the Tactical strip, which reappears once it has the panel's old spot again.
            this.transform.DOScale(this.ComputeCollapsedScale(), Mathf.Max(0.01f, this.closeDuration))
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .OnComplete(this.FinishHide);
        }

        private void FinishHide()
        {
            this.followTarget = null;
            this.transform.DOKill();
            this.transform.localScale = Vector3.one;
            this.RestoreTactical();
            this.gameObject.SetActive(false);
        }

        // The "Tactical" sibling of the operator card's Visual: operatorRect is the card art (Visual/Black_BG).
        private static RectTransform? FindTactical(RectTransform operatorRect)
        {
            for (var t = operatorRect.parent; t != null; t = t.parent)
            {
                if (t.Find(TacticalName) is RectTransform strip)
                    return strip;
            }
            return null;
        }

        // Panel scale at which it exactly covers the Tactical strip (the unfurl starts and the collapse ends there).
        private Vector3 ComputeCollapsedScale()
        {
            if (this.tactical == null)
                return Vector3.one * this.openStartScale;

            var panel   = (RectTransform)this.transform;
            var hudRoot = (RectTransform)this.transform.parent;
            var corners = new Vector3[4];
            this.tactical.GetWorldCorners(corners);
            var bl = hudRoot.InverseTransformPoint(corners[0]);
            var tr = hudRoot.InverseTransformPoint(corners[2]);

            var size = panel.rect.size;
            float sx = size.x > 0f ? Mathf.Abs(tr.x - bl.x) / size.x : this.openStartScale;
            float sy = size.y > 0f ? Mathf.Abs(tr.y - bl.y) / size.y : this.openStartScale;
            return new Vector3(Mathf.Clamp(sx, 0.05f, 1.5f), Mathf.Clamp(sy, 0.05f, 1.5f), 1f);
        }

        private void SetTacticalHidden(bool hidden)
        {
            if (this.tactical == null) return;
            if (this.tacticalGroup == null)
            {
                this.tacticalGroup = this.tactical.GetComponent<CanvasGroup>();
                if (this.tacticalGroup == null) this.tacticalGroup = this.tactical.gameObject.AddComponent<CanvasGroup>();
            }
            this.tacticalGroup.alpha = hidden ? 0f : 1f;
        }

        private void RestoreTactical()
        {
            this.SetTacticalHidden(false);
            this.tactical      = null;
            this.tacticalGroup = null;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            this.submitHandlers   = new Action[this.entries.Length];
            this.selectedHandlers = new Action[this.entries.Length];
            this.entryVisualStates = new EntryVisualState[this.entries.Length];

            for (int i = 0; i < this.entries.Length; i++)
            {
                var state = new EntryVisualState();
                if (this.entries[i].item != null)
                {
                    state.Graphics = this.entries[i].item.GetComponentsInChildren<Graphic>(includeInactive: true);
                    state.Colors   = new Color[state.Graphics.Length];
                    for (int g = 0; g < state.Graphics.Length; g++)
                        state.Colors[g] = state.Graphics[g].color;
                }

                this.entryVisualStates[i] = state;
            }
        }

        private void OnEnable()
        {
            for (int i = 0; i < this.entries.Length; i++)
            {
                var capturedCommand      = this.entries[i].command;
                var capturedAnchor       = (RectTransform)this.entries[i].item.transform;
                this.submitHandlers[i]   = () => this.OnCommandSelected?.Invoke(capturedCommand);
                this.selectedHandlers[i] = () => this.OnEntryFocused?.Invoke(capturedAnchor);

                this.entries[i].item.OnSubmit   += this.submitHandlers[i];
                this.entries[i].item.OnSelected += this.selectedHandlers[i];
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < this.entries.Length; i++)
            {
                this.entries[i].item.OnSubmit   -= this.submitHandlers[i];
                this.entries[i].item.OnSelected -= this.selectedHandlers[i];
            }
        }

        #endregion

        #region Private

        private void ApplyCommandVisualState(int index, bool enabled)
        {
            if (index < 0 || index >= this.entryVisualStates.Length)
                return;

            var state = this.entryVisualStates[index];
            for (int i = 0; i < state.Graphics.Length; i++)
            {
                var g = state.Graphics[i];
                if (g == null)
                    continue;

                Color color = enabled ? state.Colors[i] : DisabledCommandColor;
                g.color = color;
            }
        }

        private async UniTaskVoid SelectFirstNextFrame()
        {
            await UniTask.NextFrame();
            if (this.entries.Length == 0 || EventSystem.current == null)
                return;

            int focusIndex = 0;
            for (int i = 0; i < this.entries.Length; i++)
            {
                if (this.entries[i].item != null && this.entries[i].item.interactable)
                {
                    focusIndex = i;
                    break;
                }
            }

            EventSystem.current.SetSelectedGameObject(this.entries[focusIndex].item.gameObject);
        }

        #endregion
    }
}
