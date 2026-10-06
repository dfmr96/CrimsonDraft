using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class PlayerStateGrabbed : ActorStateWithDuration
    {
        [SerializeField] private AnimatorStateHandle m_HurtAnimation;
        [SerializeField] private float m_HurtAnimBlendTime = 0.2f;
        
        [Space]
        [SerializeField] private bool m_RotateToGrabber;
        [ShowIf(nameof(m_RotateToGrabber))]
        [SerializeField] private float m_RotationOffset;
        [ShowIf(nameof(m_RotateToGrabber))]
        [SerializeField] private float m_RotationDuration = 0.5f;

        [Space]
        [SerializeField] private float m_ReleaseTime;
        [SerializeField] private float m_ReleaseTimeRandomOffsett;
        [SerializeField] private float m_ReleaseAnimDelay;
        [SerializeField] private string m_ReleaseGrabberStateTag;

        [Space]
        [SerializeField] private bool m_CanPrevent;
        
        [ShowIf(nameof(m_CanPrevent))]
        [SerializeField] private float m_PreventDelay;
        [Tooltip("This is the time the player has to start the escape action")]
        [ShowIf(nameof(m_CanPrevent))]
        [SerializeField] private float m_PreventMaxTime;
        [Tooltip("This is the state the player will transition to in scape")]
        [ShowIf(nameof(m_CanPrevent))]
        [SerializeField] private ActorState m_PreventState;
        [ShowIf(nameof(m_CanPrevent))]
        [SerializeField] private string m_PreventGrabberStateTag;
        [ShowIf(nameof(m_CanPrevent))]
        [SerializeField] private ItemData m_PreventRequiresItem;
        [ShowIf(nameof(m_CanPrevent))]
        [SerializeField] private bool m_PreventRequiresItemEquipped;

        private Grabber m_Grabber;

        private Vector3 m_DirToGrabber;
        private float m_RotationAngle;

        private GroundDetector m_GroundDetect;
        private PlayerGrabHandler m_GrabHandler;
        private IPlayerInput m_Input;
        private Health m_Health;
        private UnityAction<float> m_OnHealthDecreased;
        
        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_Input = GetComponentInParent<IPlayerInput>();
            m_GrabHandler = GetComponentInParent<PlayerGrabHandler>();
            m_Health = GetComponentInParent<Health>();
            m_GroundDetect = GetComponentInParent<GroundDetector>();

            m_OnHealthDecreased = OnHealthDecreased;
        }

        // --------------------------------------------------------------------


        public override void StateEnter(IActorState fromState)
        {
            base.StateEnter(fromState);

            m_Grabber = m_GrabHandler.Grabber;
            
            m_DirToGrabber = m_Grabber.transform.position - Actor.transform.position;
            m_DirToGrabber.y = 0;
            m_DirToGrabber.Normalize();

            m_DirToGrabber = Quaternion.Euler(0, m_RotationOffset, 0) * m_DirToGrabber;

            m_RotationAngle = Vector3.Angle(m_DirToGrabber, Actor.transform.forward);

            m_Duration = Random.Range(m_ReleaseTime - m_ReleaseTimeRandomOffsett, m_ReleaseTime + m_ReleaseTimeRandomOffsett);

            m_Health.OnHealthDecreased.AddListener(m_OnHealthDecreased);

            
            
            UIManager.Get<UIInputListener>().AddBlockingContext(this);
        }

        // --------------------------------------------------------------------

        public override void StateUpdate()
        {
            base.StateUpdate();

            if (!DurationElapsed)
            {
                UpdatePrevention();
                UpdatePositioning();
                UpdateRotation();

                if (!m_GrabHandler.Grabber)
                    SetState(m_ExitState);
            }
        }

        // --------------------------------------------------------------------

        private void UpdatePositioning()
        {
            var positioning = m_GrabHandler.Positioning;
            if (positioning.Type == GrabPositioningType.MoveGrabbedToGrabber)
            {
                Actor.transform.position = Vector3.MoveTowards(Actor.transform.position, positioning.WorldSpaceRefPoint, positioning.MoveSpeed * Time.fixedDeltaTime);
            }
        }

        // --------------------------------------------------------------------

        private void UpdateRotation()
        {
            if (m_RotateToGrabber)
            {
                Actor.transform.rotation = Quaternion.RotateTowards(Actor.transform.rotation, Quaternion.LookRotation(m_DirToGrabber), m_RotationAngle * (Time.deltaTime / m_RotationDuration));
            }
        }

        // --------------------------------------------------------------------

        private void UpdatePrevention()
        {
            if (m_CanPrevent && m_TimeInState > m_PreventDelay && m_TimeInState < m_PreventMaxTime)
            {
                if (m_Input.IsAttackDown() && MeetsPreventionItemRequirements())
                {
                    m_GrabHandler.Prevent(new GrabPreventionData()
                    {
                        GrabberStateTag = m_PreventGrabberStateTag,
                    });

                    SetState(m_PreventState);
                }
            }
        }

        // --------------------------------------------------------------------
       
        private bool MeetsPreventionItemRequirements()
        {
            if (m_PreventRequiresItem)
            {
                if (!m_PreventRequiresItemEquipped && !GameManager.Instance.Inventory.Contains(m_PreventRequiresItem))
                {
                    return false;
                }
                else if (m_PreventRequiresItemEquipped)
                {
                    EquipableItemData equipableItem = m_PreventRequiresItem as EquipableItemData;
                    InventoryEntry equipmenEntry = GameManager.Instance.Inventory.GetEquipped(equipableItem.Slot);
                    if (equipmenEntry == null || equipmenEntry.Item != equipableItem)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // --------------------------------------------------------------------

        private void OnHealthDecreased(float health)
        {
            Actor.MainAnimator.CrossFadeInFixedTime(m_HurtAnimation.Hash, m_HurtAnimBlendTime);
        }

        // --------------------------------------------------------------------

        protected override void OnStateDurationEnd()
        {

            m_GrabHandler.Release(new GrabReleaseData()
            {
                GrabberStateTag = m_ReleaseGrabberStateTag,
                Delay = m_ReleaseAnimDelay
            });

            base.OnStateDurationEnd();
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            m_Health.OnHealthDecreased.RemoveListener(m_OnHealthDecreased);
            
            UIManager.Get<UIInputListener>().RemoveBlockingContext(this);
            
            base.StateExit(intoState);
        }

        
    }
}