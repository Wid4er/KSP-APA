using System;

namespace AirParkStability
{
    // Owns the dialog's pause lifetime; cancel preserves any pause that preceded it.
    internal sealed class ConfirmationPause
    {
        private readonly Func<bool> isPaused;
        private readonly Action<bool> setPaused;
        private bool active, pausedBefore;

        public ConfirmationPause(Func<bool> isPaused, Action<bool> setPaused)
        { this.isPaused = isPaused; this.setPaused = setPaused; }

        public void Begin()
        {
            if (active) return;
            pausedBefore = isPaused();
            active = true;
            Maintain();
        }

        public void Maintain() { if (active && !isPaused()) setPaused(true); }

        public void Close(bool resumeForUnpark = false)
        {
            if (!active) return;
            bool desired = resumeForUnpark ? false : pausedBefore;
            active = false; // Unpause callbacks cannot reacquire this dialog's pause.
            if (isPaused() != desired) setPaused(desired);
        }
    }
}
