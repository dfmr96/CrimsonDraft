using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorEngine
{
    [InitializeOnLoad]
    public static class MapSerializer
    {
        static MapSerializer()
        {
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // --------------------------------------------------------------------

        static void OnSceneSaved(Scene scene)
        {
            Serialize();
        }

        // --------------------------------------------------------------------

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
                Serialize();
        }

        // --------------------------------------------------------------------

        static void Serialize()
        {
            MapController[] maps = Object.FindObjectsByType<MapController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach(var map in maps)
            {
                Debug.Assert(map.Data, "Map data couldn't be serialized. No data assigned on MapController");
                SerializeAndSaveMap(map);
            }
        }

        // --------------------------------------------------------------------

        public static void SerializeAndSaveMap(MapController mapCtrl)
        {
            MapData data = mapCtrl.Data;

            data.ControllerUniqueId = mapCtrl.GetComponent<ObjectUniqueId>().Id;
            data.Rooms.Clear();
            data.Doors.Clear();

            var doors = mapCtrl.GetComponentsInChildren<MapDoor>(true);
            foreach (var door in doors)
            {
                data.Doors.Add(new MapDoorSerializedData()
                {
                    Name = door.Name,
                    UniqueId = door.GetComponent<ObjectUniqueId>().Id,
                    Size = door.Size,
                    Transform = new MapElementTransform()
                    {
                        Offset = door.Offset,
                        Rotation = door.Rotation,
                        Scale = door.Scale,
                        ZOrder = door.ZOrder
                    }
                });
            }

            var rooms = mapCtrl.GetComponentsInChildren<MapRoom>(true);
            foreach (var room in rooms)
            {

                Shape[] shapes = room.GetComponents<Shape>();
                ShapeData[] shapesData = new ShapeData[shapes.Length];

                for (int i = 0; i < shapes.Length; ++i)
                {
                    shapesData[i] = new ShapeData()
                    {
                        Points = shapes[i].Points.ToArray()
                    };
                }

                List<string> linkedElements = new List<string>();
                foreach (var l in room.LinkedElements)
                {
                    if (l && l.TryGetComponent(out ObjectUniqueId objId))
                        linkedElements.Add(objId.Id);
                }

                MapDetailingShape[] detailing = room.GetComponentsInChildren<MapDetailingShape>(true);
                MapDetailsSerializedData[] detailingSerialized = new MapDetailsSerializedData[detailing.Length];
                for (int i =0; i < detailing.Length; ++i)
                {
                    MapDetailingShape details = detailing[i];
                    if (details.TryGetComponent(out Shape shape)) 
                    {
                        var savable = details.GetComponent<MapElementSavable>();
                        detailingSerialized[i] = new MapDetailsSerializedData()
                        {
                            Transform = new MapElementTransform()
                            {
                                Offset = details.transform.localPosition.ToXZ(),
                                Rotation = details.transform.localRotation.eulerAngles.y,
                                Scale = details.transform.localScale,
                                ZOrder = details.transform.localPosition.y
                            },
                            Shape = new ShapeData()
                            {
                                Points = shape.Points.ToArray()
                            },
                            DefaultState = details.isActiveAndEnabled,
                            CreationProcess = details.CreationProcess,
                            UniqueId = savable ? savable.GetId() : ""
                        };
                    }
                }

                MapImage[] images = room.GetComponentsInChildren<MapImage>(true);
                MapImageSerialized[] imagesSerialized = new MapImageSerialized[images.Length];
                for (int i = 0; i < images.Length; ++i)
                {
                    MapImage image = images[i];
                    var savable = image.GetComponent<MapElementSavable>();
                    imagesSerialized[i] = new MapImageSerialized()
                    {
                        Transform = new MapElementTransform()
                        {
                            Offset = image.transform.localPosition.ToXZ(),
                            Rotation = image.transform.localRotation.eulerAngles.y,
                            Scale = image.transform.localScale,
                            ZOrder = image.transform.localPosition.y
                        },
                        Texture = image.Texture,
                        DefaultState = image.isActiveAndEnabled,
                        UniqueId = savable ? savable.GetId() : ""
                    };
                    
                }

                data.Rooms.Add(new MapRoomSerializedData()
                {
                    UniqueId = room.GetComponent<ObjectUniqueId>().Id,
                    Name = room.Name,
                    Transform = new MapElementTransform()
                    {
                        Offset = room.Offset,
                        Rotation = room.Rotation,
                        Scale = room.Scale,
                        ZOrder = room.ZOrder
                    },
                    Shapes = shapesData,
                    LinkedElements = linkedElements,
                    Details = detailingSerialized,
                    Images = imagesSerialized
                });
            }

            EditorUtility.SetDirty(data);
        }
    }
}