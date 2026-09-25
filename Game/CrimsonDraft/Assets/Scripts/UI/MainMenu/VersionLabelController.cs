#nullable enable

using UnityEngine;
using TMPro;

namespace CrimsonDraft.UI.MainMenu
{
    // Shows "vX.Y.Z · <git hash>" in a corner of the main menu. The hash comes from
    // Resources/BuildInfo.txt, written by BuildInfoGenerator right before each build --
    // falls back to "dev" when running in the Editor without that file present.
    public sealed class VersionLabelController : MonoBehaviour
    {
        [SerializeField] private TMP_Text label = null!;

        private void Awake()
        {
            var buildInfo = Resources.Load<TextAsset>("BuildInfo");
            var hash = buildInfo != null ? buildInfo.text.Trim() : "dev";
            this.label.text = $"v{Application.version} · {hash}";
        }
    }
}
