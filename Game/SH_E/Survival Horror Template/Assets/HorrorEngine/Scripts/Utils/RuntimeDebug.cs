using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorEngine
{
    public class RuntimeDebug : SingletonBehaviour<RuntimeDebug>
    {
        private enum ShapeType { Line, WireBox, Box, WireSphere, Sphere, WireCapsule, Capsule }

        private struct DrawCommand
        {
            public ShapeType Type;
            public Vector3 Position;
            public Vector3 Size; // Used for Box dimensions
            public Quaternion Rotation; // Used for Box/Capsule orientation
            public Vector3 Scale;
            public float Radius;
            public float Height;
            public Color Color;
            public string Category;
            public float ExpireTime;
            public Vector3 EndPoint; // Specifically for DrawLine
        }

        private List<DrawCommand> m_Commands = new List<DrawCommand>();

        // Reusable buffers to avoid allocations
        private List<Vector3> m_Vertices = new List<Vector3>(2048);
        private List<Color> m_Colors = new List<Color>(2048);
        private List<int> m_Indices = new List<int>(4096);

        private Mesh m_Mesh;
        private Material m_LineMaterial;
        private CommandBuffer m_CommandBuffer;

        public static Dictionary<string, bool> CategoryRenderingEnabled = new Dictionary<string, bool>();
        

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        protected override void Awake()
        {
            base.Awake();

            if (Instance == this)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                m_LineMaterial = new Material(shader);

                m_Mesh = new Mesh { name = "DebugGizmoMesh" };
                m_Mesh.MarkDynamic();

                m_CommandBuffer = new CommandBuffer { name = "RuntimeDebugGizmos" };
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            m_CommandBuffer?.Dispose();   
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            // Filter for Game/Scene view as before
            if (m_Commands.Count == 0 || (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView)) return;

            // We render here all the shapes that are lines as a single draw call
            m_Vertices.Clear();
            m_Colors.Clear();
            m_Indices.Clear();

            float currentTime = Time.time;
            for (int i = m_Commands.Count - 1; i >= 0; i--)
            {
                var cmd = m_Commands[i];
                bool shouldDraw = string.IsNullOrEmpty(cmd.Category) || (CategoryRenderingEnabled.ContainsKey(cmd.Category) && CategoryRenderingEnabled[cmd.Category]);

                if (shouldDraw) PopulateWireShape(cmd);
                if (currentTime >= cmd.ExpireTime) m_Commands.RemoveAt(i);
            }

            if (m_Vertices.Count == 0) 
                return;

            m_Mesh.Clear();
            m_Mesh.SetVertices(m_Vertices);
            m_Mesh.SetColors(m_Colors);
            m_Mesh.SetIndices(m_Indices, MeshTopology.Lines, 0);

            m_CommandBuffer.Clear();
            m_CommandBuffer.name = "RuntimeDebugGizmos_RenderPass";

            m_CommandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);

            m_CommandBuffer.DrawMesh(m_Mesh, Matrix4x4.identity, m_LineMaterial);

            context.ExecuteCommandBuffer(m_CommandBuffer);
            context.Submit();
        }

            
#endif
        private void PopulateWireShape(DrawCommand cmd)
        {
            switch (cmd.Type)
            {
                case ShapeType.Line:
                    AddLineInternal(cmd.Position, cmd.EndPoint, cmd.Color);
                    break;

                case ShapeType.WireBox:
                    AddWireBoxInternal(cmd);
                    break;

                case ShapeType.WireSphere:
                    AddWireSphereInternal(cmd);
                    break;

                case ShapeType.WireCapsule:
                    AddWireCapsuleInternal(cmd);
                    break;

            }
        }

        public static bool IsCategoryEnabled(string category)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Register(category);
            return string.IsNullOrEmpty(category) || CategoryRenderingEnabled[category];
#else
            return false; // In non-dev builds, categories are effectively disabled
#endif
        }

        // --- API Methods ---

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration = 0, string category = "")
        {
            AddCommand(new DrawCommand { Type = ShapeType.Line, Position = start, EndPoint = end, Color = color, Scale = Vector3.one, ExpireTime = Time.time + duration, Category = category });
        }

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void DrawWireBox(Vector3 center, Vector3 size, Quaternion rotation, Color color, Vector3 scale, float duration = 0, string category = "")
        {
            AddCommand(new DrawCommand { Type = ShapeType.WireBox, Position = center, Size = size, Rotation = rotation, Color = color, Scale = scale, ExpireTime = Time.time + duration, Category = category });
        }

        /*
        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void DrawBox(Vector3 center, Vector3 size, Quaternion rotation, Color color, Vector3 scale, float duration = 0, string category = "")
        {
            AddCommand(new DrawCommand { Type = ShapeType.Box, Position = center, Size = size, Rotation = rotation, Color = color, Scale = scale, ExpireTime = Time.time + duration, Category = category });
        }
        */

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void DrawWireSphere(Vector3 center, float radius, Color color, Vector3 scale, float duration = 0, string category = "")
        {
            AddCommand(new DrawCommand { Type = ShapeType.WireSphere, Position = center, Radius = radius, Color = color, Scale = scale, ExpireTime = Time.time + duration, Category = category });
        }

        /*
        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void DrawSphere(Vector3 center, float radius, Color color, Vector3 scale, float duration = 0, string category = "")
        {
            AddCommand(new DrawCommand { Type = ShapeType.Sphere, Position = center, Radius = radius, Color = color, Scale = scale, ExpireTime = Time.time + duration, Category = category });
        }
        */

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void DrawWireCapsule(Vector3 center, float radius, float height, Quaternion rotation, Color color, Vector3 scale, float duration = 0, string category = "")
        {
            AddCommand(new DrawCommand { Type = ShapeType.WireCapsule, Position = center, Radius = radius, Height = height, Rotation = rotation, Color = color, Scale = scale, ExpireTime = Time.time + duration, Category = category });
        }

        /*
        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void DrawCapsule(Vector3 center, float radius, float height, Quaternion rotation, Color color, Vector3 scale, float duration = 0, string category = "")
        {
            AddCommand(new DrawCommand { Type = ShapeType.Capsule, Position = center, Radius = radius, Height = height, Rotation = rotation, Color = color, Scale = scale, ExpireTime = Time.time + duration, Category = category });
        }
        */

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void DrawWireCollider(Collider collider, UnityEngine.Color color, float duration = 0, string category = "")
        {
            Transform t = collider.transform;
            //TODO - None of this is taking into account scale. We should probably add a warning if the collider's transform has non-uniform scale, since that can cause weirdness with the rendering
            if (collider is BoxCollider boxCollider)
            {
                DrawWireBox(boxCollider.transform.TransformPoint(boxCollider.center), boxCollider.size, t.rotation, color, t.lossyScale, duration, category);
            }
            else if (collider is SphereCollider sphereCollider)
            {
                DrawWireSphere(sphereCollider.transform.TransformPoint(sphereCollider.center), sphereCollider.radius, color, t.lossyScale, duration, category);
            }
            else if (collider is CapsuleCollider capsuleCollider)
            {
                DrawWireCapsule(capsuleCollider.transform.TransformPoint(capsuleCollider.center), capsuleCollider.radius, capsuleCollider.height, t.rotation, color, t.lossyScale, duration, category);
            }
            else
            {
                UnityEngine.Debug.LogWarning("Collider type not supported for drawing.");
            }
        }

        private static void AddCommand(DrawCommand cmd)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR // This shouldn't be called in a non-dev build, but just in case, we won't create the instance or register categories
            if (m_Instance == null)
            {
                CreateInstance();
            }

            if (m_Instance == null)
                return;

            Register(cmd.Category);

            m_Instance.m_Commands.Add(cmd);
