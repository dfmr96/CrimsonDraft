#nullable enable

namespace CrimsonDraft.Combat
{
    // Purely visual aim-difficulty effect (screen-space post-process, UI shake, etc). Never reads
    // or changes hit/damage logic -- AimDebuffController only ever gives it an intensity and a
    // duration to render.
    public interface IAimDebuffEffect
    {
        // Starts fading in towards intensity01 (0..1) over fadeInSeconds.
        void Begin(float intensity01, float fadeInSeconds);

        void Tick(float deltaTime);

        // Starts a graceful fade-out to zero over fadeOutSeconds. Safe to call multiple times.
        void End(float fadeOutSeconds);

        // Immediately resets to the off state, skipping any in-progress fade. Used for the
        // guaranteed shutdown path (QTE closed, cancelled, or the operator died) where combat
        // cannot be left waiting on a cosmetic fade.
        void ForceStop();

        // True once fully back to the off state after End() or ForceStop() -- safe to discard.
        bool IsFinished { get; }
    }
}
