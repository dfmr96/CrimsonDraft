// GameEvents.cs — all MessagePipe event structs for Crimson Draft
// Readonly structs = zero GC allocation on publish.
// Every type listed here must be registered in GameLifetimeScope.
namespace CrimsonDraft.Infrastructure.Events
{
    // ── Combat ────────────────────────────────────────────────────────────────

    public readonly struct CombatStartedEvent
    {
        public readonly string EncounterId;
        public CombatStartedEvent(string encounterId) => EncounterId = encounterId;
    }

    public readonly struct CombatEndedEvent
    {
        public readonly bool Victory;
        public CombatEndedEvent(bool victory) => Victory = victory;
    }

    public readonly struct CharacterDamagedEvent
    {
        public readonly string CharacterId;
        public readonly int DamageAmount;
        public readonly int RemainingHP;

        public CharacterDamagedEvent(string characterId, int damage, int remainingHP)
        {
            CharacterId  = characterId;
            DamageAmount = damage;
            RemainingHP  = remainingHP;
        }
    }

    public readonly struct CharacterDiedEvent
    {
        public readonly string CharacterId;
        public readonly bool IsPermanent; // permadeath
        public CharacterDiedEvent(string characterId, bool isPermanent)
        {
            CharacterId = characterId;
            IsPermanent = isPermanent;
        }
    }

    // ── QTE ───────────────────────────────────────────────────────────────────

    public readonly struct QTEStartedEvent
    {
        public readonly string CharacterId;
        public readonly string WeaponId;
        public QTEStartedEvent(string characterId, string weaponId)
        {
            CharacterId = characterId;
            WeaponId    = weaponId;
        }
    }

    public readonly struct QTECompletedEvent
    {
        public readonly string CharacterId;
        public readonly float AccuracyY;   // 0–1, vertical bar stop
        public readonly float AccuracyX;   // 0–1, horizontal bar stop
        public readonly int   ShotsFired;
        public QTECompletedEvent(string characterId, float accY, float accX, int shots)
        {
            CharacterId = characterId;
            AccuracyY   = accY;
            AccuracyX   = accX;
            ShotsFired  = shots;
        }
    }

    // ── Inventory ─────────────────────────────────────────────────────────────

    public readonly struct ItemUsedEvent
    {
        public readonly string CharacterId;
        public readonly string ItemId;
        public ItemUsedEvent(string characterId, string itemId)
        {
            CharacterId = characterId;
            ItemId      = itemId;
        }
    }

    public readonly struct WeaponReloadedEvent
    {
        public readonly string CharacterId;
        public readonly string WeaponId;
        public readonly string AmmoType;  // "RIP" | "FMJ"
        public WeaponReloadedEvent(string characterId, string weaponId, string ammoType)
        {
            CharacterId = characterId;
            WeaponId    = weaponId;
            AmmoType    = ammoType;
        }
    }

    // ── Krokonil ──────────────────────────────────────────────────────────────

    public readonly struct KrokoniлDoseAppliedEvent
    {
        public readonly string CharacterId;
        public readonly float  DoseMg;
        public readonly float  TotalExposure; // hidden cumulative counter
        public KrokoniлDoseAppliedEvent(string characterId, float doseMg, float totalExposure)
        {
            CharacterId   = characterId;
            DoseMg        = doseMg;
            TotalExposure = totalExposure;
        }
    }

    // ── Navigation / Stealth ─────────────────────────────────────────────────

    public readonly struct GuardAlertChangedEvent
    {
        public readonly string GuardId;
        public readonly GuardAlertState PreviousState;
        public readonly GuardAlertState NewState;
        public GuardAlertChangedEvent(string guardId, GuardAlertState prev, GuardAlertState next)
        {
            GuardId       = guardId;
            PreviousState = prev;
            NewState      = next;
        }
    }

    public enum GuardAlertState { Patrol, Suspicious, Alert }
}
