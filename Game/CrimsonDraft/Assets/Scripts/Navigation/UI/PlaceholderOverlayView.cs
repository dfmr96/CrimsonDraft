#nullable enable

using TMPro;
using UnityEngine;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class PlaceholderOverlayView : MonoBehaviour
    {
        [Header("Overlay Panels")]
        [SerializeField] private GameObject mapPanel       = null!;
        [SerializeField] private GameObject pausePanel     = null!;
        [SerializeField] private GameObject inventoryPanel = null!;

        private string? feedbackText;

        public void ShowMap()
        {
            HideAll();
            this.mapPanel.SetActive(true);
        }

        public void ShowPause()
        {
            HideAll();
            this.pausePanel.SetActive(true);
        }

        public void ShowInventory()
        {
            HideAll();
            this.inventoryPanel.SetActive(true);
        }

        public void ShowActionFeedback(string text) => this.feedbackText = text;
        public void HideActionFeedback()            => this.feedbackText = null;

        public void HideAll()
        {
            this.mapPanel.SetActive(false);
            this.pausePanel.SetActive(false);
            this.inventoryPanel.SetActive(false);
        }

        private void OnGUI()
        {
            if (this.feedbackText == null) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };

            GUI.Label(new Rect(0, Screen.height - 80, Screen.width, 60), this.feedbackText, style);
        }
    }
}
