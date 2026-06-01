using UnityEngine;

namespace CrimsonDraft.UI
{
    [RequireComponent(typeof(AudioSource))]
    public class InventorySoundManager : MonoBehaviour
    {
        public static InventorySoundManager Instance { get; private set; }

        [Header("Cursor")]
        [SerializeField] private AudioClip cursorMove;       // Cursor moves on empty cell
        [SerializeField] private AudioClip cursorOnItem;     // Cursor lands on an item

        [Header("Item Movement")]
        [SerializeField] private AudioClip itemPickup;       // Item lifted
        [SerializeField] private AudioClip itemMove;         // Cursor moves while holding item
        [SerializeField] private AudioClip itemPlace;        // Item placed successfully
        [SerializeField] private AudioClip itemPlaceInvalid; // Tried to place but no room
        [SerializeField] private AudioClip itemRotate;       // Item rotated
        [SerializeField] private AudioClip itemSwap;         // Item swapped with another

        [Header("Context Menu")]
        [SerializeField] private AudioClip menuOpen;         // Context menu opens
        [SerializeField] private AudioClip menuNavigate;     // Moving between menu options
        [SerializeField] private AudioClip menuConfirm;      // Option selected
        [SerializeField] private AudioClip menuCancel;       // Menu closed / back

        [Header("Inspect")]
        [SerializeField] private AudioClip inspectOpen;      // Inspect panel opens
        [SerializeField] private AudioClip inspectClose;     // Inspect panel closes

        [Header("Tabs")]
        [SerializeField] private AudioClip tabSwitch;        // Switching between tabs

        [Header("Settings")]
        [SerializeField] [Range(0f, 1f)] private float volume = 1f;

        private AudioSource audioSource;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // Null-safe accessor used by other scripts
        public static InventorySoundManager Get() => Instance;

        // ── Cursor ───────────────────────────────────────────────────────────
        public void PlayCursorMove()       => Play(cursorMove);
        public void PlayCursorOnItem()     => Play(cursorOnItem);

        // ── Item Movement ────────────────────────────────────────────────────
        public void PlayItemPickup()       => Play(itemPickup);
        public void PlayItemMove()         => Play(itemMove);
        public void PlayItemPlace()        => Play(itemPlace);
        public void PlayItemPlaceInvalid() => Play(itemPlaceInvalid);
        public void PlayItemRotate()       => Play(itemRotate);
        public void PlayItemSwap()         => Play(itemSwap);

        // ── Context Menu ─────────────────────────────────────────────────────
        public void PlayMenuOpen()         => Play(menuOpen);
        public void PlayMenuNavigate()     => Play(menuNavigate);
        public void PlayMenuConfirm()      => Play(menuConfirm);
        public void PlayMenuCancel()       => Play(menuCancel);

        // ── Inspect ──────────────────────────────────────────────────────────
        public void PlayInspectOpen()      => Play(inspectOpen);
        public void PlayInspectClose()     => Play(inspectClose);

        // ── Tabs ─────────────────────────────────────────────────────────────
        public void PlayTabSwitch()        => Play(tabSwitch);

        // ── Core ─────────────────────────────────────────────────────────────
        void Play(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
