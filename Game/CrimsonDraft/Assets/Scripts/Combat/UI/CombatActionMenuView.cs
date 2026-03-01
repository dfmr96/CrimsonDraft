#nullable enable

using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrimsonDraft.Combat
{
    public sealed class CombatActionMenuView : MonoBehaviour, ICombatActionMenuView
    {
        #region Events

        public event Action<int>? OnOperatorSelected;

        #endregion

        #region Fields

        [SerializeField] private ActionMenuItem[] operators    = Array.Empty<ActionMenuItem>();
        [SerializeField] private RectTransform    selectorMark = null!;
        [SerializeField] private float bobAmplitude = 4f;
        [SerializeField] private float bobDuration  = 0.4f;

        private Action[] submitHandlers   = Array.Empty<Action>();
        private Action[] selectedHandlers = Array.Empty<Action>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            var le = this.selectorMark.GetComponent<LayoutElement>();
            if (le == null) le = this.selectorMark.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        private void OnEnable()
        {
            this.submitHandlers   = new Action[this.operators.Length];
            this.selectedHandlers = new Action[this.operators.Length];

            for (int i = 0; i < this.operators.Length; i++)
            {
                int index = i;
                this.submitHandlers[i]   = () => this.OnOperatorSelected?.Invoke(index);
                this.selectedHandlers[i] = () => this.MoveSelector(index);

                this.operators[i].OnSubmit   += this.submitHandlers[i];
                this.operators[i].OnSelected += this.selectedHandlers[i];
            }

            if (this.operators.Length > 0)
                SelectFirstNextFrame().Forget();
        }

        private async UniTaskVoid SelectFirstNextFrame()
        {
            await UniTask.NextFrame();
            EventSystem.current.SetSelectedGameObject(this.operators[0].gameObject);
        }

        private void OnDisable()
        {
            this.selectorMark.DOKill();

            for (int i = 0; i < this.operators.Length; i++)
            {
                this.operators[i].OnSubmit   -= this.submitHandlers[i];
                this.operators[i].OnSelected -= this.selectedHandlers[i];
            }

            this.submitHandlers   = Array.Empty<Action>();
            this.selectedHandlers = Array.Empty<Action>();
        }

        #endregion

        #region Private

        private void MoveSelector(int index)
        {
            this.selectorMark.DOKill();

            var anchorWorldPos = this.operators[index].SelectorAnchor.position;
            var parentRect     = (RectTransform)this.selectorMark.parent;
            Vector3 localPos   = parentRect.InverseTransformPoint(anchorWorldPos);

            float centerX = localPos.x;
            float centerY = localPos.y;

            this.selectorMark.localPosition = new Vector3(centerX, centerY - this.bobAmplitude, 0f);
            this.selectorMark.DOLocalMoveY(centerY + this.bobAmplitude, this.bobDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        #endregion
    }
}
