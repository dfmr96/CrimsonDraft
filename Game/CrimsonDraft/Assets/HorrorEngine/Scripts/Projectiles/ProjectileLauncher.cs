using UnityEngine;

namespace HorrorEngine
{
    public class ProjectileLauncher : MonoBehaviour
    {
        [SerializeField] float Velocity;
        [SerializeField] Projectile m_ProjectilePrefab;
        [Tooltip("If this list is used projectiles will come out of these. If the list is empty, this object transform will be used")]
        [SerializeField] Transform[] m_OptionalLaunchPoints;

        // --------------------------------------------------------------------

        public void Launch()
        {
            if (m_OptionalLaunchPoints != null && m_OptionalLaunchPoints.Length > 0)
            {
                foreach (var optionalT in m_OptionalLaunchPoints)
                {
                    LaunchFrom(m_ProjectilePrefab.gameObject, optionalT, optionalT.forward * Velocity);
                }
            }
            else
            {
                LaunchFrom(m_ProjectilePrefab.gameObject, transform, transform.forward * Velocity);
            }
        }

        // --------------------------------------------------------------------

        private void LaunchFrom(GameObject prefab, Transform launchPoint, Vector3 initialVelocity)
        {
            var pooledPrefab = GameObjectPool.Instance.GetFromPool(prefab, null);
            pooledPrefab.transform.SetPositionAndRotation(launchPoint.position, launchPoint.rotation);

            var newProjectile = pooledPrefab.GetComponent<Projectile>();
            newProjectile.enabled = false;
            Debug.Assert(newProjectile, "Instantiated projectile doens't have a Projectile component");
            if (newProjectile)
                newProjectile.Launch(initialVelocity);

            pooledPrefab.gameObject.SetActive(true);
        }

        // --------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (m_OptionalLaunchPoints != null && m_OptionalLaunchPoints.Length > 0)
            {
                foreach(var optionalT in m_OptionalLaunchPoints)
                {
                    GizmoUtils.DrawArrow(optionalT.position, optionalT.forward, 0.5f, 0.25f);
                }
            }
            else
            {
                GizmoUtils.DrawArrow(transform.position, transform.forward, 0.5f, 0.25f);
            }
        }
    }
}
