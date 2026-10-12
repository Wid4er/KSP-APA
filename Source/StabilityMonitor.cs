using System;

namespace AirParkStability
{
    // Pure logic: no Unity/KSP references and no physics writes.
    public sealed class StabilityLimits
    {
        public readonly double SurfaceSpeed;
        public readonly double VerticalSpeed;
        public readonly double Seconds;

        public StabilityLimits(double surfaceSpeed = 1.5, double verticalSpeed = 0.5, double seconds = 10.0)
        {
            if (!Positive(surfaceSpeed) || !Positive(verticalSpeed) || !Positive(seconds))
                throw new ArgumentOutOfRangeException("limits", "Limits must be finite and positive.");
            SurfaceSpeed = surfaceSpeed;
            VerticalSpeed = verticalSpeed;
            Seconds = seconds;
        }

        internal static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
        private static bool Positive(double n) { return Finite(n) && n > 0; }
    }

    public struct StabilitySample
    {
        public double SurfaceSpeed, VerticalSpeed, UniversalTime;
        public bool PhysicsActive, AtOneX, Flying, AtmosphereValid, Anchored;

        public bool Eligible
        {
            get { return PhysicsActive && AtOneX && Flying && AtmosphereValid && !Anchored; }
        }
    }

    public enum CheckResult { Invalid, Unstable, Checking, Complete }

    public sealed class StabilityMonitor
    {
        private readonly StabilityLimits limits;
        private double? lastTime;
        public double StableSeconds { get; private set; }
        public double Progress { get { return Math.Min(1, StableSeconds / limits.Seconds); } }

        public StabilityMonitor(StabilityLimits limits) { this.limits = limits; }

        public void Reset() { lastTime = null; StableSeconds = 0; }

        public CheckResult Step(StabilitySample sample, double maximumStep)
        {
            if (!sample.Eligible || !StabilityLimits.Finite(sample.UniversalTime) ||
                !StabilityLimits.Finite(maximumStep) || maximumStep <= 0)
            {
                Reset();
                return CheckResult.Invalid;
            }
            // Surface speed is the full vector magnitude; vertical speed uses both signs.
            if (!StabilityLimits.Finite(sample.SurfaceSpeed) || sample.SurfaceSpeed < 0 ||
                !StabilityLimits.Finite(sample.VerticalSpeed) ||
                sample.SurfaceSpeed >= limits.SurfaceSpeed || Math.Abs(sample.VerticalSpeed) >= limits.VerticalSpeed)
            {
                Reset();
                return CheckResult.Unstable;
            }
            if (lastTime.HasValue)
            {
                double dt = sample.UniversalTime - lastTime.Value;
                if (dt < 0 || dt > maximumStep) StableSeconds = 0;
                else StableSeconds += dt;
            }
            lastTime = sample.UniversalTime;
            return StableSeconds >= limits.Seconds ? CheckResult.Complete : CheckResult.Checking;
        }
    }
}
