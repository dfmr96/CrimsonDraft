#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Enemy;

namespace CrimsonDraft.Tests
{
    public sealed class EnemyDetectionSensorTests
    {
        private NavigationEnemyData MakeData(
            float detectRadius   = 2.0f,
            float undetectRadius = 3.0f,
            float walkRadius     = 0f,
            float runRadius      = 0f,
            float visualRange    = 0f)
        {
            var data = ScriptableObject.CreateInstance<NavigationEnemyData>();
            data.detectRadius   = detectRadius;
            data.undetectRadius = undetectRadius;
            // Disable sound and visual by default to isolate proximity tests
            data.walkSoundRadius    = walkRadius;
            data.runSoundRadius     = runRadius;
            data.playerDeadzone     = 0.1f;
            data.playerRunThreshold = 5.5f;
            data.visualRange        = visualRange;
            return data;
        }

        [Test]
        public void Proximity_DetectsWhenInsideDetectRadius()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(1f, 0f, 0f); // inside detectRadius=2
            var playerRb = playerGO.AddComponent<Rigidbody>();

            var data = MakeData();

            Assert.IsTrue(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Proximity_NoDetectionOutsideUndetectRadius()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(5f, 0f, 0f); // outside undetectRadius=3
            var playerRb = playerGO.AddComponent<Rigidbody>();

            var data = MakeData();

            Assert.IsFalse(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Proximity_Hysteresis_StaysActiveInZoneBetweenRadii()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            var playerRb = playerGO.AddComponent<Rigidbody>();
            var data = MakeData();

            // Enter detect zone
            playerGO.transform.position = new Vector3(1f, 0f, 0f);
            sensor.Evaluate(data, playerGO.transform, playerRb, null); // activates

            // Move to hysteresis zone (between 2 and 3)
            playerGO.transform.position = new Vector3(2.5f, 0f, 0f);
            bool inHysteresis = sensor.Evaluate(data, playerGO.transform, playerRb, null);

            Assert.IsTrue(inHysteresis, "Should remain detected in hysteresis zone");

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Proximity_Hysteresis_DeactivatesOnceOutsideUndetectRadius()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            var playerRb = playerGO.AddComponent<Rigidbody>();
            var data = MakeData();

            // Enter and exit fully
            playerGO.transform.position = new Vector3(1f, 0f, 0f);
            sensor.Evaluate(data, playerGO.transform, playerRb, null);

            playerGO.transform.position = new Vector3(5f, 0f, 0f); // outside undetect=3
            bool afterExit = sensor.Evaluate(data, playerGO.transform, playerRb, null);

            Assert.IsFalse(afterExit, "Should lose detection after exiting undetect radius");

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Sound_DetectsWalkingPlayerWithinWalkRadius()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(3f, 0f, 0f); // inside walkRadius=5, outside proximity
            var playerRb = playerGO.AddComponent<Rigidbody>();
            playerRb.linearVelocity = new Vector3(4f, 0f, 0f); // walk speed (4 < runThreshold 5.5)

            var data = MakeData(detectRadius: 1f, undetectRadius: 1.5f, walkRadius: 5f, runRadius: 9f);

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

            var data = MakeData(detectRadius: 1f, undetectRadius: 1.5f, walkRadius: 5f, runRadius: 9f);

            Assert.IsFalse(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void ResetState_ClearsProximityHysteresis()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            var playerRb = playerGO.AddComponent<Rigidbody>();
            var data = MakeData();

            // Activate proximity
            playerGO.transform.position = new Vector3(1f, 0f, 0f);
            sensor.Evaluate(data, playerGO.transform, playerRb, null);

            // Reset, then move to hysteresis zone
            sensor.ResetState();
            playerGO.transform.position = new Vector3(2.5f, 0f, 0f);
            bool afterReset = sensor.Evaluate(data, playerGO.transform, playerRb, null);

            Assert.IsFalse(afterReset, "After ResetState, hysteresis zone should not detect");

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

            var data = MakeData(detectRadius: 1f, undetectRadius: 1.5f, walkRadius: 5f, runRadius: 9f);

            Assert.IsTrue(sensor.Evaluate(data, playerGO.transform, playerRb, null));

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

            var data = MakeData(detectRadius: 0.1f, undetectRadius: 0.2f, visualRange: 3f);
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

            var data = MakeData(detectRadius: 0.1f, undetectRadius: 0.2f, visualRange: 5f);
            data.visualFov = 10f;

            Assert.IsFalse(sensor.Evaluate(data, playerGO.transform, playerRb, null));

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

            var data = MakeData(detectRadius: 1f, undetectRadius: 1.5f, walkRadius: 5f, runRadius: 9f);

            Assert.IsFalse(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }
    }
}
