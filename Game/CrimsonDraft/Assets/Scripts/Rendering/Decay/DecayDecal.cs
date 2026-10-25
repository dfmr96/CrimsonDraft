#nullable enable

using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CrimsonDraft.Rendering.Decay
{
    // Marca un decal creado por la Decay Tool. "Regenerar" borra solo los que no están
    // fijados, así se puede retocar a mano (mover, borrar, fijar) sin perderlo.
    // ExecuteAlways: la vista previa de Detalle de la Decay Tool también los prende/apaga en el editor.
    [ExecuteAlways]
    [RequireComponent(typeof(DecalProjector))]
    public sealed class DecayDecal : MonoBehaviour
    {
        public enum Kind
        {
            Dirt,
            Leak,
            Rust,
            Mold,
            FloorStain,
        }

        [SerializeField] private Kind        kind;
        [Tooltip("Fijado: Regenerar no lo toca.")]
        [SerializeField] private bool        locked;
        [Tooltip("Con Detalle en Bajo, los decals que piden Medio se apagan.")]
        [SerializeField] private DecayDetail minimumDetail = DecayDetail.Low;

        private DecalProjector? projector;

        public Kind        DecalKind     { get => this.kind;          set => this.kind = value; }
        public bool        Locked        { get => this.locked;        set => this.locked = value; }
        public DecayDetail MinimumDetail { get => this.minimumDetail; set => this.minimumDetail = value; }

        private void OnEnable()
        {
            this.projector = GetComponent<DecalProjector>();
            DecayQuality.Changed += Apply;
            Apply();
        }

        private void OnDisable() => DecayQuality.Changed -= Apply;

        private void Apply()
        {
            if (this.projector != null)
                this.projector.enabled = DecayQuality.Current >= this.minimumDetail;
        }
    }
}
