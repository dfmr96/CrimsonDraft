#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrimsonDraft.Navigation.UI;

namespace CrimsonDraft.Editor
{
    public static class BuildInventoryPanel
    {
        [MenuItem("CrimsonDraft/Rebuild Inventory Panel")]
        public static void Rebuild()
        {
            var overlayView = GameObject.Find("PlaceholderOverlayView");
            if (overlayView == null)
            {
                Debug.LogError("[BuildInventoryPanel] Could not find 'PlaceholderOverlayView' in scene.");
                return;
            }

            var existing = overlayView.transform.Find("Inventory");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                Debug.Log("[BuildInventoryPanel] Deleted existing Inventory GO.");
            }

            // ── Root ──────────────────────────────────────────────────────────────
            var root     = MakeUI("Inventory", overlayView.transform);
            var rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);
            root.SetActive(false);

            var rootImg = root.AddComponent<Image>();
            rootImg.color          = new Color(0f, 0f, 0f, 0.85f);
            rootImg.raycastTarget  = false;

            var view = root.AddComponent<InventoryView>();

            // ── Cards Container ───────────────────────────────────────────────────
            var cardsGO   = MakeUI("CardsContainer", root.transform);
            var cardsRect = cardsGO.GetComponent<RectTransform>();
            cardsRect.anchorMin = new Vector2(0f, 0.3f);
            cardsRect.anchorMax = new Vector2(1f, 0.9f);
            cardsRect.offsetMin = new Vector2(40f, 0f);
            cardsRect.offsetMax = new Vector2(-40f, 0f);

            var hlg = cardsGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = 20f;
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.childControlWidth     = true;
            hlg.childControlHeight    = true;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;

            // ── Cursor ────────────────────────────────────────────────────────────
            var cursorGO   = MakeUI("Cursor", root.transform);
            var cursorRect = cursorGO.GetComponent<RectTransform>();
            cursorRect.anchorMin = Vector2.zero;
            cursorRect.anchorMax = Vector2.zero;
            cursorRect.sizeDelta = new Vector2(80f, 80f);

            var cursorImg       = cursorGO.AddComponent<Image>();
            cursorImg.color         = new Color(1f, 0.9f, 0.2f, 0.6f);
            cursorImg.raycastTarget = false;

            var iconGO   = MakeUI("CursorIcon", cursorGO.transform);
            var iconRect = iconGO.GetComponent<RectTransform>();
            Stretch(iconRect);
            var iconImg      = iconGO.AddComponent<Image>();
            iconImg.enabled       = false;
            iconImg.raycastTarget = false;

