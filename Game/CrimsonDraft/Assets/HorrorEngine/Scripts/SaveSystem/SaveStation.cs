using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    public class SaveStation : MonoBehaviour
    {
        [SerializeField] private LocalizableText m_LocationName;

        // TODO - Remove eventually
        [FormerlySerializedAs("m_LocationName")]
        [HideInInspector]
        [SerializeField] private string m_LocationName_DEPRECATED;
        // ----------------

        [Tooltip("If an item is not specified, players will be able to save as many times they want")]
        [SerializeField] private ItemData m_ItemRequiredToSave;

        [SerializeField] private DialogData m_OnUseDialog;
        [SerializeField] private DialogData m_OnNoSaveItemDialog;

        private Choice m_SaveChoice;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_SaveChoice = GetComponent<Choice>();
        }

        // --------------------------------------------------------------------

        public void Use()
        {
            if (m_OnUseDialog.IsValid())
            {
                UIManager.PushAction(new UIStackedAction()
                {
                    Action = () =>
                    {
                        ShowChoice();
                    },
                    StopProcessingActions = true,
                    Name = "SaveStation.Use (ShowChoice)"
                });
                UIManager.Get<UIDialog>().Show(m_OnUseDialog);
            }
            else
            {
                ShowChoice();
            }
        }

        // --------------------------------------------------------------------

        private void ShowChoice()
        {
            if (m_ItemRequiredToSave)
            {
                if (!GameManager.Instance.Inventory.Contains(m_ItemRequiredToSave))
                {
                    UIManager.Get<UIDialog>().Show(m_OnNoSaveItemDialog);
                    return;
                }
            }

            m_SaveChoice.Choose();
        }

        // --------------------------------------------------------------------

        public void StartSave()
        {
            UIManager.Get<UISaveGame>().Show(m_LocationName, m_ItemRequiredToSave);
        }


        // --------------------------------------------------------------------

#if UNITY_EDITOR
        public void EditorOnly_MigrateUnlocalizedData()
        {
            bool dirty = false;
            if (!string.IsNullOrEmpty(m_LocationName_DEPRECATED))
            {
                m_LocationName.Unlocalized = m_LocationName_DEPRECATED;
                m_LocationName_DEPRECATED = "";
                dirty = true;
            }

            if (dirty)
            {
                var context = this;
                EditorApplication.delayCall += () => { EditorUtility.SetDirty(context); };
            }

        }
#endif
    }
}