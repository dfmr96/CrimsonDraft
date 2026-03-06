#nullable enable

using CrimsonDraft.Combat;
using UnityEngine;

namespace CrimsonDraft.UI.HUD
{
    public sealed class OperatorEcgPanel : MonoBehaviour, IOperatorEcgFeedback
    {
        [SerializeField] private OperatorEcgWidget[] widgets = System.Array.Empty<OperatorEcgWidget>();

        public void FlashOperatorDamage(int operatorSlotIndex)
        {
            if (operatorSlotIndex < 0 || operatorSlotIndex >= this.widgets.Length)
                return;

            OperatorEcgWidget? widget = this.widgets[operatorSlotIndex];
            widget?.FlashDamage();
        }

        public void SetOperatorHealthState(int operatorSlotIndex, float hpRatio, bool isAlive)
        {
            if (operatorSlotIndex < 0 || operatorSlotIndex >= this.widgets.Length)
                return;

            OperatorEcgWidget? widget = this.widgets[operatorSlotIndex];
            widget?.SetHealthState(hpRatio, isAlive);
        }
    }
}