#endif
        }

        public static void Register(string cat, bool defaultState = false)
        {
            if (string.IsNullOrEmpty(cat)) return;

            if (!CategoryRenderingEnabled.ContainsKey(cat))
            {
                CategoryRenderingEnabled[cat] = defaultState;
            }
        }

        private static void CreateInstance()
        {
            GameObject go = new GameObject("RuntimeDebugGizmos");
            go.AddComponent<RuntimeDebug>();
            DontDestroyOnLoad(go);
        }

        // --- Internal Vertex Helpers ---

        private void AddLineInternal(Vector3 start, Vector3 end, Color col)
        {
            int indexOffset = m_Vertices.Count;
            m_Vertices.Add(start);
            m_Vertices.Add(end);
            m_Colors.Add(col);
            m_Colors.Add(col);

            m_Indices.Add(indexOffset);
            m_Indices.Add(indexOffset + 1);
        }
        private void AddWireBoxInternal(DrawCommand cmd)
        {
            // Apply cmd.Scale to the base size
            Vector3 scaledSize = Vector3.Scale(cmd.Size, cmd.Scale);
            Vector3 h = scaledSize * 0.5f;

            Vector3[] c = {
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z),
                new Vector3(h.x, -h.y, h.z),  new Vector3(-h.x, -h.y, h.z),
                new Vector3(-h.x, h.y, -h.z),  new Vector3(h.x, h.y, -h.z),
                new Vector3(h.x, h.y, h.z),   new Vector3(-h.x, h.y, h.z)
            };

            for (int i = 0; i < 8; i++)
            {
                // Local Scale -> Rotation -> Translation
                m_Vertices.Add(cmd.Position + (cmd.Rotation * c[i]));
                m_Colors.Add(cmd.Color);
            }

            int vStart = m_Vertices.Count;

            int[] sequence = { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 };
            for (int i = 0; i < sequence.Length; i++)
            {
                m_Indices.Add(vStart + sequence[i]);
            }
        }

        private void AddWireSphereInternal(DrawCommand cmd)
        {
            int segments = 24;
            // For spheres, we scale the axes used to build the circles
            Vector3 scale = cmd.Scale;

            // We pass the scale into AddCircleInternal to handle non-uniform scaling (ellipsoids)
            AddCircleInternal(cmd.Position, cmd.Radius, segments, cmd.Rotation * Vector3.up, cmd.Rotation * Vector3.forward, cmd.Color, scale);
            AddCircleInternal(cmd.Position, cmd.Radius, segments, cmd.Rotation * Vector3.right, cmd.Rotation * Vector3.up, cmd.Color, scale);
            AddCircleInternal(cmd.Position, cmd.Radius, segments, cmd.Rotation * Vector3.forward, cmd.Rotation * Vector3.right, cmd.Color, scale);
        }

        private void AddCircleInternal(Vector3 center, float radius, int segments, Vector3 normal, Vector3 side, Color col, Vector3 scale)
        {
            Vector3 tangent = Vector3.Cross(normal, side).normalized;
            int firstVertIdx = m_Vertices.Count;

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2;

                // Calculate local point on circle
                Vector3 localPos = (side * Mathf.Cos(angle) + tangent * Mathf.Sin(angle)) * radius;

                // Apply non-uniform scale BEFORE world transformation
                Vector3 scaledPos = Vector3.Scale(localPos, scale);

                m_Vertices.Add(center + scaledPos);
                m_Colors.Add(col);

                m_Indices.Add(firstVertIdx + i);
                m_Indices.Add(firstVertIdx + (i + 1) % segments);
            }
        }

        private void AddWireCapsuleInternal(DrawCommand cmd)
        {
            // Apply vertical scale to height and uniform scale to radius
            float scaledRadius = cmd.Radius * Mathf.Max(cmd.Scale.x, cmd.Scale.z);
            float scaledHeight = cmd.Height * cmd.Scale.y;

            float halfH = Mathf.Max(0, (scaledHeight * 0.5f) - scaledRadius);
            Vector3 top = cmd.Position + (cmd.Rotation * Vector3.up * halfH);
            Vector3 bot = cmd.Position + (cmd.Rotation * Vector3.down * halfH);

            Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            foreach (var dir in directions)
            {
                // Scale the offset direction
                Vector3 offset = cmd.Rotation * Vector3.Scale(dir, cmd.Scale) * cmd.Radius;
                AddLineInternal(top + offset, bot + offset, cmd.Color);
            }

            // Rings (Using the specialized AddCircle that handles scale)
            AddCircleInternal(top, cmd.Radius, 20, cmd.Rotation * Vector3.up, cmd.Rotation * Vector3.forward, cmd.Color, cmd.Scale);
            AddCircleInternal(bot, cmd.Radius, 20, cmd.Rotation * Vector3.up, cmd.Rotation * Vector3.forward, cmd.Color, cmd.Scale);

            // Arcs
            AddArcInternal(top, cmd.Radius, 10, cmd.Rotation * Vector3.forward, cmd.Rotation * Vector3.up, cmd.Color, cmd.Scale);
            AddArcInternal(top, cmd.Radius, 10, cmd.Rotation * Vector3.right, cmd.Rotation * Vector3.up, cmd.Color, cmd.Scale);
            AddArcInternal(bot, cmd.Radius, 10, cmd.Rotation * Vector3.forward, cmd.Rotation * -Vector3.up, cmd.Color, cmd.Scale);
            AddArcInternal(bot, cmd.Radius, 10, cmd.Rotation * Vector3.right, cmd.Rotation * -Vector3.up, cmd.Color, cmd.Scale);
        }

        private void AddArcInternal(Vector3 center, float radius, int segments, Vector3 side, Vector3 up, Color col, Vector3 scale)
        {
            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 0.5f;
                Vector3 localPos = (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;

                // Scale locally
                Vector3 scaledPos = Vector3.Scale(localPos, scale);

                m_Vertices.Add(center + scaledPos);
                m_Colors.Add(col);
                if (i > 0)
                {
                    m_Indices.Add(m_Vertices.Count - 2);
                    m_Indices.Add(m_Vertices.Count - 1);
                }
            }
        }
    }
}