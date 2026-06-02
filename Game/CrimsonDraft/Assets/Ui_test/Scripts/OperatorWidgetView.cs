#nullable enable

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrimsonDraft.Operators;

namespace CrimsonDraft.UI
{
    public sealed class OperatorWidgetView : MonoBehaviour
    {
        [SerializeField] private Image       portrait     = null!;
        [SerializeField] private TMP_Text    nameLabel    = null!;
        [SerializeField] private TMP_Text    hpLabel      = null!;
        [SerializeField] private GameObject  deadOverlay  = null!;

        public void Bind(OperatorRuntime op)
        {
            if (!op.IsPresent)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (this.portrait  != null) this.portrait.sprite = op.Data?.Portrait;
            if (this.nameLabel != null) this.nameLabel.text  = op.Data?.DisplayName ?? string.Empty;
            if (this.hpLabel   != null) this.hpLabel.text    = $"{op.Hp} / {op.MaxHp}";
            if (this.deadOverlay != null) this.deadOverlay.SetActive(!op.IsAlive);
        }
    }
}
