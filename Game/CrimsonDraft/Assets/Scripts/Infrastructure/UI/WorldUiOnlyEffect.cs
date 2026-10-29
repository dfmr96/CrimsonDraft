#nullable enable

using UnityEngine;

namespace CrimsonDraft.Infrastructure.UI
{
    /// <summary>
    /// On a world-space canvas that is switched on only while it is used (the save room's terminal):
    /// while it is active, the <see cref="UiOnlyEffectRig"/> draws it, so the UI volume (CRT / bloom)
    /// lands on the panel and not on the world around it.
    /// </summary>
    public sealed class WorldUiOnlyEffect : MonoBehaviour
    {
        private const string UiLayerName = "UICRTOnly";

        void OnEnable()
        {
            int layer = LayerMask.NameToLayer(UiLayerName);
            var rig   = UiOnlyEffectRig.Current;
            if (layer < 0 || rig == null) return;

            // The game camera never draws this layer; only the rig's camera does.
            foreach (var t in GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;

            rig.Acquire(this, followWorldCamera: true);
        }

        void OnDisable() => UiOnlyEffectRig.Current?.Release(this);
    }
}
