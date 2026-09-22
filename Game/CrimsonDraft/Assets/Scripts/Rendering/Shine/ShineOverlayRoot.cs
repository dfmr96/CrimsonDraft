#nullable enable

using UnityEngine;

namespace CrimsonDraft.Rendering.Shine
{
    // Empty marker on every GameObject ItemShineOverlay spawns -- lets PickupPreviewView's
    // layer reassignment recognize and skip these subtrees (see SetLayerRecursively) so the
    // world/inspect shine never gets pulled onto the "ItemPreview" layer along with the rest
    // of the model. World pickups and the inspect/inventory preview instantiate the exact same
    // prefab, so without this the glint would show up in the preview too.
    public sealed class ShineOverlayRoot : MonoBehaviour { }
}
