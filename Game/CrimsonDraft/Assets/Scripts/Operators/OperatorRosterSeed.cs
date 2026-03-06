#nullable enable

namespace CrimsonDraft.Operators
{
    public readonly struct OperatorRosterSeed
    {
        public OperatorData?[] Operators { get; }
        public int DefaultHp { get; }
        public int DefaultAmmo { get; }

        public OperatorRosterSeed(OperatorData?[] operators, int defaultHp, int defaultAmmo)
        {
            this.Operators = operators;
            this.DefaultHp = defaultHp;
            this.DefaultAmmo = defaultAmmo;
        }
    }
}
