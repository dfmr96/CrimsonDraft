using UnityEngine;

namespace PreRenderBackgrounds
{
    [CreateAssetMenu(menuName = "Pre Render Bgs/Settings")]
    public class PreRenderSettings : ScriptableObject
    {
        public Material PreRenderedBgMaterial;
        public Material PreRenderedClippingMaterial;
        public Material PreRenderedDepthTransferMaterial;
        public Vector2Int Resolution;
        //public Material[] RenderingEffects; -- TODO
        public LayerMask IncludedLayers;
    }
}