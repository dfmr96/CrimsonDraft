// #nullable enable

namespace CrimsonDraft.Combat
{
    public enum ATBActorKind { Operator, Enemy }

    public readonly struct ATBActorConfig
    {
        public int          SlotIndex      { get; }
        public ATBActorKind Kind           { get; }
        public float        GaugePerSecond { get; }

        public ATBActorConfig(int slotIndex, ATBActorKind kind, float gaugePerSecond)
        {
            this.SlotIndex      = slotIndex;
            this.Kind           = kind;
            this.GaugePerSecond = gaugePerSecond > 0f ? gaugePerSecond : 0f;
        }
    }

    public sealed class ATBActorState
    {
        public ATBActorConfig Config { get; }

        public float Gauge             { get; private set; }
        public bool  IsReady           => this.Gauge >= 1f;
        public bool  IsAwaitingCommand { get; set; }
        public bool  IsDead            { get; private set; }

        private float gaugePerSecond;

        public ATBActorState(ATBActorConfig config)
        {
            this.Config         = config;
            this.gaugePerSecond = config.GaugePerSecond;
        }

        public void Tick(float deltaTime)
        {
            if (this.IsDead) return;
            this.Gauge = (float)System.Math.Min(1.0, this.Gauge + deltaTime * this.gaugePerSecond);
        }

        public void Reset()
        {
            this.Gauge             = 0f;
            this.IsAwaitingCommand = false;
        }

        public void MarkDead() => this.IsDead = true;

        public void UpdateGaugePerSecond(float newRate)
        {
            this.gaugePerSecond = newRate > 0f ? newRate : 0f;
        }
    }
}
