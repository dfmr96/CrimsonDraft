using UnityEngine;

namespace CrimsonDraft.Navigation
{
    public class RoomSkyboxOverride : MonoBehaviour
    {
        [SerializeField] private Material skyboxMaterial;

        private Camera mainCamera;
        private Skybox skybox;
        private Material previousMaterial;
        private CameraClearFlags previousClearFlags;

        private void OnEnable()
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;

            skybox = mainCamera.GetComponent<Skybox>();
            if (skybox == null) skybox = mainCamera.gameObject.AddComponent<Skybox>();

            previousMaterial = skybox.material;
            previousClearFlags = mainCamera.clearFlags;

            skybox.material = skyboxMaterial;
            mainCamera.clearFlags = CameraClearFlags.Skybox;
        }

        private void OnDisable()
        {
            if (mainCamera == null) return;

            skybox.material = previousMaterial;
            mainCamera.clearFlags = previousClearFlags;
        }
    }
}