            // ── Info Panel ────────────────────────────────────────────────────────
            var infoGO   = MakeUI("InfoPanel", root.transform);
            var infoRect = infoGO.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0f, 0.05f);
            infoRect.anchorMax = new Vector2(0.5f, 0.28f);
            infoRect.offsetMin = new Vector2(40f, 0f);
            infoRect.offsetMax = Vector2.zero;
            infoGO.SetActive(false);

            var ivlg = infoGO.AddComponent<VerticalLayoutGroup>();
            ivlg.spacing               = 4f;
            ivlg.childAlignment        = TextAnchor.LowerLeft;
            ivlg.childControlWidth     = true;
            ivlg.childControlHeight    = false;
            ivlg.childForceExpandWidth  = true;
            ivlg.childForceExpandHeight = false;

            var infoNameGO   = MakeUI("InfoName", infoGO.transform);
            var infoNameRect = infoNameGO.GetComponent<RectTransform>();
            infoNameRect.sizeDelta = new Vector2(0f, 28f);
            var infoNameTMP  = infoNameGO.AddComponent<TextMeshProUGUI>();
            infoNameTMP.text     = "Item Name";
            infoNameTMP.fontSize = 22f;
            infoNameTMP.color    = Color.white;

            var infoDetailGO   = MakeUI("InfoDetail", infoGO.transform);
            var infoDetailRect = infoDetailGO.GetComponent<RectTransform>();
            infoDetailRect.sizeDelta = new Vector2(0f, 22f);
            var infoDetailTMP  = infoDetailGO.AddComponent<TextMeshProUGUI>();
            infoDetailTMP.text     = "";
            infoDetailTMP.fontSize = 18f;
            infoDetailTMP.color    = new Color(0.8f, 0.8f, 0.8f, 1f);

            // ── Context Menu ──────────────────────────────────────────────────────
            var ctxGO   = MakeUI("ContextMenu", root.transform);
            var ctxRect = ctxGO.GetComponent<RectTransform>();
            ctxRect.anchorMin = new Vector2(0.5f, 0.3f);
            ctxRect.anchorMax = new Vector2(0.5f, 0.3f);
            ctxRect.pivot     = new Vector2(0f, 0f);
            ctxRect.sizeDelta = new Vector2(200f, 0f);
            ctxGO.SetActive(false);

            var ctxImg       = ctxGO.AddComponent<Image>();
            ctxImg.color         = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            var ctxCsf            = ctxGO.AddComponent<ContentSizeFitter>();
            ctxCsf.verticalFit    = ContentSizeFitter.FitMode.PreferredSize;

            var cvlg = ctxGO.AddComponent<VerticalLayoutGroup>();
            cvlg.padding               = new RectOffset(8, 8, 8, 8);
            cvlg.spacing               = 4f;
            cvlg.childControlWidth     = true;
            cvlg.childControlHeight    = true;
            cvlg.childForceExpandWidth  = true;
            cvlg.childForceExpandHeight = false;

            var ctxContainerGO   = MakeUI("Container", ctxGO.transform);
            var ctxContainerRect = ctxContainerGO.GetComponent<RectTransform>();
            Stretch(ctxContainerRect);

            // ── Examine Overlay ───────────────────────────────────────────────────
            var examGO   = MakeUI("ExamineOverlay", root.transform);
            var examRect = examGO.GetComponent<RectTransform>();
            Stretch(examRect);
            examGO.SetActive(false);

            var examImg       = examGO.AddComponent<Image>();
            examImg.color         = new Color(0f, 0f, 0f, 0.95f);

            var examTextGO   = MakeUI("ExamineText", examGO.transform);
            var examTextRect = examTextGO.GetComponent<RectTransform>();
            examTextRect.anchorMin = new Vector2(0.1f, 0.1f);
            examTextRect.anchorMax = new Vector2(0.9f, 0.9f);
            examTextRect.offsetMin = Vector2.zero;
            examTextRect.offsetMax = Vector2.zero;
            var examTMP       = examTextGO.AddComponent<TextMeshProUGUI>();
            examTMP.text          = "";
            examTMP.fontSize      = 20f;
            examTMP.color         = Color.white;
            examTMP.alignment     = TextAlignmentOptions.Center;

            // ── Wire serialized fields ────────────────────────────────────────────
            var so = new SerializedObject(view);
            so.FindProperty("cardsContainer").objectReferenceValue       = cardsGO.transform;
            so.FindProperty("cursorRect").objectReferenceValue           = cursorRect;
            so.FindProperty("cursorIcon").objectReferenceValue           = iconImg;
            so.FindProperty("infoPanelRoot").objectReferenceValue        = infoGO;
            so.FindProperty("infoName").objectReferenceValue             = infoNameTMP;
            so.FindProperty("infoDetail").objectReferenceValue           = infoDetailTMP;
            so.FindProperty("contextMenuRoot").objectReferenceValue      = ctxGO;
            so.FindProperty("contextMenuContainer").objectReferenceValue = ctxContainerGO.transform;
            so.FindProperty("examineOverlayRoot").objectReferenceValue   = examGO;
            so.FindProperty("examineText").objectReferenceValue          = examTMP;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(root);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);

            Debug.Log("[BuildInventoryPanel] Done. Wire 'cardPrefab' and 'contextMenuRowPrefab' manually in the Inspector.");
        }

        private static GameObject MakeUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            GameObjectUtility.SetParentAndAlign(go, parent.gameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
#endif
