using UnityEngine;

namespace HorrorEngine
{
    public abstract class PlayerStateAttack : ActorStateWithDuration
    {
        [SerializeField] private PlayerStateAiming m_AimingState;
        [SerializeField] private ActorState m_ReloadState;
        [SerializeField] private bool m_AutoReloadOnAttackStart;
        [SerializeField] private bool m_AutoReloadOnAttackEnd;
        [SerializeField] private PlayerMovement.MovementConstrain m_MovementConstrains = PlayerMovement.MovementConstrain.Movement;
        

        protected WeaponData m_Weapon;
        protected InventoryEntry m_WeaponInventoryEntry;
        protected GameObject m_WeaponInstance;

        protected IAttack m_Attack;
        protected AttackMontage m_AttackMtg;

        private IPlayerInput m_Input;
        private IPlayerVerticalAiming m_VerticalAiming;

        private PlayerMovement m_Movement;
        private PlayerLookAtLookable m_LookAt;
        private WeaponAmmoVisuals m_AmmoVisuals;

        private float m_Delay;
        private bool m_Released;
        private bool m_Attacked;
        private int m_Attacks;
        private AttackRate m_AttackRate;
        private AttackCombo m_AttackCombo;
        private int m_ComboIndex;
        private float m_LastAttackTime;
        private Inventory m_Inventory;
        private bool m_PendingThrowableClearing;

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_Input = GetComponentInParent<IPlayerInput>();
            m_VerticalAiming = transform.parent.GetComponentInChildren<IPlayerVerticalAiming>();
            m_Movement = GetComponentInParent<PlayerMovement>();
            m_LookAt = Actor.MainAnimator.GetComponent<PlayerLookAtLookable>();
        }

        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            InitializeWeapon();

            m_Inventory = GameManager.Instance.Inventory;

            if (m_AutoReloadOnAttackStart &&
                m_Inventory.CanReloadWeapon(m_WeaponInventoryEntry) &&
                m_WeaponInventoryEntry.SecondaryCount == 0)
            {
                SetState(m_ReloadState);
                return;
            }

            m_AttackCombo = m_WeaponInstance.GetComponent<AttackCombo>();
            if (m_AttackCombo)
            {
                Debug.Assert(m_AttackCombo.Combo.Length > 0, "Attack combo was empty");
                float timeSinceLastAttack = Time.time - m_LastAttackTime;
                ++m_ComboIndex;
                if (m_ComboIndex >= m_AttackCombo.Combo.Length || timeSinceLastAttack > m_AttackCombo.Combo[m_ComboIndex].GracePeriod)
                    m_ComboIndex = 0;
            }

            m_AttackMtg = m_AttackCombo ? m_AttackCombo.Combo[m_ComboIndex].Montage : m_WeaponInstance.GetComponent<AttackMontage>();
            Debug.Assert(m_AttackMtg != null, "Weapon has no AttackMontage component");

            m_Attack = m_AttackMtg.Attack;

            m_Released = false;
            m_Duration = m_AttackMtg.Duration;
            m_Delay = m_AttackMtg.MontageDelay;
            m_AttackRate = m_WeaponInstance.GetComponent<AttackRate>();
            m_Attacks = 0;
            m_PendingThrowableClearing = false;
            m_AmmoVisuals = m_WeaponInstance.GetComponent<WeaponAmmoVisuals>();

            m_Movement.enabled = true;
            m_Movement.AddConstrain(m_MovementConstrains);

            if (m_LookAt)
                m_LookAt.LookIntensity = 0f;

            base.StateEnter(fromState);

            Debug.Assert(m_Delay <= m_Duration, "Attack Delay can't be greater than the duration of the attack");

            m_Attacked = false;

            UIManager.Get<UIInputListener>().AddBlockingContext(this);

