#nullable enable

using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    /// <summary>Last Morse code sent from the BEEPER panel. Last-value-wins — sending a new
    /// code overwrites whatever was there before. A level zone reads CurrentSignal (or
    /// subscribes to BeeperSignalSentEvent) to detect it; nothing has to ever read it.</summary>
    public sealed class BeeperSignalRegistry
    {
        public string? CurrentSignal { get; private set; }

        [Preserve]
        public BeeperSignalRegistry() { }

        public void SetSignal(string code) => this.CurrentSignal = code;
        public void ClearSignal() => this.CurrentSignal = null;
    }
}
