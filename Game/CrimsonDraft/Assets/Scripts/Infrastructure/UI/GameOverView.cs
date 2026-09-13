#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Infrastructure.UI
{
    public sealed class GameOverView : MonoBehaviour
    {
        [SerializeField] private GameObject root               = null!;
        [SerializeField] private Button     returnToMenuButton = null!;

        public Button ReturnToMenuButton => this.returnToMenuButton;

        public void Show() => this.root.SetActive(true);

        public void Hide() => this.root.SetActive(false);
    }
}
