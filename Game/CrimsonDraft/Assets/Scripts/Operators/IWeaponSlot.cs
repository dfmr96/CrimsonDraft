#nullable enable

namespace CrimsonDraft.Operators
{
    public interface IWeaponSlot
    {
        Caliber Caliber    { get; }
        int     BaseDamage { get; }
        int     CurrentAmmo { get; }
        int     MaxAmmo     { get; }
        void    SetAmmo(int value);
    }
}
