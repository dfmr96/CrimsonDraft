#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Enemy
{
    public sealed class EnemyDetectionSensor : MonoBehaviour
    {
        private bool proximityActive = false;

        public bool Evaluate(NavigationEnemyData data, Transform player, Rigidbody playerRb, Transform? eyePoint)
        {
            var playerPos = player.position;
            var distance  = Vector3.Distance(transform.position, playerPos);

            // 1. Proximity with hysteresis (omnidirectional, from sensor origin)
            if (!proximityActive && distance < data.detectRadius)
                proximityActive = true;
            else if (proximityActive && distance > data.undetectRadius)
                proximityActive = false;

            if (proximityActive) return true;

            // 2. Sound detection (distance from sensor origin to player)
            var speed = playerRb.linearVelocity.magnitude;
            if (speed > data.playerDeadzone)
            {
                var soundRadius = speed > data.playerRunThreshold
                    ? data.runSoundRadius
                    : data.walkSoundRadius;
                if (distance < soundRadius) return true;
            }

            // 3. Visual detection — 2-pass raycast from eye point
            if (distance < data.visualRange)
            {
                var origin      = eyePoint != null ? eyePoint.position : transform.position;
                var dirToPlayer = (playerPos - origin).normalized;
                var angle       = Vector3.Angle(transform.forward, dirToPlayer);

                if (angle < data.visualFov * 0.5f)
                {
                    var eyeDist = Vector3.Distance(origin, playerPos);
                    // Pass 1: is there an obstruction between eye and player?
                    if (!Physics.Raycast(origin, dirToPlayer, eyeDist, data.obstructionMask))
                    {
                        // Pass 2: is the player's collider on the target layer?
                        if (Physics.Raycast(origin, dirToPlayer, eyeDist, data.targetMask))
                            return true;
                    }
                }
            }

            return false;
        }

        public void ResetState()
        {
            proximityActive = false;
        }
    }
}
