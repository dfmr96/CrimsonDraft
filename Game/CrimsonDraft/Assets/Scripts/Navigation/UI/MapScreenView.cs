#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CrimsonDraft.Infrastructure.Map;

namespace CrimsonDraft.Navigation.UI
{
    /// <summary>Draws one floor of the map as room sprites, centred (to the whole pixel) on the
    /// whole floor's frame and shrunk to fit the viewport, so the view never moves as rooms are
    /// discovered. The player's current room pulses its alpha.</summary>
    public sealed class MapScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject      root      = null!;
        [SerializeField] private RectTransform   viewport  = null!;
        [SerializeField] private RectTransform   content   = null!;
        [SerializeField] private TextMeshProUGUI deckName  = null!;
        [SerializeField] private GameObject      upArrow   = null!;
        [SerializeField] private GameObject      downArrow = null!;

        [Header("Current room")]
        [SerializeField, Range(0f, 1f)] private float minAlpha   = 0.3f;
        [SerializeField]                private float pulseSpeed = 3f;

        private readonly List<Image> images = new();
        private Image? currentImage;

        public bool IsVisible => this.root.activeSelf;

        /// <param name="floorBounds">Frame of the whole floor (MapLayoutBounds.Floor), not of the
        /// visible rooms, so centre and scale never shift as rooms are discovered.</param>
        public void Show(IReadOnlyList<MapRoomVisual> visuals, Rect floorBounds, string floorName, bool hasUp, bool hasDown)
        {
            this.root.SetActive(true);
            this.deckName.text = floorName;
            this.upArrow.SetActive(hasUp);
            this.downArrow.SetActive(hasDown);

            var centre = new Vector2(Mathf.Round(floorBounds.center.x), Mathf.Round(floorBounds.center.y));
            this.currentImage = null;

            for (int i = 0; i < visuals.Count; i++)
            {
                var visual = visuals[i];
                var image  = Acquire(i);
                var rect   = image.rectTransform;

                image.sprite           = visual.Sprite;
                image.color            = Color.white;
                rect.sizeDelta         = visual.Sprite.rect.size;
                rect.anchoredPosition  = (Vector2)visual.Position + MapLayoutBounds.SizeOf(visual.Sprite, visual.QuarterTurns) * 0.5f - centre;
                rect.localEulerAngles  = new Vector3(0f, 0f, visual.QuarterTurns * 90f);

                if (visual.IsCurrent)
                    this.currentImage = image;
            }

            for (int i = visuals.Count; i < this.images.Count; i++)
                this.images[i].gameObject.SetActive(false);

            float scale = FitScale(floorBounds.size, this.viewport.rect.size);
            this.content.localScale = new Vector3(scale, scale, 1f);
        }

        public void Hide()
        {
            this.root.SetActive(false);
            this.currentImage = null;
        }

        public static float FitScale(Vector2 contentSize, Vector2 viewportSize)
        {
            if (contentSize.x <= 0f || contentSize.y <= 0f)
                return 1f;

            return Mathf.Min(1f, viewportSize.x / contentSize.x, viewportSize.y / contentSize.y);
        }

        public static Color PulseColor(float time, float speed, float minAlpha)
        {
            float t = (Mathf.Sin(time * speed) + 1f) * 0.5f;
            return new Color(1f, 1f, 1f, Mathf.Lerp(minAlpha, 1f, t));
        }

        private void Update()
        {
            if (this.currentImage == null)
                return;

            this.currentImage.color = PulseColor(Time.unscaledTime, this.pulseSpeed, this.minAlpha);
        }

        private Image Acquire(int index)
        {
            if (index < this.images.Count)
            {
                this.images[index].gameObject.SetActive(true);
                return this.images[index];
            }

            var go = new GameObject($"Room_{index}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(this.content, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            this.images.Add(image);
            return image;
        }
    }
}
