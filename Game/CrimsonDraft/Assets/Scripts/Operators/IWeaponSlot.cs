#nullable enable

namespace CrimsonDraft.Operators
{
    public interface IWeaponSlot
    {
        string Caliber     { get; }
        int    CurrentAmmo { get; }
        int    MaxAmmo     { get; }
        void   SetAmmo(int value);
    }
}
