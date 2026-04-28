using UnityEngine;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public class PickupMap : Pickup
    {
        [SerializeField] private MapData m_Data;
        [SerializeField] private DialogData m_MapObtainedDialog;
        [SerializeField] private string m_NameTag = "{MAPNAME}";
        [FormerlySerializedAs("m_OpenMapOnPikcup")]
        [SerializeField] private bool m_OpenMapOnPickup = true;
        [SerializeField] private bool m_GiveEntireSet = true;

        private Choice m_Choice;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Choice = GetComponent<Choice>();
            m_Choice.Data.ChoiceDialog.SetTagReplacement(m_NameTag, m_Data.Name);
        }

        // --------------------------------------------------------------------

        public override void Take()
        {
            if (m_GiveEntireSet && m_Data.MapSet)
            {
                foreach(var map in m_Data.MapSet.Maps)
                    GameManager.Instance.Inventory.Maps.Add(map);
            }
            else
            {
                GameManager.Instance.Inventory.Maps.Add(m_Data);
            }

            gameObject.SetActive(false);

            if (m_OpenMapOnPickup)
            {
                if (m_MapObtainedDialog.IsValid())
                { 
                
                    UIManager.PushAction(new UIStackedAction()
                    {
                        Action = ShowMapPickedUpDialog,
                        Name = "PickupMapDialog"
                    });
                }

                UIManager.Get<UIMap>().Show(m_Data);
            }
            else if (m_MapObtainedDialog.IsValid())
            {
                ShowMapPickedUpDialog();
            }

            base.Take();
        }

        // --------------------------------------------------------------------

        private void ShowMapPickedUpDialog()
        {
            m_MapObtainedDialog.SetTagReplacement(m_NameTag, m_Data.Name);
            UIManager.Get<UIDialog>().Show(m_MapObtainedDialog);
        }

        // --------------------------------------------------------------------

        public void UpdateTag() // Available  for calling from UnityEvents
        {
            m_Choice.Data.ChoiceDialog.SetTagReplacement(m_NameTag, m_Data.Name);
            m_MapObtainedDialog.SetTagReplacement(m_NameTag, m_Data.Name);
        }
    }
}