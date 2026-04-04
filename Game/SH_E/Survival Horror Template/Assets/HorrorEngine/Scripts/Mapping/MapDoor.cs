using System;
using UnityEngine;

namespace HorrorEngine
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(ObjectUniqueId))]
    public abstract class MapElement : MonoBehaviour
    {
        public Vector2 Offset;
        public float Rotation;
        public float ZOrder;
        public Vector3 Scale = Vector3.one;

        // --------------------------------------------------------------------

#if UNITY_EDITOR
        protected virtual void Update()
        {
            transform.localRotation = Quaternion.identity;
        }
#endif
    }

    public enum MapDoorState
    {
        Unknown = 0,
        Locked = 1,
        //CanBeUnlocked= 2, // TODO
        Unlocked = 3,
    }

    
    public class MapDoor :  MapElement, ISavableObjectStateExtra
    {
        public string Name;
        public DoorBase Door;
        public Vector2 Size = new Vector2(1f,0.25f);

        public MapDoorState State { get; private set; }

        private SavableObjectState m_Savable;

        // --------------------------------------------------------------------

        private void Awake()
        {
            if (!Door)
                Debug.LogWarning("MapDoor.Door reference not set", gameObject);

            m_Savable = GetComponent<SavableObjectState>();
        }

        // --------------------------------------------------------------------

        private void Start()
        {
            if (Door)
            {
                Door.OnLocked.AddListener(OnDoorLocked);
                Door.OnOpened.AddListener(OnDoorOpened);

                SceneDoor sceneDoor = Door as SceneDoor;
                if (sceneDoor && sceneDoor.Exit)
                {
                    sceneDoor.Exit.OnLocked.AddListener(OnDoorLocked);
                    sceneDoor.Exit.OnOpened.AddListener(OnDoorOpened);
                }
            }
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            if (Door)
            {
                Door.OnLocked.RemoveListener(OnDoorLocked);
                Door.OnOpened.RemoveListener(OnDoorOpened);

                SceneDoor sceneDoor = Door as SceneDoor;
                if (sceneDoor && sceneDoor.Exit)
                {
                    sceneDoor.Exit.OnLocked.RemoveListener(OnDoorLocked);
                    sceneDoor.Exit.OnOpened.RemoveListener(OnDoorOpened);
                }
            }
        }

        // --------------------------------------------------------------------

        private void OnDoorLocked()
        {
            MarkAs(MapDoorState.Locked);
        }

        // --------------------------------------------------------------------

        private void OnDoorOpened()
        {
            MarkAs(MapDoorState.Unlocked);
        }

        // --------------------------------------------------------------------

        public void MarkAs(MapDoorState state)
        {
            State = state;
            m_Savable.SaveState(); // Force an immediate save of the door state so the map reflects the change
        }



        // --------------------------------------------------------------------
        // ISavable implementation
        // --------------------------------------------------------------------

        public string GetSavableData()
        {
            return State.ToString();
        }

        public void SetFromSavedData(string savedData)
        {
            State = Enum.Parse<MapDoorState>(savedData);
        }
    }
}