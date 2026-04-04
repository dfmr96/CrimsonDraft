namespace HorrorEngine
{
    public abstract class WeaponAttack : AttackBase
    {
        protected Weapon m_Weapon;

        protected WeaponData m_WeaponData => m_Weapon.WeaponData;

        protected override void Awake()
        {
            base.Awake();
            m_Weapon = GetComponentInParent<Weapon>();
        }
    }
}