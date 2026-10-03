using System;
namespace NicromFPS
{
    public sealed class HandGestureTiming
    {
        public const float PeakTime = .18f;
        public const float Duration = .50f;
        public bool Playing { get; private set; }
        public float Elapsed { get; private set; }
        public bool PeakThisFrame { get; private set; }
        public float Weight => WeightAt(Elapsed);
        public bool Trigger()
        {
            if (Playing) return false;
            Playing = true; Elapsed = 0; PeakThisFrame = false; return true;
        }
        public void Tick(float delta, bool paused)
        {
            PeakThisFrame = false;
            if (!Playing || paused || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            float previous = Elapsed;
            Elapsed = Math.Min(Duration, Elapsed + delta);
            PeakThisFrame = previous < PeakTime && Elapsed >= PeakTime;
            if (Elapsed >= Duration) Playing = false;
        }
        public void Cancel() { Playing = false; Elapsed = Duration; PeakThisFrame = false; }
        public static float WeightAt(float time)
        {
            if (time <= 0 || time >= Duration) return 0;
            if (time < .06f) return .15f * Smooth(time / .06f);
            if (time < PeakTime) return .15f + .85f * Smooth((time - .06f) / .12f);
            if (time <= .28f) return 1;
            return 1 - Smooth((time - .28f) / .22f);
        }
        static float Smooth(float t) => t * t * (3 - 2 * t);
    }
}
