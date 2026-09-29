#nullable enable

namespace CrimsonDraft.Infrastructure.Graphics
{
    public interface IGraphicsSettingsService
    {
        float Gamma { get; }
        void SetGamma(float value01);

        /// <summary>
        /// Number of discrete steps between Gamma's 0 and 1 extremes -- every gamma slider/knob
        /// in the game (New Game calibration, Pause > Options > Video, MainMenu General tab)
        /// shares this so they all expose the same 9 positions (this value + 1), centered on the
        /// default 0.5 (displayed as gamma 1.0, ranging 0.20-1.80 in 0.20 increments).
        /// </summary>
        int GammaSteps { get; }

        /// <summary>
        /// Neutralizes the gamma offset (without touching CRT/PSX, which live on the same
        /// Volume) while UI like Pause or Inventory is on screen -- reference-counted so
        /// overlapping callers can't un-suppress each other early. See GraphicsSettingsService.
        /// </summary>
        void PushGammaSuppression();
        void PopGammaSuppression();
    }
}
