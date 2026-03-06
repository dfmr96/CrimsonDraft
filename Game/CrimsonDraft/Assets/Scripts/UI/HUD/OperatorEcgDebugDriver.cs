#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class OperatorEcgDebugDriver : MonoBehaviour
    {
        [SerializeField] private OperatorEcgWidget? widget;
        [SerializeField, Range(0f, 1f)] private float hpRatio = 1f;
        [SerializeField] private int bpm = 72;
        [SerializeField] private bool isActive = true;
        [SerializeField] private int lineThickness = 2;
        [SerializeField] private bool applyEachFrame = true;

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (this.applyEachFrame)
            {
                Apply();
            }
        }

        [ContextMenu("Apply Debug Vitals")]
        public void Apply()
        {
            if (this.widget == null)
            {
                this.widget = GetComponent<OperatorEcgWidget>();
            }

            if (this.widget == null)
            {
                return;
            }

            this.widget.SetLineThickness(this.lineThickness);
            this.widget.SetVitals(this.hpRatio, this.bpm, this.isActive);
        }
    }
}
