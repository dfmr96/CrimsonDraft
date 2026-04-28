using UnityEngine;

namespace HorrorEngine
{
    [RequireComponent(typeof(Weapon))]
    public class WeaponAmmoVisuals : MonoBehaviour
    {
        [SerializeField] GameObject m_AmmoVisuals;
        [SerializeField] bool m_AutoHideWhenEmpty;
        [SerializeField] SocketAttachment m_OnCharacterAttachment;

        private Weapon m_Weapon;

        private Transform m_OriginalParent;
        private Vector3 m_OriginalPosition;
        private Quaternion m_OriginalRotation;
        private Vector3 m_OriginalScale;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Weapon = GetComponent<Weapon>();

            m_OriginalParent = m_AmmoVisuals.transform.parent;
            m_OriginalPosition = m_AmmoVisuals.transform.localPosition;
            m_OriginalRotation = m_AmmoVisuals.transform.localRotation;
            m_OriginalScale = m_AmmoVisuals.transform.localScale;
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            UpdateVisibility();
        }

        // --------------------------------------------------------------------

        public void SetAmmoVisible(bool visible)
        {
            m_AmmoVisuals.SetActive(visible);
        }

        // --------------------------------------------------------------------

        public void AttachAmmoToSocket(SocketController socketCtrl)
        {
            socketCtrl.Attach(m_AmmoVisuals, m_OnCharacterAttachment);

            SetAmmoVisible(true);
        }

        // --------------------------------------------------------------------

        public void AttachAmmoToWeapon()
        {
            m_AmmoVisuals.transform.SetParent(m_OriginalParent);
            m_AmmoVisuals.transform.SetLocalPositionAndRotation(m_OriginalPosition, m_OriginalRotation);
            m_AmmoVisuals.transform.localScale = m_OriginalScale;

            SetAmmoVisible(true);
        }

        // --------------------------------------------------------------------

        public void UpdateVisibility()
        {
            // TODO, hacky! find a better way of obtaining the weapon inventory entry
            InventoryEntry weaponInvEntry = GameManager.Instance.Inventory.GetEquippedWeapon();
            if (m_Weapon.WeaponData == weaponInvEntry.Item)
            {
                SetAmmoVisible(!m_AutoHideWhenEmpty || weaponInvEntry.SecondaryCount > 0);
            }
        }
    }
}