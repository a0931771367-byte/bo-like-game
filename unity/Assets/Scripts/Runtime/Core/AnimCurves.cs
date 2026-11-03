using System;

namespace BoLike.Core
{
    /// <summary>
    /// A damped spring that follows a target with a little overshoot. The views run every procedural pose value
    /// (lean, squash, stretch) through one of these, so a state change (walk → idle, startup → hit) flows over a
    /// few frames with follow-through instead of jumping, and an impulse (landing, taking a hit) can be kicked in
    /// through <see cref="velocity"/>. Pure math, no Unity.
    /// </summary>
    public struct Spring
    {
        public float value, velocity;

        public Spring(float v) { value = v; velocity = 0; }

        /// <summary>
        /// Advances toward <paramref name="target"/> by <paramref name="dt"/> seconds. <paramref name="hz"/> is the
        /// natural frequency (how fast it gets there), <paramref name="damping"/> the ratio: 1 = no overshoot,
        /// 0.6–0.8 = a visible bounce. Semi-implicit Euler, sub-stepped so a long frame stays stable.
        /// </summary>
        public void Step(float target, float dt, float hz = 14f, float damping = 0.75f)
        {
            if (dt <= 0) return;
            float w = 2f * (float)Math.PI * hz;
            int steps = Math.Max(1, (int)Math.Ceiling(dt / 0.004f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                float a = w * w * (target - value) - 2f * damping * w * velocity;
                velocity += a * h;
                value += velocity * h;
            }
        }

        public void Snap(float v) { value = v; velocity = 0; }
    }

    /// <summary>Easing and gait shapes shared by the views and the tests.</summary>
    public static class AnimCurves
    {
        /// <summary>
        /// Vertical lift of a run cycle over one step. <paramref name="stepPhase"/> is 0 at a footfall and 1 at the
        /// next. The body is lowest on contact, rises quickly after push-off and comes down into the next contact
        /// a little faster than it rose (the reference footage bobs about 5% of body height per step).
        /// Returns 0..1.
        /// </summary>
        public static float GaitLift(float stepPhase)
        {
            float u = stepPhase - (float)Math.Floor(stepPhase);
            // Shift the peak slightly early (0.45) so the descent is the longer, heavier half.
            float p = u < 0.45f ? u / 0.45f : 1f - (u - 0.45f) / 0.55f;
            return (float)Math.Sin(p * Math.PI * 0.5);
        }

        /// <summary>Smoothstep blend used for frame cross-fades (0..1 → 0..1, flat at both ends).</summary>
        public static float CrossFade(float k)
        {
            k = Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        public static float EaseOutQuad(float k) { k = Clamp01(k); return 1f - (1f - k) * (1f - k); }
        public static float EaseOutCubic(float k) { k = Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }
        public static float EaseInCubic(float k) { k = Clamp01(k); return k * k * k; }
        public static float EaseOutBack(float k) { k = Clamp01(k); const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * (k - 1) * (k - 1) * (k - 1) + c1 * (k - 1) * (k - 1); }

        /// <summary>Shortest signed difference between two angles in degrees, in (-180, 180].</summary>
        public static float DeltaAngle(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d > 180f) d -= 360f;
            if (d <= -180f) d += 360f;
            return d;
        }

        public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
