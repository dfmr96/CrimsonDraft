using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class MapRenderer : SingletonBehaviour<MapRenderer>
    {
        [SerializeField] private Transform m_MapContent;
        [SerializeField] private Transform m_MapTarget;
        [SerializeField] private Transform m_MapPlayer;
        [SerializeField] private LayerMask m_Mask;
        [SerializeField] private float m_CameraInterpolationSpeed = 10f;
        [SerializeField] private float m_RenderTextureScale = 1f;
        [SerializeField] private FilterMode m_RenderTextureFilterMode = FilterMode.Bilinear;

        [Header("Visuals")]
        public Material GridMaterial;

        public ShapeCreationProcess RoomIncompletedSP;
        public ShapeCreationProcess RoomCompletedSP;
        public ShapeCreationProcess RoomNotVisitedSP;
        public ShapeCreationProcess WallsSP;

        public Material DoorUnknownMaterial;
        public Material DoorUnlockedMaterial;
        public Material DoorLockedMaterial;

        public Material ImageMaterial;

        private Camera m_Cam;
        private MapData m_Map;
        private RenderTexture m_RenderTexture;
        
        public RenderTexture RenderTexture => m_RenderTexture;

        // Height layers
        private readonly int m_GridHeight = -1;
        //private readonly int m_RoomsHeight = 0;
        private readonly int m_DetailingHeight = 1;
        private readonly int m_WallsHeight = 2;
        private readonly int m_DoorsHeight = 3;
        private readonly int m_PlayerHeight = 5;
        private readonly int m_TargetHeight = 9;
        private readonly int m_CameraHeight = 20;

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();
            m_Cam = GetComponentInChildren<Camera>();
            Debug.Assert(m_Cam, "MapRenderer does not contain a camera");
        }

        // --------------------------------------------------------------------

        private void Start()
        {
            m_RenderTexture = new RenderTexture((int)(Screen.width* m_RenderTextureScale), (int)(Screen.height* m_RenderTextureScale), 16);
            m_RenderTexture.filterMode = m_RenderTextureFilterMode;
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            Vector3 targetPos = new Vector3(m_MapTarget.position.x, m_Cam.transform.position.y, m_MapTarget.position.z);
            m_Cam.transform.position = Vector3.Lerp(m_Cam.transform.position, targetPos, Time.unscaledDeltaTime * m_CameraInterpolationSpeed);
        }

        // --------------------------------------------------------------------

        private int GetLayer()
        {
           return (int)(Mathf.Log((uint)m_Mask.value, 2));
        }

        // --------------------------------------------------------------------

        public void GenerateAll(MapData map)
        {
            m_Map = map;

            // Clear map
            for (int i = 0; i < m_MapContent.childCount; ++i)
            {
                if (Application.isPlaying)
                    Destroy(m_MapContent.GetChild(i).gameObject);
                else
                    DestroyImmediate(m_MapContent.GetChild(i).gameObject);
            }

            // Generate grid
            var gridGen = GetComponentInChildren<GridGenerator>();
            gridGen.gameObject.layer = GetLayer();
            gridGen.Size = map.Size;
            gridGen.Generate(map.Size.x, map.Size.y);
            gridGen.transform.localScale = new Vector3(1, 1, -1) * map.CellSize;
            gridGen.transform.rotation = Quaternion.Euler(-90, 0, 0);
            gridGen.transform.position = new Vector3(0, m_GridHeight, 0);
            gridGen.GetComponent<MeshRenderer>().sharedMaterial = GridMaterial;

            // Generate camera
            m_Cam = GetComponentInChildren<Camera>();
            m_Cam.targetTexture = m_RenderTexture;
            m_Cam.transform.position = new Vector3(map.CellSize * map.Size.x * 0.5f, m_CameraHeight, -map.CellSize * map.Size.y * 0.5f);
            m_Cam.cullingMask = m_Mask;

            Vector3 center = GetCenter();
            center.y = m_TargetHeight;
            m_MapTarget.position = center;

            // ---------------------------------------------- Map Specific

            HashSet<string> m_VisibleElements = new HashSet<string>();

            // Generate rooms
            var rooms = map.Rooms;
            for (int i = 0; i < rooms.Count; ++i)
            {

                if (GenerateRoom( rooms[i]))
                {
                    foreach (var elm in rooms[i].LinkedElements)
                        m_VisibleElements.Add(elm);
                }
            }

            // Generate rooms
            var doors = map.Doors;
            for (int i = 0; i < doors.Count; ++i)
            {
                if (m_VisibleElements.Contains(doors[i].UniqueId))
                    GenerateDoor( doors[i]);
            }

        }
        // --------------------------------------------------------------------

        private bool GenerateRoom(MapRoomSerializedData room)
        {
            MapRoomState state = room.GetState();
            bool hasMap = GameManager.Instance.Inventory.Maps.Contains(m_Map);
            if (state == MapRoomState.Unknown)
            {
                if (hasMap) // Fallback for when the player has the map but hasn't even visited the level
                    state = MapRoomState.NotVisited;
                else
                    return false;
            }

            ShapeCreationProcess roomSP = RoomNotVisitedSP;
            
            if (state == MapRoomState.Visited)
                roomSP = RoomIncompletedSP;
            else if (state == MapRoomState.Completed)
                roomSP = RoomCompletedSP;

            int layer = GetLayer();
            Matrix4x4 roomTRS = m_Map.GetTRS(room.Transform.Offset, room.Transform.Rotation, room.Transform.Scale, room.Transform.ZOrder);

            GameObject roomGO = roomSP.Create(room.Name, m_MapContent, layer, room.Shapes, roomTRS);

            var walls = WallsSP.Create(room.Name, m_MapContent, layer, room.Shapes, roomTRS);
            walls.transform.localPosition = new Vector3(walls.transform.localPosition.x, m_WallsHeight, walls.transform.localPosition.z);

            // Draw Details
            int index = 0;
            foreach(var details in room.Details)
            {
                var dTRS = roomTRS * Matrix4x4.TRS(new Vector3(details.Transform.Offset.x, details.Transform.ZOrder * 0.01f, details.Transform.Offset.y), Quaternion.Euler(0, details.Transform.Rotation, 0), details.Transform.Scale);
                var newDetails = details.CreationProcess.Create(room.Name + "_Detailing_" + index, m_MapContent, layer, new ShapeData[]{ details.Shape }, dTRS);
                newDetails.transform.localPosition = new Vector3(newDetails.transform.localPosition.x, m_DetailingHeight, newDetails.transform.localPosition.z);

                bool isActive = details.GetState();
                newDetails.gameObject.SetActive(isActive);

                ++index;
            }

            // Draw Images
            index = 0;
            foreach (var image in room.Images)
            {
                var dTRS = roomTRS * Matrix4x4.TRS(new Vector3(image.Transform.Offset.x, image.Transform.ZOrder * 0.01f, image.Transform.Offset.y), Quaternion.Euler(0, image.Transform.Rotation, 0), image.Transform.Scale);
                GameObject newImage = GameObject.CreatePrimitive(PrimitiveType.Quad);
                newImage.name = room.Name + "_Image_" + index;
                newImage.layer = layer;
                newImage.transform.SetParent(m_MapContent);
                newImage.transform.localRotation = dTRS.rotation * Quaternion.Euler(90, 0, 0);
                newImage.transform.localScale = dTRS.MultiplyVector(Vector3.one);
                newImage.transform.localPosition = dTRS.MultiplyPoint(Vector3.zero);
                
                Material mat = new Material(ImageMaterial);
                mat.SetTexture("_MainTex", image.Texture);
                newImage.GetComponent<Renderer>().material = mat;

                bool isActive = image.GetState();
                newImage.gameObject.SetActive(isActive);

                ++index;
            }

            return true;
        }

        // --------------------------------------------------------------------

        private void GenerateDoor( MapDoorSerializedData door)
        {
            GameObject go = new GameObject(door.Name + "_Mesh");
            go.transform.parent = m_MapContent;
            go.transform.position = new Vector3(0, m_DoorsHeight, 0);
            go.layer = GetLayer();

            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.receiveShadows = false;

            MapDoorState state = door.GetState();
            if (state == MapDoorState.Unknown)
                meshRenderer.sharedMaterial = DoorUnknownMaterial;
            else if (state == MapDoorState.Locked)
                meshRenderer.sharedMaterial = DoorLockedMaterial;
            else if (state == MapDoorState.Unlocked)
                meshRenderer.sharedMaterial = DoorUnlockedMaterial;

            Mesh mesh = new Mesh();
            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-door.Size.x, 0, -door.Size.y),
                new Vector3(door.Size.x, 0,-door.Size.y),
                new Vector3(door.Size.x, 0,door.Size.y),
                new Vector3(-door.Size.x, 0,door.Size.y)
            };

            Matrix4x4 t = m_Map.GetTRS(door.Transform.Offset, door.Transform.Rotation, door.Transform.Scale, door.Transform.ZOrder);
            for (int i = 0; i < vertices.Length; ++i)
            {
                vertices[i] = t.MultiplyPoint(vertices[i]);
            }
            mesh.vertices = vertices;

            int[] tris = new int[6]
            {
                // lower left triangle
                0, 2, 1,
                // upper right triangle
                0, 3, 2
            };
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            meshFilter.mesh = mesh;
        }

        // --------------------------------------------------------------------

        public void MoveTarget(Vector2 movement)
        {
            m_MapTarget.Translate(new Vector3(movement.x, 0, movement.y), Space.World);
            ClampTargetToSafeArea();
        }

        // --------------------------------------------------------------------

        public void SetTargetPosition(Vector2 pos)
        {
            m_MapTarget.position = new Vector3(pos.x, m_TargetHeight, pos.y);
            ClampTargetToSafeArea();
        }

        // --------------------------------------------------------------------

        private void ClampTargetToSafeArea()
        {
            Vector3 center = GetCenter();
            Vector3 safeArea = GetSafeArea() * 0.5f;
            Vector3 targetPos = m_MapTarget.position;

            float left = center.x - safeArea.x;
            float right= center.x + safeArea.x;
            float top = center.z - safeArea.z;
            float bottom = center.z + safeArea.z;

            if (targetPos.x < left) targetPos.x = left;
            if (targetPos.x > right) targetPos.x = right;
            if (targetPos.z < top) targetPos.z = top;
            if (targetPos.z > bottom) targetPos.z = bottom;

            m_MapTarget.position = targetPos;
        }

        // --------------------------------------------------------------------

        public void SetPlayerPositionAndRotation(Vector2 position, Quaternion rotation)
        {
            m_MapPlayer.position = new Vector3(position.x, m_PlayerHeight, position.y);
            m_MapPlayer.rotation = rotation;
        }


        // --------------------------------------------------------------------

        public void SetPlayerVisible(bool visible)
        {
            m_MapPlayer.gameObject.SetActive(visible);
        }

        // --------------------------------------------------------------------

        private Vector3 GetCenter()
        {
            return new Vector3(m_Map.CellSize * m_Map.Size.x * 0.5f, 0, -m_Map.CellSize * m_Map.Size.y * 0.5f);
        }

        private Vector3 GetSafeArea()
        {
            float height = m_Cam.orthographicSize * 2;
            float width = height * m_Cam.aspect;
            return new Vector3(m_Map.Size.x * m_Map.CellSize - width, 0, m_Map.Size.y * m_Map.CellSize - height);
        }

        // --------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (m_Map)
            {
                Gizmos.DrawWireCube(GetCenter(), GetSafeArea());
            }
        }
    }
}