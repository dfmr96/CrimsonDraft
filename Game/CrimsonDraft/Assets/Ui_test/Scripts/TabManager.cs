#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.UI
{
    public class TabManager : MonoBehaviour
    {
        [System.Serializable]
        public struct Tab
        {
            public string     name;
            public GameObject root;
        }

        [SerializeField] private Tab[]        tabs           = null!; // 0=Inventory  1=Map  2=Files
        [SerializeField] private int          startingTab    = 0;
        [SerializeField] private GameObject[] tabIndicators  = null!;
        [SerializeField] private GridCursor   gridCursor     = null!;

        [Header("Audio")]
        [SerializeField] private InventorySoundManager sfx = null!;

        [Header("Standalone (sin VContainer)")]
        [SerializeField] private InputActionAsset? standaloneInputAsset;

        [Inject] private IInputService? inputService;

        private InputAction? nextTabFallback;
        private InputAction? prevTabFallback;
        private InputActionMap? standaloneMap;

        private int currentIndex;

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Awake()
        {
            if (this.standaloneInputAsset != null)
            {
                this.standaloneMap    = this.standaloneInputAsset.FindActionMap("Inventory");
                this.nextTabFallback  = this.standaloneMap?["NextTab"];
                this.prevTabFallback  = this.standaloneMap?["PrevTab"];
            }
        }

        void OnEnable()
        {
            if (this.inputService != null)
            {
                this.inputService.InventoryNextTab.performed += OnNextTab;
                this.inputService.InventoryPrevTab.performed += OnPrevTab;
            }
            else
            {
                if (this.nextTabFallback != null) this.nextTabFallback.performed += OnNextTab;
                if (this.prevTabFallback != null) this.prevTabFallback.performed += OnPrevTab;
            }
        }

        void OnDisable()
        {
            if (this.inputService != null)
            {
                this.inputService.InventoryNextTab.performed -= OnNextTab;
                this.inputService.InventoryPrevTab.performed -= OnPrevTab;
            }
            else
            {
                if (this.nextTabFallback != null) this.nextTabFallback.performed -= OnNextTab;
                if (this.prevTabFallback != null) this.prevTabFallback.performed -= OnPrevTab;
            }
        }

        void Start()
        {
            if (this.gridCursor == null)
                this.gridCursor = GetComponentInChildren<GridCursor>(true);

            this.currentIndex = this.startingTab;
            for (int i = 0; i < this.tabs.Length; i++)
                this.tabs[i].root.SetActive(i == this.currentIndex);

            RefreshIndicators();
        }

        // ── Input Callbacks ──────────────────────────────────────────────────

        private void OnNextTab(InputAction.CallbackContext ctx) => SwitchTab(1);
        private void OnPrevTab(InputAction.CallbackContext ctx) => SwitchTab(-1);

        // ── Switching ────────────────────────────────────────────────────────

        void SwitchTab(int dir)
        {
            int next = (this.currentIndex + dir + this.tabs.Length) % this.tabs.Length;
            if (next == this.currentIndex) return;

            if (this.tabs[this.currentIndex].name == "Inventory" || this.currentIndex == 0)
                this.gridCursor?.CancelAll();

            this.tabs[this.currentIndex].root.SetActive(false);
            this.currentIndex = next;
            this.tabs[this.currentIndex].root.SetActive(true);
            RefreshIndicators();
            this.sfx?.PlayTabSwitch();
        }

        void RefreshIndicators()
        {
            if (this.tabIndicators == null) return;
            for (int i = 0; i < this.tabIndicators.Length; i++)
                if (this.tabIndicators[i] != null)
                    this.tabIndicators[i].SetActive(i == this.currentIndex);
        }

        public int    CurrentIndex => this.currentIndex;
        public string CurrentName  => this.tabs[this.currentIndex].name;
    }
}
