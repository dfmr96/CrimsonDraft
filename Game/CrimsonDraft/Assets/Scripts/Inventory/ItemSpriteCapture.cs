#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    /// <summary>
    /// Place this MonoBehaviour in SpriteCaptureScene.
    /// Assign a prefab and press "Capture Sprite" in the Inspector to render
    /// a 3D model to PNG and optionally assign it to an ItemData's icon field.
    /// </summary>
    public sealed class ItemSpriteCapture : MonoBehaviour
    {
        [SerializeField] private GameObject prefabTarget  = null!;
        [SerializeField] private ItemData?  targetItem    = null;
        [SerializeField] private Camera     captureCamera = null!;
        [SerializeField] private int        captureWidth  = 256;
        [SerializeField] private int        captureHeight = 256;
        [SerializeField] private string     outputFolder  = "Assets/Art/Sprites/Items/";

#if UNITY_EDITOR
        public void CaptureSprite()
        {
            if (this.prefabTarget == null)
            {
                Debug.LogWarning("[ItemSpriteCapture] Prefab Target is not assigned.");
                return;
            }
            if (this.captureCamera == null)
            {
                Debug.LogWarning("[ItemSpriteCapture] Capture Camera is not assigned.");
                return;
            }

            // 1. Hide existing renderers so they don't bleed into the capture
            var existingRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in existingRenderers) r.enabled = false;

            // 2. Instantiate prefab
            var instance = Instantiate(this.prefabTarget, Vector3.zero, Quaternion.identity);

            // 2. Frame the model — compute enclosing bounds from all renderers
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[ItemSpriteCapture] Prefab has no Renderer components — capture aborted.");
                DestroyImmediate(instance);
                return;
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float radius   = bounds.extents.magnitude;
            float fovRad   = this.captureCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float distance = radius / Mathf.Tan(fovRad) * 1.2f;

            this.captureCamera.transform.position =
                bounds.center - this.captureCamera.transform.forward * distance;

            // 3. Render to RenderTexture
            var rt  = new RenderTexture(this.captureWidth, this.captureHeight, 24);
            var tex = new Texture2D(this.captureWidth, this.captureHeight, TextureFormat.RGBA32, false);

            var prevTarget          = this.captureCamera.targetTexture;
            this.captureCamera.targetTexture = rt;
            this.captureCamera.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, this.captureWidth, this.captureHeight), 0, 0);
            tex.Apply();
            this.captureCamera.targetTexture = prevTarget;
            RenderTexture.active = null;
            DestroyImmediate(rt);

            // 4. Save PNG
            System.IO.Directory.CreateDirectory(this.outputFolder);
            string safeName  = this.prefabTarget.name.Replace("/", "_").Replace("\\", "_");
            string assetPath = $"{this.outputFolder.TrimEnd('/')}/{safeName}.png";
            string fullPath  = System.IO.Path.GetFullPath(assetPath);
            System.IO.File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            DestroyImmediate(tex);

            // 5. Import as Sprite
            UnityEditor.AssetDatabase.ImportAsset(assetPath);
            var importer = UnityEditor.AssetImporter.GetAtPath(assetPath)
                           as UnityEditor.TextureImporter;
            if (importer != null)
            {
                importer.textureType         = UnityEditor.TextureImporterType.Sprite;
                importer.spriteImportMode    = UnityEditor.SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled       = false;
                importer.SaveAndReimport();
            }

            // 6. Auto-assign to ItemData.icon
            var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (this.targetItem != null && sprite != null)
            {
                var so = new UnityEditor.SerializedObject(this.targetItem);
                so.FindProperty("icon").objectReferenceValue = sprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                UnityEditor.EditorUtility.SetDirty(this.targetItem);
                UnityEditor.AssetDatabase.SaveAssetIfDirty(this.targetItem);
            }

            // 7. Cleanup — destroy instance and restore existing renderers
            DestroyImmediate(instance);
            foreach (var r in existingRenderers) if (r != null) r.enabled = true;

            // 8. Ping result in Project window
            if (sprite != null)
            {
                UnityEditor.EditorGUIUtility.PingObject(sprite);
                Debug.Log($"[ItemSpriteCapture] Saved sprite to {assetPath}");
            }
        }
#endif
    }
}
