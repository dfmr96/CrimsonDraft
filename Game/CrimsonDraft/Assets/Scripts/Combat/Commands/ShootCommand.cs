#nullable enable

using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat.Commands
{
    public sealed class ShootCommand : IOperatorCommand
    {
        private readonly OperatorRuntime op;
        private readonly int             targetSlot;
        private readonly int             shotCount;
        private readonly IBattlefieldView battlefield;

        public ShootCommand(OperatorRuntime op, int targetSlot, int shotCount, IBattlefieldView battlefield)
        {
            this.op          = op;
            this.targetSlot  = targetSlot;
            this.shotCount   = shotCount;
            this.battlefield = battlefield;
        }

        public void Execute()
        {
            this.op.ConsumeAmmo(this.shotCount);
            this.battlefield.ApplyDamageToEnemy(this.targetSlot, this.shotCount * CombatMenuController.BaseDamage);
        }
    }
}