            m_AttackMtg.PreDelay(CanAttackStart());     
        }

        // --------------------------------------------------------------------

        protected abstract void InitializeWeapon();

        // --------------------------------------------------------------------

        public override void StateUpdate()
        {
            bool canAttack = CanAttackStart();
            
            if (m_AttackRate && m_AttackRate.CanBeHeld && !m_Released)
            {
                if (m_Input.IsAttackUp())
                {
                    m_Released = true;
                    m_Duration = m_AttackMtg.Duration;
                }
                else
                {
                    if (canAttack)
                        m_Duration += Time.deltaTime;
                    else
                        m_Duration = 0;
                }
            }

            base.StateUpdate();

            if (!m_Attacked && m_TimeInState > m_Delay &&
                (m_Attacks == 0 || !m_Released)) // Don't keep shooting if we have released and already shot once
            {
                if (canAttack)
                {
                    m_AttackMtg.Play(Actor.MainAnimator);

                    ConsumeAmmoFromEquippedWeapon();
                    UpdateVerticality();

                    m_Attacked = true;
                    ++m_Attacks;

                    if (m_AttackRate)
                    {
                        UpdateRateAttack();
                    }
                }
                else
                {
                    m_AttackMtg.OnNotStarted();
                    SetState(m_ExitState);
                }
            }
            else if (m_Attacked && m_AttackCombo &&
                m_ComboIndex < m_AttackCombo.Combo.Length - 1 &&
                m_TimeInState > m_AttackCombo.Combo[m_ComboIndex].MinEntryTime)
            {
                if (m_Input.IsAttackDown())
                {
                    SetState(this);
                }
            }

            if (m_VerticalAiming != null)
            {
                Actor.MainAnimator.SetFloat(PlayerStateAiming.k_AimVerticalityHash, m_VerticalAiming.Verticality);
            }
        }

        // --------------------------------------------------------------------

        private void UpdateRateAttack()
        {
            bool canStartNextAttack = CanAttackStart();
            if (canStartNextAttack) // Don't do rate attacks if the next attack can't be performed
            {
                StartNextRateAttack();
            }
            else
            {
                m_AttackMtg.Interrupt();
            }
        }

        // --------------------------------------------------------------------

        private void UpdateVerticality()
        {
            if (m_Attack != null && m_VerticalAiming != null)
            {
                IVerticalAttack verticalAttack = m_Attack as IVerticalAttack;
                verticalAttack?.SetVerticality(m_VerticalAiming.Verticality);
            }
        }

        // --------------------------------------------------------------------

        private void StartNextRateAttack()
        {
            if (m_AttackRate &&
                (m_Attacks < m_AttackRate.MaxAttackCount || m_AttackRate.MaxAttackCount <= 0) &&
                m_AttackRate.AttacksPerSecond > 0)
            {
                m_Delay += 1f / m_AttackRate.AttacksPerSecond;
                m_Attacked = false;
            }
        }

        // --------------------------------------------------------------------

        private void ConsumeAmmoFromEquippedWeapon()
        {
            if (m_WeaponInventoryEntry != null)
            {
                float amount = m_AttackMtg.CountConsumption;
                if (m_WeaponInventoryEntry.Item is ReloadableWeaponData && m_WeaponInventoryEntry.SecondaryCount > 0)
                {
                    m_WeaponInventoryEntry.SecondaryCount -= amount;
                }
                else if (m_WeaponInventoryEntry.Item is ThrowableWeaponData && m_WeaponInventoryEntry.Count > 0)
                {
                    // Don't unequip yet or the AnimationOverride will mess things up
                    // Will be cleared later, check ClearThrowable method
                    m_Inventory.Remove(m_WeaponInventoryEntry, amount, false);
                    m_PendingThrowableClearing = m_WeaponInventoryEntry.Count == 0;
                }
            }

            if (m_AmmoVisuals)
                m_AmmoVisuals.UpdateVisibility();
        }

        // --------------------------------------------------------------------

        private bool CanAttackStart()
        {
            ReloadableWeaponData reloadable = m_WeaponInventoryEntry?.Item as ReloadableWeaponData;
            return !reloadable || m_WeaponInventoryEntry.SecondaryCount > 0;
        }

        // --------------------------------------------------------------------

        protected override void OnStateDurationEnd()
        {
            if (m_AutoReloadOnAttackEnd &&
                m_Inventory.CanReloadEquippedWeapon() &&
                m_WeaponInventoryEntry.SecondaryCount == 0)
            {
                SetState(m_ReloadState);
            }
            else
            {
                if (m_PendingThrowableClearing)
                    ClearThrowable();

                bool canGoToAim = m_AimingState && m_Inventory.GetEquipped(m_Inventory.WeaponSlot) != null;
                if (canGoToAim)
                    SetState(m_AimingState);
                else
                    SetState(m_ExitState);
            }
        }

        // --------------------------------------------------------------------

        private void ClearThrowable()
        {
            m_Inventory.Unequip(m_Inventory.WeaponSlot);
            m_PendingThrowableClearing = false;
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            if (m_Attacks > 0)
            { 
                if (m_AttackMtg) // The weapon could've been destroyed
                    m_AttackMtg.Complete();
            }

            m_Movement.enabled = false;
            m_Movement.RemoveConstrain(m_MovementConstrains);

            if (m_LookAt)
                m_LookAt.LookIntensity = 1f;

            if (m_PendingThrowableClearing)
                ClearThrowable(); // Just in case the state exits before StateDurationEnd happens

            m_Weapon = null;

            m_LastAttackTime = Time.time;

            UIManager.Get<UIInputListener>().RemoveBlockingContext(this);

            base.StateExit(intoState);
        }

    }
}
