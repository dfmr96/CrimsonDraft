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

        [SerializeField] private CommandEntry[] entries        = Array.Empty<CommandEntry>();
        [SerializeField] private Vector2        offset         = Vector2.zero;
        [SerializeField] private Image          dimmingOverlay = null!;

        private Action[] submitHandlers   = Array.Empty<Action>();
        private Action[] selectedHandlers = Array.Empty<Action>();

        #endregion

        #region ICommandPanelView

        public RectTransform PanelRect => (RectTransform)this.transform;

        public void Show(RectTransform operatorRect)
        {
            var panel   = (RectTransform)this.transform;
            var hudRoot = (RectTransform)this.transform.parent;

            var corners = new Vector3[4];
            operatorRect.GetWorldCorners(corners);
            var topCenter = (corners[1] + corners[2]) * 0.5f;
            var localPos  = hudRoot.InverseTransformPoint(topCenter);

            panel.localPosition = new Vector3(localPos.x + this.offset.x, localPos.y + this.offset.y, 0f);

            this.gameObject.SetActive(true);
            SelectFirstNextFrame().Forget();
        }

        public void Focus() => SelectFirstNextFrame().Forget();

        public void SetDimmed(bool dimmed)
        {
            if (this.dimmingOverlay != null)
                this.dimmingOverlay.DOFade(dimmed ? 0.6f : 0f, 0.1f);
        }

        public void Hide() => this.gameObject.SetActive(false);

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            this.submitHandlers   = new Action[this.entries.Length];
            this.selectedHandlers = new Action[this.entries.Length];
        }

        private void OnEnable()
        {
            for (int i = 0; i < this.entries.Length; i++)
            {
                var capturedCommand      = this.entries[i].command;
                var capturedAnchor       = this.entries[i].item.SelectorAnchor;
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

        private async UniTaskVoid SelectFirstNextFrame()
        {
            await UniTask.NextFrame();
            if (this.entries.Length > 0)
                EventSystem.current.SetSelectedGameObject(this.entries[0].item.gameObject);
        }

        #endregion
    }
}
