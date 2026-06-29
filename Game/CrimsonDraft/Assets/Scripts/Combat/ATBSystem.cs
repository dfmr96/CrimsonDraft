#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Combat
{
    public sealed class ATBSystem
    {
        private readonly List<ATBActorState> actors = new();

        public IReadOnlyList<ATBActorState> Actors => this.actors;

        public void Initialize(IReadOnlyList<ATBActorConfig> configs)
        {
            this.actors.Clear();
            for (int i = 0; i < configs.Count; i++)
                this.actors.Add(new ATBActorState(configs[i]));
        }

        public void Tick(float deltaTime, bool paused)
        {
            if (paused) return;
            for (int i = 0; i < this.actors.Count; i++)
                this.actors[i].Tick(deltaTime);
        }

        public void ResetActor(int slotIndex, ATBActorKind kind)
            => GetActor(slotIndex, kind)?.Reset();

        public void FreezeActor(int slotIndex, ATBActorKind kind)
            => GetActor(slotIndex, kind)?.Freeze();

        public void UnfreezeActor(int slotIndex, ATBActorKind kind)
            => GetActor(slotIndex, kind)?.Unfreeze();

        public void MarkDead(int slotIndex, ATBActorKind kind)
            => GetActor(slotIndex, kind)?.MarkDead();

        public void UpdateActorGaugeRate(int slotIndex, ATBActorKind kind, float newGaugePerSecond)
            => GetActor(slotIndex, kind)?.UpdateGaugePerSecond(newGaugePerSecond);

        public void FillOperatorGauges()
        {
            for (int i = 0; i < this.actors.Count; i++)
            {
                if (this.actors[i].Config.Kind == ATBActorKind.Operator)
                    this.actors[i].FillGauge();
            }
        }

        public ATBActorState? GetActor(int slotIndex, ATBActorKind kind)
        {
            for (int i = 0; i < this.actors.Count; i++)
            {
                ATBActorState a = this.actors[i];
                if (a.Config.SlotIndex == slotIndex && a.Config.Kind == kind)
                    return a;
            }
            return null;
        }
    }
}
