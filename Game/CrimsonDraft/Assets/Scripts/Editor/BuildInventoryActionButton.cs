#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrimsonDraft.Navigation.UI;

namespace CrimsonDraft.Editor
{
    public static class BuildInventoryActionButton
    {
        private const string PrefabPath = "Assets/Prefabs/UI/InventoryActionButton.prefab";

        [MenuItem("CrimsonDraft/Build InventoryActionButton Prefab")]
        public static void Build()
        {
            // ── Root ─────────────────────────────────────────────────────────────
            var root     = new GameObject("InventoryActionButton", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(180f, 32f);

            var btn = root.AddComponent<InventoryActionButton>();

            var canvasGroup = root.AddComponent<CanvasGroup>();

            // ── Cursor Highlight ─────────────────────────────────────────────────
            var highlightGO   = new GameObject("CursorHighlight", typeof(RectTransform));
            highlightGO.transform.SetParent(root.transform, false);
            var highlightRect = highlightGO.GetComponent<RectTransform>();
            highlightRect.anchorMin = Vector2.zero;
            highlightRect.anchorMax = Vector2.one;
            highlightRect.offsetMin = Vector2.zero;
            highlightRect.offsetMax = Vector2.zero;
            var highlightImg        = highlightGO.AddComponent<Image>();
            highlightImg.color          = new Color(1f, 0.9f, 0.2f, 0.25f);
            highlightImg.raycastTarget  = false;
            highlightImg.enabled        = false; // off by default

            // ── Label ────────────────────────────────────────────────────────────
            var labelGO   = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(root.transform, false);
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 0f);
            labelRect.offsetMax = new Vector2(-12f, 0f);
            var tmp           = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text          = "Action";
            tmp.fontSize      = 16f;
            tmp.color         = Color.white;
            tmp.alignment     = TextAlignmentOptions.MidlineLeft;

            // ── Wire serialized fields ────────────────────────────────────────────
            var so = new SerializedObject(btn);
            so.FindProperty("label").objectReferenceValue           = tmp;
            so.FindProperty("cursorHighlight").objectReferenceValue = highlightImg;
            so.ApplyModifiedProperties();

            // ── Save as prefab ────────────────────────────────────────────────────
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            if (prefab != null)
                Debug.Log($"[BuildInventoryActionButton] Prefab saved to {PrefabPath}");
            else
                Debug.LogError("[BuildInventoryActionButton] Failed to save prefab.");
        }
    }
}
#endif
