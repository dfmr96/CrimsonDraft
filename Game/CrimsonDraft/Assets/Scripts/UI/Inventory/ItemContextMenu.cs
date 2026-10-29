#nullable enable

using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using CrimsonDraft.Infrastructure.UI;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.UI
{
    public class ItemContextMenu : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup  canvasGroup  = null!;
        [SerializeField] private MenuOption[] options      = null!; // 0=Use, 1=Inspect, 2=Combine
        [SerializeField] private InspectPanel inspectPanel = null!;

        [Header("Selection")]
        [Tooltip("Barra que se desliza bajo el texto de la opcion actual (la misma que usa la pausa). Vacio = cada opcion se marca sola, como antes.")]
        [SerializeField] private SelectionCursor? selector;

        [Header("Open Animation")]
        [Tooltip("El menu se abre en X desde 0 hasta su ancho, creciendo desde el lado pegado al objeto.")]
        [SerializeField] private float openDuration = 0.12f;

        [Header("Blocked Move")]
        [Tooltip("Cuando se intenta moverse y no se puede: el boton actual avanza esta fraccion de la distancia hasta la opcion vecina y vuelve.")]
        [SerializeField, Range(0.05f, 0.5f)] private float bumpTravel = 0.25f;
        [SerializeField] private float bumpDuration = 0.26f;
        [Tooltip("Cuantas veces tiembla de lado a lado durante el gesto.")]
        [SerializeField] private int   bumpShakes = 3;
        [Tooltip("Amplitud del temblor lateral, como fraccion del recorrido de la ida.")]
        [SerializeField] private float bumpShakeAmount = 0.3f;

        [Header("Passing Between Options")]
        [Tooltip("Al pasar de una opcion a otra, la nueva avanza esta fraccion de la distancia entre filas y vuelve (muy leve, en armonia con el temblor de 'no se puede').")]
        [SerializeField, Range(0f, 0.3f)] private float passTravel = 0.1f;
        [SerializeField] private float passDuration = 0.14f;
        [SerializeField] private float passShakeAmount = 0.2f;

        private static readonly int[] DefaultSelectionPriority = { 0, 2, 1 };

        private readonly List<MenuOption> pool = new();
        private string[]          defaultLabels = null!;
        private int               visibleCount;
        private IReadOnlyList<UseTarget>? useTargets;
        private bool              targetMode;
        private Vector2           actionSize; // the menu's authored size, restored after the operator list
        private bool[]            actionDisabled = System.Array.Empty<bool>(); // the action buttons' state while the operator list is up
        private int               selectedIndex = 0;
        private bool              isOpen        = false;
        private bool              currentCanSplit;
        private InventoryItemView? currentItem;
        private RectTransform     rectTransform = null!;
        private Tween?            openTween;
        private bool              opening;
        private Tween?            bumpTween;
        private RectTransform?    bumpRect;
        private Vector2           bumpRestPosition;

        public bool IsOpen => this.isOpen;
        public System.Action? OnClose;
        public System.Action<InventoryItemView>? OnUseRequested;
        public System.Action<InventoryItemView, int>? OnUseOnRequested; // item, operator slot
        public System.Action<InventoryItemView>? OnCombineRequested;
        public System.Action<InventoryItemView>? OnSplitRequested;

        void Awake()
        {
            this.rectTransform = GetComponent<RectTransform>();

            if (this.canvasGroup == null)
                this.canvasGroup = GetComponent<CanvasGroup>();

            this.actionSize = this.rectTransform.sizeDelta;
            this.pool.AddRange(this.options);
            this.defaultLabels = new string[this.options.Length];
            for (int i = 0; i < this.options.Length; i++) this.defaultLabels[i] = this.options[i].Label;

            this.isOpen = false;
            Hide();
        }

        public void Open(InventoryItemView item, ContextMenuOptions options)
        {
            this.currentItem     = item;
            this.isOpen          = true;
            this.currentCanSplit = options.CanSplit;
            this.useTargets      = options.CanUse ? options.UseTargets : null;
            ShowActionOptions();

            this.options[0].SetDisabled(!options.CanEquip && !options.CanUse && !options.CanSplit);
            this.options[1].SetDisabled(!options.CanInspect);
            this.options[2].SetDisabled(!options.CanCombine);

            if (options.CanEquip)
                this.options[0].SetLabel(item.BoundItem.IsEquipped ? "Unequip" : "Equip");
            else if (options.CanSplit)
                this.options[0].SetLabel("Split");
            else
                this.options[0].SetLabel("Use");

            // Start on the first selectable action option, preferring Use/Equip/Split (0)
            // then Combine (2) over Inspect (1) -- Inspect is available on virtually every
            // item, so left in raw index order it would always steal default focus away
            // from the action the player actually opened the menu for (e.g. Combine on an
            // ammo box) the moment CanInspect is true.
            this.selectedIndex = 0;
            foreach (int i in DefaultSelectionPriority)
            {
                if (!this.options[i].IsDisabled) { this.selectedIndex = i; break; }
            }

            PositionNextToItem(item);
            RefreshVisuals(snapSelector: true);
            Show();
            PlayOpenAnimation();
        }

        public void Close()
        {
            StopBump();
            StopOpenAnimation();
            this.isOpen     = false;
            this.targetMode = false;
            Hide();
            OnClose?.Invoke();
        }

        public void NavigateMenu(int dir)
        {
            if (dir == 0) return;

            int count = this.visibleCount;
            int next  = this.selectedIndex;

            // dir comes from Vector2Int.up/down (y = +1 / -1), but options are laid out
            // top-to-bottom at increasing array index -- so "down" (-1) must INCREASE the
            // index. Without the negation this stepped backwards (e.g. Use -> Combine ->
            // Inspect instead of Use -> Inspect -> Combine), which only looked "roughly
            // right" when the skipped-disabled-option logic happened to mask it.
            // The list does not wrap: past either end (or with only disabled options left that way) the
            // cursor stays put and BumpBlocked shows that it tried.
            for (int candidate = this.selectedIndex - dir; candidate >= 0 && candidate < count; candidate -= dir)
            {
                if (this.pool[candidate].IsDisabled) continue;
                next = candidate;
                break;
            }

            if (next == this.selectedIndex)
            {
                BumpBlocked(dir);
                return;
            }

            int previous = this.selectedIndex;
            StopBump();
            this.selectedIndex = next;
            RefreshVisuals();
            PlayPass(previous, next);
        }

        // The newly selected button nudges a little in the direction of travel and settles back, as if the cursor
        // brushed past it. Much smaller than the blocked-move shake, and the bar is left to glide on its own.
        void PlayPass(int from, int to)
        {
            if (this.passTravel <= 0f || from == to || to < 0 || to >= this.visibleCount || from >= this.visibleCount) return;

            Vector3 worldStep = (this.pool[to].BarAnchor.position - this.pool[from].BarAnchor.position) / Mathf.Abs(to - from);
            StartOptionMotion(this.pool[to], worldStep * this.passTravel, this.passDuration, 1, this.passShakeAmount, holdBar: false);
        }

        // The current button tries to move toward the option the player pressed toward (even a disabled one) and
        // gives up: it goes a fraction of the way there, wobbles and comes back, with the selection bar riding on
        // it. At either end of the list, where there is no such neighbour, it goes toward the space where it would be.
        void BumpBlocked(int dir)
        {
            if (this.selectedIndex >= this.visibleCount) return;

            var option  = this.pool[this.selectedIndex];
            var current = option.BarAnchor;
            int ahead   = this.selectedIndex - dir; // options grow downward, dir>0 is "up" (see NavigateMenu)
            int behind  = this.selectedIndex + dir;

            Vector3 worldStep;
            if (ahead >= 0 && ahead < this.visibleCount)
            {
                worldStep = this.pool[ahead].BarAnchor.position - current.position;
            }
            else if (behind >= 0 && behind < this.visibleCount)
            {
                worldStep = current.position - this.pool[behind].BarAnchor.position;
            }
            else
            {
                var corners = new Vector3[4];
                current.GetWorldCorners(corners);
                worldStep = current.up * (dir * (corners[1] - corners[0]).magnitude);
            }

            StartOptionMotion(option, worldStep * this.bumpTravel, this.bumpDuration, this.bumpShakes, this.bumpShakeAmount, holdBar: true);
        }

        // Moves the option's button out along worldStep and back (with a wobble), then rests it exactly where it was.
        // holdBar: the selection bar sticks to the button during the motion instead of gliding after it.
        void StartOptionMotion(MenuOption option, Vector3 worldStep, float duration, int shakes, float shakeAmount, bool holdBar)
        {
            StopBump();

            var rect   = (RectTransform)option.transform;
            var parent = rect.parent;
            Vector3 local3 = parent != null ? parent.InverseTransformVector(worldStep) : worldStep;
            Vector2 step   = new Vector2(local3.x, local3.y);

            this.bumpRect         = rect;
            this.bumpRestPosition = rect.anchoredPosition;
            if (holdBar) this.selector?.HoldRigid(duration);

            this.bumpTween = DOVirtual.Float(0f, 1f, Mathf.Max(0.01f, duration), t =>
                {
                    if (this.bumpRect != null)
                        this.bumpRect.anchoredPosition = this.bumpRestPosition + EvaluateBump(t, step, shakes, shakeAmount);
                })
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .OnComplete(StopBump)
                .OnKill(RestoreBumpRect);
        }

        // Forward to the step and back (first ~40% out, the rest in), with a side-to-side wobble that dies out.
        Vector2 EvaluateBump(float t, Vector2 step, int shakes, float shakeAmount)
        {
            const float outFraction = 0.4f;
            float travel;
            if (t < outFraction)
            {
                float go = 1f - t / outFraction;
                travel = 1f - go * go;     // ease out: fast at first, slows as it reaches the end of the step
            }
            else
            {
                float back = (t - outFraction) / (1f - outFraction);
                travel = 1f - back * back; // ease in: starts slow, speeds up on the way back
            }

            float shake = Mathf.Sin(t * Mathf.PI * 2f * Mathf.Max(1, shakes)) * (1f - t);
            return step * travel + Vector2.right * (step.magnitude * shakeAmount * shake);
        }

        // Always puts the button back exactly where it rested, however the gesture ended.
        void StopBump()
        {
            var tween = this.bumpTween;
            this.bumpTween = null;
            tween?.Kill();
            RestoreBumpRect();
        }

        void RestoreBumpRect()
        {
            if (this.bumpRect == null) return;
            this.bumpRect.anchoredPosition = this.bumpRestPosition;
            this.bumpRect = null;
        }

        public void ConfirmSelection()
        {
            if (!this.isOpen) return;
            if (this.targetMode) { ConfirmTarget(); return; }
            ExecuteOption(this.options[this.selectedIndex].Type);
        }

        // Cancel while choosing the operator goes back to the item's own actions instead of closing everything.
        public bool TryBackFromTargets()
        {
            if (!this.isOpen || !this.targetMode || this.currentItem == null) return false;

            var item = this.currentItem;
            this.targetMode = false;
            ShowActionOptions();
            for (int i = 0; i < this.options.Length; i++) this.options[i].SetDisabled(this.actionDisabled[i]);
            this.options[0].SetLabel("Use");
            this.selectedIndex = 0;
            PositionNextToItem(item);
            RefreshVisuals(snapSelector: true);
            return true;
        }

        void ConfirmTarget()
        {
            var item = this.currentItem;
            var target = this.useTargets![this.selectedIndex];
            Close();
            if (item != null && target.Enabled) OnUseOnRequested?.Invoke(item, target.Slot);
        }

        // "Use" on an item with operator targets: the same buttons now list the operators.
        void EnterTargetMode()
        {
            StopBump();
            var targets = this.useTargets!;
            this.targetMode = true;

            this.actionDisabled = new bool[this.options.Length];
            for (int i = 0; i < this.options.Length; i++) this.actionDisabled[i] = this.options[i].IsDisabled;

            while (this.pool.Count < targets.Count)
            {
                var clone = Instantiate(this.options[0], this.options[0].transform.parent);
                this.pool.Add(clone);
            }

            for (int i = 0; i < this.pool.Count; i++)
            {
                bool used = i < targets.Count;
                this.pool[i].gameObject.SetActive(used);
                if (!used) continue;
                this.pool[i].SetLabel(targets[i].Label);
                this.pool[i].SetDisabled(!targets[i].Enabled);
                this.pool[i].SetDimmed(!targets[i].Enabled);
            }

            // The menu has a fixed size sized for its three actions: give every operator the same row height
            // and widen it for the longest name, otherwise the rows get squeezed to nothing.
            float rowHeight = this.actionSize.y / this.options.Length;
            float width     = this.actionSize.x;
            for (int i = 0; i < targets.Count; i++)
                width = Mathf.Max(width, this.pool[i].GetPreferredLabelWidth(targets[i].Label) + 8f);
            this.rectTransform.sizeDelta = new Vector2(width, rowHeight * targets.Count);

            this.visibleCount  = targets.Count;
            this.selectedIndex = 0;
            for (int i = 0; i < targets.Count; i++)
                if (targets[i].Enabled) { this.selectedIndex = i; break; }

            if (this.currentItem != null) PositionNextToItem(this.currentItem);
            RefreshVisuals(snapSelector: true);
        }

        // Back to the item's own actions (Use / Inspect / Combine): the original buttons, with their
        // authored labels; any buttons cloned for the operator list are hidden.
        void ShowActionOptions()
        {
            StopBump();
            for (int i = 0; i < this.pool.Count; i++)
            {
                bool original = i < this.options.Length;
                this.pool[i].gameObject.SetActive(original);
                this.pool[i].SetDimmed(false);
                if (original) this.pool[i].SetLabel(this.defaultLabels[i]);
            }
            this.rectTransform.sizeDelta = this.actionSize;
            this.visibleCount = this.options.Length;
        }

        void ExecuteOption(MenuOption.OptionType type)
        {
            var item      = this.currentItem;
            bool toTargets = type == MenuOption.OptionType.Use && item != null && this.useTargets != null;
            if (!toTargets) Close();

            switch (type)
            {
                case MenuOption.OptionType.Use:
                    if (item == null) break;
                    if (this.useTargets != null) { EnterTargetMode(); return; }
                    if (this.currentCanSplit) OnSplitRequested?.Invoke(item);
                    else                       OnUseRequested?.Invoke(item);
                    break;
                case MenuOption.OptionType.Inspect:
                    if (this.inspectPanel == null)
                    {
                        Debug.LogWarning("[Menu] InspectPanel not assigned.");
                        break;
                    }
                    if (item != null) this.inspectPanel.Open(item);
                    break;
                case MenuOption.OptionType.Combine:
                    if (item != null) OnCombineRequested?.Invoke(item);
                    break;
            }
        }

        void PositionNextToItem(InventoryItemView item) =>
            PositionNextToRect(item.GetComponent<RectTransform>());

        void PositionNextToRect(RectTransform itemRT)
        {
            StopOpenAnimation();

            Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            Camera cam = (rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                ? rootCanvas.worldCamera : null;

            Vector3[] wc = new Vector3[4];
            itemRT.GetWorldCorners(wc);

            // GetWorldCorners orders corners by local orientation (BL/TL/TR/BR).
            // When the item is rotated the local TL/TR are no longer the visual top edge.
            // Convert to screen space and pick the two corners with the highest Y —
            // those are always the visual top edge regardless of rotation.
            Vector2[] sc = new Vector2[4];
            for (int i = 0; i < 4; i++)
                sc[i] = RectTransformUtility.WorldToScreenPoint(cam, wc[i]);

            int t0 = 0;
            for (int i = 1; i < 4; i++)
                if (sc[i].y > sc[t0].y) t0 = i;

            int t1 = t0 == 0 ? 1 : 0;
            for (int i = 0; i < 4; i++)
                if (i != t0 && sc[i].y > sc[t1].y) t1 = i;

            int idxL = sc[t0].x < sc[t1].x ? t0 : t1;
            int idxR = sc[t0].x < sc[t1].x ? t1 : t0;

            // Default: menu to the right (pivot TL anchored to visual top-right)
            this.rectTransform.pivot    = new Vector2(0f, 1f);
            this.rectTransform.position = wc[idxR];

            Vector3[] menuCorners = new Vector3[4];
            this.rectTransform.GetWorldCorners(menuCorners);

            if (RectTransformUtility.WorldToScreenPoint(cam, menuCorners[2]).x > Screen.width)
            {
                this.rectTransform.pivot    = new Vector2(1f, 1f);
                this.rectTransform.position = wc[idxL];
            }
        }

        void RefreshVisuals(bool snapSelector = false)
        {
            bool bar = this.selector != null;
            for (int i = 0; i < this.visibleCount; i++)
                this.pool[i].SetState(i == this.selectedIndex && !this.pool[i].IsDisabled, decorate: !bar);

            if (!bar) return;
            var current = this.selectedIndex < this.visibleCount ? this.pool[this.selectedIndex] : null;
            this.selector!.FollowOrHide(current != null && !current.IsDisabled ? current.BarAnchor : null, snapSelector);
        }

        // The menu "switches on": it starts with X scaled to 0 and opens to full width. The rect's pivot is
        // already on the side glued to the item (PositionNextToRect flips it to the right edge when the menu
        // opens leftwards), so scaling about the pivot always grows it away from the item.
        void PlayOpenAnimation()
        {
            StopOpenAnimation();
            if (this.openDuration <= 0f) return;

            this.opening = true;
            var scale = this.rectTransform.localScale;
            scale.x = 0f;
            this.rectTransform.localScale = scale;

            this.openTween = this.rectTransform.DOScaleX(1f, this.openDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() => this.opening = false);
        }

        void StopOpenAnimation()
        {
            this.openTween?.Kill();
            this.openTween = null;
            this.opening   = false;

            // Measuring/positioning (and the next open) always start from the real size.
            var scale = this.rectTransform.localScale;
            scale.x = 1f;
            this.rectTransform.localScale = scale;
        }

        // The bar reads the options' world rects, which are still growing during the open animation.
        void LateUpdate()
        {
            if (this.opening && this.isOpen)
                RefreshVisuals(snapSelector: true);
        }

        void Show()
        {
            this.canvasGroup.alpha          = 1f;
            this.canvasGroup.interactable   = true;
            this.canvasGroup.blocksRaycasts = true;
        }

        void Hide()
        {
            if (this.selector != null) this.selector.SetVisible(false);
            this.canvasGroup.alpha          = 0f;
            this.canvasGroup.interactable   = false;
            this.canvasGroup.blocksRaycasts = false;
        }
    }
}
