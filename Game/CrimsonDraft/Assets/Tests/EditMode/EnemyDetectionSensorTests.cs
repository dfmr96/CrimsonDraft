#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Enemy;

namespace CrimsonDraft.Tests
{
    public sealed class EnemyDetectionSensorTests
    {
        private NavigationEnemyData MakeData(
            float walkRadius  = 0f,
            float runRadius   = 0f,
            float visualRange = 0f)
        {
            var data = ScriptableObject.CreateInstance<NavigationEnemyData>();
            data.walkSoundRadius    = walkRadius;
            data.runSoundRadius     = runRadius;
            data.playerDeadzone     = 0.1f;
            data.playerRunThreshold = 5.5f;
            data.visualRange        = visualRange;
            return data;
        }

        [Test]
        public void Sound_DetectsWalkingPlayerWithinWalkRadius()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(3f, 0f, 0f); // inside walkRadius=5
            var playerRb = playerGO.AddComponent<Rigidbody>();
            playerRb.linearVelocity = new Vector3(4f, 0f, 0f); // walk speed (4 < runThreshold 5.5)

            var data = MakeData(walkRadius: 5f, runRadius: 9f);

            Assert.IsTrue(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Sound_NoDetectionForIdlePlayer()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(3f, 0f, 0f);
            var playerRb = playerGO.AddComponent<Rigidbody>();
            // linearVelocity is Vector3.zero by default — player is idle

            var data = MakeData(walkRadius: 5f, runRadius: 9f);

            Assert.IsFalse(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Sound_DetectsRunningPlayerWithinRunRadius()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(7f, 0f, 0f); // inside runRadius=9, outside walkRadius=5
            var playerRb = playerGO.AddComponent<Rigidbody>();
            playerRb.linearVelocity = new Vector3(6f, 0f, 0f); // run speed (6 > runThreshold 5.5)

            var data = MakeData(walkRadius: 5f, runRadius: 9f);

            Assert.IsTrue(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Sound_NoDetectionForRunningPlayerOutsideRunRadius()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(10f, 0f, 0f); // outside runRadius=9
            var playerRb = playerGO.AddComponent<Rigidbody>();
            playerRb.linearVelocity = new Vector3(6f, 0f, 0f); // run speed (6 > runThreshold 5.5)

            var data = MakeData(walkRadius: 5f, runRadius: 9f);

            Assert.IsFalse(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Visual_NoDetectionWhenOutsideVisualRange()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(5f, 0f, 0f); // outside visualRange=3
            var playerRb = playerGO.AddComponent<Rigidbody>();

            var data = MakeData(visualRange: 3f);
            data.visualFov = 180f;

            Assert.IsFalse(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Visual_NoDetectionWhenOutsideFOV()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            sensorGO.transform.forward = Vector3.forward; // facing +Z
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(2f, 0f, 0f); // directly to the side (+X), FOV=10° → 90° angle > 5°
            var playerRb = playerGO.AddComponent<Rigidbody>();

            var data = MakeData(visualRange: 5f);
            data.visualFov = 10f;

            Assert.IsFalse(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }
    }
}