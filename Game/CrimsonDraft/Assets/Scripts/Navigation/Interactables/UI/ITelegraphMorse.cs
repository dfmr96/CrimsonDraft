#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>A telegraph key that can transmit a message in Morse (moving the key and sounding the pulses).</summary>
    public interface ITelegraphMorse
    {
        /// <summary>How long transmitting <paramref name="text"/> takes at the key's own speed -- the typewriter
        /// reveal runs over this long so the text keeps the rhythm of the pulses.</summary>
        float MeasureSeconds(string text);

        /// <summary>Transmits <paramref name="text"/> at the key's own speed. Stops early when cancelled.</summary>
        UniTask TransmitAsync(string text, CancellationToken cancellationToken);

        /// <summary>Releases the key and silences the pulse immediately.</summary>
        void StopTransmission();
    }
}
