#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrimsonDraft.Infrastructure.UI
{
    /// <summary>
    /// The camera that draws the UI layer into an alpha render texture, under its own UI volume
    /// (CRT / bloom), and the canvas that composites it over the frame -- the same set-up the
    /// combat scene uses, so the effect lands on the UI only and never on the world behind it.
    /// Both stay switched off until some UI asks for them with <see cref="Acquire"/>.
    ///
    /// A UI that is a world-space canvas (the save room's terminal) asks with
    /// <c>followWorldCamera</c>: the rig camera then takes the pose of the game camera just before it
    /// renders, so the panel sits exactly where the game camera sees it.
    /// </summary>
    public sealed class UiOnlyEffectRig : MonoBehaviour
    {
        [SerializeField] private Camera     uiCamera  = null!;
        [SerializeField] private GameObject composite = null!;

        private readonly HashSet<object> owners    = new();
        private readonly HashSet<object> followers = new();
        private readonly List<object>    dead      = new();

        public static UiOnlyEffectRig? Current { get; private set; }

        public Camera Camera => this.uiCamera;

        void OnEnable()
        {
            Current = this;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            if (Current == this) Current = null;
        }

        public void Acquire(object owner, bool followWorldCamera = false)
        {
            this.owners.Add(owner);
            if (followWorldCamera) this.followers.Add(owner);
            Apply();
        }

        public void Release(object owner)
        {
            this.owners.Remove(owner);
            this.followers.Remove(owner);
            Apply();
        }

        void Apply()
        {
            Prune();
            bool on = this.owners.Count > 0;
            if (this.uiCamera != null && this.uiCamera.gameObject.activeSelf != on) this.uiCamera.gameObject.SetActive(on);
            if (this.composite != null && this.composite.activeSelf != on)          this.composite.SetActive(on);
        }

        // An owner destroyed without releasing must not keep the rig on.
        void Prune()
        {
            this.dead.Clear();
            foreach (var owner in this.owners)
                if (owner is Object unityObject && unityObject == null) this.dead.Add(owner);
            foreach (var owner in this.dead)
            {
                this.owners.Remove(owner);
                this.followers.Remove(owner);
            }
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering != this.uiCamera || this.followers.Count == 0) return;

            var main = Camera.main;
            if (main == null) return;

            var t = this.uiCamera.transform;
            t.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            this.uiCamera.orthographic  = main.orthographic;
            this.uiCamera.fieldOfView   = main.fieldOfView;
            this.uiCamera.nearClipPlane = main.nearClipPlane;
            this.uiCamera.farClipPlane  = main.farClipPlane;
        }
    }
}
