#nullable enable

using System;

namespace CrimsonDraft.Inventory
{
    public enum ContainerKind
    {
        Operator = 0,
        Storage  = 1,
    }

    public readonly struct ContainerId : IEquatable<ContainerId>
    {
        private ContainerId(ContainerKind kind, int index)
        {
            this.Kind  = kind;
            this.Index = index;
        }

        public ContainerKind Kind  { get; }
        public int           Index { get; }

        public static ContainerId Operator(int index) => new ContainerId(ContainerKind.Operator, index);
        public static ContainerId Storage => new ContainerId(ContainerKind.Storage, 0);

        public bool Equals(ContainerId other) => this.Kind == other.Kind && this.Index == other.Index;
        public override bool Equals(object? obj) => obj is ContainerId other && Equals(other);
        public override int GetHashCode() => ((int)this.Kind * 397) ^ this.Index;
        public override string ToString() => $"{this.Kind}[{this.Index}]";

        public static bool operator ==(ContainerId a, ContainerId b) => a.Equals(b);
        public static bool operator !=(ContainerId a, ContainerId b) => !a.Equals(b);
    }
}
