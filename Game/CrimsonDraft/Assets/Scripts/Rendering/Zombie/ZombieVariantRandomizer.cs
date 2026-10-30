#nullable enable

using UnityEngine;

namespace CrimsonDraft.Rendering
{
    /// <summary>
    /// On Awake, picks a random variant from a <see cref="ZombieVariantSet"/> and swaps the zombie's
    /// base Body / Details materials for it on every child renderer. Materials other than the two
    /// base ones are left alone. Shared material assets are assigned (no instances created), so
    /// zombies with the same variant still batch together.
    /// </summary>
    public sealed class ZombieVariantRandomizer : MonoBehaviour
    {
        [SerializeField] private ZombieVariantSet? variantSet;

        private void Awake()
        {
            if (this.variantSet == null || this.variantSet.Variants.Length == 0) return;

            var variant = this.variantSet.Variants[Random.Range(0, this.variantSet.Variants.Length)];

            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;

                for (var i = 0; i < materials.Length; i++)
                {
                    if (variant.Body != null && materials[i] == this.variantSet.BaseBody)
                    {
                        materials[i] = variant.Body;
                        changed = true;
                    }
                    else if (variant.Details != null && materials[i] == this.variantSet.BaseDetails)
                    {
                        materials[i] = variant.Details;
                        changed = true;
                    }
                }

                if (changed) renderer.sharedMaterials = materials;
            }
        }
    }
}
