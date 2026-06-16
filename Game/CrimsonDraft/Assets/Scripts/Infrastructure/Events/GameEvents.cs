#nullable enable

namespace CrimsonDraft.Infrastructure.Events
{
    public enum AmmoType { Rip, Fmj }

    public enum GuardAlertState { Patrol, Suspicious, Alert }

    public readonly struct CombatStartedEvent
    {
        public string EncounterId { get; init; }
    }

    public readonly struct CombatEndedEvent
    {
        public bool Victory { get; init; }
    }

    public readonly struct CharacterDamagedEvent
    {
        public string CharacterId { get; init; }
        public int DamageAmount { get; init; }
        public int RemainingHP { get; init; }
    }

    public readonly struct CharacterDiedEvent
    {
        public string CharacterId { get; init; }
        public bool IsPermadeath { get; init; }
    }

    public readonly struct QTEStartedEvent
    {
        public string CharacterId { get; init; }
        public string WeaponId { get; init; }
    }

    public readonly struct QTECompletedEvent
    {
        public string CharacterId { get; init; }
        public float NormalizedVerticalAccuracy { get; init; }
        public float NormalizedHorizontalAccuracy { get; init; }
        public int ShotsFired { get; init; }
    }

    public readonly struct ItemUsedEvent
    {
        public string CharacterId { get; init; }
        public string ItemId { get; init; }
    }

    public readonly struct WeaponReloadedEvent
    {
        public string CharacterId { get; init; }
        public string WeaponId { get; init; }
        public AmmoType AmmoType { get; init; }
    }

    public readonly struct KrokonilDoseAppliedEvent
    {
        public string CharacterId { get; init; }
        public float DoseMg { get; init; }
        public float CumulativeExposureMg { get; init; }
    }

    public readonly struct ShootConfigurationRequestedEvent
    {
        public int OperatorSlot { get; }

        public ShootConfigurationRequestedEvent(int operatorSlot)
        {
            this.OperatorSlot = operatorSlot;
        }
    }

    public readonly struct GuardAlertChangedEvent
    {
        public string GuardId { get; init; }
        public GuardAlertState PreviousState { get; init; }
        public GuardAlertState NewState { get; init; }
    }

    public readonly struct NoteCollectedEvent
    {
        public string NoteId { get; init; }
    }
}
