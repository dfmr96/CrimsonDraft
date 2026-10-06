using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{


    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public class Pushable : MonoBehaviour, ISavableObjectStateExtra
    {
        private static readonly float k_SnapThreshold = 0.01f;
        private static readonly float k_MinPushableDotValue = 0.4f;
        private static readonly float k_SoundDisplacementThreshold = 0.005f;

        public Vector3[] PushAxis;
        public bool LocalSpaceAxis = true;

        [SerializeField] SoundCue[] m_PushSounds;
        [SerializeField] float m_SnapSpeed = 1f;

        public UnityEvent OnStartPush;
        public UnityEvent OnStopPush;

        private AudioSource m_AudioSource;
        private bool m_Snapping;
        private Vector3 m_SnappingPos;
        private Rigidbody m_Rigidbody;
        private bool m_Pushing;

        public bool CanBePushed
        {
            get { return enabled && !m_Snapping; }
        }

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_AudioSource = GetComponent<AudioSource>();
        }

        // --------------------------------------------------------------------

        private void OnValidate()
        {
            for (int i = 0; i < PushAxis.Length; ++i)
            {
                PushAxis[i] = PushAxis[i].normalized;
            }
        }

        // --------------------------------------------------------------------

        private void OnDisable()
        {
            m_Snapping = false;
        }

        // --------------------------------------------------------------------

        private void FixedUpdate()
        {
            if (m_Snapping)
            {
                Vector3 dirToSnap = (m_SnappingPos - m_Rigidbody.position);
                if (dirToSnap.magnitude > 1f)
                {
                    dirToSnap.Normalize();
                }
                m_Rigidbody.MovePosition(m_Rigidbody.position + dirToSnap * Time.deltaTime * m_SnapSpeed);

                if (Vector3.Distance(m_SnappingPos, m_Rigidbody.position) < k_SnapThreshold)
                {
                    enabled = false;
                }
            }
        }


        // --------------------------------------------------------------------

        public void PushTo(Vector3 m_ExpectedPosition)
        {
            if (m_Pushing)
                m_Rigidbody.MovePosition(m_ExpectedPosition);
        }

        // --------------------------------------------------------------------

        public void StopPush()
        {
            OnStopPush?.Invoke();
            m_Pushing = false;
        }

        // --------------------------------------------------------------------

        public void OnStartPushEvent()
        {
            
            if (m_PushSounds.Length > 0)
            {
                Vector3 prevPos = transform.position; // Cache prev position so we don't play the sound if there is no displacement
                this.InvokeAction(() =>
                {
                    float dist = Vector3.Distance(transform.position, prevPos);
                    if (dist > k_SoundDisplacementThreshold)
                    {
                        SoundCue cue = m_PushSounds[UnityEngine.Random.Range(0, m_PushSounds.Length)];
                        AudioClip clip = cue.GetClip(out float volume, out float pitch);
                        m_AudioSource.pitch = pitch;
                        m_AudioSource.PlayOneShot(clip, volume);
                    }
                }, Time.fixedDeltaTime);
            }

            if (!m_Pushing)
                OnStartPush?.Invoke();

            m_Pushing = true;
        }

        // --------------------------------------------------------------------

        public Vector3 GetPushingAxis(Vector3 pushingDir, out bool foundAxis)
        {
            Vector3 pushingDirN = pushingDir.normalized;
            Vector3 selectedAxis = Vector3.zero;
            float bestDot = k_MinPushableDotValue;
            foundAxis = false;
            foreach (Vector3 v in PushAxis)
            {
                Vector3 axisDir = LocalSpaceAxis ? transform.TransformDirection(v) : v;
                float dot = Vector3.Dot(axisDir, pushingDirN);
                if (dot > bestDot)
                {
                    selectedAxis = axisDir;
                    bestDot = dot;
                    foundAxis = true;
                }

                dot = Vector3.Dot(-axisDir, pushingDirN);
                if (dot > bestDot)
                {
                    selectedAxis = -axisDir;
                    bestDot = dot;
                    foundAxis = true;
                }
            }

            return selectedAxis;
        }

        // --------------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            foreach(Vector3 v in PushAxis)
            {
                Vector3 dir = LocalSpaceAxis ? transform.TransformDirection(v) : v;
                dir *= 5;
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position - dir, transform.position + dir);
                Gizmos.color = Color.grey;
                Gizmos.DrawLine(transform.position - dir- Vector3.up*0.01f, transform.position + dir - Vector3.up * 0.01f);
            }
        }

        // --------------------------------------------------------------------

        public void SnapToTransform(Transform transform)
        {
            SnapToPosition(transform.position);
        }

        // --------------------------------------------------------------------

        public void SnapToPosition(Vector3 position)
        {
            m_Snapping = true;
            m_SnappingPos = position;
        }

        // --------------------------------------------------------------------
        // ISavable implementation
        // --------------------------------------------------------------------

        public string GetSavableData()
        {
            return enabled.ToString();
        }

        public void SetFromSavedData(string savedData)
        {
            enabled = Convert.ToBoolean(savedData);
        }

    }
}