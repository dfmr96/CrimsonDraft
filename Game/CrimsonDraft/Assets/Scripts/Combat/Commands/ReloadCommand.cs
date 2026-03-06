#nullable enable

using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat.Commands
{
    public sealed class ReloadCommand : IOperatorCommand
    {
        private readonly OperatorRuntime op;

        public ReloadCommand(OperatorRuntime op) => this.op = op;

        public void Execute() => this.op.Reload();
    }
}
