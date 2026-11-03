using BoLike.Core;
using NUnit.Framework;

namespace BoLike.Tests
{
    public class AnimCurvesTests
    {
        [Test]
        public void SpringSettlesOnTargetWithBoundedOvershoot()
        {
            var s = new Spring(0);
            float peak = 0;
            for (int i = 0; i < 120; i++) { s.Step(1f, 1f / 60f); peak = System.Math.Max(peak, s.value); }
            Assert.AreEqual(1f, s.value, 0.01f, "spring did not settle in 2 s");
            Assert.Less(peak, 1.25f, "overshoot too large");
            Assert.Greater(peak, 1.0f, "damping 0.75 should overshoot a little");
        }

        [Test]
        public void SpringIsStableOnLongFrames()
        {
            var s = new Spring(0);
            for (int i = 0; i < 20; i++) s.Step(1f, 0.1f); // 100 ms frames (hitch)
            Assert.AreEqual(1f, s.value, 0.02f);
            Assert.False(float.IsNaN(s.value) || float.IsInfinity(s.value));
        }

        [Test]
        public void SpringImpulseReturnsToRest()
        {
            var s = new Spring(1f) { velocity = -4f }; // landing squash kick
            float low = 1f;
            for (int i = 0; i < 90; i++) { s.Step(1f, 1f / 60f); low = System.Math.Min(low, s.value); }
            Assert.Less(low, 0.95f, "the kick should visibly squash");
            Assert.AreEqual(1f, s.value, 0.01f);
        }

        [Test]
        public void GaitLiftIsZeroAtFootfallAndPeaksMidStep()
        {
            Assert.AreEqual(0f, AnimCurves.GaitLift(0f), 1e-5f);
            Assert.AreEqual(0f, AnimCurves.GaitLift(1f), 1e-5f);
            Assert.AreEqual(0f, AnimCurves.GaitLift(2f), 1e-5f);
            Assert.AreEqual(1f, AnimCurves.GaitLift(0.45f), 1e-5f);
            float prev = -1;
            for (float u = 0; u <= 0.45f; u += 0.05f) { float v = AnimCurves.GaitLift(u); Assert.GreaterOrEqual(v, prev); prev = v; }
            for (float u = 0.45f; u <= 1f; u += 0.05f) { float v = AnimCurves.GaitLift(u); Assert.LessOrEqual(v, prev + 1e-5f); prev = v; }
        }

        [Test]
        public void CrossFadeIsMonotonicAndClamped()
        {
            Assert.AreEqual(0f, AnimCurves.CrossFade(-1f));
            Assert.AreEqual(1f, AnimCurves.CrossFade(2f));
            Assert.AreEqual(0.5f, AnimCurves.CrossFade(0.5f), 1e-5f);
            float prev = 0;
            for (float k = 0; k <= 1f; k += 0.02f) { float v = AnimCurves.CrossFade(k); Assert.GreaterOrEqual(v, prev); prev = v; }
        }

        [Test]
        public void DeltaAngleTakesTheShortWay()
        {
            Assert.AreEqual(0f, AnimCurves.DeltaAngle(360f, 0f), 1e-5f); // end of a roll → idle: no unwinding
            Assert.AreEqual(-20f, AnimCurves.DeltaAngle(10f, -10f), 1e-5f);
            Assert.AreEqual(20f, AnimCurves.DeltaAngle(350f, 10f), 1e-5f);
        }
    }
}
