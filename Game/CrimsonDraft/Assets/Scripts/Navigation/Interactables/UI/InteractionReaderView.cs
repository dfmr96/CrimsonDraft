#nullable enable

using TMPro;
using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public sealed class InteractionReaderView : MonoBehaviour
    {
        [SerializeField] private GameObject      panel      = null!;
        [SerializeField] private TextMeshProUGUI titleLabel = null!;
        [SerializeField] private TextMeshProUGUI bodyLabel  = null!;
        [SerializeField] private GameObject      prevHint   = null!;
        [SerializeField] private GameObject      nextHint   = null!;

        public void Show(string title, string pageText, bool hasPrev, bool hasNext)
        {
            this.titleLabel.text = title;
            this.bodyLabel.text  = pageText;
            this.prevHint.SetActive(hasPrev);
            this.nextHint.SetActive(hasNext);
            this.panel.SetActive(true);
        }

        public void Hide() => this.panel.SetActive(false);
    }
}
