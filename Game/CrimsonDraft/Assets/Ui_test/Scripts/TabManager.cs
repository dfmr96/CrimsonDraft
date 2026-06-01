using UnityEngine;
using UnityEngine.InputSystem;

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

        [SerializeField] private Tab[]       tabs;           // 0=Inventory  1=Map  2=Files
        [SerializeField] private int         startingTab = 0;
        [SerializeField] private GameObject[] tabIndicators; // one per tab, same order

        // Optional: reference to GridCursor so we can clean up held state on switch
        [SerializeField] private GridCursor gridCursor;

        private int currentIndex;

        private InputAction nextTabAction;
        private InputAction prevTabAction;

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Awake()
        {
            // Next tab: RB / R1 or E key
            nextTabAction = new InputAction("NextTab", InputActionType.Button);
            nextTabAction.AddBinding("<Keyboard>/e");
            nextTabAction.AddBinding("<Gamepad>/rightShoulder");
            nextTabAction.performed += _ => SwitchTab(1);

            // Prev tab: LB / L1 or Q key
            prevTabAction = new InputAction("PrevTab", InputActionType.Button);
            prevTabAction.AddBinding("<Keyboard>/q");
            prevTabAction.AddBinding("<Gamepad>/leftShoulder");
            prevTabAction.performed += _ => SwitchTab(-1);
        }

        void OnDestroy()
        {
            nextTabAction.Dispose();
            prevTabAction.Dispose();
        }

        void OnEnable()
        {
            nextTabAction.Enable();
            prevTabAction.Enable();
        }

        void OnDisable()
        {
            nextTabAction.Disable();
            prevTabAction.Disable();
        }

        void Start()
        {
            if (gridCursor == null)
                gridCursor = GetComponentInChildren<GridCursor>(true);

            // Activate only the starting tab
            currentIndex = startingTab;
            for (int i = 0; i < tabs.Length; i++)
                tabs[i].root.SetActive(i == currentIndex);

            RefreshIndicators();
        }

        // ── Switching ────────────────────────────────────────────────────────

        void SwitchTab(int dir)
        {
            int next = (currentIndex + dir + tabs.Length) % tabs.Length;
            if (next == currentIndex) return;

            // Clean up inventory state before leaving
            if (tabs[currentIndex].name == "Inventory" || currentIndex == 0)
                gridCursor?.CancelAll();

            tabs[currentIndex].root.SetActive(false);
            currentIndex = next;
            tabs[currentIndex].root.SetActive(true);
            RefreshIndicators();
            InventorySoundManager.Instance?.PlayTabSwitch();
        }

        void RefreshIndicators()
        {
            if (tabIndicators == null) return;
            for (int i = 0; i < tabIndicators.Length; i++)
                if (tabIndicators[i] != null)
                    tabIndicators[i].SetActive(i == currentIndex);
        }

        public int   CurrentIndex => currentIndex;
        public string CurrentName => tabs[currentIndex].name;
    }
}
