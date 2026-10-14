#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Operators.Appearance
{
    /// <summary>
    /// Shows one equipment variant per slot on a soldier. Every variant is a child SkinnedMeshRenderer
    /// bound to the character's own skeleton (installed by the Soldier Appearance Editor), so swapping
    /// is just enabling/disabling GameObjects: the Animator and its clips are untouched.
    /// A camo swaps the soldier atlas material (same UV layout) on the body and every piece at once.
    /// Works in Edit mode (preview / saved into the prefab) and at runtime. No per-frame work.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoldierAppearance : MonoBehaviour
    {
        [Serializable]
        public sealed class VariantEntry
        {
            public SoldierAppearanceSlot slot;
            public string                variant = string.Empty;
            public SkinnedMeshRenderer   renderer = null!;
        }

        // Every renderer whose materials this component drives (body + variants), with the imported materials.
        [Serializable]
        private sealed class MaterialTarget
        {
            public SkinnedMeshRenderer renderer = null!;
            public int                 slot = -1; // -1: body (no slot)
            public Material[]          originals = Array.Empty<Material>();
        }

        // Objects of the source FBX that predate the Slot_Variant naming: treated as each slot's Default.
        private static readonly (string objectName, SoldierAppearanceSlot slot)[] LegacyDefaults =
        {
            ("Helmet", SoldierAppearanceSlot.Helmet),
            ("Goggle", SoldierAppearanceSlot.Glasses),
            ("Pack",   SoldierAppearanceSlot.Backpack),
            ("Vest",   SoldierAppearanceSlot.Vest),
        };

        private const string CamoPrefix = "Mat_Camo_";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");
        private static MaterialPropertyBlock? propertyBlock;

        [Tooltip("Optional: applied on Awake. Usually set per operator through OperatorData instead.")]
        [SerializeField] private SoldierAppearanceProfile? profile = null;
        [SerializeField] private List<VariantEntry>        variants = new List<VariantEntry>();
        // Current selection per slot (indexed by SoldierAppearanceSlot), serialized with the prefab.
        [SerializeField] private string[]                  selection = Array.Empty<string>();

        [Header("Camo")]
        [Tooltip("Soldier atlas materials (Soldier1/Soldier2) that a camo replaces.")]
        [SerializeField] private List<Material>            camoBaseMaterials = new List<Material>();
        [Tooltip("Available camos (Mat_Camo_<Name>), filled by the installer from Variants/Camo.")]
        [SerializeField] private List<Material>            camoMaterials = new List<Material>();
        [SerializeField] private string                    camo = SoldierAppearanceVariants.Default;
        [SerializeField] private List<MaterialTarget>      materialTargets = new List<MaterialTarget>();
        [SerializeField] private Material?[]               slotMaterialOverrides = Array.Empty<Material?>();

        public SoldierAppearanceProfile? Profile  => this.profile;
        public IReadOnlyList<VariantEntry> Variants => this.variants;
        public string Camo => string.IsNullOrEmpty(this.camo) ? SoldierAppearanceVariants.Default : this.camo;

        private void Awake()
        {
            if (this.profile != null)
                this.Apply(this.profile);
        }

        /// <summary>Variant currently shown in the slot ("None" when hidden).</summary>
        public string GetVariant(SoldierAppearanceSlot slot)
        {
            this.EnsureArrays();
            return this.selection[(int)slot];
        }

        /// <summary>Applies every slot of the profile (variant, material override, tint) and its camo.</summary>
        public void Apply(SoldierAppearanceProfile appearance)
        {
            this.EnsureArrays();
            for (int i = 0; i < SoldierAppearanceVariants.SlotCount; i++)
            {
                var data = appearance.Get((SoldierAppearanceSlot)i);
                this.SetVariant(data.slot, data.variant);
                this.slotMaterialOverrides[i] = data.materialOverride;
            }
            this.camo = this.HasCamo(appearance.Camo) ? appearance.Camo : SoldierAppearanceVariants.Default;
            this.RefreshMaterials();
            for (int i = 0; i < SoldierAppearanceVariants.SlotCount; i++)
            {
                var data = appearance.Get((SoldierAppearanceSlot)i);
                this.SetSlotTint(data.slot, data.overrideTint, data.tint);
            }
        }

        /// <summary>
        /// Shows <paramref name="variant"/> and hides the rest of the slot. "None" hides the whole slot.
        /// Returns false (and leaves the slot untouched) when the variant does not exist.
        /// </summary>
        public bool SetVariant(SoldierAppearanceSlot slot, string variant)
        {
            this.EnsureArrays();
            bool hide = string.Equals(variant, SoldierAppearanceVariants.None, StringComparison.OrdinalIgnoreCase);
            if (!hide && !this.HasVariant(slot, variant))
            {
                Debug.LogWarning($"[SoldierAppearance] '{this.name}' has no variant '{variant}' for slot {slot}.", this);
                return false;
            }

            foreach (var entry in this.variants)
            {
                if (entry.slot != slot || entry.renderer == null) continue;
                bool show = !hide && string.Equals(entry.variant, variant, StringComparison.OrdinalIgnoreCase);
                if (entry.renderer.gameObject.activeSelf != show)
                    entry.renderer.gameObject.SetActive(show);
            }

            this.selection[(int)slot] = hide ? SoldierAppearanceVariants.None : variant;
            return true;
        }

        public bool HasVariant(SoldierAppearanceSlot slot, string variant)
        {
            foreach (var entry in this.variants)
                if (entry.slot == slot && entry.renderer != null
                    && string.Equals(entry.variant, variant, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>Fills <paramref name="result"/> with the variant names of a slot (without "None").</summary>
        public void GetVariantNames(SoldierAppearanceSlot slot, List<string> result)
        {
            result.Clear();
            foreach (var entry in this.variants)
                if (entry.slot == slot && entry.renderer != null && !result.Contains(entry.variant))
                    result.Add(entry.variant);
        }

        // -------------------------------------------------------------------------------------
        // Camo / materials
        // -------------------------------------------------------------------------------------

        /// <summary>Swaps the soldier atlas on the body and every piece. "Default" restores the imported one.</summary>
        public bool SetCamo(string camoName)
        {
            if (!this.HasCamo(camoName))
            {
                Debug.LogWarning($"[SoldierAppearance] '{this.name}' has no camo '{camoName}'.", this);
                return false;
            }
            this.camo = camoName;
            this.RefreshMaterials();
            return true;
        }

        public bool HasCamo(string camoName) =>
            string.Equals(camoName, SoldierAppearanceVariants.Default, StringComparison.OrdinalIgnoreCase)
            || this.FindCamo(camoName) != null;

        /// <summary>"Default" followed by every available camo name.</summary>
        public void GetCamoNames(List<string> result)
        {
            result.Clear();
            result.Add(SoldierAppearanceVariants.Default);
            foreach (var m in this.camoMaterials)
                if (m != null) result.Add(CamoName(m));
        }

        public Material? FindCamo(string camoName)
        {
            foreach (var m in this.camoMaterials)
                if (m != null && string.Equals(CamoName(m), camoName, StringComparison.OrdinalIgnoreCase))
                    return m;
            return null;
        }

        public static string CamoName(Material material) =>
            material.name.StartsWith(CamoPrefix, StringComparison.Ordinal) ? material.name.Substring(CamoPrefix.Length) : material.name;

        /// <summary>Material override (null restores the imported/camo one) and optional tint for a slot.</summary>
        public void SetSlotLook(SoldierAppearanceSlot slot, Material? materialOverride, bool overrideTint, Color tint)
        {
            this.EnsureArrays();
            this.slotMaterialOverrides[(int)slot] = materialOverride;
            this.RefreshMaterials();
            this.SetSlotTint(slot, overrideTint, tint);
        }

        /// <summary>Editor: which atlas materials a camo replaces and which camos exist.</summary>
        public void ConfigureCamos(IEnumerable<Material> baseMaterials, IEnumerable<Material> camos)
        {
            this.camoBaseMaterials = new List<Material>(baseMaterials);
            this.camoMaterials = new List<Material>(camos);
            if (!this.HasCamo(this.Camo)) this.camo = SoldierAppearanceVariants.Default;
        }

        private void SetSlotTint(SoldierAppearanceSlot slot, bool overrideTint, Color tint)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            foreach (var entry in this.variants)
            {
                if (entry.slot != slot || entry.renderer == null) continue;
                if (overrideTint)
                {
                    propertyBlock.Clear();
                    propertyBlock.SetColor(BaseColorId, tint);
                    propertyBlock.SetColor(ColorId, tint);
                    entry.renderer.SetPropertyBlock(propertyBlock);
                }
                else
                {
                    entry.renderer.SetPropertyBlock(null);
                }
            }
        }

        // Resolves every driven renderer: slot override > camo (for atlas materials) > imported material.
        private void RefreshMaterials()
        {
            this.EnsureArrays();
            var camoMaterial = this.FindCamo(this.Camo);
            foreach (var target in this.materialTargets)
            {
                if (target.renderer == null || target.originals.Length == 0) continue;
                var slotOverride = target.slot >= 0 ? this.slotMaterialOverrides[target.slot] : null;
                var current = target.renderer.sharedMaterials;
                bool changed = current.Length != target.originals.Length;
                var resolved = changed ? new Material[target.originals.Length] : current;
                for (int i = 0; i < target.originals.Length; i++)
                {
                    var original = target.originals[i];
                    var m = slotOverride != null ? slotOverride
                        : camoMaterial != null && this.camoBaseMaterials.Contains(original) ? camoMaterial
                        : original;
                    if (resolved[i] != m) { resolved[i] = m; changed = true; }
                }
                if (changed) target.renderer.sharedMaterials = resolved;
            }
        }

        // -------------------------------------------------------------------------------------
        // Setup
        // -------------------------------------------------------------------------------------

        /// <summary>
        /// Rebuilds the variant list from child SkinnedMeshRenderers named "Slot_Variant"
        /// (plus the legacy Helmet/Goggle/Pack/Vest objects as Default), the material targets
        /// (body included), and syncs the selection with what is currently active.
        /// </summary>
        [ContextMenu("Rebuild Variant List")]
        public void RebuildVariantList()
        {
            // Keep the imported materials of renderers already tracked (they may currently show a camo).
            var previous = new Dictionary<SkinnedMeshRenderer, Material[]>();
            foreach (var t in this.materialTargets)
                if (t.renderer != null) previous[t.renderer] = t.originals;

            this.variants.Clear();
            this.materialTargets.Clear();
            foreach (var smr in this.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                int slotIndex = -1;
                if (TryParse(smr.name, out var slot, out var variant))
                {
                    this.variants.Add(new VariantEntry { slot = slot, variant = variant, renderer = smr });
                    slotIndex = (int)slot;
                }
                this.materialTargets.Add(new MaterialTarget
                {
                    renderer = smr,
                    slot = slotIndex,
                    originals = previous.TryGetValue(smr, out var mats) ? mats : smr.sharedMaterials,
                });
            }

            this.EnsureArrays();
            for (int i = 0; i < SoldierAppearanceVariants.SlotCount; i++)
            {
                string active = SoldierAppearanceVariants.None;
                foreach (var e in this.variants)
                    if ((int)e.slot == i && e.renderer.gameObject.activeSelf) { active = e.variant; break; }
                this.selection[i] = active;
            }
            this.RefreshMaterials();
        }

        /// <summary>"Helmet_Hood" -> (Helmet, "Hood"); legacy "Goggle" -> (Glasses, "Default").</summary>
        public static bool TryParse(string objectName, out SoldierAppearanceSlot slot, out string variant)
        {
            foreach (var (legacyName, legacySlot) in LegacyDefaults)
            {
                if (objectName == legacyName)
                {
                    slot = legacySlot;
                    variant = SoldierAppearanceVariants.Default;
                    return true;
                }
            }

            int sep = objectName.IndexOf('_');
            if (sep > 0 && sep < objectName.Length - 1
                && Enum.TryParse(objectName.Substring(0, sep), false, out slot)
                && Enum.IsDefined(typeof(SoldierAppearanceSlot), slot))
            {
                variant = objectName.Substring(sep + 1);
                return true;
            }

            slot = default;
            variant = string.Empty;
            return false;
        }

        // Grows the per-slot arrays when slots are added (keeps existing values).
        private void EnsureArrays()
        {
            int n = SoldierAppearanceVariants.SlotCount;
            if (this.selection == null || this.selection.Length != n)
            {
                var old = this.selection ?? Array.Empty<string>();
                this.selection = new string[n];
                for (int i = 0; i < n; i++)
                    this.selection[i] = i < old.Length ? old[i] : SoldierAppearanceVariants.DefaultVariant((SoldierAppearanceSlot)i);
            }
            if (this.slotMaterialOverrides == null || this.slotMaterialOverrides.Length != n)
            {
                var old = this.slotMaterialOverrides ?? Array.Empty<Material?>();
                this.slotMaterialOverrides = new Material?[n];
                for (int i = 0; i < n && i < old.Length; i++)
                    this.slotMaterialOverrides[i] = old[i];
            }
        }
    }
}
