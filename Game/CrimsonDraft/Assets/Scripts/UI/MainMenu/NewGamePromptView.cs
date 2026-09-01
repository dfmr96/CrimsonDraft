#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI.MainMenu
{
    // The Modern/Classic picker on the New Game station (NewGame_canva/Control). Pure view:
    // MainMenuController owns the decision logic, this just exposes the two buttons and
    // reflects which scheme is currently selected via a simple tint.
    public sealed class NewGamePromptView : MonoBehaviour
    {
        [SerializeField] private Button modernButton   = null!;
        [SerializeField] private Button classicButton  = null!;
        [SerializeField] private Image  modernImage    = null!;
        [SerializeField] private Image  classicImage   = null!;
        [SerializeField] private Color  selectedColor   = Color.white;
        [SerializeField] private Color  unselectedColor = new(0.6f, 0.6f, 0.6f, 1f);

        public Button ModernButton  => this.modernButton;
        public Button ClassicButton => this.classicButton;

        public void SetSelectedScheme(bool isClassic)
        {
            this.modernImage.color  = isClassic ? this.unselectedColor : this.selectedColor;
            this.classicImage.color = isClassic ? this.selectedColor   : this.unselectedColor;
        }
    }
}
