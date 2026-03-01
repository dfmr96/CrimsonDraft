#nullable enable

using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Combat
{
    public sealed class AimViewController : MonoBehaviour, IAimView
    {
        #region Events

        public event Action<Vector2>? OnShotFired;

        #endregion

        #region Fields

        private enum AimPhase { VerticalAiming, HorizontalAiming, WaitingDismiss }

        [SerializeField] private RectTransform verticalSpace      = null!;
        [SerializeField] private Image         verticalSelector   = null!;
        [SerializeField] private RectTransform horizontalSpace    = null!;
        [SerializeField] private Image         horizontalSelector = null!;
        [SerializeField] private RectTransform aimSpace           = null!;
        [SerializeField] private GameObject    shotMarkerPrefab   = null!;
        [SerializeField] private float         speed              = 0.8f;
        [SerializeField] private float         dimmingAlpha       = 0.3f;

        private AimPhase phase;
        private float    confirmedY;
        private Vector2  pendingShot;

        #endregion

        #region IAimView

        public void Show()
        {
            this.gameObject.SetActive(true);
            this.StartVerticalOscillation();
            this.phase = AimPhase.VerticalAiming;
        }

        public void Confirm()
        {
            if (this.phase == AimPhase.VerticalAiming)
            {
                float halfH      = this.verticalSpace.rect.height / 2f;
                this.confirmedY  = (this.verticalSelector.rectTransform.localPosition.y + halfH) / (halfH * 2f);
                this.verticalSelector.rectTransform.DOKill();
                this.verticalSelector.DOFade(this.dimmingAlpha, 0.15f);
                this.StartHorizontalOscillation();
                this.phase = AimPhase.HorizontalAiming;
            }
            else if (this.phase == AimPhase.HorizontalAiming)
            {
                float halfW      = this.horizontalSpace.rect.width / 2f;
                float confirmedX = (this.horizontalSelector.rectTransform.localPosition.x + halfW) / (halfW * 2f);
                this.horizontalSelector.rectTransform.DOKill();
                this.horizontalSelector.DOFade(this.dimmingAlpha, 0.15f);
                this.pendingShot = new Vector2(confirmedX, this.confirmedY);
                this.SpawnMarker(confirmedX, this.confirmedY);
                this.phase = AimPhase.WaitingDismiss;
            }
            else if (this.phase == AimPhase.WaitingDismiss)
            {
                this.OnShotFired?.Invoke(this.pendingShot);
            }
        }

        public void Hide()
        {
            this.verticalSelector.DOKill();
            this.verticalSelector.rectTransform.DOKill();
            this.horizontalSelector.DOKill();
            this.horizontalSelector.rectTransform.DOKill();

            foreach (Transform child in this.aimSpace)
                Destroy(child.gameObject);

            this.gameObject.SetActive(false);
        }

        #endregion

        #region Private

        private void StartVerticalOscillation()
        {
            float halfH = this.verticalSpace.rect.height / 2f;
            this.verticalSelector.DOKill();
            this.verticalSelector.rectTransform.DOKill();
            this.verticalSelector.DOFade(1f, 0f);
            this.verticalSelector.rectTransform.localPosition = new Vector3(0f, -halfH, 0f);
            this.verticalSelector.rectTransform
                .DOLocalMoveY(halfH, this.speed)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void StartHorizontalOscillation()
        {
            float halfW = this.horizontalSpace.rect.width / 2f;
            this.horizontalSelector.DOKill();
            this.horizontalSelector.rectTransform.DOKill();
            this.horizontalSelector.DOFade(1f, 0f);
            this.horizontalSelector.rectTransform.localPosition = new Vector3(-halfW, 0f, 0f);
            this.horizontalSelector.rectTransform
                .DOLocalMoveX(halfW, this.speed)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void SpawnMarker(float normalizedX, float normalizedY)
        {
            var   r      = this.aimSpace.rect;
            float x      = Mathf.Lerp(r.xMin, r.xMax, normalizedX);
            float y      = Mathf.Lerp(r.yMin, r.yMax, normalizedY);
            var   marker = Instantiate(this.shotMarkerPrefab, this.aimSpace);
            ((RectTransform)marker.transform).localPosition = new Vector3(x, y, 0f);
        }

        #endregion
    }
}
