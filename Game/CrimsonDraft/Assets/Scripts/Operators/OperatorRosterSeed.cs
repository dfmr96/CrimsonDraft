#nullable enable

namespace CrimsonDraft.Operators
{
    public readonly struct OperatorRosterSeed
    {
        public OperatorData?[] Operators { get; }
        public int             DefaultHp { get; }

        public OperatorRosterSeed(OperatorData?[] operators, int defaultHp)
        {
            this.Operators = operators;
            this.DefaultHp = defaultHp;
        }
    }
}
