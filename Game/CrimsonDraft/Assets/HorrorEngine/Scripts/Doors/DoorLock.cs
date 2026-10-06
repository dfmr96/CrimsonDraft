using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public class DoorLock : MonoBehaviour, ISavableObjectStateExtra
    {
        [FormerlySerializedAs("IsLocked")]
        [SerializeField] private bool m_Locked = true;
     
        [SerializeField] protected DialogData m_OnLockedDialog;
        [SerializeField] protected AudioClip m_LockedSound;

        [HideInInspector]
        [FormerlySerializedAs("m_LockedDialog")]
        [SerializeField] protected string[] m_LockedDialog_DEPRECATED = { "The door is locked" };


        public bool IsLocked 
        {
            get
            {
                return m_Locked;
            }
            
            protected set
            {
                m_Locked = value;
                SaveState(); // Save the object state manually so it's reflected on the map
            } 
        }

        public UnityEvent OnUnlock;

        protected DoorBase Door;
        protected AudioSource AudioSource;

        private SavableObjectState m_Savable;

        public void SetLocked(bool locked)
        {
            IsLocked = locked;
        }

        protected virtual void Awake()
        {
            AudioSource = GetComponent<AudioSource>();
            Door = GetComponent<DoorBase>();
            m_Savable = GetComponent<SavableObjectState>();
        }

        public virtual void OnTryToUnlock(out bool openImmediately)
        {
            // Just notify the player it's locked, base lock can only be unlocked by direct call to the Unlock function
            if (m_LockedSound)
                AudioSource.PlayOneShot(m_LockedSound);

            UIManager.Get<UIDialog>().Show(m_OnLockedDialog);

            openImmediately = false;
        }

        protected void SaveState()
        {
            m_Savable.SaveState();
        }

        public virtual void OnTryToUnlockFromExit(out bool openImmediately) { openImmediately = false; }

        //-----------------------------------------------------
        // ISavable implementation
        //-----------------------------------------------------

        public string GetSavableData()
        {
            return IsLocked.ToString();
        }

        public void SetFromSavedData(string savedData)
        {
            IsLocked = Convert.ToBoolean(savedData);
        }
    }
}