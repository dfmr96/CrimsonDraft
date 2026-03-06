#nullable enable

using System.Collections.Generic;
using CrimsonDraft.Health;

namespace CrimsonDraft.Combat
{
    public sealed class CombatOperatorHealthBridge
    {
        private readonly IOperatorHealthService healthService;

        public CombatOperatorHealthBridge(IOperatorHealthService healthService)
        {
            this.healthService = healthService;
        }

        public void Initialize(int slotCount, int defaultHp)
        {
            this.healthService.Initialize(slotCount, defaultHp);
        }

        public IReadOnlyList<int> GetAliveSlots() => this.healthService.GetAliveSlots();

        public bool IsAlive(int slotIndex) => this.healthService.IsAlive(slotIndex);

        public float GetHpRatio(int slotIndex) => this.healthService.GetHpRatio(slotIndex);

        public OperatorDamageResult ApplyDamage(int slotIndex, int damage) =>
            this.healthService.ApplyDamage(slotIndex, damage);
    }
}
