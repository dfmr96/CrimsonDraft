#nullable enable

using System;

namespace CrimsonDraft.Navigation.Gates
{
    /// <summary>Completion of any gate, by <see cref="IGate.GateId"/>. GateStateService adapts the
    /// saved registries; tests use a fake.</summary>
    public interface IGateStates
    {
        bool IsCompleted(string gateId);

        /// <summary>Raised when any gate may have changed (a socket, puzzle or radio registry changed).</summary>
        event Action? Changed;
    }
}
