using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

#if UNITY_6000_0_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

using UnityEngine.Rendering;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PreRenderBackgrounds
{
    [RequireComponent(typeof(Camera))]
    public class PreRenderedBackground : MonoBehaviour
    {
        private static readonly int k_ColorTexHash = Shader.PropertyToID("_ColorTex");
        private static readonly int k_DepthTexHash = Shader.PropertyToID("_DepthTex");
        private static readonly int k_ClippingRectHash = Shader.PropertyToID("_ClippingRect");
        private static readonly int k_PlayerPosHash = Shader.PropertyToID("_PlayerPos");
        private static readonly int k_NearFarPlanesHash = Shader.PropertyToID("_NearFarPlanes");
        private static readonly int k_DepthOffsetHash = Shader.PropertyToID("_DepthOffset");
        private static readonly int k_InverseProjectionMatrixHash = Shader.PropertyToID("_InverseProjectionMatrix");

        private static List<PreRenderedBackground> m_BgQueue = new List<PreRenderedBackground>();

        public string Name = "UnnamedCamera";
        [SerializeField] private PreRenderSettings m_Settings;
        [SerializeField] int m_Priority = 0;

        [Tooltip("You can provide an external ColorTexture rendered in DCC software. Depth buffer will still be rendered in game, so level geometry is still needed if any dynamic elements need to be occluded")]
        public Texture2D ColorTexture;

        [Tooltip("You can provide an external DepthTexture rendered in DCC software.")]
        public Texture2D DepthTexture;
        [Tooltip("If a external depth texture is provided you can use this value to tweak depth representation")]
        public float DepthOffset = 0;

        [Tooltip("If the texture is bigger than the target resolution it will be clipped and scroll following a target")]
        [SerializeField] bool m_Scrolls = true;

        [Min(0)]
        [SerializeField] float m_ScrollingZoomScalar = 0.5f;

        [SerializeField] FilterMode m_ColorFilterMode;

        [SerializeField] bool m_RenderAlwaysOnEnable = true;

        [Tooltip("Set this to any value to re-render the scene. Set to 0 to disable realtime rendering. This value is ignored if Scrolls is set to true")]
        [SerializeField] float m_RenderFrequency = 0;

        [Tooltip("Materials applied on top of the color render")]
        [SerializeField] Material[] m_RuntimeEffects;

        [Tooltip("Optional light capture object to refine the lights that affect the prerendered bg")]
        [SerializeField] LightCapture m_LightingCapture;

        [SerializeField] bool m_ShowDebug;

        // --------------------------------------------------------------------

        private RenderTextureFormat m_RenderTargetFormat = RenderTextureFormat.Default;
        private Camera m_Camera;
        private bool m_Rendered;
        private Material m_BgMaterialInstance;
        private Material m_ClippingMaterialInstance;
        private Material m_DepthTransferMaterialInstance;

#if UNITY_6000_0_OR_NEWER
        private CinemachineCamera m_VirtualCam;
#else
        private CinemachineVirtualCamera m_VirtualCam;
#endif

        private float m_LastRenderTime;
        private MeshRenderer m_MeshRnd;
        private RenderTexture m_FinalRT;
        private RenderTexture m_FinalRTNoEffects;
        private RenderTexture m_DepthRT;
        private Matrix4x4 m_LastProjectionMatrix;
        private Transform m_ScrollingTarget;
        private UnityAction<CinemachineBrain> m_UpdateBrainAction;

#if UNITY_URP
        private UniversalRenderPipeline.SingleCameraRequest m_RenderColorRequest = new UniversalRenderPipeline.SingleCameraRequest();
        private UniversalRenderPipeline.SingleCameraRequest m_RenderDepthRequest = new UniversalRenderPipeline.SingleCameraRequest();
#endif

        

        private bool IsScrolling => m_Scrolls && m_ScrollingTarget;

        public RenderTexture FinalRenderTexture => m_FinalRT;

        // --------------------------------------------------------------------

        public Vector2Int Resolution => m_Settings ? m_Settings.Resolution : Vector2Int.zero;

        // --------------------------------------------------------------------

        private void OnValidate()
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            m_Camera = GetComponent<Camera>();
            if (m_Camera)
                m_Camera.cullingMask = m_Settings.IncludedLayers;
        }

        // --------------------------------------------------------------------

        private void Awake()
        {
            Debug.Assert(m_Settings, "PreRendering settings has not been assigned", gameObject);
            Debug.Assert(Screen.width >= m_Settings.Resolution.x || Screen.height >= m_Settings.Resolution.y, "Screen resolution has to be greater than the one set in the PreRendering settings");
            Debug.Assert(!m_Scrolls || ColorTexture, "Scrolling can only be used with a PreRendered color texture", gameObject);

            m_UpdateBrainAction = OnCameraBrainUpdate;

#if UNITY_6000_0_OR_NEWER
            m_VirtualCam = GetComponentInParent<CinemachineCamera>();
#else
            m_VirtualCam = GetComponentInParent<CinemachineVirtualCamera>();
#endif

            m_Camera = GetComponent<Camera>();
            m_Camera.enabled = false;

            m_BgMaterialInstance = new Material(m_Settings.PreRenderedBgMaterial);
            m_BgMaterialInstance.name = "Background (Instance)";

            m_ClippingMaterialInstance = new Material(m_Settings.PreRenderedClippingMaterial);
            m_ClippingMaterialInstance.name = "BgClipping (Instance)";

            m_DepthTransferMaterialInstance = new Material(m_Settings.PreRenderedDepthTransferMaterial);
            m_DepthTransferMaterialInstance.name = "BgDepthTransfer (Instance)";

            m_FinalRT = new RenderTexture(m_Settings.Resolution.x, m_Settings.Resolution.y, 24, m_RenderTargetFormat);
            m_FinalRT.filterMode = m_ColorFilterMode;
            m_FinalRT.useMipMap = false;

            m_FinalRTNoEffects = new RenderTexture(m_Settings.Resolution.x, m_Settings.Resolution.y, 24, m_RenderTargetFormat);
            m_FinalRTNoEffects.filterMode = m_ColorFilterMode;
            m_FinalRTNoEffects.useMipMap = false;

            m_DepthRT = new RenderTexture(Screen.width, Screen.height, 24);
            m_DepthRT.format = RenderTextureFormat.Depth;
            
            GameObject preRdBg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            preRdBg.name = "PreRenderedBackground_"  + Name;;
            preRdBg.GetComponent<Collider>().enabled = false;
            preRdBg.transform.SetPositionAndRotation(transform.position, transform.rotation);
            //preRdBg.transform.SetParent(transform);

            m_MeshRnd = preRdBg.GetComponent<MeshRenderer>();
            m_MeshRnd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_MeshRnd.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            m_MeshRnd.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            m_MeshRnd.material = m_BgMaterialInstance;
            m_MeshRnd.gameObject.SetActive(false);
            UdpateRendererTransform();

#if UNITY_EDITOR
            SceneVisibilityManager.instance.Hide(preRdBg, false);
#endif
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            Camera mainCam = Camera.main;
            if (mainCam)
                Camera.main.depthTextureMode = DepthTextureMode.Depth;

            if (!m_Rendered || m_RenderAlwaysOnEnable)
                Render();

            m_BgQueue.Add(this);


            var highestPrioBG = GetHighestPriorityBg();
            foreach (var bg in m_BgQueue)
            {
                bg.SetRenderingEnabled(highestPrioBG == bg);
            }
        }

        // --------------------------------------------------------------------

        private void Start()
        {
            if (m_RenderAlwaysOnEnable) // We also re-render on start to fix startup coloring issues
                Render();
        }

        // --------------------------------------------------------------------

        private void OnDisable()
        {
            CinemachineCore.CameraUpdatedEvent.RemoveListener(m_UpdateBrainAction);

            if (m_MeshRnd)
                m_MeshRnd.gameObject.SetActive(false);

            m_BgQueue.Remove(this);

            var highestPrioBG = GetHighestPriorityBg();
            foreach (var bg in m_BgQueue)
            {
                bg.SetRenderingEnabled(highestPrioBG == bg);
            }
        }

        // --------------------------------------------------------------------

        private void SetRenderingEnabled(bool renderingEnabled)
        {
            if (m_MeshRnd)
                m_MeshRnd.gameObject.SetActive(renderingEnabled);
        }

        // --------------------------------------------------------------------

        private static PreRenderedBackground GetHighestPriorityBg()
        {
            PreRenderedBackground highestPrio = null;
            foreach(var bg in m_BgQueue)
            {
                if (highestPrio == null || bg.m_Priority > highestPrio.m_Priority)
                {
                    highestPrio = bg;
                }
            }
            return highestPrio;
        }

        // --------------------------------------------------------------------

        public void Render(Camera cam = null)
        {
            var target = ScrollingTarget.Instance;
            m_ScrollingTarget = target ? target.transform : null;

            if (!m_VirtualCam)
            {
#if UNITY_6000_0_OR_NEWER
                m_VirtualCam = GetComponentInParent<CinemachineCamera>();
#else
                m_VirtualCam = GetComponentInParent<CinemachineVirtualCamera>();
#endif
            }

            if (Application.isPlaying)
            {
                Debug.Assert(m_VirtualCam, "PreRenderedBackground needs to be placed as a direct child of a virtual camera");
            }

            PrepareCameraForRender(cam, Screen.width, Screen.height);
            UdpateRendererTransform(); // Needs to happen after PrepareCamera

            RenderTexture renderText = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, m_RenderTargetFormat);

            if (m_DepthRT.width != Screen.width || m_DepthRT.height != Screen.height)
            {
                m_DepthRT.Release();
                m_DepthRT = new RenderTexture(Screen.width, Screen.height, 24);
                m_DepthRT.format = RenderTextureFormat.Depth;
            }

            Rect clippingRect = new Rect(0, 0, 1, 1);
            if (IsScrolling)
            {
                clippingRect = GetScrollingRect();

                m_LastProjectionMatrix = GetClippingProjectionMatrix(m_Camera, clippingRect);
                m_Camera.projectionMatrix = m_LastProjectionMatrix;

                PreRenderedUtils.DrawCameraFrustum(m_Camera);
            }


            PrepareMaterialsForRender(clippingRect);

#if UNITY_URP

            // TODO - Rendering twice shouldn't be necessary, improve this!
            if (!DepthTexture)
            {
                m_RenderDepthRequest.destination = m_DepthRT;
                RenderPipeline.SubmitRenderRequest(m_Camera, m_RenderDepthRequest);
            }

            if (!ColorTexture)
            {
                m_RenderColorRequest.destination = renderText;
                RenderPipeline.SubmitRenderRequest(m_Camera, m_RenderColorRequest);
            }
            
#else
            m_Camera.SetTargetBuffers(renderText.colorBuffer, m_DepthRT.depthBuffer);
            m_Camera.Render();
#endif

            Graphics.Blit(ColorTexture ? ColorTexture : renderText, m_FinalRT, m_ClippingMaterialInstance);

            if (DepthTexture)
            {
                RenderTexture rt = RenderTexture.active;
                RenderTexture.active = m_DepthRT;
                GL.Clear(true, true, Color.black);
                RenderTexture.active = rt;
                Graphics.Blit(DepthTexture, m_DepthRT, m_DepthTransferMaterialInstance);
            }

            if (Application.isPlaying)
                m_Rendered = true;

            m_LastRenderTime = Time.time;

            RenderTexture.ReleaseTemporary(renderText);

            if (IsScrolling)
            {
                CinemachineCore.CameraUpdatedEvent.AddListener(m_UpdateBrainAction);
            }

            Graphics.Blit(m_FinalRT, m_FinalRTNoEffects);
        }

        // --------------------------------------------------------------------

        public static Matrix4x4 GetClippingProjectionMatrix(Camera camera, Rect clippingRect)
        {
            Rect r = clippingRect;
            Matrix4x4 m2 = Matrix4x4.TRS(new Vector3((1 / r.width - 1), (1 / r.height - 1), 0), Quaternion.identity, new Vector3(1 / r.width, 1 / r.height, 1));
            Matrix4x4 m3 = Matrix4x4.TRS(new Vector3(-r.x * 2 / r.width, -r.y * 2 / r.height, 0), Quaternion.identity, Vector3.one);
            return m3 * m2 * camera.projectionMatrix;
        }

        // --------------------------------------------------------------------

        Rect GetScrollingRect()
        {
            Vector2 fullSize = new Vector2(Screen.width, Screen.height);
            Vector3 targetScreenPos = m_Camera.WorldToScreenPoint(m_ScrollingTarget.position);
            Vector2 scrolledSize = (Vector2)fullSize * m_ScrollingZoomScalar;
            Vector2 halfScrolledSize = scrolledSize * 0.5f;
            float rectStartX = (targetScreenPos.x - halfScrolledSize.x);
            float rectEndX = (targetScreenPos.x + halfScrolledSize.x);
            float rectStartY = (targetScreenPos.y - halfScrolledSize.y);
            float rectEndY = (targetScreenPos.y + halfScrolledSize.y);

            Rect clippedRect = new Rect(rectStartX, rectStartY, rectEndX - rectStartX, rectEndY - rectStartY);

            Vector2 min = Vector2.zero;
            Vector2 max = fullSize;

            // X Clamp
            if (clippedRect.x < min.x)
            {
                clippedRect.x = min.x;
            }
            if ((clippedRect.x + clippedRect.width) > max.x)
            {
                clippedRect.x -= ((clippedRect.x + clippedRect.width) - max.x);
            }

            // Y Clamp
            if (clippedRect.y < min.y)
            {
                clippedRect.y = min.y;
            }
            if ((clippedRect.y + clippedRect.height) > max.y)
            {
                clippedRect.y -= ((clippedRect.y + clippedRect.height) - max.y);
            }

            return new Rect(
                clippedRect.x / fullSize.x,
                clippedRect.y / fullSize.y,
                clippedRect.width / fullSize.x,
                clippedRect.height / fullSize.y);
        }


        // --------------------------------------------------------------------

        private void PrepareCameraForRender(Camera cam, int width, int height)
        {
            if (cam)
                m_Camera = cam;

            if (!m_Camera)
                m_Camera = gameObject.AddComponent<Camera>();

            m_Camera.rect = new Rect(0, 0, 1, 1);
            m_Camera.ResetProjectionMatrix();

            MimicCamera mimicCam = GetComponentInParent<MimicCamera>();
            if (!mimicCam)
            {
                m_Camera.transform.position = m_VirtualCam.transform.position;
                m_Camera.transform.rotation = m_VirtualCam.transform.rotation;

#if UNITY_6000_0_OR_NEWER
                var lens = m_VirtualCam.Lens;
#else
                var lens = m_VirtualCam.m_Lens;
#endif

                m_Camera.nearClipPlane = lens.NearClipPlane;
                m_Camera.farClipPlane = lens.FarClipPlane;
                m_Camera.usePhysicalProperties = lens.IsPhysicalCamera;

#if UNITY_6000_0_OR_NEWER
                m_Camera.sensorSize = lens.PhysicalProperties.SensorSize;
                m_Camera.lensShift = lens.PhysicalProperties.LensShift;
                m_Camera.gateFit = lens.PhysicalProperties.GateFit;
#else
                m_Camera.sensorSize = lens.SensorSize;
                m_Camera.lensShift = lens.LensShift;
                m_Camera.gateFit = lens.GateFit;

#endif
                m_Camera.orthographic = lens.Orthographic;
                m_Camera.orthographicSize = lens.OrthographicSize;
                m_Camera.fieldOfView = lens.FieldOfView;
            }
            else
            {
                Debug.Assert(mimicCam.Camera.transform.lossyScale == Vector3.one, "Mimicking camera doesn't have a uniform 1 scale, this could lead to incorrect scrolling. Please make sure the camera has a uniform world scale of 1", mimicCam.Camera);
                m_Camera.CopyFrom(mimicCam.Camera);
            }

            m_Camera.cameraType = CameraType.Game;
            m_Camera.enabled = false;
            m_Camera.clearFlags = CameraClearFlags.Skybox;
            m_Camera.forceIntoRenderTexture = true;
            m_Camera.renderingPath = RenderingPath.Forward;
            m_Camera.useOcclusionCulling = true;
            m_Camera.cullingMask = m_Settings.IncludedLayers;
            m_Camera.scene = gameObject.scene;

            m_Camera.aspect = width / Mathf.Max(height, Mathf.Epsilon);
            m_Camera.depthTextureMode = DepthTextureMode.Depth;
        }

        // --------------------------------------------------------------------

        private void PrepareMaterialsForRender(Rect clippingRect)
        {
            
            Vector4 clippingRectV = new Vector4(clippingRect.width, clippingRect.height, clippingRect.x, clippingRect.y);
            m_ClippingMaterialInstance.SetVector(k_ClippingRectHash, clippingRectV);
            m_DepthTransferMaterialInstance.SetVector(k_ClippingRectHash, clippingRectV);

            Matrix4x4 VP = GL.GetGPUProjectionMatrix(m_Camera.projectionMatrix, false) * m_Camera.worldToCameraMatrix;
            Matrix4x4 VP_I = VP.inverse;
            m_BgMaterialInstance.SetMatrix(k_InverseProjectionMatrixHash, VP_I);

            m_BgMaterialInstance.SetTexture(k_ColorTexHash, m_FinalRT);
            m_BgMaterialInstance.SetTexture(k_DepthTexHash, m_DepthRT);

            if (m_ScrollingTarget)
                m_BgMaterialInstance.SetVector(k_PlayerPosHash, m_ScrollingTarget.position);

            m_DepthTransferMaterialInstance.SetVector(k_NearFarPlanesHash, new Vector4(m_Camera.nearClipPlane, m_Camera.farClipPlane, 0, 0));
            m_DepthTransferMaterialInstance.SetFloat(k_DepthOffsetHash, DepthOffset);
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            if (m_RenderFrequency > 0 || IsScrolling)
            {
                if ((Time.time - m_LastRenderTime) > m_RenderFrequency)
                {
                    Render();
                }
            }

            if (m_RuntimeEffects.Length > 0)
                ApplyRuntimeEffects();
        }

        // --------------------------------------------------------------------

        private void ApplyRuntimeEffects()
        {
            var rt1 = RenderTexture.GetTemporary(m_FinalRTNoEffects.width, m_FinalRTNoEffects.height, 0);
            var rt2 = RenderTexture.GetTemporary(m_FinalRTNoEffects.width, m_FinalRTNoEffects.height, 0);

            Graphics.Blit(m_FinalRTNoEffects, rt1);

            var front = rt1;
            var back = rt2;
            foreach (var effect in m_RuntimeEffects)
            {
                Graphics.Blit(front, back, effect);

                // Swap buffers
                var currentFront = front;
                front = back;
                back = currentFront;
            }

            Graphics.Blit(front, m_FinalRT);

            m_BgMaterialInstance.SetTexture(k_ColorTexHash, m_FinalRT);

            RenderTexture.ReleaseTemporary(rt1);
            RenderTexture.ReleaseTemporary(rt2);
        }

        // --------------------------------------------------------------------

        void OnCameraBrainUpdate(CinemachineBrain brain)
        {
            var cam = brain.OutputCamera;
            if (cam != null)
            {
                cam.ResetProjectionMatrix();

                if (m_Scrolls && m_ScrollingTarget)
                {
                    cam.projectionMatrix = m_LastProjectionMatrix;
                }
            }

            CinemachineCore.CameraUpdatedEvent.RemoveListener(m_UpdateBrainAction);
        }

        // --------------------------------------------------------------------

        private void UdpateRendererTransform()
        {
            if (m_LightingCapture)
            {
                m_MeshRnd.transform.localScale = m_LightingCapture.transform.localScale;
                m_MeshRnd.transform.position = m_LightingCapture.transform.position;
                m_MeshRnd.transform.rotation = m_LightingCapture.transform.rotation;
            }
            else
            {
                m_MeshRnd.transform.position = m_Camera.transform.position + m_Camera.transform.forward * (m_Camera.nearClipPlane + Mathf.Epsilon);
                m_MeshRnd.transform.localScale = Vector3.right;
            }

        }

        private void OnGUI()
        {
            if (m_ShowDebug)
            {
                GUI.DrawTexture(new Rect(0, 0, Screen.width/2, Screen.height / 2), m_FinalRT);
                GUI.DrawTexture(new Rect(0, Screen.height / 2, Screen.width / 2, Screen.height / 2), m_DepthRT);
            }
        }
    }
}