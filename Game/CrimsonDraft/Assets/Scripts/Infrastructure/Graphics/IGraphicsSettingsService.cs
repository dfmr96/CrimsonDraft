#nullable enable

namespace CrimsonDraft.Infrastructure.Graphics
{
    public interface IGraphicsSettingsService
    {
        float Gamma { get; }
        void SetGamma(float value01);

        /// <summary>
        /// Neutralizes the gamma offset (without touching CRT/PSX, which live on the same
        /// Volume) while UI like Pause or Inventory is on screen -- reference-counted so
        /// overlapping callers can't un-suppress each other early. See GraphicsSettingsService.
        /// </summary>
        void PushGammaSuppression();
        void PopGammaSuppression();
    }
}
