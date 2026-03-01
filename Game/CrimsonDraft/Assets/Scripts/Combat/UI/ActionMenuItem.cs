#nullable enable

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrimsonDraft.Combat
{
    public sealed class ActionMenuItem : Selectable, ISubmitHandler
    {
        #region Events

        public event Action? OnSubmit;
        public event Action? OnSelected;

        #endregion

        #region Fields

        [SerializeField] private RectTransform selectorAnchor = null!;
        public RectTransform SelectorAnchor => this.selectorAnchor;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            this.transition = Transition.None;
        }

        #endregion

        #region Selectable Overrides

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            this.OnSelected?.Invoke();
        }

        public override void OnPointerDown(PointerEventData eventData) { }
        public override void OnPointerUp(PointerEventData eventData)   { }

        #endregion

        #region ISubmitHandler

        void ISubmitHandler.OnSubmit(BaseEventData eventData) => this.OnSubmit?.Invoke();

        #endregion
    }
}
