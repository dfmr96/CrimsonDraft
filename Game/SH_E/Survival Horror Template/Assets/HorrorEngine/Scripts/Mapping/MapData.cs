using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    [System.Serializable]
    public class ShapeData
    {
        public Vector3[] Points;
    }

    [Serializable]
    public class MapElementTransform
    {
        public Vector2 Offset;
        public Vector3 Scale;
        [Tooltip("Elements with higher ZOrder will show on top of those with lower order")]
        public float ZOrder;
        public float Rotation;
    }

    [Serializable]
    public class MapImageSerialized
    {
        public MapElementTransform Transform;
        public Texture2D Texture;
        public string UniqueId;
        public bool DefaultState;

        public bool GetState()
        {
            if (string.IsNullOrEmpty(UniqueId))
                return DefaultState;

            if (ObjectStateManager.Instance.GetState(UniqueId, out ObjectStateSaveDataEntry savedObjectState))
            {
                return savedObjectState.Active;
            }

            return DefaultState;
        }
    }

    [Serializable]
    public class MapDetailsSerializedData
    {
        public MapElementTransform Transform;
        public ShapeData Shape;
        public ShapeCreationProcess CreationProcess;
        public string UniqueId;
        public bool DefaultState;

        public bool GetState()
        {
            if (string.IsNullOrEmpty(UniqueId))
                return DefaultState;

            if (ObjectStateManager.Instance.GetState(UniqueId, out ObjectStateSaveDataEntry savedObjectState))
            {
                return savedObjectState.Active;
            }

            return DefaultState;
        }
    }

    [Serializable]
    public class MapRoomSerializedData
    {
        public string Name; // Not a UI/game text, used for gameobjects
        public string UniqueId;
        public MapElementTransform Transform;
        public ShapeData[] Shapes;
        public List<string> LinkedElements;
        public MapDetailsSerializedData[] Details;
        public MapImageSerialized[] Images;

        public MapRoomState GetState()
        {
             if (ObjectStateManager.Instance.GetState(UniqueId, out ObjectStateSaveDataEntry savedObjectState))
             {
                string componentData = savedObjectState.GetComponentData<MapRoom>();
                if (!string.IsNullOrEmpty(componentData))
                    return Enum.Parse<MapRoomState>(componentData);
             }

            return MapRoomState.Unknown;
        }

    }

    [Serializable]
    public class MapDoorSerializedData
    {
        public string Name; // Not a UI/game text, used for gameobjects
        public string UniqueId;
        public MapElementTransform Transform;
        public Vector2 Size;

        public MapDoorState GetState()
        {
            if (ObjectStateManager.Instance.GetState(UniqueId, out ObjectStateSaveDataEntry savedObjectState))
            {
                string componentData = savedObjectState.GetComponentData<MapDoor>();
                if (!string.IsNullOrEmpty(componentData))
                    return Enum.Parse<MapDoorState>(componentData);
            }

            return MapDoorState.Unknown;
        }

    }

    [CreateAssetMenu(menuName = "Horror Engine/Mapping/MapData")]
    public class MapData : Register
    {
        public string ControllerUniqueId;

        public LocalizableText Name;
        public LocalizableText Abbreviation;

        // TODO - Remove eventually
        [FormerlySerializedAs("Name")]
        [HideInInspector]
        public string Name_DEPRECATED = "NoName";

        [FormerlySerializedAs("Abbreviation")]
        [HideInInspector]
        public string Abbreviation_DEPRECATED = "";
        // ----


        public Vector2Int Size = new Vector2Int(25, 25);
        public int CellSize = 50;
        public float GlobalScale = 10f;
        public MapDataSet MapSet;
        public List<MapRoomSerializedData> Rooms = new List<MapRoomSerializedData>();
        public List<MapDoorSerializedData> Doors = new List<MapDoorSerializedData>();

        public Matrix4x4 GetTRS(Vector2 offset, float rotation, Vector3 scale, float zOrder)
        {
            return Matrix4x4.TRS(new Vector3(offset.x, zOrder, -offset.y), Quaternion.Euler(0, -rotation, 0), GlobalScale * scale);
        }

        public bool GetVisited()
        {
            if (ObjectStateManager.Instance.GetState(ControllerUniqueId, out ObjectStateSaveDataEntry savedObjectState))
            {
                string componentData = savedObjectState.GetComponentData<MapController>();
                if (!string.IsNullOrEmpty(componentData))
                    return Convert.ToBoolean(componentData);
            }

            return false;
        }

        public bool IsKnownByPlayer()
        {
            return GetVisited() || GameManager.Instance.Inventory.Maps.Contains(this);
        }
        
#if UNITY_EDITOR
        public void EditorOnly_MigrateUnlocalizedData()
        {
            bool dirty = false;
            if (!string.IsNullOrEmpty(Name_DEPRECATED))
            {
                Name.Unlocalized = Name_DEPRECATED;
                Name_DEPRECATED = "";
                dirty = true;
            }

            if (!string.IsNullOrEmpty(Abbreviation_DEPRECATED))
            {
                Abbreviation.Unlocalized = Abbreviation_DEPRECATED;
                Abbreviation_DEPRECATED = "";
                dirty = true;
            }

            if (dirty)
            {
                var context = this;
                EditorApplication.delayCall += () => { EditorUtility.SetDirty(context); };
            }
        }
#endif
    }
}