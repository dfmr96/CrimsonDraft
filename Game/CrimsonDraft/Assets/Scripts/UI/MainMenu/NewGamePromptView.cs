#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI.MainMenu
{
    // The Modern/Classic picker on the New Game station (NewGame_canva/Control). Pure view:
    // MainMenuController owns the decision logic, this just exposes the two buttons and
    // reflects which scheme is currently selected via a simple tint plus a description label.
    public sealed class NewGamePromptView : MonoBehaviour
    {
        [SerializeField] private Button           modernButton   = null!;
        [SerializeField] private Button           classicButton  = null!;
        [SerializeField] private Image            modernImage    = null!;
        [SerializeField] private Image            classicImage   = null!;
        [SerializeField] private TextMeshProUGUI  descriptionText = null!;
        [SerializeField] private Color            selectedColor   = Color.white;
        [SerializeField] private Color            unselectedColor = new(0.6f, 0.6f, 0.6f, 1f);

        [SerializeField, TextArea]
        private string modernDescription = "Modern: directional movement with natural, camera-relative turning.";

        [SerializeField, TextArea]
        private string classicDescription = "Classic: tank controls - the character rotates in place to turn.";

        public Button ModernButton  => this.modernButton;
        public Button ClassicButton => this.classicButton;

        public void SetSelectedScheme(bool isClassic)
        {
            this.modernImage.color  = isClassic ? this.unselectedColor : this.selectedColor;
            this.classicImage.color = isClassic ? this.selectedColor   : this.unselectedColor;
            this.descriptionText.text = isClassic ? this.classicDescription : this.modernDescription;
        }
    }
}
