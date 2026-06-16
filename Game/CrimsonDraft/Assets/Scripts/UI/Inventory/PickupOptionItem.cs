#nullable enable

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yarn.Unity;

namespace CrimsonDraft.UI
{
    public class PickupOptionItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [SerializeField] private TMP_Text label           = null!;
        [SerializeField] private Image?   selectionBorder;
        [SerializeField] private Color    labelColor      = Color.white;

        public DialogueOption? Option { get; private set; }

        public Action<PickupOptionItem>? Hovered;
        public Action<PickupOptionItem>? Clicked;

        void Awake() => label.color = labelColor;

        public void Setup(DialogueOption option)
        {
            Option     = option;
            label.text = option.Line.Text.Text;
            SetHighlight(false);
        }

        public void SetHighlight(bool on)
        {
            if (selectionBorder != null)
                selectionBorder.enabled = on;
        }

        public void ClearListeners()
        {
            Hovered = null;
            Clicked = null;
        }

        public void OnPointerEnter(PointerEventData _) => Hovered?.Invoke(this);
        public void OnPointerClick(PointerEventData _) => Clicked?.Invoke(this);
    }
}
