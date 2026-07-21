#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class MapScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject root = null!;
        [SerializeField] private RawImage mapImage = null!;
        [SerializeField] private TextMeshProUGUI deckName = null!;

        public bool IsVisible => this.root.activeSelf;

        public void Show(Texture texture, string deckDisplayName)
        {
            this.mapImage.texture = texture;
            this.deckName.text = deckDisplayName;
            this.root.SetActive(true);
        }

        public void Hide() => this.root.SetActive(false);
    }
}
