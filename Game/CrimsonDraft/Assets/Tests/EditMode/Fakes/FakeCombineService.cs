#nullable enable

using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    public sealed class FakeCombineService : ICombineService
    {
        private readonly ItemData? inputA;
        private readonly ItemData? inputB;
        private readonly ItemData? output;

        public FakeCombineService(ItemData inputA, ItemData inputB, ItemData output)
        {
            this.inputA = inputA;
            this.inputB = inputB;
            this.output = output;
        }

        private FakeCombineService() { }

        public static FakeCombineService None => new FakeCombineService();

        public ItemData? TryGetResult(ItemData a, ItemData b)
        {
            if (this.output == null) return null;
            bool match = (a == this.inputA && b == this.inputB) || (a == this.inputB && b == this.inputA);
            return match ? this.output : null;
        }
    }
}
