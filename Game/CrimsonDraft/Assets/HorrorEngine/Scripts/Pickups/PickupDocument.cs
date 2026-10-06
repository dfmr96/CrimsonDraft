using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public class PickupDocument : Pickup
    {
        [FormerlySerializedAs("Data")]
        [SerializeField] private DocumentData m_Data;
        [SerializeField] private bool m_ReadOnPickup = true;
        [SerializeField] private DialogData m_DocObtainedDialog;
        [SerializeField] private string m_DocObtainedDialogNameTag = "{DOCNAME}";

        // --------------------------------------------------------------------

        public override void Take()
        {
            GameManager.Instance.Inventory.Documents.Add(m_Data);

            if (m_ReadOnPickup)
                Read();

            gameObject.SetActive(false);

            base.Take();
        }

        // --------------------------------------------------------------------

        public void Read()
        {
            if (m_DocObtainedDialog.IsValid())
            {
                UIManager.PushAction(new UIStackedAction()
                {
                    Action = ShowDocumentPickedDialog,
                    Name = "PickupDialog"
                });
            }
            
            UIManager.Get<UIDocument>().Show(m_Data);
        }

        // --------------------------------------------------------------------

        private void ShowDocumentPickedDialog()
        {
            m_DocObtainedDialog.SetTagReplacement(m_DocObtainedDialogNameTag, m_Data.Name);
            UIManager.Get<UIDialog>().Show(m_DocObtainedDialog);
        }

        // --------------------------------------------------------------------

        public void UpdateTag() // Available  for calling from UnityEvents
        {
            m_DocObtainedDialog.SetTagReplacement(m_DocObtainedDialogNameTag, m_Data.Name);
        }
    }
}