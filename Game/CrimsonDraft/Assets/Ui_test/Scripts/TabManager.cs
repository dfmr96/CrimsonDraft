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

        [SerializeField] private Tab[]        tabs               = null!; // 0=Inventory  1=Map  2=Files
        [SerializeField] private int          startingTab        = 0;
        [SerializeField] private GameObject[] tabIndicators      = null!;
        [SerializeField] private GameObject[] tabFocusHighlights = System.Array.Empty<GameObject>();
        [SerializeField] private GridCursor   gridCursor         = null!;

        [Header("Audio")]
        [SerializeField] private InventorySoundManager sfx = null!;

        [Header("Navigation Feel")]
        [SerializeField] private float initialRepeatDelay = 0.4f;
        [SerializeField] private float repeatInterval     = 0.1f;

        [Inject] private IInputService inputService = null!;
        private bool inputBound;

        private int  currentIndex;
        private bool tabBarActive;
        private int  focusedTabIndex;
        private bool holding;
        private Vector2Int lastDir;
        private float nextMoveTime;

        // ── Lifecycle ────────────────────────────────────────────────────────

        void OnEnable()
        {
            if (this.inputService == null || this.inputBound) return;
            this.inputService.InventoryConfirm.performed += OnConfirmTab;
            this.inputBound = true;
        }

        void OnDisable()
        {
            if (!this.inputBound || this.inputService == null) return;
            this.inputService.InventoryConfirm.performed -= OnConfirmTab;
            this.inputBound = false;
        }

        void Start()
        {
            if (this.gridCursor == null)
                this.gridCursor = GetComponentInChildren<GridCursor>(true);

            this.currentIndex = this.startingTab;
            for (int i = 0; i < this.tabs.Length; i++)
                this.tabs[i].root.SetActive(i == this.currentIndex);

            RefreshIndicators();
            ClearTabBarFocus();
            OnEnable();
        }

        void Update()
        {
            Vector2Int dir = ReadDirection();

            if (!this.tabBarActive)
            {
                // On a non-inventory tab: UP enters the tab bar
                if (this.currentIndex != 0 && dir.y > 0 && dir != this.lastDir)
                {
                    EnterTabBar();
                    this.lastDir    = dir;
                    this.holding    = true;
                    this.nextMoveTime = Time.unscaledTime + this.initialRepeatDelay;
                }
                else if (dir == Vector2Int.zero)
                {
                    this.holding = false;
                    this.lastDir = Vector2Int.zero;
                }
                return;
            }

            // Tab bar navigation
            if (dir == Vector2Int.zero)
            {
                this.holding = false;
                this.lastDir = Vector2Int.zero;
                return;
            }

            if (dir != this.lastDir)
            {
                ProcessTabBarMove(dir);
                this.lastDir      = dir;
                this.holding      = true;
                this.nextMoveTime = Time.unscaledTime + this.initialRepeatDelay;
            }
            else if (this.holding && Time.unscaledTime >= this.nextMoveTime)
            {
                ProcessTabBarMove(dir);
                this.nextMoveTime = Time.unscaledTime + this.repeatInterval;
            }
        }

        void ProcessTabBarMove(Vector2Int dir)
        {
            if (dir.y < 0) // DOWN → exit tab bar back to grid
            {
                ExitTabBar();
            }
            else if (dir.x != 0) // LEFT/RIGHT → navigate between tabs
            {
                this.focusedTabIndex = (this.focusedTabIndex + dir.x + this.tabs.Length) % this.tabs.Length;
                SetTabBarFocus(this.focusedTabIndex);
                this.sfx?.PlayCursorMove();
            }
        }

        // ── Input Callbacks ──────────────────────────────────────────────────

        void OnConfirmTab(InputAction.CallbackContext ctx)
        {
            if (!this.tabBarActive) return;
            int idx = this.focusedTabIndex;
            ExitTabBar();
            ActivateTab(idx);
        }

        // ── Tab Bar ──────────────────────────────────────────────────────────

        public void EnterTabBar()
        {
            this.tabBarActive    = true;
            this.focusedTabIndex = this.currentIndex;
            SetTabBarFocus(this.focusedTabIndex);
            this.gridCursor?.HideSelectorForTabBar();
            this.sfx?.PlayCursorOnItem();
        }

        void ExitTabBar()
        {
            this.tabBarActive = false;
            ClearTabBarFocus();
            if (this.currentIndex == 0)
                this.gridCursor?.ShowSelectorAfterTabBar();
        }

        // ── Switching ────────────────────────────────────────────────────────

        public void ActivateTab(int index)
        {
            if (index == this.currentIndex) return;

            if (this.tabs[this.currentIndex].name == "Inventory" || this.currentIndex == 0)
                this.gridCursor?.CancelAll();

            this.tabs[this.currentIndex].root.SetActive(false);
            this.currentIndex = index;
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

        public void SetTabBarFocus(int index)
        {
            for (int i = 0; i < this.tabFocusHighlights.Length; i++)
                if (this.tabFocusHighlights[i] != null)
                    this.tabFocusHighlights[i].SetActive(i == index);
        }

        public void ClearTabBarFocus()
        {
            foreach (var h in this.tabFocusHighlights)
                if (h != null) h.SetActive(false);
        }

        // ── Input Reading ────────────────────────────────────────────────────

        Vector2Int ReadDirection()
        {
            if (this.inputService == null) return Vector2Int.zero;
            Vector2 raw = this.inputService.InventoryNavigate.ReadValue<Vector2>();
            if (raw.sqrMagnitude < 0.01f) return Vector2Int.zero;
            float absX = Mathf.Abs(raw.x);
            float absY = Mathf.Abs(raw.y);
            if (absX >= absY) return raw.x > 0 ? Vector2Int.right : Vector2Int.left;
            return raw.y > 0 ? Vector2Int.up : Vector2Int.down;
        }

        // ── Public ───────────────────────────────────────────────────────────

        public bool   IsTabBarActive => this.tabBarActive;
        public int    TabCount       => this.tabs.Length;
        public int    CurrentIndex   => this.currentIndex;
        public string CurrentName    => this.tabs[this.currentIndex].name;
    }
}
