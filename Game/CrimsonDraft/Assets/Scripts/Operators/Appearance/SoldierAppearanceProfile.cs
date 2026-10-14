#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Operators.Appearance
{
    /// <summary>
    /// Saved look of a soldier: one variant per slot ("None" allowed), an optional material
    /// override and tint per slot, and the camo (atlas variant shared by the body and every piece). Applied by <see cref="SoldierAppearance"/>; referenced per operator
    /// from <see cref="OperatorData.AppearanceProfile"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "SoldierAppearance", menuName = "CrimsonDraft/Operators/Soldier Appearance Profile")]
    public sealed class SoldierAppearanceProfile : ScriptableObject
    {
        [Serializable]
        public sealed class SlotSelection
        {
            public SoldierAppearanceSlot slot;
            public string                variant          = SoldierAppearanceVariants.Default;
            [Tooltip("Optional: replaces the variant's material (e.g. Mat_Helmet_Hood).")]
            public Material?             materialOverride = null;
            public bool                  overrideTint     = false;
            public Color                 tint             = Color.white;
            [Tooltip("Glasses/Mask only: x = depth (+ toward the face), y = height, z = size; each -1..1.")]
            public Vector3               fit              = Vector3.zero;
        }

        // Filled by EnsureSlots (one entry per SoldierAppearanceSlot).
        [SerializeField] private SlotSelection[]? slots = null;
        [Tooltip("Camo name (Mat_Camo_<Name>) or \"Default\" for the original texture.")]
        [SerializeField] private string camo = SoldierAppearanceVariants.Default;

        [Tooltip("Body type blend shape weights (0..100), indexed by SoldierBodyShape.")]
        [SerializeField] private float[] bodyShapes = new float[SoldierAppearanceVariants.BodyShapeCount];
        [SerializeField, Range(SoldierAppearanceVariants.MinHeight, SoldierAppearanceVariants.MaxHeight)]
        private float height = 1f;

        public string Camo
        {
            get => string.IsNullOrEmpty(this.camo) ? SoldierAppearanceVariants.Default : this.camo;
            set => this.camo = value;
        }

        public float Height
        {
            get => this.height <= 0f ? 1f : this.height;
            set => this.height = Mathf.Clamp(value, SoldierAppearanceVariants.MinHeight, SoldierAppearanceVariants.MaxHeight);
        }

        public float GetBodyShape(SoldierBodyShape shape)
        {
            int i = (int)shape;
            return this.bodyShapes != null && i < this.bodyShapes.Length ? this.bodyShapes[i] : 0f;
        }

        public void SetBodyShape(SoldierBodyShape shape, float weight)
        {
            if (this.bodyShapes == null || this.bodyShapes.Length != SoldierAppearanceVariants.BodyShapeCount)
                Array.Resize(ref this.bodyShapes, SoldierAppearanceVariants.BodyShapeCount);
            this.bodyShapes[(int)shape] = Mathf.Clamp(weight, 0f, 100f);
        }

        public SlotSelection Get(SoldierAppearanceSlot slot)
        {
            this.EnsureSlots();
            return this.slots![(int)slot];
        }

        public string GetVariant(SoldierAppearanceSlot slot) => this.Get(slot).variant;

        public void SetVariant(SoldierAppearanceSlot slot, string variant) => this.Get(slot).variant = variant;

        // Keeps one entry per slot, indexed by the enum, even if the asset was edited by hand.
        private void EnsureSlots()
        {
            if (this.slots != null && this.slots.Length == SoldierAppearanceVariants.SlotCount)
            {
                bool ordered = true;
                for (int i = 0; i < this.slots.Length; i++)
                    ordered &= this.slots[i] != null && (int)this.slots[i].slot == i;
                if (ordered) return;
            }

            var fixedSlots = CreateDefaultSlots();
            if (this.slots != null)
                foreach (var s in this.slots)
                    if (s != null && (int)s.slot < fixedSlots.Length)
                        fixedSlots[(int)s.slot] = s;
            this.slots = fixedSlots;
        }

        private void OnValidate() => this.EnsureSlots();

        private static SlotSelection[] CreateDefaultSlots()
        {
            var result = new SlotSelection[SoldierAppearanceVariants.SlotCount];
            for (int i = 0; i < result.Length; i++)
                result[i] = new SlotSelection
                {
                    slot = (SoldierAppearanceSlot)i,
                    variant = SoldierAppearanceVariants.DefaultVariant((SoldierAppearanceSlot)i),
                };
            return result;
        }
    }
}
