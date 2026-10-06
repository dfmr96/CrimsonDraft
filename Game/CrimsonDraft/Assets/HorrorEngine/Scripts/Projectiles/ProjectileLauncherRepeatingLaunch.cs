using System.Collections;
using UnityEngine;

namespace HorrorEngine
{
    [RequireComponent(typeof(ProjectileLauncher))]
    public class ProjectileLauncherRepeatingLaunch: MonoBehaviour
    {
        [SerializeField] float m_Interval = 1.0f;
        [Tooltip("Max number of launches performed. Set to 0 for infinite launches")]
        [SerializeField] int m_MaxCount = 0;

        private ProjectileLauncher m_Laucher;
        private int m_LaunchCount;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Laucher = GetComponent<ProjectileLauncher>();
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            m_LaunchCount = 0;
            StartCoroutine(DoRepeatingLaunch());
        }

        // --------------------------------------------------------------------

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        // --------------------------------------------------------------------

        IEnumerator DoRepeatingLaunch()
        {
            while (true)
            {
                yield return Yielders.Time(m_Interval);

                m_Laucher.Launch();

                ++m_LaunchCount;
                if (m_MaxCount > 0 && m_LaunchCount >= m_MaxCount)
                    enabled = false;
            }
        }

    }
}