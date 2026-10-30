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
    /// discovered. The player's current room pulses its alpha. Doors draw in their own layer
    /// above every room, with the sprite for their state and orientation.</summary>
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

        private const string DoorLayerName = "Doors";

        private readonly List<Image> images = new();
        private readonly List<Image> doorImages = new();
        private RectTransform? doorLayer;
        private Image? currentImage;

        public bool IsVisible => this.root.activeSelf;

        public void Show(IReadOnlyList<MapRoomVisual> visuals, Rect floorBounds, string floorName, bool hasUp, bool hasDown)
            => Show(visuals, System.Array.Empty<MapDoorVisual>(), null, floorBounds, floorName, hasUp, hasDown);

        /// <param name="floorBounds">Frame of the whole floor (MapLayoutBounds.Floor), not of the
        /// visible rooms, so centre and scale never shift as rooms are discovered.</param>
        /// <param name="doorSprites">Sprite per door state and orientation; a door whose sprite
        /// is unassigned is skipped.</param>
        public void Show(
            IReadOnlyList<MapRoomVisual> visuals,
            IReadOnlyList<MapDoorVisual> doors,
            MapDoorSprites? doorSprites,
            Rect floorBounds,
            string floorName,
            bool hasUp,
            bool hasDown)
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

            ShowDoors(doors, doorSprites, centre);

            float scale = FitScale(floorBounds.size, this.viewport.rect.size);
            this.content.localScale = new Vector3(scale, scale, 1f);
        }

        // A door's Position is its sprite's bottom-left corner, like a room's, and the vertical
        // sprite is used instead of rotating, so doors never need a rotation here.
        private void ShowDoors(IReadOnlyList<MapDoorVisual> doors, MapDoorSprites? doorSprites, Vector2 centre)
        {
            int shown = 0;
            foreach (var door in doors)
            {
                var sprite = doorSprites?.For(door.State, door.IsVertical, door.Stairs);
                if (sprite == null)
                    continue;

                var image = AcquireDoor(shown++);
                var rect  = image.rectTransform;
                image.sprite          = sprite;
                image.color           = Color.white;
                rect.sizeDelta        = sprite.rect.size;
                rect.anchoredPosition = door.Position + sprite.rect.size * 0.5f - centre;
            }

            for (int i = shown; i < this.doorImages.Count; i++)
                this.doorImages[i].gameObject.SetActive(false);

            if (this.doorLayer != null)
                this.doorLayer.SetAsLastSibling();
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

        private Image AcquireDoor(int index)
        {
            if (index < this.doorImages.Count)
            {
                this.doorImages[index].gameObject.SetActive(true);
                return this.doorImages[index];
            }

            if (this.doorLayer == null)
            {
                this.doorLayer = new GameObject(DoorLayerName, typeof(RectTransform)).GetComponent<RectTransform>();
                this.doorLayer.SetParent(this.content, false);
            }

            var go = new GameObject($"Door_{index}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(this.doorLayer, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            this.doorImages.Add(image);
            return image;
        }
    }
}
