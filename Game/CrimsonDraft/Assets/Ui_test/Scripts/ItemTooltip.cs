using UnityEngine;
using TMPro;

namespace CrimsonDraft.UI
{
    public class ItemTooltip : MonoBehaviour
    {
        [SerializeField] private CanvasGroup   canvasGroup;
        [SerializeField] private TMP_Text      nameLabel;
        [SerializeField] private float         horizontalPadding = 4f;

        private RectTransform rectTransform;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            canvasGroup.blocksRaycasts = false;
            Hide();
        }

        // Shows tooltip anchored to the top-right of the item, flips left if off-screen.
        public void ShowAtItem(string itemName, RectTransform itemRect)
        {
            SetText(itemName);

            Vector3[] corners = new Vector3[4];
            itemRect.GetWorldCorners(corners);
            // corners[1]=TL  corners[2]=TR
            PlaceWithClamp(rightAnchor: corners[2], leftAnchor: corners[1]);

            SetVisible(true);
        }

        // Moves tooltip above the selector center (when context menu is open).
        public void ShowAboveSelector(RectTransform selectorRect)
        {
            Vector3[] corners = new Vector3[4];
            selectorRect.GetWorldCorners(corners);
            Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;
            topCenter += Vector3.up * (rectTransform.sizeDelta.y + 4f);

            // Center horizontally above selector, then clamp horizontally
            rectTransform.pivot    = new Vector2(0.5f, 0f);
            rectTransform.position = topCenter;

            ClampHorizontal();
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

        // Place to the right of rightAnchor; flip to left of leftAnchor if off-screen. Clamps all edges.
        void PlaceWithClamp(Vector3 rightAnchor, Vector3 leftAnchor)
        {
            rectTransform.pivot    = new Vector2(0f, 1f);
            rectTransform.position = rightAnchor;

            Vector3[] tooltipCorners = new Vector3[4];
            rectTransform.GetWorldCorners(tooltipCorners);

            if (tooltipCorners[2].x > Screen.width)
            {
                rectTransform.pivot    = new Vector2(1f, 1f);
                rectTransform.position = leftAnchor;
                rectTransform.GetWorldCorners(tooltipCorners);
            }

            Vector3 pos = rectTransform.position;

            if (tooltipCorners[0].x < 0)
                pos.x -= tooltipCorners[0].x;
            else if (tooltipCorners[2].x > Screen.width)
                pos.x -= tooltipCorners[2].x - Screen.width;

            if (tooltipCorners[0].y < 0)
                pos.y -= tooltipCorners[0].y;
            else if (tooltipCorners[1].y > Screen.height)
                pos.y -= tooltipCorners[1].y - Screen.height;

            rectTransform.position = pos;
        }

        void ClampHorizontal()
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            float overflow = corners[2].x - Screen.width;
            if (overflow > 0)
            {
                Vector3 pos = rectTransform.position;
                pos.x -= overflow;
                rectTransform.position = pos;
            }

            if (corners[0].x < 0)
            {
                Vector3 pos = rectTransform.position;
                pos.x -= corners[0].x;
                rectTransform.position = pos;
            }
        }

        void SetVisible(bool visible) => canvasGroup.alpha = visible ? 1f : 0f;
    }
}
