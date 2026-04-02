#nullable enable

using TMPro;
using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public sealed class PoiDialogView : MonoBehaviour
    {
        [SerializeField] private GameObject      panel = null!;
        [SerializeField] private TextMeshProUGUI label = null!;

        public void Show(string line)
        {
            this.label.text = line;
            this.panel.SetActive(true);
        }

        public void Hide() => this.panel.SetActive(false);
    }
}
