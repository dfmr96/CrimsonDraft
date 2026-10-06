using UnityEngine;
using UnityEngine.Serialization;

namespace HorrorEngine
{
 
    public class UISettingsList : MonoBehaviour
    {
        [FormerlySerializedAs("Settings")]
        [SerializeField] SettingsSet m_Settings;

        // ---------------------------------------------------------

        private void Start()
        {
            foreach (SettingsElementContent content in m_Settings.Elements)
            {
                Debug.Assert(content, "A setting element does not have any assigned content");
                if (content)
                {
                    Debug.Assert(content.UIPrefab, "A setting element content does not have any assigned prefab");
                    if (content.UIPrefab)
                    {
                        GameObject instance = Instantiate(content.UIPrefab, transform);
                        UISettingsElement uiElement = instance.GetComponent<UISettingsElement>();
                        uiElement.Fill(content);
                        uiElement.OnValueChanged.AddListener((newValue) =>
                        {
                            SettingsManager.Instance.Set(content, newValue);
                            if (content.ApplyImmediate)
                                content.Apply();
                        });
                    }
                }

                gameObject.SetActive(false);
                gameObject.SetActive(true);
            }
        }
    }
}