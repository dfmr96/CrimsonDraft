using UnityEngine;
using TMPro;

namespace CrimsonDraft.UI
{
    public class ItemTooltip : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text    nameLabel;
        [SerializeField] private float       horizontalPadding = 4f;

        private RectTransform rectTransform;
        private Camera        eventCam;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            canvasGroup.blocksRaycasts = false;

            Canvas root = GetComponentInParent<Canvas>()?.rootCanvas;
            eventCam = (root != null && root.renderMode == RenderMode.ScreenSpaceCamera)
                ? root.worldCamera : null;

            Hide();
        }

        // Shows the name beside the item — flips left/right to avoid screen edges.
        // Works correctly for rotated items and Screen Space Camera canvas.
        public void ShowAtItem(string itemName, RectTransform itemRect)
        {
            SetText(itemName);
            PositionBesideRect(itemRect);
            SetVisible(true);
        }

        // Shows the name above the item's top edge (used while context menu is open).
        public void ShowAboveSelector(RectTransform selectorRect)
        {
            Vector3[] wc = new Vector3[4];
            selectorRect.GetWorldCorners(wc);

            // Find the two topmost corners (rotation-agnostic)
            int t0 = 0;
            for (int i = 1; i < 4; i++)
                if (RectTransformUtility.WorldToScreenPoint(eventCam, wc[i]).y >
                    RectTransformUtility.WorldToScreenPoint(eventCam, wc[t0]).y) t0 = i;
            int t1 = t0 == 0 ? 1 : 0;
            for (int i = 0; i < 4; i++)
                if (i != t0 && RectTransformUtility.WorldToScreenPoint(eventCam, wc[i]).y >
                               RectTransformUtility.WorldToScreenPoint(eventCam, wc[t1]).y) t1 = i;

            // Anchor tooltip bottom-center to top-center of selector
            rectTransform.pivot    = new Vector2(0.5f, 0f);
            rectTransform.position = (wc[t0] + wc[t1]) * 0.5f;

            SetVisible(true);
        }

        public void Hide() => SetVisible(false);

        // ── Internals ────────────────────────────────────────────────────────

        void SetText(string text)
        {
            nameLabel.text = text;
            nameLabel.ForceMeshUpdate();

            float width = nameLabel.preferredWidth + horizontalPadding * 2f;
            rectTransform.sizeDelta = new Vector2(width, rectTransform.sizeDelta.y);
        }

        void PositionBesideRect(RectTransform targetRect)
        {
            Vector3[] wc = new Vector3[4];
            targetRect.GetWorldCorners(wc);

            // Convert to screen space to find the visual top edge regardless of item rotation
            Vector2[] sc = new Vector2[4];
            for (int i = 0; i < 4; i++)
                sc[i] = RectTransformUtility.WorldToScreenPoint(eventCam, wc[i]);

            // Two corners with highest screen Y = visual top edge
            int t0 = 0;
            for (int i = 1; i < 4; i++)
                if (sc[i].y > sc[t0].y) t0 = i;
            int t1 = t0 == 0 ? 1 : 0;
            for (int i = 0; i < 4; i++)
                if (i != t0 && sc[i].y > sc[t1].y) t1 = i;

            int idxL = sc[t0].x < sc[t1].x ? t0 : t1;
            int idxR = sc[t0].x < sc[t1].x ? t1 : t0;

            // Default: right side (pivot TL at visual TR)
            rectTransform.pivot    = new Vector2(0f, 1f);
            rectTransform.position = wc[idxR];

            // Flip left if right edge overflows
            Vector3[] tc = new Vector3[4];
            rectTransform.GetWorldCorners(tc);
            if (RectTransformUtility.WorldToScreenPoint(eventCam, tc[2]).x > Screen.width)
            {
                rectTransform.pivot    = new Vector2(1f, 1f);
                rectTransform.position = wc[idxL];
            }
        }

        void SetVisible(bool visible) => canvasGroup.alpha = visible ? 1f : 0f;
    }
}
