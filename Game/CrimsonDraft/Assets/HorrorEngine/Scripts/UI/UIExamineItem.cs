using System;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UIExamineItem : MonoBehaviour, IInteractor
    {
        [SerializeField] private RawImage m_Image;
        [SerializeField] private Vector3 m_InitialRotation;
        [SerializeField] private float m_XRotationSpeed = 1f;
        [SerializeField] private float m_YRotationSpeed = 1f;
        [Range(0.0f,1.0f)]
        [SerializeField] private float m_RotationDrag = 1f;        
        [SerializeField] private TMPro.TextMeshProUGUI m_NameText;
        [SerializeField] private Transform m_InteractionPrompt;
        [SerializeField] private GameObject m_ButtonLegend;

        private IUIInput m_Input;
        private UIExamineItemRenderer m_Renderer;

        private bool m_CanInteract;
        private bool m_CanCloseSelf;

        private float m_XSpeed;
        private float m_YSpeed;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Input = GetComponentInParent<IUIInput>();
        }

        // --------------------------------------------------------------------

        void Start()
        {
            gameObject.SetActive(false);

            m_Renderer = FindFirstObjectByType<UIExamineItemRenderer>();
            Debug.Assert(m_Renderer, "Item examination renderer could not be found");
            m_Renderer.gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        public void Show(ItemData item, bool canInteract = true,  bool canCloseSelf = true)
        {
            m_CanInteract = canInteract;
            m_CanCloseSelf = canCloseSelf;

            ClearPreviousModel();

            PooledGameObject newPooled = item.ExamineModel.GetComponent<PooledGameObject>();
            GameObject newModel = newPooled ? GameObjectPool.Instance.GetFromPool(item.ExamineModel).gameObject : Instantiate(item.ExamineModel);
            newModel.transform.SetParent(m_Renderer.PreviewObject);
            newModel.transform.localPosition = Vector3.zero;
            newModel.transform.localRotation = Quaternion.identity;
            newModel.gameObject.SetActive(true);

            m_Renderer.gameObject.SetActive(true);
            m_Renderer.PreviewObject.localRotation = Quaternion.Euler(m_InitialRotation);
            m_Image.texture = m_Renderer.Texture; // Reset in case resolution changed

            PauseController.Instance.Pause(this);
            CursorController.Instance.SetInUI(true);

            m_NameText.text = item.Name;

            gameObject.SetActive(true);

            m_ButtonLegend?.SetActive(m_CanCloseSelf);

            m_XSpeed = 0;
            m_YSpeed = 0;

            m_InteractionPrompt.gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        private void ClearPreviousModel()
        {
            for (int i = 0; i < m_Renderer.PreviewObject.childCount; ++i)
            {
                var child = m_Renderer.PreviewObject.GetChild(i);
                var pooled = child.GetComponent<PooledGameObject>();
                if (pooled)
                    pooled.ReturnToPool();
                else
                    Destroy(child.gameObject);
            }
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            if (m_CanCloseSelf && m_Input.IsCancelDown())
            {
                UIManager.PushAction(new UIStackedAction()
                {
                    Action = () =>
                    {
                        UIManager.Get<UIInventory>().Show();
                    },
                    StopProcessingActions = true,
                    Name = "UIExamineItem.Update (Show Inventory)"
                });
                Hide();
            }

            UpdateRotation();

            if (m_CanInteract)
            {
                Physics.SyncTransforms(); // Needed for interaction since time is paused 

                m_Renderer.InteractionDetector.Cast();
                if (m_Renderer.InteractionDetector.FocusedInteractive)
                {
                    m_InteractionPrompt.gameObject.SetActive(true);
                    m_InteractionPrompt.position = m_Renderer.Camera.WorldToScreenPoint(m_Renderer.InteractionDetector.FocusedInteractive.transform.position);
                    
                    if (m_Input.IsConfirmDown())
                    {
                        m_Renderer.InteractionDetector.FocusedInteractive.Interact(this);
                    }
                }
                else
                {
                    m_InteractionPrompt.gameObject.SetActive(false);
                }
            }

#if UNITY_EDITOR
            m_Image.texture = m_Renderer.Texture; // Reset in case resolution changed (this should only happen here in editor)
#endif
        }

        // --------------------------------------------------------------------

        private void UpdateRotation()
        {
            m_XSpeed += m_Input.GetPrimaryAxis().x * Time.unscaledDeltaTime * m_XRotationSpeed;
            m_YSpeed += m_Input.GetPrimaryAxis().y * Time.unscaledDeltaTime * m_YRotationSpeed;

            Vector3 objectUp = m_Renderer.PreviewObject.InverseTransformDirection(Vector3.up);
            Vector3 objectRight = m_Renderer.PreviewObject.InverseTransformDirection(Vector3.right);

            m_Renderer.PreviewObject.Rotate(-objectUp * m_XSpeed);
            m_Renderer.PreviewObject.Rotate(objectRight * m_YSpeed);

            m_XSpeed = m_XSpeed - m_XSpeed * m_RotationDrag;
            m_YSpeed = m_YSpeed - m_YSpeed * m_RotationDrag;
        }

        // --------------------------------------------------------------------

        public void Hide()
        {
            ClearPreviousModel();

            PauseController.Instance.Resume(this);
            CursorController.Instance.SetInUI(false);

            gameObject.SetActive(false);

            m_Renderer.gameObject.SetActive(false);

            UIManager.PopAction();
        }
    }
}
