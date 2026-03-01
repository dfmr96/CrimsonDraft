#nullable enable

using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CrimsonDraft.Combat
{
    public sealed class SubPanelView : MonoBehaviour, ISubPanelView
    {
        #region Events

        public event Action<int>?           OnItemSelected;
        public event Action<RectTransform>? OnEntryFocused;

        #endregion

        #region Fields

        [Serializable]
        private sealed class SubPanelSlot
        {
            public ActionMenuItem  item  = null!;
            public TextMeshProUGUI label = null!;
        }

        [SerializeField] private SubPanelSlot[] slots  = Array.Empty<SubPanelSlot>();
        [SerializeField] private Vector2        offset = Vector2.zero;

        private Action[] submitHandlers   = Array.Empty<Action>();
        private Action[] selectedHandlers = Array.Empty<Action>();

        #endregion

        #region ISubPanelView

        public void Show(SubPanelItem[] items, RectTransform commandPanelRect)
        {
            var panel = (RectTransform)this.transform;
            panel.localPosition = commandPanelRect.localPosition + new Vector3(this.offset.x, this.offset.y, 0f);
            panel.sizeDelta     = commandPanelRect.sizeDelta;

            for (int i = 0; i < this.slots.Length; i++)
            {
                bool active = i < items.Length;
                this.slots[i].item.gameObject.SetActive(active);
                if (active)
                    this.slots[i].label.text = items[i].Label;
            }

            this.gameObject.SetActive(true);
            SelectFirstNextFrame(items.Length).Forget();
        }

        public void Hide() => this.gameObject.SetActive(false);

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            this.submitHandlers   = new Action[this.slots.Length];
            this.selectedHandlers = new Action[this.slots.Length];
        }

        private void OnEnable()
        {
            for (int i = 0; i < this.slots.Length; i++)
            {
                int            captured       = i;
                RectTransform capturedAnchor = this.slots[i].item.SelectorAnchor;
                this.submitHandlers[i]   = () => this.OnItemSelected?.Invoke(captured);
                this.selectedHandlers[i] = () => this.OnEntryFocused?.Invoke(capturedAnchor);

                this.slots[i].item.OnSubmit   += this.submitHandlers[i];
                this.slots[i].item.OnSelected += this.selectedHandlers[i];
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < this.slots.Length; i++)
            {
                this.slots[i].item.OnSubmit   -= this.submitHandlers[i];
                this.slots[i].item.OnSelected -= this.selectedHandlers[i];
            }
        }

        #endregion

        #region Private

        private async UniTaskVoid SelectFirstNextFrame(int count)
        {
            await UniTask.NextFrame();
            if (count > 0)
                EventSystem.current.SetSelectedGameObject(this.slots[0].item.gameObject);
        }

        #endregion
    }
}
