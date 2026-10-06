using UnityEngine;
using UnityEditor;

namespace HorrorEngine
{
    public class MappingEditorWindow : EditorWindow
    {
        public Texture2D ToolWindowIcon;
        public GameObject MapRoomPrefab;
        public GameObject MapDoorPrefab;
        public GameObject MapDetailsLinePrefab;
        public GameObject MapDetailsShapePrefab;
        //public GameObject MapDetailsStairsPrefab;
        public GameObject MapImagePrefab;
        public Texture2D RoomIcon;
        public Texture2D DoorIcon;
        public Material ImageMaterial;

        private Vector2 m_ScrollPos;
        private Vector2 m_ScrollPosInspector;

        private bool m_Moving;
        private bool m_Rotating;

        private MapController m_SelectedMap;
        private MapElement m_Selected;
        private Material m_Mat;

        private bool m_EditingMapDetails;
        private float m_InspectorWidth = 250;

        // --------------------------------------------------------------------

        public static void Init(MapController selected)
        {
            MappingEditorWindow window = (MappingEditorWindow)EditorWindow.GetWindow(typeof(MappingEditorWindow));
            window.Show();
            window.rootVisualElement.focusable = true;
            window.m_SelectedMap = selected;
        }


        // --------------------------------------------------------------------

        void OnGUI()
        {
            SetupMaterial();

            titleContent = new GUIContent("HE Map Editor", ToolWindowIcon);
            UpdateKeyboardEvents();

            if (m_SelectedMap && m_SelectedMap.Data)
            {
                MapData map = m_SelectedMap.Data;
                DrawGrid(map.Size.x, map.Size.y, map.CellSize, new Color(0.35f, 0.35f, 0.35f, 1f));

                GameObject selectedGO = Selection.activeObject as GameObject;
                m_Selected = selectedGO ? selectedGO.GetComponent<MapElement>() : null;

                var rooms = m_SelectedMap.GetComponentsInChildren<MapRoom>(true);
                for (int i = 0; i < rooms.Length; ++i)
                {
                    DrawRoom(m_SelectedMap, rooms[i], m_Selected == rooms[i]);
                }

                var doors = m_SelectedMap.GetComponentsInChildren<MapDoor>(true);
                for (int i = 0; i < doors.Length; ++i)
                {
                    DrawDoor(m_SelectedMap, doors[i], m_Selected == doors[i]);
                }

                // Images need to go last since GUI.DrawTexture seems to interfere with other graphic GL calls
                foreach (var room in rooms)
                {
                    Matrix4x4 trs = GetElementTRS(m_SelectedMap.Data, room);
                    DrawImages(m_SelectedMap, room);
                }

                // This needs to go before the inspector so new items are already in the arrays
                if (m_Selected)
                {
                    UpdateTransformOps();
                    DrawSelectedProperties();
                }

                int defaultFontSize = GUI.skin.label.fontSize;
                GUI.skin.label.fontSize = 32;
                GUI.Label(new Rect(m_InspectorWidth + 10, 10, 1000, 50), map.Name);
                
                GUI.skin.label.fontSize = 42;
                GUI.Label(new Rect(m_InspectorWidth + 10, 50, 1000, 50), map.Abbreviation);
                GUI.skin.label.fontSize = defaultFontSize;

                DrawInspector(m_SelectedMap, rooms, doors);

            }
            else
            {
                float w = Mathf.Max(position.width * 0.5f, 200f);
                float h = Mathf.Max(position.height * 0.25f, 100f);
                GUILayout.BeginArea(new Rect(position.width * 0.5f - w * 0.5f, position.height * 0.5f - h * 0.5f, w, h));
                GUILayout.BeginVertical();

                m_SelectedMap = FindFirstObjectByType<MapController>();
                if (!m_SelectedMap)
                {
                    EditorGUILayout.HelpBox("There is no MapController in the scene", MessageType.Error);
                    if (GUILayout.Button("Create"))
                    {
                        NewMapWizard.ShowWindow();
                    }
                }
                else if (!m_SelectedMap.Data)
                {
                    EditorGUILayout.HelpBox("The MapController has no Data", MessageType.Error);
                    if (GUILayout.Button("Create"))
                    {
                        NewMapWizard.ShowWindow();
                    }

                    int pickMapDataControlI = EditorGUIUtility.GetControlID(FocusType.Passive) + 100;
                    if (GUILayout.Button("Select"))
                    {
                        EditorGUIUtility.ShowObjectPicker<MapData>(null, false, "t:MapData", pickMapDataControlI);
                    }

                    if (Event.current.commandName == "ObjectSelectorUpdated")
                    {
                        int controlId = EditorGUIUtility.GetObjectPickerControlID();
                        if (controlId == pickMapDataControlI)
                        {
                            MapData selectedMap = EditorGUIUtility.GetObjectPickerObject() as MapData;
                            if (selectedMap)
                            {
                                m_SelectedMap.Data = selectedMap;
                                EditorUtility.SetDirty(m_SelectedMap);
                            }
                        }
                    }
                }
                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }

        // --------------------------------------------------------------------

        private void SetupMaterial()
        {
            // Fixes Mac error "Metal: Vertex shader missing buffer binding at index 0"
            // https://issuetracker.unity3d.com/issues/macos-metal-cannot-write-to-unused-constant-buffer-dot-error-is-thrown-when-using-unityengine-dot-gl-inside-onscenegui
            if (m_Mat == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                m_Mat = new Material(shader);
                m_Mat.hideFlags = HideFlags.HideAndDontSave;
            }
            m_Mat.SetPass(0);
        }

        // --------------------------------------------------------------------

        private void UpdateKeyboardEvents()
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {

                GUI.FocusControl(null);
                //e.Use();
            }
            if (e.type == EventType.MouseDrag && e.button == 2)
            {
                m_ScrollPos += e.delta;
                e.Use();
            }

            if (GUIUtility.keyboardControl == 0)
            {
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.G)
                {
                    GUI.FocusControl(null);
                    m_Moving = true;
                    e.Use();
                }
                else if (e.type == EventType.KeyUp && e.keyCode == KeyCode.G)
                {
                    GUI.FocusControl(null);
                    m_Moving = false;
                    e.Use();
                }
                else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
                {
                    GUI.FocusControl(null);
                    m_Rotating = true;
                    e.Use();
                }
                else if (e.type == EventType.KeyUp && e.keyCode == KeyCode.R)
                {
                    GUI.FocusControl(null);
                    m_Rotating = false;
                    e.Use();
                }
                else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.A)
                {
                    GUI.FocusControl(null);
                    m_ScrollPos = Vector2.zero;
                    e.Use();
                }
            }
        }

        // --------------------------------------------------------------------

        private void UpdateTransformOps()
        {

            Vector2 scrolledMousePos = Event.current.mousePosition - m_ScrollPos;
            if (m_Moving)
            {
                m_Selected.Offset = scrolledMousePos;
                Repaint();
                EditorUtility.SetDirty(m_Selected);
            }
            else if (m_Rotating)
            {
                float angle = Vector2.SignedAngle(Vector2.up, scrolledMousePos - m_Selected.Offset);
                m_Selected.Rotation = -Mathf.RoundToInt(angle / 5.0f) * 5; // Round to neares 5
                Repaint();
                EditorUtility.SetDirty(m_Selected);
            }

        }

        // --------------------------------------------------------------------

        private void DrawSelectedProperties()
        {
            if (m_Selected is MapRoom)
            {
                DrawSelectedRoomProperties(m_Selected as MapRoom);
            }

            if (m_Selected is MapDoor)
            {
                DrawSelectedDoorProperties(m_Selected as MapDoor);
            }

            GUI.Label(new Rect(m_InspectorWidth + 10, 90, 300, 50), "Hold [G] for moving");
            GUI.Label(new Rect(m_InspectorWidth + 10, 110, 300, 50), "Hold [R] for rotating");

            if (m_Selected.Scale != m_Selected.transform.lossyScale)
            {
                m_Selected.Scale = m_Selected.transform.lossyScale;
                EditorUtility.SetDirty(m_Selected);
            }
        }

        // --------------------------------------------------------------------

        private void DrawSelectedRoomProperties(MapRoom room)
        {
            GUIStyle propsStyle = new GUIStyle();
            propsStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.75f));

            GUILayout.BeginArea(new Rect(m_InspectorWidth + 20, position.height - 190, 300, 200));
            GUILayout.BeginVertical(propsStyle);

            GUILayout.Label("Room", EditorStyles.boldLabel);

            GUILayout.Space(10);

            EditorGUI.BeginChangeCheck();
            room.Name = EditorGUILayout.TextField("Name", room.Name);
            room.Offset = EditorGUILayout.Vector2Field("Offset", room.Offset);
            room.Rotation = EditorGUILayout.FloatField("Rotation", room.Rotation);
            room.ZOrder = EditorGUILayout.FloatField("ZOrder", room.ZOrder);
            if (EditorGUI.EndChangeCheck())
            {
                room.gameObject.name = "MapRoom " + room.Name;
                Undo.RecordObject(room, "Changed room properties");
                EditorUtility.SetDirty(room);
            }
            GUILayout.Label("Details", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("+ Shape"))
            {
                var detail = PrefabUtility.InstantiatePrefab(MapDetailsShapePrefab, room.transform) as GameObject;
                detail.transform.localPosition = Vector3.zero;
                Selection.activeObject = detail;
            }

            if (GUILayout.Button("+ Line"))
            {
                var detail = PrefabUtility.InstantiatePrefab(MapDetailsLinePrefab, room.transform) as GameObject;
                detail.transform.localPosition = Vector3.zero;
                Selection.activeObject = detail;
            }

            if (GUILayout.Button("+ Image"))
            {
                var image = PrefabUtility.InstantiatePrefab(MapImagePrefab, room.transform) as GameObject;
                image.transform.localPosition = Vector3.zero;
                Selection.activeObject = image;
            }
            /* 
            if (GUILayout.Button("+ Stairs"))
            {
                var shape = PrefabUtility.InstantiatePrefab(MapDetailsStairsPrefab) as GameObject;
                shape.transform.SetParent(room.transform);
                shape.transform.localPosition = Vector3.zero;
            }
            if (GUILayout.Button("+ Circle"))
            {
                var shape = PrefabUtility.InstantiatePrefab(MapDetailsStairsPrefab) as GameObject;
                shape.transform.SetParent(room.transform);
                shape.transform.localPosition = Vector3.zero;
            }
            */
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // --------------------------------------------------------------------

        private void DrawSelectedDoorProperties(MapDoor door)
        {
            GUIStyle propsStyle = new GUIStyle();
            propsStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.75f));

            GUILayout.BeginArea(new Rect(m_InspectorWidth + 20, position.height - 180, 300, 200));
            GUILayout.BeginVertical(propsStyle);

            GUILayout.Label("Door", EditorStyles.boldLabel);

            GUILayout.Space(10);

            EditorGUI.BeginChangeCheck();
            door.Name = EditorGUILayout.TextField("Name", door.Name);
            door.Offset = EditorGUILayout.Vector2Field("Offset", door.Offset);
            door.Size = EditorGUILayout.Vector2Field("Size", door.Size);
            door.Rotation = EditorGUILayout.FloatField("Rotation", door.Rotation);
            door.ZOrder = EditorGUILayout.FloatField("ZOrder", door.ZOrder);
            if (EditorGUI.EndChangeCheck())
            {
                door.gameObject.name = "MapDoor " + door.Name;
                Undo.RecordObject(door, "Changed door properties");
                EditorUtility.SetDirty(door);
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // --------------------------------------------------------------------

        private void DrawInspector(MapController mapCtrl, MapRoom[] rooms, MapDoor[] doors)
        {
            MapData map = mapCtrl.Data;

            GUIStyle inspectorStyle = new GUIStyle();
            inspectorStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.8f));

            GUIStyle detailsStyle = new GUIStyle();
            detailsStyle.normal.background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.15f, 1f));
            detailsStyle.margin = new RectOffset(5, 5, 5, 5);
            detailsStyle.border = new RectOffset(2, 2, 2, 2);

            GUIStyle selectedStyle = new GUIStyle(detailsStyle);
            selectedStyle.normal.background = MakeTex(2, 2, new Color(0f, 1f, 0f, 0.5f));

            GUILayout.BeginArea(new Rect(0, 0, m_InspectorWidth, position.height), inspectorStyle);

            m_ScrollPosInspector = EditorGUILayout.BeginScrollView(m_ScrollPosInspector);

            // ------------- Map selector
            MapController[] mapCtrls = FindObjectsByType<MapController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            GUILayout.BeginVertical();
            GUILayout.Label("Maps", EditorStyles.boldLabel);

            GUILayout.Space(10);

            for (int i = 0; i < mapCtrls.Length; ++i)
            {
                GUILayout.BeginHorizontal(m_SelectedMap == mapCtrls[i] ? selectedStyle : detailsStyle, GUILayout.Height(24));
                GUILayout.Box(ToolWindowIcon, GUILayout.Width(24), GUILayout.Height(16));
                string mapName = mapCtrls[i].Data ? mapCtrls[i].Data.Name : mapCtrls[i].name;

                if (GUILayout.Button(mapName))
                {
                    if (m_SelectedMap != mapCtrls[i])
                    {
                        m_EditingMapDetails = true;
                        m_SelectedMap = mapCtrls[i];
                    }
                    else
                    {
                        m_EditingMapDetails = !m_EditingMapDetails;
                    }
                    Selection.activeObject = mapCtrls[i].gameObject;
                }

                GUILayout.EndHorizontal();


                if (m_SelectedMap == mapCtrls[i] && m_EditingMapDetails)
                {
                    DrawMapDetails(m_SelectedMap, detailsStyle);
                }
            }

            GUILayout.EndVertical();

           

            GUILayout.Space(20);


            // ----------------- Rooms
            GUILayout.BeginVertical();
            GUILayout.Label("Rooms", EditorStyles.boldLabel);

            GUILayout.Space(10);

            for (int i = 0; i < rooms.Length; ++i)
            {
                GUILayout.BeginHorizontal(m_Selected == rooms[i] ? selectedStyle : detailsStyle, GUILayout.Height(24));
                GUILayout.Box(RoomIcon, GUILayout.Width(24), GUILayout.Height(16));
                string roomName = string.IsNullOrEmpty(rooms[i].Name) ? rooms[i].name : rooms[i].Name;

                if (GUILayout.Button(roomName))
                {
                    Selection.activeObject = rooms[i].gameObject;
                    SceneView.lastActiveSceneView.Frame(rooms[i].GetBounds());
                }
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Room"))
            {
                GameObject newRoom = PrefabUtility.InstantiatePrefab(MapRoomPrefab, mapCtrl.transform) as GameObject;
                var objectUniqueId = newRoom.GetComponent<ObjectUniqueId>();
                objectUniqueId?.RegenerateId();
                newRoom.transform.position = Vector3.zero;

                MapRoom room = newRoom.GetComponent<MapRoom>();
                room.Offset = new Vector2(map.CellSize * map.Size.x, map.CellSize * map.Size.y) * 0.5f;

                Selection.activeObject = newRoom;
            }


            GUILayout.EndVertical();

            GUILayout.Space(20);


            // ----------- Doors
            GUILayout.BeginVertical();

            GUILayout.Label("Doors", EditorStyles.boldLabel);

            GUILayout.Space(10);

            for (int i = 0; i < doors.Length; ++i)
            {
                GUILayout.BeginHorizontal(m_Selected == doors[i] ? selectedStyle : detailsStyle, GUILayout.Height(24));
                GUILayout.Box(DoorIcon, GUILayout.Width(24), GUILayout.Height(16));

                string doorName = string.IsNullOrEmpty(doors[i].Name) ? doors[i].name : doors[i].Name;
                if (GUILayout.Button(doorName))
                {
                    Selection.activeObject = doors[i].gameObject;
                }
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Door"))
            {
                GameObject newDoor = PrefabUtility.InstantiatePrefab(MapDoorPrefab, mapCtrl.transform) as GameObject;
                var objectUniqueId = newDoor.GetComponent<ObjectUniqueId>();
                objectUniqueId?.RegenerateId();
                newDoor.transform.position = Vector3.zero;
                MapDoor door = newDoor.GetComponent<MapDoor>();
                door.Offset = new Vector2(map.CellSize * map.Size.x, map.CellSize * map.Size.y) * 0.5f;
                Selection.activeObject = newDoor;
            }

            GUILayout.EndVertical();

            EditorGUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        // --------------------------------------------------------------------

        private void DrawMapDetails(MapController mapCtrl, GUIStyle detailsStyle)
        {
            // ------------- Map details
            GUILayout.BeginVertical();
            GUILayout.Label("Map details", EditorStyles.boldLabel);

            GUILayout.BeginVertical(detailsStyle);
            GUILayout.Space(10);

            EditorGUI.BeginChangeCheck();
            MapData map = mapCtrl.Data;
            var mapSerialized = new SerializedObject(map);

            EditorGUILayout.PropertyField(mapSerialized.FindProperty(nameof(MapData.Name)));
            EditorGUILayout.PropertyField(mapSerialized.FindProperty(nameof(MapData.Abbreviation)));
            map.CellSize = EditorGUILayout.IntField("CellSize", map.CellSize);
            map.Size = EditorGUILayout.Vector2IntField("Size", map.Size);
            map.GlobalScale = EditorGUILayout.FloatField("Global Scale", map.GlobalScale);
            map.MapSet = EditorGUILayout.ObjectField("Set", map.MapSet, typeof(MapDataSet), false) as MapDataSet;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(map, "Changed map properties");
                EditorUtility.SetDirty(map);
            }

            if (GUILayout.Button("Save"))
            {
                MapSerializer.SerializeAndSaveMap(mapCtrl);
                EditorUtility.SetDirty(mapCtrl);
                EditorUtility.SetDirty(mapCtrl.Data);
            }
            GUILayout.EndVertical();

            GUILayout.EndVertical();
        }

        // --------------------------------------------------------------------

        private Matrix4x4 GetElementTRS(MapData map, MapElement element)
        {
            Vector3 scale = element.Scale;
            scale.z *= -1;
            return Matrix4x4.TRS(new Vector3(element.Offset.x, 0, element.Offset.y) + new Vector3(m_ScrollPos.x, 0, m_ScrollPos.y), Quaternion.Euler(0, element.Rotation, 0), scale * map.GlobalScale);
        }

        // --------------------------------------------------------------------

        private void DrawDoor(MapController mapCtrl, MapDoor door, bool selected)
        {
            Matrix4x4 t = GetElementTRS(mapCtrl.Data, door);

            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.QUADS);

            Color color = Color.yellow;
            if (door.Door && door.Door.IsLocked())
                color = Color.red;
            else if (!door)
                color = Color.magenta;

            GL.Color(color);

            Vector3 p1 = t.MultiplyPoint(new Vector3(-door.Size.x, 0, -door.Size.y));
            Vector3 p2 = t.MultiplyPoint(new Vector3(door.Size.x, 0, -door.Size.y));
            Vector3 p3 = t.MultiplyPoint(new Vector3(door.Size.x, 0, door.Size.y));
            Vector3 p4 = t.MultiplyPoint(new Vector3(-door.Size.x, 0, door.Size.y));

            GL.Vertex3(p1.x, p1.z, 0);
            GL.Vertex3(p2.x, p2.z, 0);
            GL.Vertex3(p3.x, p3.z, 0);
            GL.Vertex3(p4.x, p4.z, 0);

            GL.End();

            if (selected)
            {
                GL.Begin(GL.LINES);
                GL.Color(Color.green);

                p1 = t.MultiplyPoint(new Vector3(-door.Size.x * 2f, 0, -door.Size.y * 2f));
                p2 = t.MultiplyPoint(new Vector3(door.Size.x * 2f, 0, -door.Size.y * 2f));
                p3 = t.MultiplyPoint(new Vector3(door.Size.x * 2f, 0, door.Size.y * 2f));
                p4 = t.MultiplyPoint(new Vector3(-door.Size.x * 2f, 0, door.Size.y * 2f));

                GL.Vertex3(p1.x, p1.z, 0);
                GL.Vertex3(p2.x, p2.z, 0);

                GL.Vertex3(p2.x, p2.z, 0);
                GL.Vertex3(p3.x, p3.z, 0);

                GL.Vertex3(p3.x, p3.z, 0);
                GL.Vertex3(p4.x, p4.z, 0);

                GL.Vertex3(p4.x, p4.z, 0);
                GL.Vertex3(p1.x, p1.z, 0);

                GL.End();
            }


            GL.PopMatrix();

        }

        // --------------------------------------------------------------------

        private void DrawRoom(MapController mapCtrl, MapRoom room, bool selected)
        {
            Matrix4x4 trs = GetElementTRS(mapCtrl.Data, room);

            DrawShape(room.GetComponent<Shape>(), trs, selected ? Color.green : Color.white);

            DrawDetails(mapCtrl, room);
            //DrawImages(mapCtrl, room);

            if (selected)
            {
                // Draw Arrow
                DrawArrow(trs);

                // Linked elements
                GL.Begin(GL.LINES);
                GL.Color(Color.cyan);
                foreach (var elm in room.LinkedElements)
                {
                    if (elm)
                    {
                        Vector3 p1 = trs.MultiplyPoint(Vector3.zero);
                        Matrix4x4 elementT = GetElementTRS(mapCtrl.Data, elm);
                        Vector3 p2 = elementT.MultiplyPoint(Vector3.zero);
                        GL.Vertex3(p1.x, p1.z, 0);
                        GL.Vertex3(p2.x, p2.z, 0);
                    }
                }

                GL.End();

                GL.Begin(GL.QUADS);
                GL.Color(Color.cyan);

                // Completion steps
                foreach (var step in room.CompletionSteps)
                {
                    var localPos = room.transform.InverseTransformPoint(step.transform.position);
                    var pos = trs.MultiplyPoint(localPos);

                    float size = 5f;
                    Vector3 p1 = new Vector3(-size, 0, -size);
                    Vector3 p2 = new Vector3(size, 0, -size);
                    Vector3 p3 = new Vector3(size, 0, size);
                    Vector3 p4 = new Vector3(-size, 0, size);

                    GL.Vertex3(pos.x + p1.x, pos.z + p1.z, 0);
                    GL.Vertex3(pos.x + p2.x, pos.z + p2.z, 0);
                    GL.Vertex3(pos.x + p3.x, pos.z + p3.z, 0);
                    GL.Vertex3(pos.x + p4.x, pos.z + p4.z, 0);
                }

                GL.End();
            }

        }

        // --------------------------------------------------------------------

        private void DrawArrow(Matrix4x4 trs)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.white);

            Vector3 arrowStart = trs.MultiplyPoint(Vector3.zero);
            Vector3 arrowL = trs.MultiplyPoint(Vector3.left + Vector3.forward * 0.5f);
            Vector3 arrowR = trs.MultiplyPoint(Vector3.right + Vector3.forward * 0.5f);
            Vector3 arrowEnd = trs.MultiplyPoint(Vector3.forward * 2);
            GL.Vertex3(arrowStart.x, arrowStart.z, 0);
            GL.Vertex3(arrowEnd.x, arrowEnd.z, 0);
            GL.Vertex3(arrowL.x, arrowL.z, 0);
            GL.Vertex3(arrowEnd.x, arrowEnd.z, 0);
            GL.Vertex3(arrowR.x, arrowR.z, 0);
            GL.Vertex3(arrowEnd.x, arrowEnd.z, 0);

            GL.End();

            GL.Begin(GL.QUADS);
            GL.Color(Color.cyan);
        }

        // --------------------------------------------------------------------

        private void DrawShape(Shape shape, Matrix4x4 trs, Color color)
        {
            Vector3[] points = new Vector3[shape.Points.Count + (shape.CloseShape ? 1 : 0)];
            for (int i = 0; i < shape.Points.Count; i++)
            {
                Vector3 position = trs.MultiplyPoint(shape.Points[i]);
                points[i] = new Vector3(position.x, position.z, 0);
            }

            if (shape.Points.Count >= 3 && shape.CloseShape)
            {
                Vector3 p2 = trs.MultiplyPoint(shape.Points[0]);
                points[points.Length - 1] = new Vector3(p2.x, p2.z, 0);
            }

            Handles.color = color;
            Handles.DrawAAPolyLine(
                        Texture2D.whiteTexture,
                        3,
                        points);
        }

        // --------------------------------------------------------------------

        private void DrawDetails(MapController mapCtrl, MapRoom room)
        {
            Matrix4x4 trs = GetElementTRS(mapCtrl.Data, room);

            MapDetailingShape[] detailing = room.GetComponentsInChildren<MapDetailingShape>();
            foreach (var details in detailing)
            {
                var dTRS = Matrix4x4.TRS(new Vector3(details.transform.localPosition.x, 0, details.transform.localPosition.z), details.transform.localRotation, details.transform.localScale);
                DrawShape(details.GetComponent<Shape>(), trs * dTRS, Color.grey);
            }
        }

        // --------------------------------------------------------------------

        private void DrawImages(MapController mapCtrl, MapRoom room)
        {
            Matrix4x4 trs = GetElementTRS(mapCtrl.Data, room);

            MapImage[] images = room.GetComponentsInChildren<MapImage>();
            foreach (var image in images)
            {
                var dTRS = trs * Matrix4x4.TRS(new Vector3(image.transform.localPosition.x, 0, image.transform.localPosition.z), image.transform.localRotation, image.transform.localScale);
                Vector3 pos = dTRS.MultiplyPoint(new Vector3(0, 0, 0));

                float w = dTRS.lossyScale.x;
                float h = dTRS.lossyScale.z;

                // TODO - This does not support rotation atm :(
                Handles.BeginGUI();
                GUI.DrawTexture(new Rect(pos.x + w * 0.5f, pos.z - h * 0.5f, -w, h), image.Texture, ScaleMode.ScaleAndCrop, true, 1, Color.white, 0, 0);
                GUI.color = Color.white;
                Handles.EndGUI();
            }
        }

        // --------------------------------------------------------------------

        private void DrawGrid(int countX, int countY, int cellSize, Color cellColor)
        {
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.LINES);
            for (int x = 0; x < countX; x++)
            {
                for (int y = 0; y < countY; y++)
                {
                    DrawGridBox(m_ScrollPos.x + x * cellSize, m_ScrollPos.y + y * cellSize, cellSize, cellSize, cellColor);
                }
            }

            // Mid lines
            Color midColor = Color.cyan;
            midColor.a = 0.5f;
            GL.Color(midColor);
            GL.Vertex3(m_ScrollPos.x + cellSize * countX * 0.5f, m_ScrollPos.y, 0);
            GL.Vertex3(m_ScrollPos.x + cellSize * countX * 0.5f, m_ScrollPos.y + cellSize * countY, 0);

            GL.Vertex3(m_ScrollPos.x, m_ScrollPos.y + cellSize * countY * 0.5f, 0);
            GL.Vertex3(m_ScrollPos.x + cellSize * countX, m_ScrollPos.y + cellSize * countY * 0.5f, 0);

            GL.End();
            GL.PopMatrix();
        }

        // --------------------------------------------------------------------

        private void DrawGridBox(float x, float y, float width, float height, Color color)
        {
            GL.Color(color);

            GL.Vertex3(x, y, 0);
            GL.Vertex3(x + width, y, 0);

            GL.Vertex3(x + width, y, 0);
            GL.Vertex3(x + width, y + height, 0);

            GL.Vertex3(x + width, y + height, 0);
            GL.Vertex3(x, y + height, 0);

            GL.Vertex3(x, y + height, 0);
            GL.Vertex3(x, y, 0);
        }

        // --------------------------------------------------------------------

        private Texture2D MakeTex(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // --------------------------------------------------------------------

    }
}