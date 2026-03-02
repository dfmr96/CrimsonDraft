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
        private float    confirmedWorldY;
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
                this.verticalSelector.rectTransform.DOKill();
                var vLocal = this.verticalSelector.rectTransform.localPosition;
                vLocal.y   = Mathf.Round(vLocal.y);
                this.verticalSelector.rectTransform.localPosition = vLocal;

                float halfH         = this.verticalSpace.rect.height / 2f;
                this.confirmedY     = (vLocal.y + halfH) / (halfH * 2f);
                this.confirmedWorldY = this.verticalSelector.rectTransform.position.y;

                this.verticalSelector.DOFade(this.dimmingAlpha, 0.15f);
                this.StartHorizontalOscillation();
                this.phase = AimPhase.HorizontalAiming;
            }
            else if (this.phase == AimPhase.HorizontalAiming)
            {
                this.horizontalSelector.rectTransform.DOKill();
                var hLocal = this.horizontalSelector.rectTransform.localPosition;
                hLocal.x   = Mathf.Round(hLocal.x);
                this.horizontalSelector.rectTransform.localPosition = hLocal;

                float halfW      = this.horizontalSpace.rect.width / 2f;
                float confirmedX = (hLocal.x + halfW) / (halfW * 2f);
                float confirmedWorldX = this.horizontalSelector.rectTransform.position.x;

                this.horizontalSelector.DOFade(this.dimmingAlpha, 0.15f);
                this.pendingShot = new Vector2(confirmedX, this.confirmedY);
                this.SpawnMarker(confirmedWorldX, this.confirmedWorldY);
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

        private void SpawnMarker(float worldX, float worldY)
        {
            var worldPos = new Vector3(worldX, worldY, this.aimSpace.position.z);
            var localPos = this.aimSpace.InverseTransformPoint(worldPos);
            var marker   = Instantiate(this.shotMarkerPrefab, this.aimSpace);
            ((RectTransform)marker.transform).localPosition = new Vector3(
                Mathf.Round(localPos.x),
                Mathf.Round(localPos.y),
                0f);
        }

        #endregion
    }
}
