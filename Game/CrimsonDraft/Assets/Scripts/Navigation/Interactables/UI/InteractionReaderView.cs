#nullable enable

using TMPro;
using UnityEngine;
using Yarn.Unity;

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public sealed class InteractionReaderView : MonoBehaviour
    {
        [SerializeField] private GameObject      panel      = null!;
        [SerializeField] private TextMeshProUGUI titleLabel = null!;
        [SerializeField] private TextMeshProUGUI bodyLabel  = null!;
        [SerializeField] private GameObject      prevHint   = null!;
        [SerializeField] private GameObject      nextHint   = null!;
        [SerializeField] private MarkupPalette?  markupPalette;

        public void Show(string title, string pageText, bool hasPrev, bool hasNext)
        {
            this.titleLabel.text = title;
            this.bodyLabel.text  = DocumentMarkupFormatter.Format(pageText, this.markupPalette);
            this.prevHint.SetActive(hasPrev);
            this.nextHint.SetActive(hasNext);
            this.panel.SetActive(true);
        }

        public void Hide() => this.panel.SetActive(false);
    }
}
