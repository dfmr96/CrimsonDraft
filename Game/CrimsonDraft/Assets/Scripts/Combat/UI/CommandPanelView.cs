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

        private Action[] submitHandlers   = Array.Empty<Action>();
        private Action[] selectedHandlers = Array.Empty<Action>();
        private EntryVisualState[] entryVisualStates = Array.Empty<EntryVisualState>();
        private static readonly Color DisabledCommandColor = new Color(0.45f, 0.45f, 0.45f, 1f);

        #endregion

        #region ICommandPanelView

        public RectTransform PanelRect => (RectTransform)this.transform;

        // Panel pivot is bottom-center — this aligns that pivot point to the top-center
        // of the operator's card, so the panel sits directly above it with a shared edge
        // (unrelated to how tall the panel currently is mid-unfurl).
        public void RepositionTo(RectTransform operatorRect)
        {
            var panel   = (RectTransform)this.transform;
            var hudRoot = (RectTransform)this.transform.parent;

            var corners = new Vector3[4];
            operatorRect.GetWorldCorners(corners);
            var topCenter = (corners[1] + corners[2]) * 0.5f; // 1=TL, 2=TR
            var localPos  = hudRoot.InverseTransformPoint(topCenter);

            panel.localPosition = new Vector3(localPos.x + this.offset.x, localPos.y + this.offset.y, 0f);
        }

        // No longer grows itself — the operator's own OperatorBorderPanel does that now,
        // so this just positions/activates with its content hidden. Call RevealContent()
        // once that border finishes expanding.
        public void Show(RectTransform operatorRect)
        {
            RepositionTo(operatorRect);

            // Defensive, in addition to Awake() — this GameObject starts inactive in the
            // scene, and belt-and-suspenders is cheap here.
            var ownBackground = GetComponent<Image>();
            if (ownBackground != null) ownBackground.enabled = false;

            this.contentGroup?.DOKill();
            if (this.contentGroup != null) this.contentGroup.alpha = 0f;

            this.gameObject.SetActive(true);
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
                this.contentGroup.alpha = 0f;

            this.gameObject.SetActive(false);
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // The operator's own OperatorBorderPanel is the visible frame now — this
            // panel is purely a content/position holder above it.
            var ownBackground = GetComponent<Image>();
            if (ownBackground != null) ownBackground.enabled = false;

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
