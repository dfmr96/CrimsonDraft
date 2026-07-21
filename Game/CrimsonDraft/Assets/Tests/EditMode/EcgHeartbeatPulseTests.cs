#nullable enable

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.UI;

namespace CrimsonDraft.Tests
{
    public sealed class EcgHeartbeatPulseTests
    {
        // baseColor/pulsePeriod are private with no public accessor, so the
        // health-state bucket is asserted via reflection rather than color output.
        private static (Color color, float period) GetState(EcgHeartbeatPulse pulse)
        {
            var type   = typeof(EcgHeartbeatPulse);
            var color  = (Color)type.GetField("baseColor", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pulse)!;
            var period = (float)type.GetField("pulsePeriod", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pulse)!;
            return (color, period);
        }

        private static (Color color, float period) GetDefault(EcgHeartbeatPulse pulse, string colorField, string periodField)
        {
            var type   = typeof(EcgHeartbeatPulse);
            var color  = (Color)type.GetField(colorField, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pulse)!;
            var period = (float)type.GetField(periodField, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pulse)!;
            return (color, period);
        }

        private sealed class Scope : System.IDisposable
        {
            private readonly GameObject root;
            public EcgHeartbeatPulse Pulse { get; }

            public Scope()
            {
                this.root  = new GameObject("ecg-test");
                this.root.SetActive(false); // avoid OnEnable/Update running before SetHealthState
                this.Pulse = this.root.AddComponent<EcgHeartbeatPulse>();
            }

            public void Dispose() => Object.DestroyImmediate(this.root);
        }

        [Test]
        public void SetHealthState_fullHp_usesStableBucket()
        {
            using var scope = new Scope();
            scope.Pulse.SetHealthState(1f);

            var actual   = GetState(scope.Pulse);
            var expected = GetDefault(scope.Pulse, "colorStable", "pulsePeriodStable");

            Assert.AreEqual(expected.color, actual.color);
            Assert.AreEqual(expected.period, actual.period);
        }

        [Test]
        public void SetHealthState_justAboveCautionThreshold_usesStableBucket()
        {
            using var scope = new Scope();
            scope.Pulse.SetHealthState(0.76f);

            var actual   = GetState(scope.Pulse);
            var expected = GetDefault(scope.Pulse, "colorStable", "pulsePeriodStable");

            Assert.AreEqual(expected.color, actual.color);
        }

        [Test]
        public void SetHealthState_atSeventyFivePercent_usesCautionBucket()
        {
            using var scope = new Scope();
            scope.Pulse.SetHealthState(0.75f);

            var actual   = GetState(scope.Pulse);
            var expected = GetDefault(scope.Pulse, "colorCaution", "pulsePeriodCaution");

            Assert.AreEqual(expected.color, actual.color);
        }

        [Test]
        public void SetHealthState_atFiftyPercent_usesWarningBucket()
        {
            using var scope = new Scope();
            scope.Pulse.SetHealthState(0.5f);

            var actual   = GetState(scope.Pulse);
            var expected = GetDefault(scope.Pulse, "colorWarning", "pulsePeriodWarning");

            Assert.AreEqual(expected.color, actual.color);
        }

        [Test]
        public void SetHealthState_atTwentyFivePercent_usesCriticalBucket()
        {
            using var scope = new Scope();
            scope.Pulse.SetHealthState(0.25f);

            var actual   = GetState(scope.Pulse);
            var expected = GetDefault(scope.Pulse, "colorCritical", "pulsePeriodCritical");

            Assert.AreEqual(expected.color, actual.color);
        }

        [Test]
        public void SetHealthState_zero_usesCriticalBucket()
        {
            using var scope = new Scope();
            scope.Pulse.SetHealthState(0f);

            var actual   = GetState(scope.Pulse);
            var expected = GetDefault(scope.Pulse, "colorCritical", "pulsePeriodCritical");

            Assert.AreEqual(expected.color, actual.color);
        }

        [Test]
        public void SetHealthState_outOfRangeValues_areClamped()
        {
            using var scope = new Scope();
            scope.Pulse.SetHealthState(5f);

            var actual   = GetState(scope.Pulse);
            var expected = GetDefault(scope.Pulse, "colorStable", "pulsePeriodStable");

            Assert.AreEqual(expected.color, actual.color, "hpRatio > 1 should clamp to 1, landing in the stable bucket");
        }
    }
}
