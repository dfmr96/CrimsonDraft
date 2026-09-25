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
        // Optional: null skips it. Left unassigned on the New Game station right now (only the
        // Pause menu's description label is wired) -- was crashing MainMenuController's whole
        // VContainer scope (SetSelectedScheme is called during Construct), which is why no UI
        // input worked on the main menu at all.
        [SerializeField] private TextMeshProUGUI? descriptionText;
        // Same white tint for both -- only the alpha differs, so the unselected option dims
        // instead of shifting hue.
        [SerializeField] private Color            selectedColor   = Color.white;
        [SerializeField] private Color            unselectedColor = new(1f, 1f, 1f, 0.45f);

        [SerializeField, TextArea]
        private string modernDescription = "Modern: directional movement with natural, camera-relative turning.";

        [SerializeField, TextArea]
        private string classicDescription = "Classic: tank controls - the character rotates in place to turn.";

        // Controls hint (Controls-Windows/Control_Scheme vs Keys) -- shows the mapping for
        // whichever device last performed an input, kept in sync live by MainMenuController.
        [Header("Controls Layout")]
        [SerializeField] private GameObject gamepadLayout  = null!;
        [SerializeField] private GameObject keyboardLayout = null!;

        public Button ModernButton  => this.modernButton;
        public Button ClassicButton => this.classicButton;

        public void SetSelectedScheme(bool isClassic)
        {
            this.modernImage.color  = isClassic ? this.unselectedColor : this.selectedColor;
            this.classicImage.color = isClassic ? this.selectedColor   : this.unselectedColor;
            if (this.descriptionText != null)
                this.descriptionText.text = isClassic ? this.classicDescription : this.modernDescription;
        }

        public void ShowControlsLayout(bool gamepad)
        {
            this.gamepadLayout.SetActive(gamepad);
            this.keyboardLayout.SetActive(!gamepad);
        }
    }
}
