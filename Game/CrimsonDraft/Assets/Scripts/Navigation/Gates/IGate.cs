#nullable enable

namespace CrimsonDraft.Navigation.Gates
{
    /// <summary>
    /// Something in the world that can end up "completed": a socket filled (and turned, if it has a
    /// valve), a puzzle solved, a radio call heard. Other systems wait on gates without knowing which
    /// kind they are -- a camera zone that stops firing, a milestone that completes. Whether a gate
    /// is complete is answered by <see cref="IGateStates"/> from the saved world state, so it holds
    /// for gates in inactive rooms or other scenes too.
    /// </summary>
    public interface IGate
    {
        /// <summary>Kind-prefixed id, see <see cref="GateIds"/> (e.g. "socket:engine_fuse").</summary>
        string GateId { get; }
    }
}
