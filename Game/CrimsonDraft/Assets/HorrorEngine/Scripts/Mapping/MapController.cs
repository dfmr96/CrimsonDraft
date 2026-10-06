using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    
    public class MapController : MonoBehaviour, ISavableObjectStateExtra
    {
        public MapData Data;

        private bool m_Visited;
        private MapRoom[] m_Rooms;
        private SavableObjectState m_Savable;

        private static List<MapController> m_Controllers = new List<MapController>();
        
        public static bool Exists
        {
            get { return m_Controllers.Count > 0; }
        }

        public static int Count
        {
            get { return m_Controllers.Count; }
        }

        public static MapController GetCurrent()
        {
            Vector3 playerPos = GameManager.Instance.Player.transform.position;
            foreach(var mapCtrl in m_Controllers)
            {
                if (mapCtrl.GetContainingRoom(playerPos))
                    return mapCtrl;
            }
            return null;
        }

        // --------------------------------------------------------------------

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                transform.position = Vector3.zero;
                transform.rotation = Quaternion.identity;
                transform.localScale = Vector3.one;
            }
        }

        // --------------------------------------------------------------------

        protected void Awake()
        {
            m_Rooms = GetComponentsInChildren<MapRoom>(true);
            m_Savable = GetComponent<SavableObjectState>();

            MessageBuffer<SceneTransitionPostMessage>.Subscribe(OnSceneTransitionPost);
            MessageBuffer<DoorTransitionEndMessage>.Subscribe(OnDoorTransitionEnd);
            MessageBuffer<MapStepCompletedMessage>.Subscribe(OnMapStepCompleted);

            m_Controllers.Add(this);
        }
        
        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            MessageBuffer<SceneTransitionPostMessage>.Unsubscribe(OnSceneTransitionPost);
            MessageBuffer<DoorTransitionEndMessage>.Unsubscribe(OnDoorTransitionEnd);
            MessageBuffer<MapStepCompletedMessage>.Unsubscribe(OnMapStepCompleted);

            m_Controllers.Remove(this);
        }

        // --------------------------------------------------------------------

        private void Start()
        {
            this.InvokeActionNextFrame(() =>
            {
                MarkCurrentRoomVisited();
            });
        }

        // --------------------------------------------------------------------

        public void UpdateContent()
        {
            bool hasMap = GameManager.Instance.Inventory.Maps.Contains(Data);

            MarkCurrentRoomVisited();

            foreach (var room in m_Rooms)
            {
                if (room.CheckIfCompleted())
                    room.TryMarkAs(MapRoomState.Completed);

                if (hasMap && room.State == MapRoomState.Unknown)
                    room.TryMarkAs(MapRoomState.NotVisited);
            }
        }

        // --------------------------------------------------------------------

        private void OnDoorTransitionEnd(DoorTransitionEndMessage msg)
        {
            UpdateContent();
            MarkCurrentRoomVisited();
        }


        // --------------------------------------------------------------------

        private void OnSceneTransitionPost(SceneTransitionPostMessage msg)
        {
            UpdateContent();
            MarkCurrentRoomVisited();
        }

        // --------------------------------------------------------------------

        private void OnMapStepCompleted(MapStepCompletedMessage msg)
        {
            UpdateContent();
        }

        // --------------------------------------------------------------------

        private void MarkCurrentRoomVisited()
        {
            if (!GameManager.Instance.Player)
                return;

            MapRoom room = GetContainingRoom(GameManager.Instance.Player.transform.position);
            if (room)
            {
                room.TryMarkAs(MapRoomState.Visited);
                MarkVisited();
            }
        }

        // --------------------------------------------------------------------

        private MapRoom GetContainingRoom(Vector3 worldPosition)
        {
            foreach (var room in m_Rooms)
            {
                if (room.ContainsWorldPosition(worldPosition))
                {
                    return room;
                }
            }

            return null;
        }

        // --------------------------------------------------------------------

        public void GetTransformInRoom(Transform transform, out Vector3 roomPos, out Quaternion roomRotation)
        {
            MapRoom room = GetContainingRoom(transform.position);
            if (room)
            {
                TransformToRoom(transform, room, out roomPos, out roomRotation);
            }
            else
            {
                roomPos = Vector3.zero;
                roomRotation = Quaternion.identity;
            }
        }

        // --------------------------------------------------------------------

        // Transform a world position to room position (not local) this means room offset, rotation and map scale are applied
        private void TransformToRoom(Transform transform, MapRoom room, out Vector3 roomPos, out Quaternion roomRotation)
        {
            Matrix4x4 roomTRS = Data.GetTRS(room.Offset, room.Rotation, room.Scale, room.ZOrder);
            Vector3 localPos = room.transform.InverseTransformPoint(transform.position);
            roomPos = roomTRS.MultiplyPoint(localPos).ToXZ();
            roomRotation = transform.rotation * roomTRS.rotation;
        }

        // --------------------------------------------------------------------

        private void MarkVisited() 
        {
            m_Visited = true;
            m_Savable.SaveState();
        }

        // --------------------------------------------------------------------
        // ISavable implementation
        // --------------------------------------------------------------------


        public string GetSavableData()
        {
            return m_Visited.ToString();
        }

        public void SetFromSavedData(string savedData)
        {
            m_Visited = Convert.ToBoolean(savedData);
        }
    }
}