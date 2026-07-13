namespace CrimsonDraft.Audio
{
    // Lets audio code read player movement state without depending on the
    // Navigation assembly (which itself depends on Audio for DoorAudio).
    public interface IPlayerMotion
    {
        float CurrentSpeed { get; }
        bool  IsSprinting  { get; }
    }
}
