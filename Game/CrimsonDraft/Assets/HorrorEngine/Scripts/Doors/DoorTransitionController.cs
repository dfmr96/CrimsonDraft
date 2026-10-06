using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class DoorTransitionMidWayMessage : BaseMessage
    {
        public GameObject User;
        public DoorBase Door; // Door that triggered the transition
    }

    public class DoorTransitionMidWayPostRoutineMessage : BaseMessage
    {
        public GameObject User;
        public DoorBase Door; // Door that triggered the transition
    }

    public class DoorTransitionEndMessage : BaseMessage
    {
        public GameObject User;
        public DoorBase Door; // Door that triggered the transition
    }

    public class DoorTransitionController : SingletonBehaviour<DoorTransitionController>
    {
        public GameObject Animation;

        [SerializeField] private float m_FadeOutDuration;
        [SerializeField] private float m_FadeInDuration;
        [SerializeField] private Vector3 m_InstantiationPosition = Vector3.one * 10000f;
        [SerializeField] private bool m_PauseGame = true;

        private AudioSource m_AudioSource;
        private Dictionary<DoorAnimation, DoorAnimation> m_AnimationInstances = new Dictionary<DoorAnimation, DoorAnimation>();

        private UIFade m_UIFade;

        private DoorTransitionMidWayMessage m_TransitionMidWayMsg = new DoorTransitionMidWayMessage();
        private DoorTransitionMidWayPostRoutineMessage m_TransitionMidWayPostRoutineMsg = new DoorTransitionMidWayPostRoutineMessage();
        private DoorTransitionEndMessage m_TransitionEndMsg = new DoorTransitionEndMessage();

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();
            m_AudioSource = GetComponent<AudioSource>();
        }


        // --------------------------------------------------------------------

        private void Start()
        {
            m_UIFade = UIManager.Get<UIFade>();
        }

        // --------------------------------------------------------------------

        public void Trigger(DoorBase door, GameObject user, Func<IEnumerator> transitionRoutine)
        {
            StartCoroutine(StartTransitionRoutine(door, user, transitionRoutine));
        }

        // --------------------------------------------------------------------

        private IEnumerator StartTransitionRoutine(DoorBase door, GameObject user, Func<IEnumerator> transitionRoutine)
        {
            m_TransitionEndMsg.Door = door;
            m_TransitionMidWayMsg.Door = door;
            m_TransitionMidWayPostRoutineMsg.Door = door;

            m_TransitionEndMsg.User = user;
            m_TransitionMidWayMsg.User = user;
            m_TransitionMidWayPostRoutineMsg.User = user;

            if (m_PauseGame)
                PauseController.Instance.Pause(this);

            Actor actor = user.GetComponent<Actor>();
            if (actor) actor.Disable(this);

            // Fade Out
            yield return m_UIFade.Fade(0f, 1f, m_FadeOutDuration);

            if (door.Animation)
            {
                // Door animation
                DoorAnimation doorAnim = GetDoorAnimInstance(door);
                doorAnim.gameObject.SetActive(true);
                Coroutine doorAnimRoutine = StartCoroutine(doorAnim.Play());
                
                if (doorAnim.Sound)
                    m_AudioSource.PlayOneShot(doorAnim.Sound);

                // Fade In (for door animation)
                yield return m_UIFade.Fade(1f, 0f, 0.25f);

                // Wait for anim
                yield return doorAnimRoutine;

                // Needs to happen before the transitionRoutine since ObjStates are captured here
                MessageBuffer<DoorTransitionMidWayMessage>.Dispatch(m_TransitionMidWayMsg); 

                yield return StartCoroutine(transitionRoutine?.Invoke());

                // Fade Out (from door animation)
                yield return m_UIFade.Fade(0f, 1f, 0.25f);

                doorAnim.gameObject.SetActive(false);

            }
            else
            {
                // Needs to happen before the transitionRoutine since ObjStates are captured here
                MessageBuffer<DoorTransitionMidWayMessage>.Dispatch(m_TransitionMidWayMsg);

                yield return StartCoroutine(transitionRoutine?.Invoke());
            }

            MessageBuffer<DoorTransitionMidWayPostRoutineMessage>.Dispatch(m_TransitionMidWayPostRoutineMsg);

            // Give the camera system one frame to switch
            yield return Yielders.EndOfFrame;

            if (m_PauseGame)
                PauseController.Instance.Resume(this);

            if (actor)
                actor.Enable(this);

            // Fade In (back to game)
            yield return m_UIFade.Fade(1f, 0f, m_FadeInDuration);

            MessageBuffer<DoorTransitionEndMessage>.Dispatch(m_TransitionEndMsg);
        }

       
        // --------------------------------------------------------------------

        private DoorAnimation GetDoorAnimInstance(DoorBase door)
        {
            var doorAnim = door.Animation;
            if (!doorAnim.gameObject.scene.IsValid()) // Animation is a prefab so it has to be instantiated
            {
                if (!m_AnimationInstances.TryGetValue(doorAnim, out DoorAnimation existingAnimInstance))
                {
                    GameObject animInstanceGO = Instantiate(doorAnim.gameObject);
                    animInstanceGO.transform.position = m_InstantiationPosition;
                    animInstanceGO.transform.SetParent(transform);

                    DoorAnimation newAnimInstance = animInstanceGO.GetComponentInChildren<DoorAnimation>();
                    m_AnimationInstances.Add(doorAnim, newAnimInstance);
                    return newAnimInstance;
                }
                else
                {
                    return existingAnimInstance;
                }

            }
            return doorAnim; // Animation is not a prefab, it's an existing instance placed in the scene
        }

    }
}