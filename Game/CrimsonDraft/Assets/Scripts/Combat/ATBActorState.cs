#nullable enable

namespace CrimsonDraft.Combat
{
    public enum ATBActorKind { Operator, Enemy }

    public readonly struct ATBActorConfig
    {
        public int          SlotIndex      { get; }
        public ATBActorKind Kind           { get; }
        public float        GaugePerSecond { get; }
        public float        InitialGauge   { get; }

        public ATBActorConfig(int slotIndex, ATBActorKind kind, float gaugePerSecond, float initialGauge = 0f)
        {
            this.SlotIndex      = slotIndex;
            this.Kind           = kind;
            this.GaugePerSecond = gaugePerSecond > 0f ? gaugePerSecond : 0f;
            this.InitialGauge   = UnityEngine.Mathf.Clamp01(initialGauge);
        }
    }

    public sealed class ATBActorState
    {
        public ATBActorConfig Config { get; }

        public float Gauge             { get; private set; }
        public bool  IsReady           => this.Gauge >= 1f;
        public bool  IsAwaitingCommand { get; set; }
        public bool  IsDead            { get; private set; }
        public bool  IsFrozen          { get; private set; }

        private float gaugePerSecond;

        public ATBActorState(ATBActorConfig config)
        {
            this.Config         = config;
            this.gaugePerSecond = config.GaugePerSecond;
            this.Gauge          = config.InitialGauge;
        }

        public void Tick(float deltaTime)
        {
            if (this.IsDead || this.IsFrozen) return;
            this.Gauge = (float)System.Math.Min(1.0, this.Gauge + deltaTime * this.gaugePerSecond);
        }

        public void Reset()
        {
            this.Gauge             = 0f;
            this.IsAwaitingCommand = false;
        }

        public void Freeze()   => this.IsFrozen = true;
        public void Unfreeze() => this.IsFrozen = false;
        public void MarkDead() => this.IsDead = true;
        public void FillGauge() { this.Gauge = 1f; }

        public void UpdateGaugePerSecond(float newRate)
        {
            this.gaugePerSecond = newRate > 0f ? newRate : 0f;
        }
    }
}
