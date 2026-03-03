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

        [SerializeField] private RectTransform verticalSpace          = null!;
        [SerializeField] private Image         verticalSelector       = null!;
        [SerializeField] private RectTransform horizontalSpace        = null!;
        [SerializeField] private Image         horizontalSelector     = null!;
        [SerializeField] private RectTransform aimSpace               = null!;
        [SerializeField] private GameObject    shotMarkerPrefab       = null!;
        [SerializeField] private GameObject    dispersionCirclePrefab = null!;
        [SerializeField] private float         speed                  = 0.8f;
        [SerializeField] private float         dimmingAlpha           = 0.3f;
        [SerializeField] private int           dispersionRadius       = 10;

        private AimPhase phase;
        private Vector2  confirmedLocalPos;
        private Vector2  pendingNormalizedShot;

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

                this.horizontalSelector.DOFade(this.dimmingAlpha, 0.15f);

                var worldIntersection = new Vector3(
                    this.horizontalSelector.rectTransform.position.x,
                    this.verticalSelector.rectTransform.position.y,
                    this.aimSpace.position.z);
                var raw = this.aimSpace.InverseTransformPoint(worldIntersection);
                this.confirmedLocalPos = new Vector2(Mathf.Round(raw.x), Mathf.Round(raw.y));

                this.SpawnDispersionCircle(this.confirmedLocalPos);
                var shotLocal = this.ComputeRandomShotLocal();
                this.SpawnMarker(shotLocal);
                this.pendingNormalizedShot = this.NormalizeShotLocal(shotLocal);

                this.phase = AimPhase.WaitingDismiss;
            }
            else if (this.phase == AimPhase.WaitingDismiss)
            {
                this.OnShotFired?.Invoke(this.pendingNormalizedShot);
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
            float halfH = Mathf.Floor(this.verticalSpace.rect.height / 2f);
            this.verticalSelector.DOKill();
            this.verticalSelector.rectTransform.DOKill();
            this.verticalSelector.DOFade(1f, 0f);
            this.verticalSelector.rectTransform.localPosition = new Vector3(0f, -halfH, 0f);
            this.verticalSelector.rectTransform
                .DOLocalMoveY(halfH, this.speed, snapping: true)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void StartHorizontalOscillation()
        {
            float halfW = Mathf.Floor(this.horizontalSpace.rect.width / 2f);
            this.horizontalSelector.DOKill();
            this.horizontalSelector.rectTransform.DOKill();
            this.horizontalSelector.DOFade(1f, 0f);
            this.horizontalSelector.rectTransform.localPosition = new Vector3(-halfW, 0f, 0f);
            this.horizontalSelector.rectTransform
                .DOLocalMoveX(halfW, this.speed, snapping: true)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void SpawnMarker(Vector2 localPos)
        {
            var marker = Instantiate(this.shotMarkerPrefab, this.aimSpace);
            ((RectTransform)marker.transform).localPosition = new Vector3(
                Mathf.Round(localPos.x),
                Mathf.Round(localPos.y),
                0f);
        }

        private void SpawnDispersionCircle(Vector2 localPos)
        {
            var circle = Instantiate(this.dispersionCirclePrefab, this.aimSpace);
            var rt     = (RectTransform)circle.transform;
            rt.localPosition = new Vector3(Mathf.Round(localPos.x), Mathf.Round(localPos.y), 0f);
            circle.GetComponent<Image>().SetNativeSize();
        }

        private Vector2 ComputeRandomShotLocal()
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float r     = this.dispersionRadius * Mathf.Sqrt(UnityEngine.Random.value);
            return new Vector2(
                Mathf.Round(this.confirmedLocalPos.x + r * Mathf.Cos(angle)),
                Mathf.Round(this.confirmedLocalPos.y + r * Mathf.Sin(angle)));
        }

        private Vector2 NormalizeShotLocal(Vector2 localPos)
        {
            float halfW = this.aimSpace.rect.width  / 2f;
            float halfH = this.aimSpace.rect.height / 2f;
            return new Vector2(
                Mathf.Clamp01((localPos.x + halfW) / (halfW * 2f)),
                Mathf.Clamp01((localPos.y + halfH) / (halfH * 2f)));
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (this.aimSpace == null) return;

            float radius = this.dispersionRadius * this.aimSpace.lossyScale.x;
            var   center = this.aimSpace.TransformPoint(this.confirmedLocalPos);
            UnityEditor.Handles.color = new Color(0f, 0.9f, 1f, 0.8f);
            UnityEditor.Handles.DrawWireDisc(center, Vector3.forward, radius);
        }
#endif

        #endregion
    }
}
