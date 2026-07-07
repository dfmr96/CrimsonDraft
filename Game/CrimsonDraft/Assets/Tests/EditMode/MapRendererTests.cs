#nullable enable

using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Navigation.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapRendererTests
    {
        [Test]
        public void Generate_buildsVisibleRoomAndDoorGeometry()
        {
            var map = CreateMap();
            var roomStateRegistry = new RoomStateRegistry();
            roomStateRegistry.MarkVisited("room-a");
            var knownMaps = new KnownMapsRegistry();
            knownMaps.MarkKnown("deck-a");
            var pickupRegistry = new PickupRegistry();
            var doorRegistry = new DoorStateRegistry();
            doorRegistry.MarkUnlocked("door-a");

            var renderer = CreateRenderer();

            try
            {
                renderer.Construct(roomStateRegistry, knownMaps, pickupRegistry, doorRegistry);
                renderer.Generate(map, "room-a");

                Assert.GreaterOrEqual(renderer.transform.Find("Content")!.childCount, 3);
                Assert.IsNotNull(renderer.Texture);
            }
            finally
            {
                Object.DestroyImmediate(renderer.gameObject);
                Object.DestroyImmediate(map);
            }
        }

        [Test]
        public void Pan_clampsCameraPositionToMapBounds()
        {
            var map = CreateMap();
            var renderer = CreateRenderer();

            try
            {
                renderer.Construct(new RoomStateRegistry(), new KnownMapsRegistry(), new PickupRegistry(), new DoorStateRegistry());
                renderer.Generate(map, null);

                renderer.Pan(new Vector2(999f, 999f));
                var position = renderer.GetComponentInChildren<CinemachineCamera>().transform.position;

                Assert.LessOrEqual(Mathf.Abs(position.x), map.GridSize.x * map.CellSize * 0.5f + 0.01f);
                Assert.LessOrEqual(Mathf.Abs(position.z), map.GridSize.y * map.CellSize * 0.5f + 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(renderer.gameObject);
                Object.DestroyImmediate(map);
            }
        }

        private static MapData CreateMap()
        {
            var map = ScriptableObject.CreateInstance<MapData>();
            var so = new SerializedObject(map);
            so.FindProperty("sceneName").stringValue = "deck-a";
            so.FindProperty("gridSize").vector2IntValue = new Vector2Int(10, 10);
            so.FindProperty("cellSize").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();

            map.EditorSetBakedContent(
                new System.Collections.Generic.List<MapRoomData>
                {
                    new()
                    {
                        RoomId = "room-a",
                        Polygon = new[]
                        {
                            new Vector2(0f, 0f),
                            new Vector2(2f, 0f),
                            new Vector2(2f, 2f),
                            new Vector2(0f, 2f),
                        },
                        DoorIds = new[] { "door-a" },
                    },
                },
                new System.Collections.Generic.List<MapDoorData>
                {
                    new()
                    {
                        DoorId = "door-a",
                        Size = new Vector2(1f, 0.25f),
                    },
                });

            return map;
        }

        private static MapRenderer CreateRenderer()
        {
            var go = new GameObject("MapRenderer");
            var renderer = go.AddComponent<MapRenderer>();

            var cameraGO = new GameObject("MapCamera");
            cameraGO.transform.SetParent(go.transform, false);
            var vcamGO = new GameObject("MapVirtualCamera");
            vcamGO.transform.SetParent(go.transform, false);

            var so = new SerializedObject(renderer);
            so.FindProperty("mapCamera").objectReferenceValue = cameraGO.AddComponent<Camera>();
            so.FindProperty("mapVirtualCamera").objectReferenceValue = vcamGO.AddComponent<CinemachineCamera>();
            var content = new GameObject("Content").transform;
            content.SetParent(go.transform, false);
            so.FindProperty("contentRoot").objectReferenceValue = content;
            so.FindProperty("renderLayer").intValue = 0;
            so.FindProperty("roomVisitedMaterial").objectReferenceValue = CreateMaterial();
            so.FindProperty("roomNotVisitedMaterial").objectReferenceValue = CreateMaterial();
            so.FindProperty("roomCompletedMaterial").objectReferenceValue = CreateMaterial();
            so.FindProperty("currentRoomMaterial").objectReferenceValue = CreateMaterial();
            so.FindProperty("wallMaterial").objectReferenceValue = CreateMaterial();
            so.FindProperty("doorUnknownMaterial").objectReferenceValue = CreateMaterial();
            so.FindProperty("doorLockedMaterial").objectReferenceValue = CreateMaterial();
            so.FindProperty("doorUnlockedMaterial").objectReferenceValue = CreateMaterial();
            so.ApplyModifiedPropertiesWithoutUndo();

            return renderer;
        }

        private static Material CreateMaterial()
        {
            var shader = Shader.Find("Sprites/Default");
            Assert.IsNotNull(shader, "Sprites/Default shader must exist in the test environment");
            return new Material(shader);
        }
    }
}
