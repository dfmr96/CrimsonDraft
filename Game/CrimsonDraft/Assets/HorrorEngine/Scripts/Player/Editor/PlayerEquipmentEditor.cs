using UnityEditor;
using UnityEngine;

namespace HorrorEngine
{
    [CustomEditor(typeof(PlayerEquipment))]
    public class PlayerEquipmentEditor : Editor
    {
        private EquipableItemData m_Previewing;
        private GameObject m_PreviewInstance;

       

        private void OnDisable()
        {
            PlayerEquipment equipment = (target as PlayerEquipment);
            equipment.GetEquipped(EquipmentSlot.Primary, out ItemData equippedItem, out GameObject equippedInstance);
            bool currentlyEquipped = equippedItem == m_Previewing;
            if (!currentlyEquipped && m_PreviewInstance)
                DestroyImmediate(m_PreviewInstance);
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();


            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            PlayerEquipment equipment = (target as PlayerEquipment);
            equipment.GetEquipped(EquipmentSlot.Primary, out ItemData equippedItem, out GameObject equippedInstance);

            if (!m_Previewing && equippedItem != null)
            {
                var equippable = equippedItem as EquipableItemData;
                m_Previewing = equippable;
                m_PreviewInstance = equippedInstance;

                m_Previewing.CharacterAttachment.Position = m_PreviewInstance.transform.localPosition;
                m_Previewing.CharacterAttachment.Rotation = m_PreviewInstance.transform.localRotation.eulerAngles;
                m_Previewing.CharacterAttachment.Scale = m_PreviewInstance.transform.localScale;
            }

            if (!m_Previewing)
                EditorGUILayout.HelpBox("Drop a WeaponData here to preview how it looks when attached to the character", MessageType.Info);

            m_Previewing = (EquipableItemData)EditorGUILayout.ObjectField(m_Previewing, typeof(EquipableItemData), false);

            bool currentlyEquipped = equippedItem == m_Previewing; 
            SocketController sockets = equipment.GetComponentInChildren<SocketController>();
            if  (m_Previewing)
            {
                m_Previewing.CharacterAttachment.Position = EditorGUILayout.Vector3Field("Position", m_Previewing.CharacterAttachment.Position);
                m_Previewing.CharacterAttachment.Rotation = EditorGUILayout.Vector3Field("Rotation", m_Previewing.CharacterAttachment.Rotation);
                m_Previewing.CharacterAttachment.Scale = EditorGUILayout.Vector3Field("Scale", m_Previewing.CharacterAttachment.Scale);

                if (m_PreviewInstance)
                {
                    m_PreviewInstance.transform.localPosition = m_Previewing.CharacterAttachment.Position;
                    m_PreviewInstance.transform.localRotation = Quaternion.Euler(m_Previewing.CharacterAttachment.Rotation);
                    m_PreviewInstance.transform.localScale = m_Previewing.CharacterAttachment.Scale;
                }

                if (EditorGUI.EndChangeCheck())
                {
                    if (!currentlyEquipped)
                    {
                        if (m_PreviewInstance)
                            DestroyImmediate(m_PreviewInstance);
                        m_PreviewInstance = Instantiate(m_Previewing.EquipPrefab);
                        sockets.Attach(m_PreviewInstance, m_Previewing.CharacterAttachment);
                    }

                    EditorUtility.SetDirty(m_Previewing);
                }

                if (currentlyEquipped && m_PreviewInstance && GUILayout.Button("Go to equipped instance"))
                {
                    Selection.activeObject = m_PreviewInstance;
                }

                if (GUILayout.Button("Apply changes to preview item"))
                {
                    EditorUtility.SetDirty(m_Previewing);
                    AssetDatabase.SaveAssetIfDirty(m_Previewing);
                }
            }
            else if (!currentlyEquipped && m_PreviewInstance)
            { 
                    DestroyImmediate(m_PreviewInstance);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}