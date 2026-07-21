#nullable enable

namespace CrimsonDraft.Operators
{
    public interface IWeaponSlot
    {
        Caliber Caliber    { get; }
        GunType GunType    { get; }
        int     BaseDamage { get; }
        int     CurrentAmmo { get; }
        int     MaxAmmo     { get; }
        int     PoiseDamage { get; }
        void    SetAmmo(int value);
    }
}
