using UnityEngine;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public class DoorLockOtherSide : DoorLock
    {
        [SerializeField] private DialogData m_OnUnlockedDialog;
        [SerializeField] private AudioClip m_OnUnlockedSound;


        [HideInInspector]
        [FormerlySerializedAs("m_OnUnlockDialog")]
        [SerializeField] private string[] m_OnUnlockDialog_DEPRECATED = { "You unlocked the door" };


        public override void OnTryToUnlock(out bool openImmediately)
        {
            openImmediately = false;

            if (m_LockedSound)
                AudioSource.PlayOneShot(m_LockedSound);

            if (m_OnLockedDialog.IsValid())
                UIManager.Get<UIDialog>().Show(m_OnLockedDialog);
        }

        public override void OnTryToUnlockFromExit(out bool openImmediately)
        {
            openImmediately = false;

            IsLocked = false;
            OnUnlock?.Invoke();

            if (m_OnUnlockedSound)
                AudioSource.PlayOneShot(m_OnUnlockedSound);

            if (m_OnUnlockedDialog.IsValid())
                UIManager.Get<UIDialog>().Show(m_OnUnlockedDialog);
        }
    }
}